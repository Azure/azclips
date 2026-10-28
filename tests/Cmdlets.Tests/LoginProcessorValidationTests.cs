// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

/// <summary>
/// Regression tests for the surface-aware parameter naming in every <see cref="LoginProcessor"/>
/// error and warning that references a parameter name. The shared processor backs both
/// <c>az login</c> (CLI) and <c>Connect-AzAccount</c> (PowerShell), so its messages must render
/// <c>--kebab-case</c> on CLI and <c>-PascalCase</c> on PowerShell — hardcoding either form
/// mis-instructs the other surface.
/// <para>
/// Exercises parameter validation and the refactored tenant/subscription resolution flow.
/// </para>
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class LoginProcessorValidationTests
{
    private const string ValidSubscriptionId = "11111111-1111-1111-1111-111111111111";

    // ── Shared helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the DI container for the given invocation surface, optionally with mocked
    /// auth and subscription services. Returns a list that captures every <c>ILoggingService.Warn</c>
    /// call — used by tests that assert against warning output.
    /// </summary>
    private static List<string> InitDi(
        ClientType clientType,
        IAuthService? auth = null,
        ISubscriptionService? subs = null)
    {
        var warns = new List<string>();
        var logger = new Mock<ILoggingService>();
        logger.Setup(l => l.Warn(It.IsAny<string>()))
              .Callback<string>(warns.Add);

        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(logger.Object);
            if (auth is not null) services.AddSingleton(auth);
            if (subs is not null) services.AddSingleton(subs);
        });
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
        return warns;
    }

    /// <summary>
    /// Invokes a private instance method via reflection. Awaits <see cref="Task"/> results and
    /// unwraps <see cref="TargetInvocationException"/> so tests can assert on the real exception.
    /// </summary>
    private static object? InvokePrivate(LoginProcessor processor, string name, object?[] args)
    {
        if (name == "ValidateArguments")
        {
            return LoginProcessorTestHelpers.ResolveParameterSet(processor);
        }

        var method = typeof(LoginProcessor).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            var result = method.Invoke(processor, args);
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
                var resultProp = task.GetType().GetProperty("Result");
                return resultProp?.GetValue(task);
            }
            return result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }

    private static AuthResult FakeAuthResult(string tenantId = "00000000-0000-0000-0000-000000000000") => new()
    {
        AccessToken = "token",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = Array.Empty<string>(),
        TenantId = tenantId,
        CorrelationId = Guid.Empty,
    };

    private static void SetBaseField(LoginProcessor processor, string fieldName, object value) =>
        typeof(LoginProcessorBase)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, value);

    private static string FlagFor(ClientType surface, string propertyName)
    {
        var meta = new ExtensionIndex().GetAllCommands(surface)
            .First(c => c.ProcessorType == typeof(LoginProcessor));
        var invocationName = meta.Parameters.Values
            .First(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal))
            .InvocationName;
        return (surface == ClientType.PowerShell ? "-" : "--") + invocationName;
    }

    private static ClientType OtherSurface(ClientType surface)
        => surface == ClientType.CLI ? ClientType.PowerShell : ClientType.CLI;

    // ── Section 1: ValidateArguments (screenshot bug — bare interactive login) ─────────────

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_BareInteractiveLogin_NamesSurfaceCorrectSubscriptionFlag(ClientType surface)
    {
        InitDi(surface);
        var expected = FlagFor(surface, nameof(LoginProcessor.SubscriptionId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.SubscriptionId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            new LoginProcessor(),
            "ValidateArguments",
            Array.Empty<object?>()));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_InvalidSubscriptionId_NamesSurfaceCorrectSubscriptionFlag(ClientType surface)
    {
        InitDi(surface);
        var expected = FlagFor(surface, nameof(LoginProcessor.SubscriptionId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.SubscriptionId));
        const string invalidSubscriptionId = "Steven Ma [stema] subscription";
        var processor = new LoginProcessor { SubscriptionId = invalidSubscriptionId };

        var ex = Assert.Throws<UsageErrorException>(() => InvokePrivate(
            processor,
            "ValidateArguments",
            Array.Empty<object?>()));

        Assert.Contains(invalidSubscriptionId, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Contains("Invalid subscription id", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_InvalidSubscriptionIdWithProfile_NamesSurfaceCorrectSubscriptionFlag(ClientType surface)
    {
        InitDi(surface);
        var expected = FlagFor(surface, nameof(LoginProcessor.SubscriptionId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.SubscriptionId));
        const string invalidSubscriptionId = "Steven Ma [stema] subscription";
        var processor = new LoginProcessor
        {
            Profile = "prod",
            SubscriptionId = invalidSubscriptionId,
        };

        var ex = Assert.Throws<UsageErrorException>(() => InvokePrivate(
            processor,
            "ValidateArguments",
            Array.Empty<object?>()));

        Assert.Contains(invalidSubscriptionId, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Contains("Invalid subscription id", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    // ── Section 2: post-validation error paths ─────────────────────────────────────────────

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ResolveTenantAndSubAsync_CannotAcquire_NamesSurfaceCorrectTenantFlag(ClientType surface)
    {
        const string owningTenant = "22222222-2222-2222-2222-222222222222";
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), owningTenant, false, It.IsAny<CancellationToken>(), It.IsAny<Profile>()))
            .ThrowsAsync(new InvalidOperationException("MFA required"));
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ResolveSubscriptionTenantAsync(
                It.IsAny<string>(), ValidSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owningTenant);
        InitDi(surface, auth: auth.Object, subs: subs.Object);
        var expectedFlag = FlagFor(surface, nameof(LoginProcessor.TenantId));
        var forbiddenFlag = FlagFor(OtherSurface(surface), nameof(LoginProcessor.TenantId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            new LoginProcessor { SubscriptionId = ValidSubscriptionId },
            "ResolveTenantAndSubAsync",
            [AuthRequestFactory.CreateInteractive(), FakeAuthResult()]));

        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
        Assert.Contains("MFA required", ex.Message, StringComparison.Ordinal);
        subs.Verify(s => s.GetSubscriptionByIdAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ResolveSubscriptionTenantAsync: ARM lookup threw (LoginProcessor.cs ~L373)

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ResolveSubscriptionTenantAsync_LookupFailed_NamesSurfaceCorrectTenantFlag(ClientType surface)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ResolveSubscriptionTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ARM down"));
        InitDi(surface, subs: subs.Object);
        var expected = FlagFor(surface, nameof(LoginProcessor.TenantId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.TenantId));

        var ex = Assert.Throws<InvalidOperationException>(() => InvokePrivate(
            new LoginProcessor(),
            "ResolveSubscriptionTenantAsync",
            new object?[] { FakeAuthResult(), "sub-1" }));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    // ── Section 3: remaining ValidateArguments branches ────────────────────────────────────

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ProfileWithOutProfile_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor { Profile = "existing", OutProfile = "new" };
        var expectedProfile = FlagFor(surface, nameof(LoginProcessorBase.Profile));
        var expectedOutProfile = FlagFor(surface, nameof(LoginProcessor.OutProfile));
        var forbiddenProfile = FlagFor(OtherSurface(surface), nameof(LoginProcessorBase.Profile));
        var forbiddenOutProfile = FlagFor(OtherSurface(surface), nameof(LoginProcessor.OutProfile));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedProfile, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedOutProfile, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenProfile, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenOutProfile, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ProfileWithAuthFlag_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor { Profile = "existing", Identity = true };
        var expectedIdentity = FlagFor(surface, nameof(LoginProcessor.Identity));
        var expectedProfile = FlagFor(surface, nameof(LoginProcessorBase.Profile));
        var forbiddenIdentity = FlagFor(OtherSurface(surface), nameof(LoginProcessor.Identity));
        var forbiddenProfile = FlagFor(OtherSurface(surface), nameof(LoginProcessorBase.Profile));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedIdentity, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedProfile, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenIdentity, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenProfile, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_NonGuidClientId_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Identity = true,
            ClientId = "not-a-guid",
        };
        var expected = FlagFor(surface, nameof(LoginProcessor.ClientId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ClientId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_NonGuidObjectId_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Identity = true,
            ObjectId = "not-a-guid",
        };
        var expected = FlagFor(surface, nameof(LoginProcessor.ObjectId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ObjectId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_IdentityWithTenantId_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Identity = true,
            TenantId = "11111111-1111-1111-1111-111111111111",
        };
        var expectedIdentity = FlagFor(surface, nameof(LoginProcessor.Identity));
        var expectedTenant = FlagFor(surface, nameof(LoginProcessor.TenantId));
        var forbiddenIdentity = FlagFor(OtherSurface(surface), nameof(LoginProcessor.Identity));
        var forbiddenTenant = FlagFor(OtherSurface(surface), nameof(LoginProcessor.TenantId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedIdentity, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedTenant, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenIdentity, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenTenant, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_MultipleManagedIdentitySelectors_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Identity = true,
            ClientId = "22222222-2222-2222-2222-222222222222",
            ObjectId = "33333333-3333-3333-3333-333333333333",
        };
        var expectedClient = FlagFor(surface, nameof(LoginProcessor.ClientId));
        var expectedObject = FlagFor(surface, nameof(LoginProcessor.ObjectId));
        var expectedResource = FlagFor(surface, nameof(LoginProcessor.ResourceId));
        var forbiddenClient = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ClientId));
        var forbiddenObject = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ObjectId));
        var forbiddenResource = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ResourceId));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedClient, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedObject, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedResource, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenClient, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenObject, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenResource, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_IdentityWithUseDeviceCode_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Identity = true,
            UseDeviceCode = true,
        };
        var expectedIdentity = FlagFor(surface, nameof(LoginProcessor.Identity));
        var expectedDeviceCode = FlagFor(surface, nameof(LoginProcessor.UseDeviceCode));
        var forbiddenIdentity = FlagFor(OtherSurface(surface), nameof(LoginProcessor.Identity));
        var forbiddenDeviceCode = FlagFor(OtherSurface(surface), nameof(LoginProcessor.UseDeviceCode));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedIdentity, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedDeviceCode, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenIdentity, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenDeviceCode, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ManagedIdentitySelectorsWithoutIdentity_NamesSurfaceCorrectFlags(ClientType surface)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            SubscriptionId = ValidSubscriptionId,
            ClientId = "22222222-2222-2222-2222-222222222222",
        };
        var expectedSelector = FlagFor(surface, nameof(LoginProcessor.ClientId));
        var expectedIdentity = FlagFor(surface, nameof(LoginProcessor.Identity));
        var forbiddenSelector = FlagFor(OtherSurface(surface), nameof(LoginProcessor.ClientId));
        var forbiddenIdentity = FlagFor(OtherSurface(surface), nameof(LoginProcessor.Identity));

        var ex = Assert.Throws<InvalidInputException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));

        Assert.Contains(expectedSelector, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedIdentity, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenSelector, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenIdentity, ex.Message, StringComparison.Ordinal);
    }

    // ── Section 4: FormatParameterName default branch ──────────────────────────────────────

    [Fact]
    public void FormatParameterName_UnknownClientType_Throws()
    {
        InitDi(ClientType.CLI);
        var unknownSurface = (ClientType)999;

        var ex = Assert.Throws<NotSupportedException>(() => InvokePrivate(
            new LoginProcessor(),
            "FormatParameterName",
            new object?[] { "SubscriptionId", unknownSurface }));

        Assert.Contains("999", ex.Message, StringComparison.Ordinal);
    }

    // ── Section 5: ValidateArguments happy paths ───────────────────────────────────────────

    public static IEnumerable<object[]> ValidArgumentCombinations()
    {
        yield return new object[] { "bare-interactive",
            new LoginProcessor { SubscriptionId = ValidSubscriptionId } };
        yield return new object[] { "device-code",
            new LoginProcessor { SubscriptionId = ValidSubscriptionId, UseDeviceCode = true } };
        yield return new object[] { "managed-identity-system-assigned",
            new LoginProcessor { Identity = true } };
        yield return new object[] { "managed-identity-system-assigned-with-subscription",
            new LoginProcessor { Identity = true, SubscriptionId = ValidSubscriptionId } };
        yield return new object[] { "managed-identity-with-client-id-and-subscription",
            new LoginProcessor { Identity = true, SubscriptionId = ValidSubscriptionId, ClientId = "33333333-3333-3333-3333-333333333333" } };
        yield return new object[] { "managed-identity-with-client-id",
            new LoginProcessor { Identity = true, ClientId = "33333333-3333-3333-3333-333333333333" } };
        yield return new object[] { "managed-identity-with-object-id",
            new LoginProcessor { Identity = true, ObjectId = "44444444-4444-4444-4444-444444444444" } };
        yield return new object[] { "managed-identity-with-resource-id",
            new LoginProcessor { Identity = true, ResourceId = "r" } };
        yield return new object[] { "re-auth-existing-profile",
            new LoginProcessor { Profile = "existing" } };
    }

    [Theory]
    [MemberData(nameof(ValidArgumentCombinations))]
    public void ValidateArguments_ValidCombination_DoesNotThrow(string _, LoginProcessor processor)
    {
        InitDi(ClientType.CLI);

        InvokePrivate(processor, "ValidateArguments", Array.Empty<object?>());
    }

    [Fact]
    public void ValidateArguments_OutProfileReserved_Throws()
    {
        InitDi(ClientType.CLI);
        var processor = new LoginProcessor
        {
            SubscriptionId = ValidSubscriptionId,
            OutProfile = ProfileNameValidator.ReservedDefaultName,
        };

        Assert.ThrowsAny<UsageErrorException>(() => InvokePrivate(
            processor, "ValidateArguments", Array.Empty<object?>()));
    }

    [Fact]
    public void ResolveTenantAndSubAsync_CrossTenant_ValidatesWithOwningTenantToken()
    {
        const string owningTenant = "22222222-2222-2222-2222-222222222222";
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), owningTenant, false, It.IsAny<CancellationToken>(),
                It.Is<Profile>(p => p.TenantId == owningTenant && p.AuthMethod == AuthMethod.Browser)))
            .ReturnsAsync(FakeAuthResult(owningTenant) with { AccessToken = "owning-tenant-token" });
        var subs = new Mock<ISubscriptionService>(MockBehavior.Strict);
        subs.Setup(s => s.ResolveSubscriptionTenantAsync("token", ValidSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owningTenant);
        subs.Setup(s => s.GetSubscriptionByIdAsync("owning-tenant-token", ValidSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(ValidSubscriptionId, "Test", "Enabled"));
        InitDi(ClientType.CLI, auth: auth.Object, subs: subs.Object);

        var result = InvokePrivate(
            new LoginProcessor { SubscriptionId = ValidSubscriptionId },
            "ResolveTenantAndSubAsync",
            [AuthRequestFactory.CreateInteractive(), FakeAuthResult()]);

        Assert.NotNull(result);
        Assert.Equal(owningTenant, result.GetType().GetProperty("TenantID")!.GetValue(result));
        Assert.Equal(ValidSubscriptionId, result.GetType().GetProperty("SubscriptionID")!.GetValue(result));
        subs.VerifyAll();
    }

    public static IEnumerable<object[]> WhitespaceArguments()
    {
        string[] properties =
        [
            nameof(LoginProcessor.SubscriptionId),
            nameof(LoginProcessor.TenantId),
            nameof(LoginProcessor.ClientId),
            nameof(LoginProcessor.ObjectId),
            nameof(LoginProcessor.ResourceId),
            nameof(LoginProcessor.FederatedToken),
            nameof(LoginProcessor.ClientSecret),
        ];
        foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
        foreach (var property in properties)
        foreach (var value in new[] { " ", "\t", "\r\n" })
        {
            yield return [surface, property, value];
        }
    }

    [Theory]
    [MemberData(nameof(WhitespaceArguments))]
    public async Task ProcessAsync_WhitespaceArgument_IsRejectedBeforeResolution(
        ClientType surface, string property, string value)
    {
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        InitDi(surface, auth: auth.Object);
        var processor = new LoginProcessor
        {
            SubscriptionId = ValidSubscriptionId,
            Identity = property == nameof(LoginProcessor.TenantId),
        };
        typeof(LoginProcessor).GetProperty(property)!.SetValue(processor, value);
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        SetBaseField(processor, "Publisher", publisher.Object);
        InvokePrivate(processor, "ConfigureParameterSets", Array.Empty<object?>());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains(FlagFor(surface, property), ex.Message, StringComparison.Ordinal);
        Assert.Contains("must not be whitespace", ex.Message, StringComparison.Ordinal);
        auth.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task ProcessAsync_WhitespaceTokenWithCompleteFederatedArguments_IsRejected(ClientType surface)
    {
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        InitDi(surface, auth: auth.Object);
        var processor = new LoginProcessor
        {
            ServicePrincipal = true,
            ClientId = "22222222-2222-2222-2222-222222222222",
            TenantId = "33333333-3333-3333-3333-333333333333",
            SubscriptionId = ValidSubscriptionId,
            FederatedToken = " ",
        };
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        SetBaseField(processor, "Publisher", publisher.Object);
        InvokePrivate(processor, "ConfigureParameterSets", Array.Empty<object?>());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.FederatedToken)), ex.Message, StringComparison.Ordinal);
        Assert.Contains("must not be whitespace", ex.Message, StringComparison.Ordinal);
        auth.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, null, ProfileNameValidator.ReservedDefaultName)]
    [InlineData(" ", "\t\r\n", ProfileNameValidator.ReservedDefaultName)]
    [InlineData("", "", ProfileNameValidator.ReservedDefaultName)]
    [InlineData(" ", " result ", "result")]
    public async Task ProcessAsync_PublishesEnrichedRuntimeProfile_AfterLoginTokenAcquisition(
        string? profile, string? outProfile, string expectedName)
    {
        const string objectId = "99999999-9999-9999-9999-999999999999";
        var authResult = FakeAuthResult("22222222-2222-2222-2222-222222222222") with
        {
            EntraObjectId = objectId,
            UserPrincipalName = "user@microsoft.com",
        };

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        Profile? savedProfile = null;
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.SaveAndActivateProfileAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .Callback<Profile, CancellationToken>((profile, _) => savedProfile = profile)
            .Returns(Task.CompletedTask);
        var subscriptions = new Mock<ISubscriptionService>();
        subscriptions.Setup(s => s.GetSubscriptionByIdAsync(
                authResult.AccessToken, ValidSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(ValidSubscriptionId, "Test", "Enabled"));

        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(new Mock<ILoggingService>().Object);
            services.AddSingleton(auth.Object);
            services.AddSingleton(profiles.Object);
            services.AddSingleton(subscriptions.Object);
        });
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        Profile? publishedProfile = null;
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        publisher
            .Setup(p => p.Publish(Events.AuthAfterTokenAcquisition, It.IsAny<EventData>()))
            .Callback<string, EventData>((_, data) => publishedProfile = data.Payload as Profile)
            .Returns(Task.CompletedTask);

        var processor = new LoginProcessor
        {
            TenantId = "22222222-2222-2222-2222-222222222222",
            SubscriptionId = ValidSubscriptionId,
            Profile = profile,
            OutProfile = outProfile,
        };
        SetBaseField(processor, "Publisher", publisher.Object);
        SetBaseField(processor, "WriteOutput", (OutputStreamDelegate)((_, _) => Task.CompletedTask));

        // Init() wires up the parameter-set resolver; this test drives ProcessAsync directly, so run
        // the same step that Init performs before resolution is needed.
        InvokePrivate(processor, "ConfigureParameterSets", Array.Empty<object?>());

        await processor.ProcessAsync();

        Assert.NotNull(savedProfile);
        Assert.Equal(expectedName, savedProfile.Name);
        Assert.Null(processor.Profile);
        Assert.Equal(outProfile?.Trim() is { Length: > 0 } name ? name : null, processor.OutProfile);
        Assert.Equal(objectId, savedProfile!.Metadata.EntraObjectId);
        Assert.NotNull(publishedProfile);
        Assert.Equal(objectId, publishedProfile!.Metadata.EntraObjectId);
    }

    // ── Section 7: ResolveSubscriptionTenantAsync branches ─────────────────────────────────

    [Fact]
    public void ResolveSubscriptionTenantAsync_OwningTenantDiffers_ReturnsOwningTenant()
    {
        const string OwningTenant = "22222222-2222-2222-2222-222222222222";
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ResolveSubscriptionTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwningTenant);
        InitDi(ClientType.CLI, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveSubscriptionTenantAsync",
            new object?[] { FakeAuthResult("00000000-0000-0000-0000-000000000000"), "sub-1" });

        Assert.Equal(OwningTenant, result);
    }

    [Fact]
    public void ResolveSubscriptionTenantAsync_OwningTenantMatchesSignedIn_ReturnsEmpty()
    {
        const string SignedInTenant = "33333333-3333-3333-3333-333333333333";
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ResolveSubscriptionTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SignedInTenant);
        InitDi(ClientType.CLI, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveSubscriptionTenantAsync",
            new object?[] { FakeAuthResult(SignedInTenant), "sub-1" });

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AuthRequestFactory_EmptyTenant_IsNormalizedToNull(string? value)
    {
        string[] scopes = ["https://graph.microsoft.com/.default"];
        var request = AuthRequestFactory.CreateInteractive(value, scopes);

        Assert.Null(request.TenantId);
        Assert.Equal(scopes, request.Scopes);
    }
}
