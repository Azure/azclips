// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Reflection;
using Core.Abstractions;
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
/// Two logical sections:
/// <list type="number">
///   <item>ValidateArguments — the pre-authentication rule checks.</item>
///   <item>Post-validation error paths — EnsureCrossTenantAccessible, ResolveSubscriptionTenantAsync,
///         and ResolveDefaultSubscriptionAsync, which run after sign-in and reference parameters
///         in their exception messages and warnings.</item>
/// </list>
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

    private static Profile ProfileForTenant(string tenantId) => new()
    {
        Name = "temp",
        TenantId = tenantId,
        SubscriptionId = ValidSubscriptionId,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    private static AuthResult FakeAuthResult(string tenantId = "00000000-0000-0000-0000-000000000000") => new()
    {
        AccessToken = "token",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = Array.Empty<string>(),
        TenantId = tenantId,
        CorrelationId = Guid.Empty,
    };

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

    // EnsureCrossTenantAccessible: probe threw (LoginProcessor.cs ~L307)

    [Theory]
    [InlineData(ClientType.CLI, "az login", "Connect-AzAccount")]
    [InlineData(ClientType.PowerShell, "Connect-AzAccount", "az login")]
    public void EnsureCrossTenantAccessible_ProbeThrew_UsesSurfaceCorrectCommandAndFlag(
        ClientType surface, string expectedCommand, string forbiddenCommand)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CanAcquireTokenSilentlyAsync(
                It.IsAny<string?>(), It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("tenant probe failed"));
        InitDi(surface, auth: auth.Object);
        var expectedFlag = FlagFor(surface, nameof(LoginProcessor.TenantId));
        var forbiddenFlag = FlagFor(OtherSurface(surface), nameof(LoginProcessor.TenantId));

        var ex = Assert.Throws<InvalidOperationException>(() => InvokePrivate(
            new LoginProcessor(),
            "EnsureCrossTenantAccessible",
            new object?[] { ProfileForTenant("11111111-1111-1111-1111-111111111111"), "sub-1" }));

        Assert.Contains(expectedCommand, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenCommand, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
    }

    // EnsureCrossTenantAccessible: probe returned false (LoginProcessor.cs ~L316)

    [Theory]
    [InlineData(ClientType.CLI, "az login", "Connect-AzAccount")]
    [InlineData(ClientType.PowerShell, "Connect-AzAccount", "az login")]
    public void EnsureCrossTenantAccessible_CannotAcquire_UsesSurfaceCorrectCommandAndFlag(
        ClientType surface, string expectedCommand, string forbiddenCommand)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CanAcquireTokenSilentlyAsync(
                It.IsAny<string?>(), It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        InitDi(surface, auth: auth.Object);
        var expectedFlag = FlagFor(surface, nameof(LoginProcessor.TenantId));
        var forbiddenFlag = FlagFor(OtherSurface(surface), nameof(LoginProcessor.TenantId));

        var ex = Assert.Throws<AzclipsErrorException>(() => InvokePrivate(
            new LoginProcessor(),
            "EnsureCrossTenantAccessible",
            new object?[] { ProfileForTenant("11111111-1111-1111-1111-111111111111"), "sub-1" }));

        Assert.Equal("NotSignedIn", ex.Code);
        Assert.Contains(expectedCommand, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenCommand, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
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

    // ResolveDefaultSubscriptionAsync: no subs found (LoginProcessor.cs ~L395, Warn)

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ResolveDefaultSubscriptionAsync_NoSubscriptions_WarnsWithSurfaceCorrectFlag(ClientType surface)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubscriptionInfo>());
        var warns = InitDi(surface, subs: subs.Object);
        var expected = FlagFor(surface, nameof(LoginProcessor.SubscriptionId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.SubscriptionId));

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal(string.Empty, result);
        var warn = Assert.Single(warns);
        Assert.Contains(expected, warn, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, warn, StringComparison.Ordinal);
    }

    // ResolveDefaultSubscriptionAsync: ARM listing threw (LoginProcessor.cs ~L404, Warn)

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ResolveDefaultSubscriptionAsync_ListFailed_WarnsWithSurfaceCorrectFlag(ClientType surface)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ARM down"));
        var warns = InitDi(surface, subs: subs.Object);
        var expected = FlagFor(surface, nameof(LoginProcessor.SubscriptionId));
        var forbidden = FlagFor(OtherSurface(surface), nameof(LoginProcessor.SubscriptionId));

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal(string.Empty, result);
        var warn = Assert.Single(warns);
        Assert.Contains(expected, warn, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, warn, StringComparison.Ordinal);
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
            ClientId = "c",
            ObjectId = "o",
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
            ClientId = "c",
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

    // ── Section 6: EnsureCrossTenantAccessible happy path ──────────────────────────────────

    [Fact]
    public void EnsureCrossTenantAccessible_ProbeReturnsTrue_DoesNotThrow()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CanAcquireTokenSilentlyAsync(
                It.IsAny<string?>(), It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        InitDi(ClientType.CLI, auth: auth.Object);

        InvokePrivate(
            new LoginProcessor(),
            "EnsureCrossTenantAccessible",
            new object?[] { ProfileForTenant("11111111-1111-1111-1111-111111111111"), "sub-1" });
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

    // ── Section 8: ResolveDefaultSubscriptionAsync selection branches ──────────────────────

    [Fact]
    public void ResolveDefaultSubscriptionAsync_ReturnsFirstEnabledSubscription()
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SubscriptionInfo("disabled-sub", "Disabled", "Disabled"),
                new SubscriptionInfo("enabled-sub-1", "Enabled 1", "Enabled"),
                new SubscriptionInfo("enabled-sub-2", "Enabled 2", "Enabled"),
            });
        InitDi(ClientType.CLI, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal("enabled-sub-1", result);
    }

    [Fact]
    public void ResolveDefaultSubscriptionAsync_NoEnabled_FallsBackToFirstAny()
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SubscriptionInfo("disabled-sub-1", "Disabled 1", "Disabled"),
                new SubscriptionInfo("disabled-sub-2", "Disabled 2", "Warned"),
            });
        InitDi(ClientType.CLI, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal("disabled-sub-1", result);
    }

    // ── Section 9: ShouldResolveTenantFromSubscription (pure) ──────────────────────────────

    [Theory]
    [InlineData(null,        AuthMethod.Browser,          "",         "sub-1", true)]
    [InlineData(null,        AuthMethod.DeviceCode,       "",         "sub-1", true)]
    [InlineData("existing",  AuthMethod.Browser,          "",         "sub-1", false)]
    [InlineData(null,        AuthMethod.ManagedIdentity,  "",         "sub-1", false)]
    [InlineData(null,        AuthMethod.Browser,          "tenant-1", "sub-1", false)]
    [InlineData(null,        AuthMethod.Browser,          "",         "",      false)]
    public void ShouldResolveTenantFromSubscription_ReturnsExpected(
        string? reAuthName, AuthMethod authMode, string tenantId, string subscriptionId, bool expected)
    {
        var method = typeof(LoginProcessor).GetMethod(
            "ShouldResolveTenantFromSubscription",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (bool)method.Invoke(null, new object?[] { reAuthName, authMode, tenantId, subscriptionId })!;

        Assert.Equal(expected, result);
    }
}
