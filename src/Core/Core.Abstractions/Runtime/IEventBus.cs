namespace Core.Abstractions.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public interface IValidates
    {
        Task Validate(IEventPublisher publisher);
    }

    public interface IEventPublisher
    {
        // Publish overloads with CancellationToken
        Task Publish(string id, CancellationToken token, GetEventData createMessage);
        Task Publish(string id, CancellationToken token);
        Task Publish(string id, CancellationToken token, string messageText);
        Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request);
        Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response);
        Task Publish(string id, CancellationToken token, string messageText, double magnitude);
        Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request);
        Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response);
        Task Publish(string id, CancellationToken token, HttpRequestMessage request);
        Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response);
        Task Publish(string id, CancellationToken token, HttpResponseMessage response);
        Task Publish(string id, CancellationToken token, EventData message);
        Task Publish(string id, CancellationToken token, string activityName, string messageText);

        // Publish overloads without CancellationToken (uses Token property)
        Task Publish(string id, GetEventData createMessage);
        Task Publish(string id);
        Task Publish(string id, string messageText);
        Task Publish(string id, string messageText, HttpRequestMessage request);
        Task Publish(string id, string messageText, HttpResponseMessage response);
        Task Publish(string id, string messageText, double magnitude);
        Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request);
        Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response);
        Task Publish(string id, HttpRequestMessage request);
        Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response);
        Task Publish(string id, HttpResponseMessage response);
        Task Publish(string id, EventData message);
        Task Publish(string id, Uri uri);
        Task Publish(string id, string activityName, string messageText);

        CancellationToken Token { get; }
        Action Cancel { get; }
    }

    public interface IEventSubscriber
    {
        // Subscribe a handler to a specific event id.
        // Multiple handlers per id are supported; they are invoked in subscription order.
        void Subscribe(string name, Event callback);
        void Subscribe(string name, SyncEvent callback);

        // Wildcard subscription: handler is invoked for every published event,
        // in addition to any id-specific handlers.
        void SubscribeAll(Event callback);
        void SubscribeAll(SyncEvent callback);
    }

    public interface IEventBus: IEventPublisher, IEventSubscriber { }
}
