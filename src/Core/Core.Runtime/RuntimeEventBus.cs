namespace Core.Runtime;

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;

internal partial class RuntimeEventBus : IEventBus
{
    public CancellationToken Token { get; private set; }    
    public Action Cancel { get; private set; }
    private Dictionary<string, Event> subscribers;
    private Event? globalSubscriber;

    internal RuntimeEventBus(CancellationToken cancellationToken)
    {
        Token = cancellationToken;
        Cancel = () => { };
        subscribers = new Dictionary<string, Event>();
        globalSubscriber = null;
    }

    // Yabo: IDisposable needless returned by Subscribe() because unnecessary to unsubscribe at runtime
    public void Subscribe(string name, Event callback)
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

    public void Subscribe(string name, SyncEvent callback)
    {
        Subscribe(name, (message) => { callback(message); return Task.CompletedTask; });
    }

    public void SubscribeAll(Event callback)
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

    public void SubscribeAll(SyncEvent callback)
    {
        SubscribeAll((message) => { callback(message); return Task.CompletedTask; });
    }

    public async Task Publish(string id, CancellationToken token, Func<object> createMessage)
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

    // Publish overloads with CancellationToken
    public Task Publish(string id, CancellationToken token) => Publish(id, token, () => new EventData { Id = id, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText) => Publish(id, token, () => new EventData { Id = id, Message = messageText, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) => Publish(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) => Publish(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude) => Publish(id, token, () => new EventData { Id = id, Message = messageText, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) => Publish(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) => Publish(id, token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request) => Publish(id, token, () => new EventData { Id = id, RequestMessage = request, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) => Publish(id, token, () => new EventData { Id = id, RequestMessage = request, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, HttpResponseMessage response) => Publish(id, token, () => new EventData { Id = id, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, CancellationToken token, EventData message) => Publish(id, token, () => { message.Id = id; message.Cancel = Cancel; return message; });
    public Task Publish(string id, CancellationToken token, string activityName, string messageText) => Publish(id, token, () => new EventData { Id = id, ActivityName = activityName, Message = messageText, Cancel = Cancel });

    // Publish overloads without CancellationToken (uses Token property)
    public async Task Publish(string id, Func<object> createMessage)
    {
        await Publish(id, Token, createMessage);
    }
    public Task Publish(string id) => Publish(id, Token, () => new EventData { Id = id, Cancel = Cancel });
    public Task Publish(string id, string messageText) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, Cancel = Cancel });
    public Task Publish(string id, string messageText, HttpRequestMessage request) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Cancel = Cancel });
    public Task Publish(string id, string messageText, HttpResponseMessage response) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, string messageText, double magnitude) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = request, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) => Publish(id, Token, () => new EventData { Id = id, Message = messageText, RequestMessage = response.RequestMessage!, ResponseMessage = response, Value = magnitude, Cancel = Cancel });
    public Task Publish(string id, HttpRequestMessage request) => Publish(id, Token, () => new EventData { Id = id, RequestMessage = request, Cancel = Cancel });
    public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => Publish(id, Token, () => new EventData { Id = id, RequestMessage = request, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, HttpResponseMessage response) => Publish(id, Token, () => new EventData { Id = id, RequestMessage = response.RequestMessage!, ResponseMessage = response, Cancel = Cancel });
    public Task Publish(string id, EventData message) => Publish(id, Token, () => { message.Id = id; message.Cancel = Cancel; return message; });
    public Task Publish(string id, Uri uri) => Publish(id, Token, () => new EventData { Id = id, Message = uri.ToString(), Cancel = Cancel });
    public Task Publish(string id, string activityName, string messageText) => Publish(id, Token, () => new EventData { Id = id, ActivityName = activityName, Message = messageText, Cancel = Cancel });
}



