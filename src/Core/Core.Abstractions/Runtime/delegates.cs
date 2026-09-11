// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public readonly record struct SerializedJson(string Value)
    {
        public override string ToString() => Value;
    }

    public delegate Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback);
    public delegate Task<HttpResponseMessage> SendAsyncStep(HttpRequestMessage request, IEventPublisher callback, ISendAsync next);
    public delegate Task<HttpResponseMessage> SendAsyncStepExpand(
        HttpRequestMessage request,
        CancellationToken token,
        Action cancel,
        PublishEvent PublishEvent,
        Func<HttpRequestMessage, CancellationToken, Action, Func<string, CancellationToken, GetEventData, Task<HttpResponseMessage>>, Task<HttpResponseMessage>> next);
    public delegate Task PublishEvent(string id, CancellationToken token, GetEventData getEventData);
    public delegate Task Event(object message);
    public delegate void SyncEvent(object message);

    /// <summary>
    /// Delegate for acquiring an access token silently.
    /// Matches <see cref="IAuthService.AcquireTokenSilentlyAsync"/> signature.
    /// </summary>
    public delegate Task<AuthResult> AcquireTokenAsyncDelegate(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null);

    /// <summary>
    /// Delegate for stream output processing.
    ///</summary>
    public delegate Task OutputStreamDelegate(object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delegate for starting an activity for telemetry.
    /// </summary>
    public delegate Activity? StartActivityDelegate(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default);
}