namespace Core.Runtime;

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Runtime;

internal partial class InvocationSession : IInvocationSession
{
    private Dictionary<string, Event> subscribers;
    private Event? globalSubscriber;

    // Yabo: IDisposable needless returned by Subscribe() because unnecessary to unsubscribe at runtime
    internal void Subscribe(string name, Event callback)
    {
        if (callback != null && !string.IsNullOrEmpty(name))
        {
            if (subscribers.ContainsKey(name))
            {
                subscribers[name] += callback;
            }
            else
            {
                subscribers[name] = callback;
            }
        }
        else
        {
            throw new ArgumentException("Event name must be non-empty and callback must be non-null.");
        }
    }

    internal void Subscribe(string name, SyncEvent callback)
    {
        Subscribe(name, (message) => { callback(message); return Task.CompletedTask; });
    }

    internal void SubscribeAll(Event callback)
    {
        if (callback != null)
        {
            globalSubscriber += callback;
        }
        else
        {
            throw new ArgumentException("Callback must be non-null.");
        }
    }

    internal void SubscribeAll(SyncEvent callback)
    {
        SubscribeAll((message) => { callback(message); return Task.CompletedTask; });
    }

    public async Task Signal(string id, CancellationToken token, Func<object> createMessage)
    {
        // Yabo: Cancellation checkpoint — always checked
        // Catch and ignore OperationCanceledException from callers
        // So that no need to check cancellation during internal life cycle
        token.ThrowIfCancellationRequested();

        using (RuntimeExtensions.NoSynchronizationContext)
        {
            if (!string.IsNullOrEmpty(id))
            {
                bool hasGlobal = globalSubscriber != null;
                bool hasNamed = subscribers.TryGetValue(id, out Event? listener) && listener != null;

                if (hasGlobal || hasNamed)
                {
                    EventData message = (EventData)createMessage();
                    message.Id = id;

                    if (hasGlobal)
                    {
                        await globalSubscriber!.Invoke(message);
                    }
                    if (hasNamed)
                    {
                        await listener!.Invoke(message);
                    }
                }
            }
        }
    }

    // Signal overloads with CancellationToken
    public Task Signal(string id, CancellationToken token) => Signal(id, token, () => new EventData { Id = id, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText) => Signal(id, token, () => new EventData { Id = id, Message = messageText, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText, HttpRequestMessage request) => Signal(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText, HttpResponseMessage response) => Signal(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText, double magnitude) => Signal(id, token, () => new EventData { Id = id, Message = messageText, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) => Signal(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) => Signal(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, HttpRequestMessage request) => Signal(id, token, () => new EventData { Id = id, RequestMessage = request, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) => Signal(id, token, () => new EventData { Id = id, RequestMessage = request, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, HttpResponseMessage response) => Signal(id, token, () => new EventData { Id = id, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, CancellationToken token, EventData message) => Signal(id, token, () => { message.Id = id; message.Cancel = Cancel; return message; });

    // Signal overloads without CancellationToken (uses Token property)
    public async Task Signal(string id, Func<object> createMessage)
    {
        await Signal(id, Token, createMessage);
    }
    public Task Signal(string id) => Signal(id, Token, () => new EventData { Id = id, Cancel = Cancel });
    public Task Signal(string id, string messageText) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, Cancel = Cancel });
    public Task Signal(string id, string messageText, HttpRequestMessage request) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Cancel = Cancel });
    public Task Signal(string id, string messageText, HttpResponseMessage response) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, string messageText, double magnitude) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, string messageText, double magnitude, HttpRequestMessage request) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, string messageText, double magnitude, HttpResponseMessage response) => Signal(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Value = magnitude, Cancel = Cancel });
    public Task Signal(string id, HttpRequestMessage request) => Signal(id, Token, () => new EventData { Id = id, RequestMessage = request, Cancel = Cancel });
    public Task Signal(string id, HttpRequestMessage request, HttpResponseMessage response) => Signal(id, Token, () => new EventData { Id = id, RequestMessage = request, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, HttpResponseMessage response) => Signal(id, Token, () => new EventData { Id = id, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Signal(string id, EventData message) => Signal(id, Token, () => { message.Id = id; message.Cancel = Cancel; return message; });
    public Task Signal(string id, Uri uri) => Signal(id, Token, () => new EventData { Id = id, Message = uri.ToString(), Cancel = Cancel });
}



