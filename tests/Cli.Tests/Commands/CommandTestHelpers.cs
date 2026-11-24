// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Cli.Parser;
using Core.Engine;
using Core.Runtime;
using Core.Telemetry;
using Moq;

namespace Cli.Tests.Commands;

/// <summary>
/// Builds a real <see cref="Engine"/> for one extension with every outside dependency mocked, so a test can
/// pass CLI arguments in and check what was sent and written. The real CLI parser, extension index, binder,
/// processor and HTTP pipeline all run; only the wire is a mock.
/// </summary>
internal static class CommandTestHelpers
{
    internal const string TenantId = "11111111-1111-1111-1111-111111111111";
    internal const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    internal const string ArmUrl = "https://management.azure.com";
    internal const string EmptyList = """{"value":[]}""";
    private const string PollingIntervalVariable = "TEMP_AZCLIPS_PLAYBACK_TEST_POLLING_INTERVAL";

    /// <summary>
    /// Creates an engine that loads <paramref name="extension"/>, signs in as a fixed profile, and sends every
    /// HTTP request to <paramref name="mockWire"/>. Create the wire mock with <see cref="MockBehavior.Strict"/>
    /// so a request the test did not set up throws <see cref="MockException"/>. With <paramref name="signedIn"/> false there is no active profile.
    /// </summary>
    internal static Engine CreateEngine(Assembly extension, Mock<ISendAsync> mockWire, bool signedIn = true)
    {
        // Polling waits this many seconds when a response has no Retry-After header; the default is 15.
        Environment.SetEnvironmentVariable(PollingIntervalVariable, "0");

        Profile profile = new() { Name = "test", TenantId = TenantId, SubscriptionId = SubscriptionId };
        Mock<IProfileService> mockProfiles = new();
        mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync(signedIn ? profile.Name : null);
        mockProfiles.Setup(p => p.GetProfileAsync(profile.Name, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        mockProfiles.Setup(p => p.GetProfileKeysForDefault()).Returns([nameof(Profile.SubscriptionId)]);
        mockProfiles.Setup(p => p.GetProfileValueByKey(It.IsAny<Profile?>(), nameof(Profile.SubscriptionId)))
            .Returns((Profile? p, string _) => p?.SubscriptionId);

        Mock<IAuthService> mockAuth = new();
        mockAuth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ReturnsAsync(new AuthResult { AccessToken = "token", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), TenantId = TenantId, Scopes = [] });

        Mock<IConfigurationService> mockConfiguration = new();
        mockConfiguration.Setup(c => c.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
        mockConfiguration.Setup(c => c.GetConfiguration<BenchmarkOptions>()).Returns(new BenchmarkOptions());

        Mock<IAssemblyLoadingService> mockLoader = new();
        mockLoader.Setup(l => l.LoadExtension(It.IsAny<string>(), It.IsAny<string?>())).Returns(extension);

        Mock<ISendAsyncTerminalFactory> mockWireFactory = new();
        mockWireFactory.Setup(f => f.Create()).Returns(mockWire.Object);

        SendAsyncStep passThrough = (request, callback, next) => next.SendAsync(request, callback);
        Mock<IHttpPipelineService> mockPipelineSteps = new();
        mockPipelineSteps.SetupGet(s => s.HttpAddUserAgentStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpAddClientRequestIdStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpRequestMessageTracingStep).Returns(passThrough);
        mockPipelineSteps.SetupGet(s => s.HttpPipelineTerminatingStep).Returns(passThrough);

        // A cached data boundary for the tenant stops the engine looking it up over the network.
        Mock<IPersistenceStore> mockPersistence = new();
        mockPersistence.Setup(s => s.ReadAsync(HomePaths.DataBoundaryCacheFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($"{{\"{TenantId}\":\"Global\"}}"));

        ConsoleLoggingService logging = new();
        ArgumentParsingService binder = new(new CLIArgumentParsingProvider(new PropertyParser(logging), logging), mockProfiles.Object);

        return new Engine(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"),
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
    /// Parses <paramref name="commandLine"/> with the real CLI parser into the command to run in
    /// <paramref name="extensionName"/> and its parameters. Every word before the first flag is part of the command name.
    /// </summary>
    internal static CommandInvocation CreateInvocation(string extensionName, string[] commandLine)
    {
        (string command, IDictionary<string, object> parameters) = new ExtensionParser().Parse(commandLine);
        return new CommandInvocation(extensionName, command, parameters);
    }

    /// <summary>Output streams that add everything the command writes to <paramref name="output"/> and ignore its messages.</summary>
    internal static OutputStreams CreateOutput(List<object> output)
    {
        OutputStreamDelegate ignore = (_, _) => Task.CompletedTask;
        return new OutputStreams(
            WriteOutput: (value, _) =>
            {
                output.Add(value);
                return Task.CompletedTask;
            },
            WriteVerbose: ignore,
            WriteDebug: ignore,
            WriteWarning: ignore,
            WriteError: ignore,
            WriteInformation: ignore);
    }

    /// <summary>
    /// Answers every <paramref name="method"/> request to <paramref name="url"/> (any query string) with
    /// <paramref name="statusCode"/> and the JSON <paramref name="responseBody"/>. Returns the body of each matching
    /// request, read as it is sent because the pipeline disposes the request content afterwards.
    /// </summary>
    internal static List<string?> RespondTo(
        this Mock<ISendAsync> mockWire, HttpMethod method, string url, HttpStatusCode statusCode, string? responseBody = null) =>
        mockWire.RespondWithHeaders(method, url, statusCode, responseBody);

    /// <summary>
    /// Like <see cref="RespondTo"/>, and also adds each of <paramref name="responseHeaders"/> ("Name: value") to the
    /// response, e.g. "Retry-After: 0" so a long-running operation polls straight away instead of waiting.
    /// </summary>
    internal static List<string?> RespondWithHeaders(
        this Mock<ISendAsync> mockWire, HttpMethod method, string url, HttpStatusCode statusCode, string? responseBody, params string[] responseHeaders)
    {
        List<string?> sentBodies = [];
        mockWire
            .Setup(w => w.SendAsync(
                It.Is<HttpRequestMessage>(r => r.Method == method && Uri.UnescapeDataString(r.RequestUri!.GetLeftPart(UriPartial.Path)) == url),
                It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) =>
            {
                sentBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
                HttpResponseMessage response = CreateResponse(request, statusCode, responseBody);
                foreach (string header in responseHeaders)
                {
                    int separator = header.IndexOf(':');
                    response.Headers.TryAddWithoutValidation(header[..separator].Trim(), header[(separator + 1)..].Trim());
                }

                return Task.FromResult(response);
            });
        return sentBodies;
    }

    /// <summary>
    /// Answers every <paramref name="method"/> request, whatever its URL, with <paramref name="statusCode"/> and the JSON
    /// <paramref name="responseBody"/>, e.g. when the command generates part of the URL. Returns the body of each matching request.
    /// </summary>
    internal static List<string?> RespondToAnyUrl(
        this Mock<ISendAsync> mockWire, HttpMethod method, HttpStatusCode statusCode, string? responseBody = null)
    {
        List<string?> sentBodies = [];
        mockWire
            .Setup(w => w.SendAsync(It.Is<HttpRequestMessage>(r => r.Method == method), It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) =>
            {
                sentBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
                return Task.FromResult(CreateResponse(request, statusCode, responseBody));
            });
        return sentBodies;
    }

    /// <summary>Answers every request, whatever its method and URL, with 200 OK and the JSON <paramref name="responseBody"/>.</summary>
    internal static void RespondToAnyRequest(this Mock<ISendAsync> mockWire, string responseBody) =>
        mockWire
            .Setup(w => w.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) => Task.FromResult(CreateResponse(request, HttpStatusCode.OK, responseBody)));

    /// <summary>Answers each request, whatever its URL, with 200 OK and the next of <paramref name="responseBodies"/> in order.</summary>
    internal static void RespondInOrder(this Mock<ISendAsync> mockWire, params string[] responseBodies)
    {
        Queue<string> remaining = new(responseBodies);
        mockWire
            .Setup(w => w.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) =>
                Task.FromResult(CreateResponse(request, HttpStatusCode.OK, remaining.Dequeue())));
    }

    /// <summary>Every request that reached the wire, in the order sent.</summary>
    internal static HttpRequestMessage[] SentRequests(this Mock<ISendAsync> mockWire) =>
        mockWire.Invocations
            .Where(i => i.Method.Name == nameof(ISendAsync.SendAsync))
            .Select(i => (HttpRequestMessage)i.Arguments[0])
            .ToArray();

    /// <summary>"METHOD /path" for every request sent, in order.</summary>
    internal static string[] RequestLines(this Mock<ISendAsync> mockWire) =>
        mockWire.SentRequests().Select(r => $"{r.Method} {r.RequestUri!.AbsolutePath}").ToArray();

    /// <summary>"METHOD /path?query" for every request sent, in order, with the URL decoded.</summary>
    internal static string[] FullRequestLines(this Mock<ISendAsync> mockWire) =>
        mockWire.SentRequests().Select(r => $"{r.Method} {Uri.UnescapeDataString(r.RequestUri!.PathAndQuery)}").ToArray();

    /// <summary>The decoded value of query parameter <paramref name="name"/>, or null when it is not sent.</summary>
    internal static string? QueryValue(this HttpRequestMessage request, string name) =>
        HttpUtility.ParseQueryString(request.RequestUri!.Query)[name];

    /// <summary>The values of header <paramref name="name"/> joined with commas, or null when it is not sent.</summary>
    internal static string? HeaderValues(this HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(",", values) : null;

    private static HttpResponseMessage CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string? responseBody) =>
        new(statusCode)
        {
            RequestMessage = request,
            Content = responseBody is null ? null : new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
}
