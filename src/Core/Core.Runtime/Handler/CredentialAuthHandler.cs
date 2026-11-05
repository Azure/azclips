// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Core.Runtime.Handler;

internal sealed class CredentialAuthHandler
{
    private readonly IProfileCredential credential;
    private readonly string[] scopes;

    public CredentialAuthHandler(IProfileCredential credential, string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Length == 0)
            throw new ArgumentException("An authenticated pipeline requires explicit scopes.", nameof(scopes));
        foreach (var scope in scopes)
            ArgumentException.ThrowIfNullOrWhiteSpace(scope, nameof(scopes));
        this.credential = credential;
        this.scopes = [.. scopes];
    }

    public SendAsyncStep SendAsync => async (request, publisher, next) =>
    {
        publisher.Token.ThrowIfCancellationRequested();
        await publisher.Publish(Events.AuthBeforeTokenAcquisition);
        var result = await credential.GetTokenAsync([.. scopes], publisher.Token);
        publisher.Token.ThrowIfCancellationRequested();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        await publisher.Publish(Events.AuthAfterTokenAcquisition,
            new EventData { Payload = credential.Profile.WithRuntimeMetadata(result) });
        return await next.SendAsync(request, publisher);
    };
}
