// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Extensions;

namespace Core.Cli.Parser;
public class ExtensionDispatcher //: IParser
{
    private readonly IExtensionResolver _resolver;
    private readonly Lazy<LeafIndex> _leafIndex;
    private readonly ILoggingService? _loggingService;

    public ExtensionDispatcher()
        : this(
            new ExtensionResolver(new EmptyExtensionRegistryStore()),
            () => LeafIndex.Build(new ExtensionRegistry()),
            null)
    {
    }

    public ExtensionDispatcher(IExtensionResolver resolver)
        : this(resolver, () => LeafIndex.Build(new ExtensionRegistry()), null)
    {
    }

    /// <summary>Test-only constructor: inject both the verb resolver and an explicit leaf-index factory.</summary>
    public ExtensionDispatcher(IExtensionResolver resolver, Func<LeafIndex> leafIndexFactory)
        : this(resolver, leafIndexFactory, null)
    {
    }

    public ExtensionDispatcher(IExtensionResolver resolver, Func<LeafIndex> leafIndexFactory, ILoggingService? loggingService)
    {
        _resolver = resolver;
        _leafIndex = new Lazy<LeafIndex>(leafIndexFactory);
        _loggingService = loggingService;
    }

    public CommandInvocation Parse(string[] args)
    {
        // Defensive: GlobalArgParser routes bare / help / version invocations before Main reaches
        // dispatch, so empty args here is a programmer error, not a user request.
        if (args.Length == 0)
        {
            throw new ArgumentException("Command args cannot be empty; help / version dispatch happens in Program.Main.", nameof(args));
        }

        if (string.IsNullOrWhiteSpace(args[0])) throw AzclipsErrorException.UsageError("Command-group token cannot be empty or whitespace.");

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
                resolved = new ResolvedExtension(verb, leaf.PackageId, leaf.Version)
                {
                    AssemblyReference = leaf.AssemblyReference,
                };
                matchedPathTokens = len;
                break;
            }
        }

        // Fallback to verb-only resolution
        if (resolved is null)
        {
            // Authoritative discovery must not be bypassed by stale registry command surfaces.
            if (_leafIndex.Value.IsAuthoritative && _leafIndex.Value.HasVerb(verb))
            {
                throw new CommandNotFoundException(verb, nonFlagLeadingArgs, args.Skip(1).ToArray());
            }

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

                // Intrinsic verb with trailing tokens: precise usage error instead of "command group not found".
                if (IntrinsicVerbs.TryResolve(verb, out var intrinsicVerb))
                {
                    var trailing = string.Join(" ", args.Skip(1));
                    throw AzclipsErrorException.UsageError(
                        $"Unrecognized arguments for '{intrinsicVerb}': {trailing}",
                        new Dictionary<string, object?>
                        {
                            ["verb"] = intrinsicVerb,
                            ["arguments"] = args.Skip(1).ToArray(),
                        });
                }

                // Generic miss → OrchestratorEngine hands off to AI fallback.
                throw new KeyNotFoundException(
                    $"Command group '{verb}' not found. Run 'az extension list' to see installed extensions.");
            }

            if (_leafIndex.Value.HasSelectedAssembly(resolved.PackageId))
            {
                throw new CommandNotFoundException(verb, nonFlagLeadingArgs, args.Skip(1).ToArray());
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

            // matchedPathTokens == 0 means longest-prefix matched only the root leaf (verb, "")
            // — no deeper path leaf — so the invocation may name a command group (a root like
            // `agent`, or an intermediate group like `agent tool`) that has no runnable leaf.
            // Surface CommandNotFound with a `--help` hint rather than dispatching it to the
            // extension, which would throw a generic "command not found" and fall through to the AI
            // agent. The matchedPathTokens > 0 branch above hit an exact deeper leaf and can't reach
            // here. Mirrors the verb-only fallback above.
            if (_leafIndex.Value.IsCommandGroup(verb, string.Join(" ", nonFlagLeadingArgs)))
            {
                throw new CommandNotFoundException(verb, nonFlagLeadingArgs, args.Skip(1).ToArray());
            }
        }

        _loggingService?.Debug($"[{nameof(ExtensionDispatcher)}] Dispatched command '{commandName}' to extension '{extensionName}' (version: {(string.IsNullOrWhiteSpace(resolved.Version) ? "default" : resolved.Version)}).");

        return new CommandInvocation(
            extensionName,
            commandName,
            parameters,
            IsTransient: true,
            ModuleName: extensionName,
            ModuleVersion: resolved.Version)
        {
            AssemblyReference = resolved.AssemblyReference,
        };
    }
}
