using Core.Abstractions;
using Core.Agent.Probes;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="EnvironmentProber"/>. Assert on the snapshot
/// properties the caller consumes — NOT the string format of
/// <see cref="EnvironmentSnapshot.FormatForSystemPrompt"/>.
/// Format-string assertions are the anti-pattern from cirrus-anti-patterns §B
/// (rename a label, test breaks; user never sees the label).
/// </summary>
public class EnvironmentProberTests
{
    private sealed class FakeAuthContext : IAuthContextService
    {
        private readonly AuthContext? _ctx;
        public FakeAuthContext(AuthContext? ctx) => _ctx = ctx;
        public Task<AuthContext?> GetCurrentContextAsync(CancellationToken ct = default) => Task.FromResult(_ctx);
        public Task SaveContextAsync(AuthContext c, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearContextAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SignedIn_SnapshotExposesSubAndTenant()
    {
        var ctx = new AuthContext(TenantId: "tenant-1", SubscriptionId: "sub-1");
        var prober = new EnvironmentProber(new FakeAuthContext(ctx), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.True(snap.SignedIn);
        Assert.Equal("sub-1", snap.SubscriptionId);
        Assert.Equal("tenant-1", snap.TenantId);
    }

    [Fact]
    public async Task NotSignedIn_SnapshotReportsAbsence()
    {
        var prober = new EnvironmentProber(new FakeAuthContext(null), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.False(snap.SignedIn);
        Assert.Null(snap.SubscriptionId);
        Assert.Null(snap.TenantId);
    }

    [Fact]
    public async Task AuthContextService_Throws_ProberStillReturnsSnapshot()
    {
        var prober = new EnvironmentProber(new ThrowingAuthContext(), flavor: "azure cli");

        var snap = await prober.ProbeAsync();

        Assert.False(snap.SignedIn);
    }

    private sealed class ThrowingAuthContext : IAuthContextService
    {
        public Task<AuthContext?> GetCurrentContextAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("auth backend down");
        public Task SaveContextAsync(AuthContext c, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearContextAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
