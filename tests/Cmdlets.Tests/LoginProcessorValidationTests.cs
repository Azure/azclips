// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Reflection;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Login.Processors;
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
        AzclipsServiceProvider.Initialize(clientType);
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
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException!;
        }
    }

    private static Profile ProfileForTenant(string tenantId) => new()
    {
        Name = "temp",
        TenantId = tenantId,
        SubscriptionId = "sub-1",
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

    // ── Section 1: ValidateArguments (screenshot bug — bare interactive login) ─────────────

    [Theory]
    [InlineData(ClientType.CLI, "--subscription-id", "-SubscriptionId")]
    [InlineData(ClientType.PowerShell, "-SubscriptionId", "--subscription-id")]
    public void ValidateArguments_BareInteractiveLogin_NamesSurfaceCorrectSubscriptionFlag(
        ClientType surface, string expectedFlag, string forbiddenFlag)
    {
        InitDi(surface);

        var ex = Assert.Throws<ArgumentException>(() => InvokePrivate(
            new LoginProcessor(),
            "ValidateArguments",
            Array.Empty<object?>()));

        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
    }

    // ── Section 2: post-validation error paths ─────────────────────────────────────────────

    // EnsureCrossTenantAccessible: probe threw (LoginProcessor.cs ~L307)

    [Theory]
    [InlineData(ClientType.CLI, "az login", "--tenant-id", "Connect-AzAccount", "-TenantId")]
    [InlineData(ClientType.PowerShell, "Connect-AzAccount", "-TenantId", "az login", "--tenant-id")]
    public void EnsureCrossTenantAccessible_ProbeThrew_UsesSurfaceCorrectCommandAndFlag(
        ClientType surface,
        string expectedCommand, string expectedFlag,
        string forbiddenCommand, string forbiddenFlag)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CanAcquireTokenSilentlyAsync(
                It.IsAny<string?>(), It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("tenant probe failed"));
        InitDi(surface, auth: auth.Object);

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
    [InlineData(ClientType.CLI, "az login", "--tenant-id", "Connect-AzAccount", "-TenantId")]
    [InlineData(ClientType.PowerShell, "Connect-AzAccount", "-TenantId", "az login", "--tenant-id")]
    public void EnsureCrossTenantAccessible_CannotAcquire_UsesSurfaceCorrectCommandAndFlag(
        ClientType surface,
        string expectedCommand, string expectedFlag,
        string forbiddenCommand, string forbiddenFlag)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CanAcquireTokenSilentlyAsync(
                It.IsAny<string?>(), It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        InitDi(surface, auth: auth.Object);

        var ex = Assert.Throws<InvalidOperationException>(() => InvokePrivate(
            new LoginProcessor(),
            "EnsureCrossTenantAccessible",
            new object?[] { ProfileForTenant("11111111-1111-1111-1111-111111111111"), "sub-1" }));

        Assert.Contains(expectedCommand, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenCommand, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
    }

    // ResolveSubscriptionTenantAsync: ARM lookup threw (LoginProcessor.cs ~L373)

    [Theory]
    [InlineData(ClientType.CLI, "--tenant-id", "-TenantId")]
    [InlineData(ClientType.PowerShell, "-TenantId", "--tenant-id")]
    public void ResolveSubscriptionTenantAsync_LookupFailed_NamesSurfaceCorrectTenantFlag(
        ClientType surface, string expectedFlag, string forbiddenFlag)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ResolveSubscriptionTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ARM down"));
        InitDi(surface, subs: subs.Object);

        var ex = Assert.Throws<InvalidOperationException>(() => InvokePrivate(
            new LoginProcessor(),
            "ResolveSubscriptionTenantAsync",
            new object?[] { FakeAuthResult(), "sub-1" }));

        Assert.Contains(expectedFlag, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, ex.Message, StringComparison.Ordinal);
    }

    // ResolveDefaultSubscriptionAsync: no subs found (LoginProcessor.cs ~L395, Warn)

    [Theory]
    [InlineData(ClientType.CLI, "--subscription-id", "-SubscriptionId")]
    [InlineData(ClientType.PowerShell, "-SubscriptionId", "--subscription-id")]
    public void ResolveDefaultSubscriptionAsync_NoSubscriptions_WarnsWithSurfaceCorrectFlag(
        ClientType surface, string expectedFlag, string forbiddenFlag)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubscriptionInfo>());
        var warns = InitDi(surface, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal(string.Empty, result);
        var warn = Assert.Single(warns);
        Assert.Contains(expectedFlag, warn, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, warn, StringComparison.Ordinal);
    }

    // ResolveDefaultSubscriptionAsync: ARM listing threw (LoginProcessor.cs ~L404, Warn)

    [Theory]
    [InlineData(ClientType.CLI, "--subscription-id", "-SubscriptionId")]
    [InlineData(ClientType.PowerShell, "-SubscriptionId", "--subscription-id")]
    public void ResolveDefaultSubscriptionAsync_ListFailed_WarnsWithSurfaceCorrectFlag(
        ClientType surface, string expectedFlag, string forbiddenFlag)
    {
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.ListSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ARM down"));
        var warns = InitDi(surface, subs: subs.Object);

        var result = (string?)InvokePrivate(
            new LoginProcessor(),
            "ResolveDefaultSubscriptionAsync",
            new object?[] { "token" });

        Assert.Equal(string.Empty, result);
        var warn = Assert.Single(warns);
        Assert.Contains(expectedFlag, warn, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenFlag, warn, StringComparison.Ordinal);
    }
}
