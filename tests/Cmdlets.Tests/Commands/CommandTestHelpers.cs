// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Core.Engine;
using Core.Runtime;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests.Commands;

/// <summary>
/// Builds a real <see cref="Engine"/> for one extension with every outside dependency mocked, and a PowerShell
/// runspace that runs one real cmdlet against it. The real PowerShell binder, engine, PS argument binder, extension and
/// HTTP pipeline all run; only the wire is a mock.
/// </summary>
internal static class CommandTestHelpers
{
    internal const string TenantId = "11111111-1111-1111-1111-111111111111";
    internal const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// Creates an engine that loads <paramref name="extension"/>, signs in as a fixed profile, and sends every
    /// HTTP request to <paramref name="mockWire"/>. Create the wire mock with <see cref="MockBehavior.Strict"/>
    /// so a request the test did not set up throws <see cref="MockException"/>.
    /// </summary>
    internal static Engine CreateEngine(Assembly extension, Mock<ISendAsync> mockWire)
    {
        var profile = new Profile { Name = "test", TenantId = TenantId, SubscriptionId = SubscriptionId };
        var mockProfiles = new Mock<IProfileService>();
        mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile.Name);
        mockProfiles.Setup(p => p.GetProfileAsync(profile.Name, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        mockProfiles.Setup(p => p.GetProfileKeysForDefault()).Returns([nameof(Profile.SubscriptionId)]);
        mockProfiles.Setup(p => p.GetProfileValueByKey(It.IsAny<Profile?>(), nameof(Profile.SubscriptionId)))
            .Returns((Profile? p, string _) => p?.SubscriptionId);

        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ReturnsAsync(new AuthResult { AccessToken = "token", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), TenantId = TenantId, Scopes = [] });

        var mockConfiguration = new Mock<IConfigurationService>();
        mockConfiguration.Setup(c => c.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
        mockConfiguration.Setup(c => c.GetConfiguration<BenchmarkOptions>()).Returns(new BenchmarkOptions());

        var mockLoader = new Mock<IAssemblyLoadingService>();
        mockLoader.Setup(l => l.LoadExtension(It.IsAny<string>(), It.IsAny<string?>())).Returns(extension);

        var mockWireFactory = new Mock<ISendAsyncTerminalFactory>();
        mockWireFactory.Setup(f => f.Create()).Returns(mockWire.Object);

        SendAsyncStep passThrough = (request, callback, next) => next.SendAsync(request, callback);
        var mockPipelineSteps = new Mock<IHttpPipelineService>();
        mockPipelineSteps.SetupGet(s => s.HttpAddUserAgentStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpAddClientRequestIdStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpRequestMessageTracingStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpPipelineTerminatingStep).Returns(passThrough);

        // A cached data boundary for the tenant stops the engine looking it up over the network.
        var mockPersistence = new Mock<IPersistenceStore>();
        mockPersistence.Setup(s => s.ReadAsync(HomePaths.DataBoundaryCacheFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($"{{\"{TenantId}\":\"Global\"}}"));

        var binder = new ArgumentParsingService(new PSArgumentParsingProvider(), mockProfiles.Object);

        return new Engine(
            new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"),
            mockWireFactory.Object,
            mockProfiles.Object,
            mockAuth.Object,
            Mock.Of<ITelemetryService>(),
            Mock.Of<ILoggingService>(),
            mockLoader.Object,
            binder,
            mockConfiguration.Object,
            mockPipelineSteps.Object,
            new DataBoundaryResolver(mockPersistence.Object, mockProfiles.Object));
    }

    /// <summary>
    /// Makes <paramref name="engine"/> the <see cref="IEngine"/> the cmdlets resolve from <see cref="AzclipsServiceProvider"/>
    /// in BeginProcessing, replacing whatever an earlier test registered. Call <see cref="AzclipsServiceProvider.Dispose"/>
    /// before disposing the engine.
    /// </summary>
    internal static void RegisterEngine(Engine engine)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IEngine>(engine);
            services.AddSingleton(Mock.Of<ITelemetryService>());
        });
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"));
    }

    /// <summary>
    /// A PowerShell instance, with its own runspace, that has <typeparamref name="TCmdlet"/> loaded and added as the
    /// command to run. Add parameters, then call <see cref="PowerShell.Invoke()"/>.
    /// </summary>
    internal static PowerShell CreateShell<TCmdlet>() where TCmdlet : PSCmdlet => CreateShell(typeof(TCmdlet));

    /// <inheritdoc cref="CreateShell{TCmdlet}"/>
    internal static PowerShell CreateShell(Type cmdletType)
    {
        string name = CommandName(cmdletType);
        InitialSessionState state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry(name, cmdletType, null));
        return PowerShell.Create(state).AddCommand(name);
    }

    /// <summary>A <see cref="SecureString"/> holding <paramref name="value"/>, as a PowerShell user would pass it.</summary>
    internal static SecureString ToSecureString(string value) => new NetworkCredential(string.Empty, value).SecurePassword;

    /// <summary>The Verb-Noun name from the cmdlet's <see cref="CmdletAttribute"/>.</summary>
    internal static string CommandName(Type cmdletType)
    {
        CmdletAttribute cmdlet = cmdletType.GetCustomAttribute<CmdletAttribute>()
            ?? throw new ArgumentException($"{cmdletType.Name} has no [Cmdlet] attribute.", nameof(cmdletType));
        return $"{cmdlet.VerbName}-{cmdlet.NounName}";
    }
}
