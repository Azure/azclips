// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Core.Runtime.Handler;

internal sealed class CredentialAuthHandler
{
    private readonly IProfileCredential credential;
    private readonly string[]? scopes;
    private readonly Action<Profile>? onAuthenticated;

    public CredentialAuthHandler(IProfileCredential credential, string[]? scopes = null, Action<Profile>? onAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (scopes is { Length: 0 })
            throw new ArgumentException("An authenticated pipeline requires explicit scopes.", nameof(scopes));
        if (scopes is not null)
            foreach (var scope in scopes)
                ArgumentException.ThrowIfNullOrWhiteSpace(scope, nameof(scopes));
        this.credential = credential;
        this.scopes = scopes is null ? null : [.. scopes];
        this.onAuthenticated = onAuthenticated;
    }

    public SendAsyncStep SendAsync => async (request, publisher, next) =>
    {
        publisher.Token.ThrowIfCancellationRequested();
        await publisher.Publish(Events.AuthBeforeTokenAcquisition);
        var result = await credential.GetTokenAsync(scopes is null ? null : [.. scopes], publisher.Token);
        publisher.Token.ThrowIfCancellationRequested();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        var profile = credential.Profile.WithRuntimeMetadata(result);
        onAuthenticated?.Invoke(profile);
        await publisher.Publish(Events.AuthAfterTokenAcquisition,
            new EventData { Payload = profile });
        return await next.SendAsync(request, publisher);
    };
}
