namespace Core.Abstractions.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public interface IValidates
    {
        Task Validate(IEventListener listener);
    }

    /// <summary>
    /// The IEventListener Interface defines the communication mechanism for Signaling events during a remote call.
    /// </summary>
    /// <remarks>
    /// The interface is designed to be as minimal as possible, allow for quick peeking of the event type (<c>id</c>) 
    /// and the cancellation status and provides a delegate for retrieving the event details themselves.
    /// </remarks>
    public interface IEventListener
    {
        // Signal overloads with CancellationToken
        Task Signal(string id, CancellationToken token, GetEventData createMessage);
        Task Signal(string id, CancellationToken token);
        Task Signal(string id, CancellationToken token, string messageText);
        Task Signal(string id, CancellationToken token, string messageText, HttpRequestMessage request);
        Task Signal(string id, CancellationToken token, string messageText, HttpResponseMessage response);
        Task Signal(string id, CancellationToken token, string messageText, double magnitude);
        Task Signal(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request);
        Task Signal(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response);
        Task Signal(string id, CancellationToken token, HttpRequestMessage request);
        Task Signal(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response);
        Task Signal(string id, CancellationToken token, HttpResponseMessage response);
        Task Signal(string id, CancellationToken token, EventData message);

        // Signal overloads without CancellationToken (uses Token property)
        Task Signal(string id, GetEventData createMessage);
        Task Signal(string id);
        Task Signal(string id, string messageText);
        Task Signal(string id, string messageText, HttpRequestMessage request);
        Task Signal(string id, string messageText, HttpResponseMessage response);
        Task Signal(string id, string messageText, double magnitude);
        Task Signal(string id, string messageText, double magnitude, HttpRequestMessage request);
        Task Signal(string id, string messageText, double magnitude, HttpResponseMessage response);
        Task Signal(string id, HttpRequestMessage request);
        Task Signal(string id, HttpRequestMessage request, HttpResponseMessage response);
        Task Signal(string id, HttpResponseMessage response);
        Task Signal(string id, EventData message);
        Task Signal(string id, Uri uri);

        CancellationToken Token { get; }
        Action Cancel { get; }
    }
}
