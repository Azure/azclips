using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.Cli;
using Core.Cli.Parser;
using Core.Configuration.Abstractions;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common.Recording;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Scenario.Tests;

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
    private const string ResourcesPackageId = "Microsoft.Azure.Azclips.Extensions.Resources";

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
    private readonly List<object> _outputs = [];
    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="useLiveAuth">
    /// Record mode needs a real token, so it keeps the host's real profile/auth/config and the
    /// signed-in profile. Playback stubs them and needs no credentials.
    /// </param>
    public ScenarioHost(ClientType clientType, ITracingInterceptor interceptor, HttpMessageHandler? backendHandler = null, bool useLiveAuth = false)
    {
        _clientType = clientType;

        foreach (var key in s_environmentKeys)
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }

        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IAssemblyLoadingProvider>(
                new StubExtensionLoader(typeof(Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex).Assembly, "Resources"));
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
                services.AddSingleton<IArgumentParsingProvider, Microsoft.Azure.Azclips.Cmdlets.Login.PSArgumentParsingProvider>();
            }
        });
        AzclipsServiceProvider.Initialize(clientType);
    }

    /// <summary>
    /// Parses <paramref name="args"/> with the real <see cref="ExtensionDispatcher"/> (against an
    /// in-memory registry that maps the <c>group</c> verb to the Resources extension) and runs the
    /// resulting invocation the way <c>Program.Main</c> does.
    /// </summary>
    public IReadOnlyList<object> RunCli(params string[] args)
    {
        if (_clientType != ClientType.CLI)
        {
            throw new InvalidOperationException($"This host was booted for {_clientType}; create a ClientType.CLI host to run CLI commands.");
        }

        var registry = ResourcesRegistry();
        var dispatcher = new ExtensionDispatcher(
            new ExtensionResolver(new InMemoryRegistryStore(registry)),
            () => LeafIndex.Build(BuiltInExtensions.Map, registry));
        var invocation = dispatcher.Parse(args);
        return Execute(invocation, useAsyncSession: true);
    }

    /// <summary>
    /// Runs the invocation a cmdlet's <c>ProcessRecord</c> would build (same extension name,
    /// command name, and parameter dictionary shape as e.g. <c>NewResourceGroupCmdlet</c>)
    /// through the engine's synchronous session path, as PowerShell hosts do.
    /// </summary>
    public IReadOnlyList<object> RunPowerShell(string commandName, IDictionary<string, object> parameters)
    {
        if (_clientType != ClientType.PowerShell)
        {
            throw new InvalidOperationException($"This host was booted for {_clientType}; create a ClientType.PowerShell host to run cmdlet invocations.");
        }

        var invocation = new CommandInvocation("Resources", commandName, parameters, IsTransient: false, ModuleName: "Resources");
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

    private static ExtensionRegistry ResourcesRegistry()
    {
        var registry = new ExtensionRegistry();
        registry.Extensions[ResourcesPackageId] = new ExtensionRegistryEntry
        {
            Version = "1.0.0",
            Contributions =
            [
                new Contribution { Verb = "group", Path = "" },
                new Contribution { Verb = "group", Path = "create" },
                new Contribution { Verb = "group", Path = "show" },
                new Contribution { Verb = "group", Path = "list" },
            ],
            Namespaces = ["group"],
        };
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
