// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Extensions;

namespace Core.Cli.Parser;
public class ExtensionDispatcher //: IParser
{
    // Flag forms only. Literal `help` is excluded so it isn't mistaken for a positional
    // value; the `az help <group>` subcommand form is matched explicitly via args[0].
    private static readonly string[] s_helpFlags = ["--help", "-h", "-?", "?"];

    private readonly record struct HelpRequest(string Verb, IReadOnlyList<string> Path, bool IsCommandHelp);

    private readonly IExtensionResolver _resolver;
    private readonly Lazy<LeafIndex> _leafIndex;

    public ExtensionDispatcher()
        : this(new FileExtensionRegistryStore())
    {
    }

    private ExtensionDispatcher(FileExtensionRegistryStore store)
        : this(
            new ExtensionResolver(store),
            () => LeafIndex.Build(BuiltInExtensions.Map, store.Load()))
    {
    }

    public ExtensionDispatcher(IExtensionResolver resolver)
        : this(resolver, () => LeafIndex.Build(BuiltInExtensions.Map, new ExtensionRegistry()))
    {
    }

    /// <summary>Test-only constructor: inject both the verb resolver and an explicit leaf-index factory.</summary>
    public ExtensionDispatcher(IExtensionResolver resolver, Func<LeafIndex> leafIndexFactory)
    {
        _resolver = resolver;
        _leafIndex = new Lazy<LeafIndex>(leafIndexFactory);
    }

    public CommandInvocation Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
        if (string.IsNullOrWhiteSpace(args[0])) throw new ArgumentException("Command-group token cannot be empty or whitespace.", nameof(args));

        // Bare top-level help: `az --help`, `az -h`, `az help`, etc.
        if (IsBareTopLevelHelp(args))
        {
            throw new DispatchHelpException(this);
        }

        if (TryCreateHelpRequest(args, out var helpRequest))
        {
            throw new RuntimeExtensionHelpException(helpRequest.Verb, helpRequest.Path, helpRequest.IsCommandHelp);
        }

        var verb = args[0];

        // Longest-prefix match against the (verb, path) leaf index; first hit wins.
        var nonFlagLeadingArgs = args
            .Skip(1)
            .TakeWhile(a => !a.StartsWith("-", StringComparison.Ordinal))
            .ToArray();

        ResolvedExtension? resolved = null;
        int matchedPathTokens = 0;

        for (int len = nonFlagLeadingArgs.Length; len >= 0; len--)
        {
            var path = string.Join(" ", nonFlagLeadingArgs, 0, len);
            if (_leafIndex.Value.TryGet(verb, path, out var leaf) && leaf is not null)
            {
                resolved = new ResolvedExtension(verb, leaf.PackageId, leaf.Version);
                matchedPathTokens = len;
                break;
            }
        }

        // Fallback to verb-only resolution
        if (resolved is null)
        {
            if (!_resolver.TryResolve(verb, out resolved) || resolved is null)
            {
                for (int len = nonFlagLeadingArgs.Length; len >= 0; len--)
                {
                    var path = string.Join(" ", nonFlagLeadingArgs, 0, len);
                    if (ExtensionHintTable.Default.TryGetPackageId(verb, path, out var hintedPackage))
                    {
                        throw new ExtensionNotInstalledException(verb, hintedPackage);
                    }
                }

                // Generic miss → OrchestratorEngine hands off to AI fallback.
                throw new KeyNotFoundException(
                    $"Command group '{verb}' not found. Run 'az extension list' to see installed extensions.");
            }
        }

        // Strip a known package prefix; the loader re-prepends it. Case-insensitive because
        // registry keys are stored OrdinalIgnoreCase and NuGet ids are case-insensitive.
        var extensionName = Constants.StripKnownExtensionPrefix(resolved.PackageId);

        var extensionArgs = args.Skip(1 + matchedPathTokens).ToArray();
        var extensionParser = new ExtensionParser();

        string commandName;
        IDictionary<string, object> parameters;

        if (matchedPathTokens > 0)
        {
            var residualPositional = extensionArgs
                .TakeWhile(a => !a.StartsWith("-", StringComparison.Ordinal))
                .ToArray();
            var flagArgs = extensionArgs.Skip(residualPositional.Length).ToArray();
            (_, parameters) = extensionParser.Parse(flagArgs);
            if (residualPositional.Length > 0)
            {
                parameters[string.Empty] = new List<List<string>> { residualPositional.ToList() };
            }

            var matchedPath = string.Join(" ", nonFlagLeadingArgs, 0, matchedPathTokens);
            commandName = $"{verb} {matchedPath}";
        }
        else
        {
            (string subCommand, parameters) = extensionParser.Parse(extensionArgs);
            commandName = string.IsNullOrEmpty(subCommand) ? verb : $"{verb} {subCommand}";
        }

        return new CommandInvocation(
            extensionName,
            commandName,
            parameters,
            IsTransient: true,
            ModuleName: extensionName,
            ModuleVersion: resolved.Version);
    }

    private static bool IsBareTopLevelHelp(string[] args)
        => args.Length == 1 && (IsHelpToken(args[0]) || IsHelpSubcommand(args[0]));

    private static bool IsHelpSubcommand(string token)
        => token.Equals("help", StringComparison.OrdinalIgnoreCase);

    private static bool IsHelpToken(string token)
        => s_helpFlags.Contains(token, StringComparer.OrdinalIgnoreCase);

    private static bool TryCreateHelpRequest(string[] args, out HelpRequest request)
    {
        request = default;

        // Two recognized shapes:
        //   1. `az help <group> [...]`        — strip args[0]
        //   2. `az <group> [...] --help [...]` — strip the help flag in place
        string[] targetTokens;
        if (IsHelpSubcommand(args[0]))
        {
            targetTokens = StripHelpFlags(args[1..]);
        }
        else if (args.Skip(1).Any(IsHelpToken))
        {
            targetTokens = StripHelpFlags(args);
        }
        else
        {
            return false;
        }

        if (targetTokens.Length == 0)
        {
            return false;
        }

        var groupToken = targetTokens[0];
        if (string.IsNullOrWhiteSpace(groupToken))
        {
            return false;
        }

        var verb = groupToken.ToLowerInvariant();

        var commandSegments = targetTokens
            .Skip(1)
            .TakeWhile(static token => !token.StartsWith('-'))
            .Select(static token => token.ToLowerInvariant())
            .ToArray();

        var path = new List<string> { verb };
        path.AddRange(commandSegments);

        request = new HelpRequest(verb, path, commandSegments.Length > 0);
        return true;
    }

    private static string[] StripHelpFlags(string[] tokens)
        => tokens.Where(static t => !IsHelpToken(t)).ToArray();
}
