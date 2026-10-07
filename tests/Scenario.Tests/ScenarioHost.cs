using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.Cli;
using Core.Cli.Parser;
using Core.Configuration.Abstractions;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Tests.Recording;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Scenario.Tests;

/// <summary>The extension a scenario drives: its short name, index assembly, and CLI leaves.</summary>
internal sealed record ExtensionUnderTest(string Name, string PackageId, System.Reflection.Assembly Assembly, string Verb, string[] Leaves)
{
    public static readonly ExtensionUnderTest Resources = new(
        "Resources",
        "Microsoft.Azure.Azclips.Extensions.Resources",
        typeof(Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex).Assembly,
        "group",
        ["", "create", "delete", "exists", "show", "list", "update"]);

    public static readonly ExtensionUnderTest ResourcesDeployments = new(
        "Resources",
        "Microsoft.Azure.Azclips.Extensions.Resources",
        typeof(Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex).Assembly,
        "deployment",
        [
            "",
            "mg",
            "mg create",
            "mg list",
            "mg show",
            "mg cancel",
            "mg delete",
            "mg export",
            "mg validate",
            "mg what-if",
            "mg wait",
        ]);

    public static readonly ExtensionUnderTest Compute = new(
        "Compute",
        "Microsoft.Azure.Azclips.Extensions.Compute",
        typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly,
        "vm",
        ["", "create", "create-basic", "show", "list", "start", "stop", "deallocate", "delete"]);

    public static readonly ExtensionUnderTest ManagedServiceIdentity = new(
        "ManagedServiceIdentity",
        "Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity",
        typeof(Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex).Assembly,
        "identity",
        ["create", "list", "show", "update", "delete"]);

    public static readonly ExtensionUnderTest Network = new(
        "Network",
        "Microsoft.Azure.Azclips.Extensions.Network",
        typeof(Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex).Assembly,
        "network",
        ["vnet create", "vnet show", "vnet list", "vnet delete",
         "public-ip create", "public-ip show", "public-ip list", "public-ip delete"]);
}

/// <summary>
/// Boots the full Azclips host for one client surface and runs commands subcutaneously, through
/// the real dispatcher/binder and engine, with the supplied interceptor filling the record/playback
/// seam. <paramref name="backendHandler"/> stands in for the network in record-mode tests; leave it
/// null when the interceptor short-circuits (playback).
/// <para>
/// <see cref="AzclipsServiceProvider"/> is process-wide and the client type is fixed at init, so one
/// host serves one surface. Tests that use it must live in the non-parallel "Scenario Tests" collection.
/// </para>
/// </summary>
internal sealed class ScenarioHost : IDisposable
{
    // Cleared for the host's lifetime so ambient developer settings can't leak into runs.
    // Mirrors the isolation list used by ConfigCliTests.
    private static readonly string[] s_environmentKeys =
    [
        "AZCLIPS_LOG_LEVEL",
        "ZIPKIN_ENDPOINT",
        "PS_HTTP_MAX_RETRIES",
        "AZURE_PS_HTTP_MAX_RETRIES",
        "PS_HTTP_MAX_RETRIES_FOR_429",
        "AZURE_PS_HTTP_MAX_RETRIES_FOR_429"
    ];

    private readonly ClientType _clientType;
    private readonly ExtensionUnderTest[] _extensions;
    private readonly List<object> _outputs = [];
    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="useLiveAuth">
    /// Record mode needs a real token, so it keeps the host's real profile/auth/config and the
    /// signed-in profile. Playback stubs them and needs no credentials.
    /// </param>
    /// <param name="extensions">Extensions the scenario drives; defaults to Resources.</param>
    public ScenarioHost(ClientType clientType, ITracingInterceptor interceptor, HttpMessageHandler? backendHandler = null, bool useLiveAuth = false, params ExtensionUnderTest[] extensions)
    {
        _clientType = clientType;
        _extensions = extensions.Length > 0 ? extensions : [ExtensionUnderTest.Resources];

        foreach (var key in s_environmentKeys)
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }

        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider>(
                new StubExtensionLoader(_extensions.ToDictionary(e => e.Name, e => e.Assembly)));
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton(interceptor);

            // Stubbing auth in record mode would send a fake token and get a 401 instead of a recording.
            if (!useLiveAuth)
            {
                services.AddSingleton<IConfigurationProvider>(new InMemoryConfigurationProvider());
                services.AddSingleton<IProfileService>(new StubProfileService());
                services.AddSingleton<IAuthService, StubAuthService>();
            }
            if (backendHandler is not null)
            {
                services.AddSingleton(new HttpClient(backendHandler));
            }

            // Each surface binds arguments with its real provider; that binding difference
            // is exactly what the parity tests exercise.
            if (clientType == ClientType.CLI)
            {
                services.AddSingleton<PropertyParser>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
            }
            else
            {
                services.AddSingleton<IArgumentParsingProvider, Microsoft.Azure.Azclips.Cmdlets.Accounts.PSArgumentParsingProvider>();
            }
        });
        AzclipsServiceProvider.Initialize(clientType);
    }

    /// <summary>
    /// Parses <paramref name="args"/> with the real <see cref="ExtensionDispatcher"/>, against an
    /// in-memory registry built from the scenario's extensions, and runs the resulting invocation
    /// the way <c>Program.Main</c> does.
    /// </summary>
    public IReadOnlyList<object> RunCli(params string[] args)
    {
        if (_clientType != ClientType.CLI)
        {
            throw new InvalidOperationException($"This host was booted for {_clientType}; create a ClientType.CLI host to run CLI commands.");
        }

        var registry = BuildRegistry(_extensions);
        var dispatcher = new ExtensionDispatcher(
            new ExtensionResolver(new InMemoryRegistryStore(registry)),
            () => LeafIndex.Build(registry));
        var invocation = dispatcher.Parse(args);
        return Execute(invocation, useAsyncSession: true);
    }

    /// <summary>
    /// The subscription of the signed-in profile. Only meaningful with live auth; record-mode
    /// scenarios use it for commands whose subscription argument is required.
    /// </summary>
    public string LiveSubscriptionId()
    {
        var profiles = AzclipsServiceProvider.GetRequiredService<IProfileService>();
        var active = profiles.GetActiveProfileAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("No active profile; sign in with the azclips CLI before recording.");
        return active.SubscriptionId
            ?? throw new InvalidOperationException("The active profile has no subscription; log in with a subscription before recording.");
    }

    /// <summary>
    /// Runs the invocation a cmdlet's <c>ProcessRecord</c> would build (same extension name,
    /// command name, and parameter dictionary shape as e.g. <c>NewResourceGroupCmdlet</c>)
    /// through the engine's synchronous session path, as PowerShell hosts do.
    /// </summary>
    public IReadOnlyList<object> RunPowerShell(string commandName, IDictionary<string, object> parameters, string? extension = null)
    {
        if (_clientType != ClientType.PowerShell)
        {
            throw new InvalidOperationException($"This host was booted for {_clientType}; create a ClientType.PowerShell host to run cmdlet invocations.");
        }

        var extensionName = extension ?? _extensions[0].Name;
        var invocation = new CommandInvocation(extensionName, commandName, parameters, IsTransient: false, ModuleName: extensionName);
        return Execute(invocation, useAsyncSession: false);
    }

    private IReadOnlyList<object> Execute(CommandInvocation invocation, bool useAsyncSession)
    {
        _outputs.Clear();

        var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        var streams = new OutputStreams(
            WriteOutput: (value, _) =>
            {
                _outputs.Add(value);
                return Task.CompletedTask;
            },
            WriteVerbose: (_, _) => Task.CompletedTask,
            WriteDebug: (_, _) => Task.CompletedTask,
            WriteWarning: (_, _) => Task.CompletedTask,
            WriteError: (_, _) => Task.CompletedTask,
            WriteInformation: (_, _) => Task.CompletedTask);

        using ISession session = engine.OpenExecutionSession(invocation, streams, CancellationToken.None);
        if (useAsyncSession)
        {
            session.RunAsync().GetAwaiter().GetResult();
        }
        else
        {
            session.Run();
        }

        return _outputs.ToArray();
    }

    private static ExtensionRegistry BuildRegistry(ExtensionUnderTest[] extensions)
    {
        var registry = new ExtensionRegistry();
        foreach (var extension in extensions)
        {
            registry.Extensions[extension.PackageId] = new ExtensionRegistryEntry
            {
                Version = "1.0.0",
                Contributions = [.. extension.Leaves.Select(leaf => new Contribution { Verb = extension.Verb, Path = leaf })],
                Namespaces = [extension.Verb],
            };
        }
        return registry;
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();

        foreach (var entry in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }
}
