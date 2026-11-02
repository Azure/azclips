// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;
using Moq;

namespace Cli.Tests;

public class MsalAuthTelemetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedIdentitySuccess_RecordsSuccessfulOutcome(bool silently)
    {
        var credential = new Mock<TokenCredential>();
        credential
            .Setup(c => c.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1)));
        var factory = new Mock<AzureCredentialFactory>();
        factory
            .Setup(f => f.CreateManagedIdentityCredential(
                It.IsAny<string?>(),
                It.IsAny<ManagedIdentityIdType?>()))
            .Returns(credential.Object);
        using var telemetry = new RecordingTelemetryService();
        var service = CreateService(telemetry, factory.Object);

        if (silently)
        {
            await service.AcquireTokenSilentlyAsync(null, null, profile: new Profile
            {
                Name = "managed",
                AuthMethod = AuthMethod.ManagedIdentity,
            });
        }
        else
        {
            await service.LoginAsync(new LoginRequest(
                null, null, new AuthCredential.ManagedIdentity(ManagedIdentityIdType.SystemAssigned)));
        }

        var activity = Assert.Single(
            telemetry.Stopped,
            a => a.OperationName == (silently
                ? "MsalAuthService.AcquireTokenSilentlyAsync"
                : "MsalAuthService.AuthenticateWithManagedIdentityAsync"));
        Assert.Equal(true, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthDurationTotalMs));
    }

    [Theory]
    [InlineData(AuthMethod.Federated, typeof(InvalidInputException))]
    [InlineData(AuthMethod.ServicePrincipalSecret, typeof(ArgumentException))]
    public async Task AcquireTokenSilentlyAsync_InvalidApplicationProfile_RecordsFailedOutcome(
        AuthMethod authMethod, Type exceptionType)
    {
        using var telemetry = new RecordingTelemetryService();
        var service = CreateService(telemetry, new AzureCredentialFactory());
        var profile = new Profile { Name = "invalid", AuthMethod = authMethod };

        var exception = await Assert.ThrowsAsync(exceptionType,
            () => service.AcquireTokenSilentlyAsync(null, null, profile: profile));

        var activity = Assert.Single(
            telemetry.Stopped,
            a => a.OperationName == "MsalAuthService.AcquireTokenSilentlyAsync");
        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(exception.GetType().FullName, activity.GetTagItem(TelemetryTags.ExceptionType));
    }

    [Fact]
    public async Task LoginAsync_FederatedFailure_RecordsFailedOutcome()
    {
        using var telemetry = new RecordingTelemetryService();
        var service = CreateService(telemetry, new AzureCredentialFactory());

        var exception = await Assert.ThrowsAsync<InvalidInputException>(
            () => service.LoginAsync(new LoginRequest(
                null, null, new AuthCredential.FederatedIdentity(string.Empty, string.Empty))));

        var activity = Assert.Single(
            telemetry.Stopped,
            a => a.OperationName == "MsalAuthService.AuthenticateWithFederatedTokenAsync");
        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(exception.GetType().FullName, activity.GetTagItem(TelemetryTags.ExceptionType));
    }

    [Fact]
    public void SetAuthenticationSuccessTags_RecordsOutcomeAndTotalDuration()
    {
        using var activity = new Activity("auth").Start();

        MsalAuthService.SetAuthenticationSuccessTags(activity, 125);

        Assert.Equal(true, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(125L, activity.GetTagItem(TelemetryTags.AuthDurationTotalMs));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthStatusCode));
    }

    [Fact]
    public void SetAuthenticationSuccessTags_RecordsOutcomeWithoutUnavailableDuration()
    {
        using var activity = new Activity("auth").Start();

        MsalAuthService.SetAuthenticationSuccessTags(activity);

        Assert.Equal(true, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthDurationTotalMs));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthStatusCode));
    }

    [Fact]
    public void SetAuthenticationFailureTags_RecordsMsalServiceStatusCode()
    {
        using var activity = new Activity("auth").Start();
        var exception = new MsalServiceException("service_error", "message", 429);

        MsalAuthService.SetAuthenticationFailureTags(activity, exception);

        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(429, activity.GetTagItem(TelemetryTags.AuthStatusCode));
        Assert.Equal(
            typeof(MsalServiceException).FullName,
            activity.GetTagItem(TelemetryTags.ExceptionType));
    }

    [Fact]
    public void SetAuthenticationFailureTags_OmitsStatusCodeWhenUnavailable()
    {
        using var activity = new Activity("auth").Start();

        MsalAuthService.SetAuthenticationFailureTags(activity, new InvalidOperationException());

        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthStatusCode));
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            activity.GetTagItem(TelemetryTags.ExceptionType));
    }

    private static MsalAuthService CreateService(
        ITelemetryService telemetry,
        AzureCredentialFactory credentialFactory)
    {
        var logger = Mock.Of<ILoggingService>();
        var secretStore = new Mock<IServicePrincipalSecretStore>(MockBehavior.Strict).Object;
        var options = new MsalClientOptions
        {
            CacheDirectory = Path.Combine(
                Path.GetTempPath(),
                "azclips-auth-telemetry-" + Guid.NewGuid().ToString("N")),
        };

        return new MsalAuthService(
            logger,
            telemetry,
            Mock.Of<IProfileService>(),
            new ClientMetadata(ClientType.CLI, "11111111-1111-1111-1111-111111111111"),
            new ManagedIdentityAuthenticator(credentialFactory, logger, telemetry),
            new FederatedTokenAuthenticator(logger, telemetry, secretStore, options),
            new ServicePrincipalSecretAuthenticator(logger, telemetry, secretStore, options),
            secretStore,
            options);
    }

    private sealed class RecordingTelemetryService : ITelemetryService
    {
        private readonly ActivitySource _source =
            new("auth-telemetry-test-" + Guid.NewGuid().ToString("N"));
        private readonly ActivityListener _listener;

        public RecordingTelemetryService()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source == _source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = Stopped.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Stopped { get; } = [];

        public Activity? StartActivity(
            string name,
            ActivityKind kind = ActivityKind.Internal,
            DateTimeOffset startTime = default) =>
            _source.StartActivity(name, kind, default(ActivityContext), startTime: startTime);

        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
            StartActivity(name ?? "Test.Activity.Root", ActivityKind.Server, startTime);

        public TelemetryActivityScope? StartActivityWithParent(
            string name,
            ActivityContext parentContext,
            ActivityKind kind = ActivityKind.Internal,
            DateTimeOffset startTime = default) =>
            throw new NotSupportedException();

        public TelemetryActivityScope? StartIndependentRootActivity(
            string name,
            ActivityKind kind = ActivityKind.Server,
            DateTimeOffset startTime = default,
            bool activate = true) =>
            throw new NotSupportedException();

        public Activity? TagInvocation(CommandInvocation commandInvocation) =>
            throw new NotSupportedException();

        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _listener.Dispose();
            _source.Dispose();
        }
    }
}
