// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Agent.Tests;

internal sealed class AgentOfflineAuth : IAuthService
{
    private int _count;
    internal int AcquireCount => Volatile.Read(ref _count);
    internal string[]? RequestedScopes { get; private set; }
    internal Func<CancellationToken, Task<AuthResult>> Acquire { get; set; } =
        _ => throw new InvalidOperationException("Unexpected authentication in an offline test.");

    internal static AuthResult Token() => new()
    {
        AccessToken = "offline-token", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = [], TenantId = "offline-tenant", CorrelationId = Guid.NewGuid(),
    };

    public Task<AuthResult> AcquireTokenSilentlyAsync(
        string[]? scopes, string? tenantId, bool promptOnError = false,
        CancellationToken cancellationToken = default, Profile? profile = null)
    {
        Interlocked.Increment(ref _count);
        RequestedScopes = scopes;
        return Acquire(cancellationToken);
    }

    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Interactive authentication is forbidden in offline tests.");
    public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

internal sealed class AgentOfflineProfiles : IProfileService
{
    private int _reads;
    internal int Reads => Volatile.Read(ref _reads);
    internal ITelemetryService? Telemetry { get; set; }
    internal Func<CancellationToken, Task<Profile?>> Read { get; set; } = _ => Task.FromResult<Profile?>(null);

    public async Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reads);
        using var activity = Telemetry?.StartActivity("ProfileService.GetActiveProfileAsync");
        return await Read(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Profile>>([]);
    public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) => Read(cancellationToken);
    public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public List<string> GetProfileKeysForDefault() => [];
    public string? GetProfileValueByKey(Profile? profile, string key) => null;
}

internal sealed class AgentOfflineHttp(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null) : HttpMessageHandler
{
    private int _requests;
    internal int Requests => Volatile.Read(ref _requests);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        return respond?.Invoke(request, cancellationToken)
            ?? throw new InvalidOperationException("An offline test attempted an unconfigured HTTP request.");
    }
}
