namespace Core.Abstractions.Runtime
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public delegate Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventListener callback);
    public delegate Task<HttpResponseMessage> SendAsyncStep(HttpRequestMessage request, IEventListener callback, ISendAsync next);
    public delegate Task<HttpResponseMessage> SendAsyncStepExpand(
        HttpRequestMessage request,
        CancellationToken token,
        Action cancel,
        SignalEvent signalEvent,
        Func<HttpRequestMessage, CancellationToken, Action, Func<string, CancellationToken, GetEventData, Task<HttpResponseMessage>>, Task<HttpResponseMessage>> next);
    public delegate Task SignalEvent(string id, CancellationToken token, GetEventData getEventData);
    public delegate Task Event(object message);
    public delegate void SynchEvent(object message);

    /// <summary>
    /// Delegate for acquiring an access token silently.
    /// Matches <see cref="IAuthService.AcquireTokenSilentlyAsync"/> signature.
    /// </summary>
    public delegate Task<AuthResult> AcquireTokenAsyncDelegate(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delegate for command output processing.
    ///</summary>
    public delegate Task CommandOutputDelegate(object value, CancellationToken cancellationToken = default);
}
