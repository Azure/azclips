// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Exceptions;
using Core.Extensions;
using Core.Runtime;

namespace Core.Cli.Help;

/// <summary>
/// Renders root, group, and command help to the console. Encapsulates the help
/// orchestration that used to live in <c>Program.Main</c>'s catch blocks: projecting
/// discovery-session command tables through <see cref="ExtensionIndexHelpAdapter"/>
/// (one adapter per verb), and dispatching to <see cref="IHelpRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Help is verb-scoped, not package-scoped. <see cref="RenderRoot"/> runs discovery to
/// enumerate installed extensions from disk, then projects each through
/// discovered verb metadata to discover root groups; this is what makes
/// multi-verb extensions like Compute surface as both <c>vm</c> and <c>disk</c> roots.
/// </para>
/// <para>
/// <see cref="RenderExtensionHelp"/> resolves verb ownership from discovered extension metadata;
/// on a miss it consults <see cref="ExtensionHintTable.Default"/> so <c>az vm --help</c>
/// (when Compute is uninstalled) yields a precise "install it with: az extension add --name Compute"
/// hint instead of a bare <see cref="KeyNotFoundException"/>.
/// </para>
/// </remarks>
public sealed class RuntimeHelpDispatcher
{
    private readonly IHelpRenderer _renderer;
    private readonly IEngine _engine;
    private readonly HashSet<string> _builtInPackageIds;
    private readonly ExtensionHintTable _hintTable;
    private readonly TextWriter _output;
    private readonly OutputStreams _streams;

    public RuntimeHelpDispatcher(IEngine engine, OutputStreams streams)
        : this(
            engine,
            BuiltInExtensions.Map,
            ExtensionHintTable.Default,
            new RuntimeHelpRenderer(),
            Console.Out,
            streams)
    {
    }

    internal RuntimeHelpDispatcher(
        IEngine engine,
        IReadOnlyDictionary<string, BuiltInExtension> builtIns,
        ExtensionHintTable hintTable,
        IHelpRenderer renderer,
        TextWriter output,
        OutputStreams streams)
    {
        _engine = engine;
        _builtInPackageIds = builtIns.Values
            .Select(static b => b.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _hintTable = hintTable;
        _renderer = renderer;
        _output = output;
        _streams = streams;
    }

    /// <summary>Renders the top-level <c>az</c> help (every registered extension's root groups).</summary>
    public int RenderRoot()
    {
        var rootGroups = EnumerateAllVerbRoots().ToArray();
        WriteLines(_renderer.RenderRoot(rootGroups, TerminalWidth()));
        return 0;
    }

    /// <summary>Renders help for a group or command path raised via <see cref="RuntimeExtensionHelpException"/>.</summary>
    public int RenderExtensionHelp(RuntimeExtensionHelpException helpException)
    {
        var discovered = DiscoverInstalledExtensions();
        if (!TryResolveExtension(discovered, helpException.Verb, out var extensionInfo))
        {
            // No discovered owner for this verb. If the embedded hint table knows it,
            // surface the structured "extension not installed" message — same UX as
            // the execute path (see ExtensionDispatcher.Parse).
            if (_hintTable.TryGetPackageId(helpException.Verb, out var hintedPackage))
            {
                throw new ExtensionNotInstalledException(helpException.Verb, hintedPackage);
            }

            // Truly unknown verb (no install hint either). Surface as a structured error so the
            // top-level AzclipsErrorException catch renders it cleanly.
            throw new AzclipsErrorException(
                code: "VerbNotFound",
                exitCode: 4,
                message: $"No installed extension owns verb '{helpException.Verb}'. Run 'az extension list' to see installed extensions.",
                details: new Dictionary<string, object?> { ["verb"] = helpException.Verb });
        }

        var helpProvider = new ExtensionIndexHelpAdapter(extensionInfo.Commands, helpException.Verb);

        IEnumerable<string> lines;
        if (helpException.IsCommandHelp)
        {
            // The parser flags any path with extra tokens as a command-help request, but the
            // user may have asked about an intermediate group (e.g. `az deployment group --help`
            // when the only leaf is `deployment group create`). Try the command first; fall back
            // to group help so partial paths don't crash the renderer.
            var command = helpProvider.GetCommandHelp(helpException.Path);
            if (command != null)
            {
                lines = _renderer.RenderCommand(command, TerminalWidth());
            }
            else
            {
                var group = helpProvider.GetGroupHelp(helpException.Path)
                    ?? throw new KeyNotFoundException($"No help found for path '{string.Join(" ", helpException.Path)}'.");
                lines = _renderer.RenderGroup(group, TerminalWidth());
            }
        }
        else
        {
            // A verb root can also be a runnable command (e.g. "az login" is both).
            var rootCommand = helpProvider.GetCommandHelp(helpException.Path);
            if (rootCommand != null)
            {
                lines = _renderer.RenderCommand(rootCommand, TerminalWidth());
            }
            else
            {
                var group = helpProvider.GetGroupHelp(helpException.Path)
                    ?? throw new KeyNotFoundException($"Group help not found for path '{string.Join(" ", helpException.Path)}'.");
                lines = _renderer.RenderGroup(group, TerminalWidth());
            }
        }

        WriteLines(lines);
        return 0;
    }

    private void WriteLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }
    }

    /// <summary>
    /// Yields one <see cref="RuntimeHelpGroup"/> per discovered (package, verb) pair.
    /// Discovery is authoritative for installed state; built-ins are used only as tie-breaker
    /// precedence when two discovered extensions claim the same verb.
    /// </summary>
    private IEnumerable<RuntimeHelpGroup> EnumerateAllVerbRoots()
    {
        var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discovered = DiscoverInstalledExtensions();
        var ordered = discovered
            .OrderByDescending(info => _builtInPackageIds.Contains(info.PackageId))
            .ThenBy(info => info.PackageId, StringComparer.OrdinalIgnoreCase);

        foreach (var info in ordered)
        {
            var verbs = GetOwnedVerbs(info);
            foreach (var verb in verbs)
            {
                if (!seenVerbs.Add(verb))
                {
                    // First owner wins; ordering above ensures built-ins win when present.
                    continue;
                }

                RuntimeHelpGroup? root;
                try
                {
                    root = new ExtensionIndexHelpAdapter(info.Commands, verb).GetRootGroup();
                }
                catch
                {
                    continue;
                }

                yield return root;
            }
        }
    }

    private IReadOnlyList<DiscoveredExtensionInfo> DiscoverInstalledExtensions()
    {
        var discoveredInfos = new List<DiscoveredExtensionInfo>();
        var outputStreams = new OutputStreams(
            WriteOutput: async (result, _) =>
            {
                if (result is List<DiscoveredExtensionInfo> infos)
                {
                    discoveredInfos.AddRange(infos);
                }

                await Task.CompletedTask;
            },
            WriteVerbose: _streams.WriteVerbose,
            WriteDebug: _streams.WriteDebug,
            WriteWarning: _streams.WriteWarning,
            WriteError: _streams.WriteError,
            WriteInformation: _streams.WriteInformation);

        try
        {
            using var session = _engine.OpenDiscoverySession(
                extensionNames: [],
                commandName: null,
                outputStreams: outputStreams,
                isTransient: true);

            session.RunAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return [];
        }

        var discovered = discoveredInfos
            .Where(static info => !string.IsNullOrWhiteSpace(info.PackageId))
            .ToList();

        return SelectPreferredCandidates(discovered);
    }

    private IReadOnlyList<DiscoveredExtensionInfo> SelectPreferredCandidates(IReadOnlyList<DiscoveredExtensionInfo> discovered)
    {
        if (discovered.Count == 0)
        {
            return discovered;
        }

        ExtensionRegistry? registry = null;
        try
        {
            registry = new FileExtensionRegistryStore().Load();
        }
        catch
        {
            // Help discovery should remain resilient in degraded environments.
        }

        var selected = new List<DiscoveredExtensionInfo>();
        foreach (var group in discovered.GroupBy(static info => info.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            ExtensionRegistryEntry? persisted = null;
            if (registry is not null && registry.Extensions.TryGetValue(group.Key, out var entry))
            {
                persisted = entry;
            }

            var selection = ExtensionCandidateSelector.Select(group.ToList(), persisted);
            EmitSelectionDebug(
                context: "RuntimeHelpDispatcher.SelectPreferredCandidates",
                selection: selection,
                persistedPreference: persisted);
            selected.Add(selection.Selected);
        }

        return selected;
    }

    private void EmitSelectionDebug(
        string context,
        SelectedExtensionCandidate selection,
        ExtensionRegistryEntry? persistedPreference)
    {
        try
        {
            _streams.WriteDebug(
                ExtensionCandidateSelector.BuildDiagnostics(context, selection, persistedPreference),
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Help rendering must remain resilient even if debug sink fails.
        }
    }

    private bool TryResolveExtension(IReadOnlyList<DiscoveredExtensionInfo> discovered, string verb, out DiscoveredExtensionInfo extensionInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);

        var match = discovered
            .Where(info => OwnsVerb(info, verb))
            .OrderByDescending(info => _builtInPackageIds.Contains(info.PackageId))
            .ThenBy(info => info.PackageId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (match is null)
        {
            extensionInfo = null!;
            return false;
        }

        extensionInfo = match;
        return true;
    }

    private static IReadOnlyList<string> GetOwnedVerbs(DiscoveredExtensionInfo info)
    {
        if (info.Verbs.Count > 0)
        {
            return info.Verbs;
        }

        return info.Commands.Keys
            .Select(static command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault())
            .Where(static verb => !string.IsNullOrWhiteSpace(verb))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private static bool OwnsVerb(DiscoveredExtensionInfo info, string verb)
    {
        if (info.Verbs.Any(v => v.Equals(verb, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return info.Commands.Keys.Any(command =>
            command.Equals(verb, StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase));
    }
    private static uint? TerminalWidth()
    {
        try
        {
            var width = (uint)Console.WindowWidth;
            return width > 0 ? width : null;
        }
        catch
        {
            return null;
        }
    }
}
