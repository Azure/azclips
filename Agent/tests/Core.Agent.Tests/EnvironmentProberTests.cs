using Core.Abstractions;
using Core.Agent.Tools.Env;
using Tests.Common.Stubs;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Profile read failures must not be mistaken for a signed-out account.
/// </summary>
public class EnvironmentProberTests
{
    [Fact]
    public async Task ProfileService_Throws_ProberDoesNotReportSignedOut()
    {
        var prober = new EnvironmentProber(new ThrowingProfileService(), flavor: "azure cli");

        await Assert.ThrowsAsync<InvalidOperationException>(() => prober.ProbeAsync());
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
    }
}
