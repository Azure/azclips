// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Exceptions;
using Core.Cli.Output;
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
    private readonly ExtensionHintTable _hintTable;
    private readonly TextWriter _output;
    private readonly OutputStreams _streams;

    public RuntimeHelpDispatcher(IEngine engine, OutputStreams streams)
        : this(engine, streams, output: null)
    {
    }

    public RuntimeHelpDispatcher(IEngine engine, OutputStreams streams, TextWriter? output)
        : this(
            engine,
            ExtensionHintTable.Default,
            new RuntimeHelpRenderer(ShouldBoldHeaders(output)),
            output ?? Console.Out,
            streams)
    {
    }

    internal static bool ShouldBoldHeaders(TextWriter? output)
    {
        if (ReferenceEquals(output, Console.Error))
        {
            return ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected);
        }

        if (output is null || ReferenceEquals(output, Console.Out))
        {
            return ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected);
        }

        return false;
    }

    internal RuntimeHelpDispatcher(
        IEngine engine,
        ExtensionHintTable hintTable,
        IHelpRenderer renderer,
        TextWriter output,
        OutputStreams streams)
    {
        _engine = engine;
        _hintTable = hintTable;
        _renderer = renderer;
        _output = output;
        _streams = streams;
    }

    /// <summary>
    /// Supplies the package ids routing selected. When set and non-empty, help discovers only those
    /// packages, and each loads the copy routing selected, so help shows the commands dispatch runs.
    /// </summary>
    internal Func<IReadOnlyList<string>>? SelectedPackageIds { get; init; }

    /// <summary>Renders the top-level <c>az</c> help (every registered extension's root groups).</summary>
    public int RenderRoot()
    {
        var rootGroups = EnumerateAllVerbRoots().ToArray();
        WriteLines(_renderer.RenderRoot(rootGroups, TerminalWidth()));
        return 0;
    }

    /// <summary>Renders help for a group or command path. <paramref name="path"/>[0] is the verb; extra
    /// tokens indicate a command-help request (the caller supplies whatever tokens the user typed after
    /// <c>--help</c> / <c>help</c>).</summary>
    public int RenderExtensionHelp(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            // Root help is handled by RenderRoot; reaching here with an empty path is a programmer error.
            throw new ArgumentException("Path must be non-empty; root help is handled by RenderRoot.", nameof(path));
        }

        var verb = path[0];
        // Command-help hint; branches below still fall back to group help if no command matches.
        var isCommandHelp = path.Count > 1;

        var discovered = DiscoverInstalledExtensions();
        if (!TryResolveExtension(discovered, verb, out var extensionInfo))
        {
            // Intrinsic host verbs (e.g. `version`) are not owned by any installed extension, but
            // their help still lives in the markdown corpus so `az <verb> --help` keeps working.
            if (TryRenderIntrinsicHelp(verb, out var intrinsicExitCode))
            {
                return intrinsicExitCode;
            }

            // Matches the execute-path UX in ExtensionDispatcher.Parse.
            if (_hintTable.TryGetPackageId(verb, out var hintedPackage))
            {
                throw new ExtensionNotInstalledException(verb, hintedPackage);
            }

            // Truly unknown verb (no install hint either). Surface as a known error so the
            // top-level AzclipsErrorException catch renders it cleanly.
            throw new AzclipsErrorException(
                code: "VerbNotFound",
                exitCode: 4,
                message: $"No installed extension owns verb '{verb}'. Run 'az extension list' to see installed extensions.",
                details: new Dictionary<string, object?> { ["verb"] = verb });
        }

        var helpProvider = new ExtensionIndexHelpAdapter(extensionInfo.Commands, verb);

        IEnumerable<string> lines;
        if (isCommandHelp)
        {
            // Extra tokens may name an intermediate group; fall back to group help if no command matches.
            var command = helpProvider.GetCommandHelp(path);
            if (command != null)
            {
                lines = _renderer.RenderCommand(command, TerminalWidth());
            }
            else
            {
                var group = helpProvider.GetGroupHelp(path)
                    ?? throw new KeyNotFoundException($"No help found for path '{string.Join(" ", path)}'.");
                lines = _renderer.RenderGroup(group, TerminalWidth());
            }
        }
        else
        {
            // A verb root can also be a runnable command (e.g. "az login" is both).
            var rootCommand = helpProvider.GetCommandHelp(path);
            if (rootCommand != null)
            {
                lines = _renderer.RenderCommand(rootCommand, TerminalWidth());
            }
            else
            {
                var group = helpProvider.GetGroupHelp(path)
                    ?? throw new KeyNotFoundException($"Group help not found for path '{string.Join(" ", path)}'.");
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

    // Renders help for an intrinsic host verb straight from its markdown doc, bypassing discovery
    // and the ExtensionIndex adapter (there is no extension to project). Returns false for
    // non-intrinsic verbs so the caller can fall through to the hint-table / not-found path.
    private bool TryRenderIntrinsicHelp(string verb, out int exitCode)
    {
        exitCode = 0;

        // Accept both the bare verb (`az version --help`) and the top-level-flag form
        // (`az --version --help`)
        if (!IntrinsicVerbs.TryResolve(verb, out var canonicalVerb))
        {
            return false;
        }

        var markdown = MarkdownCommandHelpProvider.Get(canonicalVerb, string.Empty);
        var examples = markdown.Examples
            .Where(static example => !example.IsPowerShell)
            .Select(static example => new RuntimeHelpExample(example.Title, example.Command))
            .ToList();

        var command = new RuntimeHelpCommand(
            CommandPath: canonicalVerb,
            Summary: markdown.Summary,
            Description: markdown.Description,
            Parameters: [],
            Examples: examples,
            Tags: []);

        WriteLines(_renderer.RenderCommand(command, TerminalWidth()));
        return true;
    }

    /// <summary>
    /// Yields one <see cref="RuntimeHelpGroup"/> per discovered (package, verb) pair.
    /// Discovery is authoritative for installed state; deterministic package-id order is the
    /// tiebreak when multiple discovered extensions claim the same verb.
    /// </summary>
    private IEnumerable<RuntimeHelpGroup> EnumerateAllVerbRoots()
    {
        var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discovered = DiscoverInstalledExtensions();
        LeafIndex? leafIndex = null;
        try
        {
            var routingRegistry = new ExtensionRegistry
            {
                Extensions = discovered.ToDictionary(
                    static info => info.PackageId,
                    static info => new ExtensionRegistryEntry
                    {
                        Version = info.ResolvedVersion,
                        Contributions = CliExtensionServiceRegistration.BuildContributionProjection(info).Contributions,
                    },
                    StringComparer.OrdinalIgnoreCase),
            };
            leafIndex = LeafIndex.Build(routingRegistry);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TryWriteDebug($"RuntimeHelpDispatcher: routing conflict; root command tags suppressed: {ex.Message}");
        }
        var ordered = discovered
            .OrderBy(info => info.PackageId, StringComparer.OrdinalIgnoreCase);

        foreach (var info in ordered)
        {
            var verbs = GetOwnedVerbs(info);
            foreach (var verb in verbs)
            {
                if (!seenVerbs.Add(verb))
                {
                    continue;
                }

                if (!TryResolveExtensionCore(discovered, verb, out var rootOwner))
                {
                    TryWriteDebug($"RuntimeHelpDispatcher: no owner found for discovered verb '{verb}'.");
                    continue;
                }

                RuntimeHelpGroup? root;
                try
                {
                    root = new ExtensionIndexHelpAdapter(rootOwner.Commands, verb).GetRootGroup();
                    if (leafIndex is null || leafIndex.IsCommandGroup(verb, string.Empty))
                    {
                        root = root with { RootCommandTags = [] };
                    }
                }
                catch (Exception ex)
                {
                    TryWriteDebug($"RuntimeHelpDispatcher: failed to build root help for '{rootOwner.PackageId}' verb '{verb}': {ex.Message}");
                    continue;
                }

                yield return root;
            }
        }

        // Intrinsic host verbs (cold-path commands like `version`) aren't discovered as extensions,
        // so surface them from the markdown corpus to keep them listed in `az --help`.
        foreach (var intrinsic in IntrinsicVerbs.All)
        {
            if (!seenVerbs.Add(intrinsic.Name))
            {
                continue;
            }

            var summary = MarkdownCommandHelpProvider.Get(intrinsic.Name, string.Empty).Summary;
            yield return new RuntimeHelpGroup(intrinsic.Name, summary, [], []);
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

        IReadOnlyList<string> selectedPackageIds = [];
        try
        {
            selectedPackageIds = SelectedPackageIds?.Invoke() ?? [];
        }
        catch (Exception ex)
        {
            TryWriteDebug($"RuntimeHelpDispatcher: routing selection unavailable: {ex.Message}");
        }

        // An empty name list asks discovery for every copy on disk; help must only describe
        // the copies routing selected, so with no selection there are no extensions to show.
        if (selectedPackageIds.Count == 0)
        {
            TryWriteDebug("RuntimeHelpDispatcher: routing selected no extensions.");
            return [];
        }

        try
        {
            using var session = _engine.OpenDiscoverySession(
                extensionNames: selectedPackageIds.ToList(),
                commandName: null,
                outputStreams: outputStreams,
                isTransient: true);

            session.RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            TryWriteDebug($"RuntimeHelpDispatcher: discovery session failed: {ex.Message}");
            return [];
        }

        // Routing already chose one copy per package; discovery loaded only those.
        return discoveredInfos
            .Where(static info => !string.IsNullOrWhiteSpace(info.PackageId))
            .ToList();
    }

    private void TryWriteDebug(string message)
    {
        try
        {
            _streams.WriteDebug(message, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Keep help paths resilient even if debug output is unavailable.
        }
    }

    private bool TryResolveExtension(IReadOnlyList<DiscoveredExtensionInfo> discovered, string verb, out DiscoveredExtensionInfo extensionInfo)
        => TryResolveExtensionCore(discovered, verb, out extensionInfo);

    // Prefer the root-leaf owner so `az <verb> --help` matches where `az <verb>` dispatches.
    internal static bool TryResolveExtensionCore(IReadOnlyList<DiscoveredExtensionInfo> discovered, string verb, out DiscoveredExtensionInfo extensionInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);

        var match = discovered
            .Where(info => OwnsVerb(info, verb))
            .OrderByDescending(info => OwnsRootLeaf(info, verb))
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

    private static bool OwnsRootLeaf(DiscoveredExtensionInfo info, string verb)
        => info.Commands.Keys.Any(command => command.Equals(verb, StringComparison.OrdinalIgnoreCase));

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
    private static uint? TerminalWidth() =>
        TerminalWidthProvider.Get() is { } width ? (uint)width : null;
}
