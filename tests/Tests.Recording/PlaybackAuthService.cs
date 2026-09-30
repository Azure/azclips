// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Recording;

/// <summary>
/// Answers token requests during playback, where no request reaches Azure and no real
/// credential exists. The token value is a fixed marker; recordings never store it because
/// the sanitisers redact bearer tokens at record time.
/// </summary>
internal sealed class PlaybackAuthService : IAuthService
{
    private static AuthResult PlaybackResult() => new()
    {
        AccessToken = "playback-token",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = [],
        TenantId = RecordingSanitizers.PlaceholderGuid,
        CorrelationId = Guid.Empty,
    };

    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(PlaybackResult());

    public Task LogoutAsync(string profileName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null)
        => Task.FromResult(PlaybackResult());

    public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
