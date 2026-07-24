// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

/// <summary>
/// Per-request auth decision returned by <see cref="IRequestAuthProvider"/>.
/// </summary>
public enum RequestAuthAction
{
    /// <summary>Use the default pipeline auth (ARM token via MSAL).</summary>
    UseDefault,

    /// <summary>Skip auth entirely — no Authorization header is added.</summary>
    Skip,

    /// <summary>Use the provided token as the Bearer value.</summary>
    UseToken
}

/// <summary>
/// Result of a per-request auth decision.
/// </summary>
/// <param name="Action">What the auth handler should do for this request.</param>
/// <param name="Token">The Bearer token to use when <paramref name="Action"/> is <see cref="RequestAuthAction.UseToken"/>.</param>
public record RequestAuthResult(RequestAuthAction Action, string? Token = null)
{
    /// <summary>Shorthand: fall through to default auth.</summary>
    public static readonly RequestAuthResult Default = new(RequestAuthAction.UseDefault);

    /// <summary>Shorthand: skip auth for this request.</summary>
    public static readonly RequestAuthResult Skip = new(RequestAuthAction.Skip);

    /// <summary>Use the given token as Bearer.</summary>
    public static RequestAuthResult WithToken(string token) => new(RequestAuthAction.UseToken, token);
}

/// <summary>
/// Interface that extensions implement to control per-request authentication.
/// Set on <see cref="IAuthHandler.RequestAuthProvider"/> so the pipeline can consult it
/// before each outgoing HTTP request.
/// </summary>
public interface IRequestAuthProvider
{
    /// <summary>
    /// Determines how the given request should be authenticated.
    /// </summary>
    Task<RequestAuthResult> GetAuthAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
