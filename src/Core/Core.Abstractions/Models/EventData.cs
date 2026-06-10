using Core.Abstractions.Runtime;

namespace Core.Abstractions
{
    ///<summary>Represents the data in Published event.</summary>
    public class EventData: EventArgs
    {
        /// <summary>
        /// The type of the event being Published
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// The user-ready message from the event.
        /// </summary>
        public string Message = string.Empty;

        public string ModuleName = string.Empty;

        public string ModuleVersion = string.Empty;
        /// <summary>
        /// The name of the class that raised the event.
        /// Useful for diagnostics, logging, and tracing the origin of the event.
        /// </summary>
        public string ClassName = string.Empty;

        /// <summary>
        /// The name of the function or method that raised the event.
        /// Useful for diagnostics, logging, and tracing the origin of the event.
        /// </summary>
        public string FunctionName = string.Empty;

        /// <summary>
        /// When the event is about a parameter, this is the parameter name.
        /// Used in Validation Events
        /// </summary>
        public string Parameter = string.Empty;

        /// <summary>
        /// This represents a numeric value associated with the event.
        /// Use for progress-style events
        /// </summary>
        public double Value;

        /// <summary>
        /// Any extended data for an event should be serialized and stored here.
        /// </summary>
        public string ExtendedData = string.Empty;

        /// <summary>
        /// If the event triggers after the request message has been created, this will contain the Request Message (which in HTTP calls would be HttpRequestMessage)
        ///
        /// Typically you'd cast this to the expected type to use it:
        /// <code>
        /// if(eventData.RequestMessgae is HttpRequestMessage httpRequest)
        /// {
        ///   httpRequest.Headers.Add("x-request-flavor", "vanilla");
        /// }
        /// </code>
        /// </summary>
        public object RequestMessage = null!;

        /// <summary>
        /// If the event triggers after the response is back, this will contain the Response Message (which in HTTP calls would be HttpResponseMessage)
        ///
        /// Typically you'd cast this to the expected type to use it:
        /// <code>
        /// if(eventData.ResponseMessage is HttpResponseMessage httpResponse){
        ///   var flavor = httpResponse.Headers.GetValue("x-request-flavor");
        /// }
        /// </code>
        /// </summary>
        public object ResponseMessage = null!;

        /// <summary>
        /// Cancellation method for this event.
        ///
        /// If the event consumer wishes to cancel the request that initiated this event, call <c>Cancel()</c>
        /// </summary>
        /// <remarks>
        /// The original initiator of the request must provide the implementation of this.
        /// </remarks>
        public Action Cancel = () => { };

        public EventData()
        {
        }

        public EventData(dynamic sourceValue)
        {
            Id = To<string>(() => sourceValue.Id) ?? string.Empty;
            Message = To<string>(() => sourceValue.Message) ?? string.Empty;
            Parameter = To<string>(() => sourceValue.Parameter) ?? string.Empty;
            Value = To<double>(() => sourceValue.Value);
            ExtendedData = To<string>(() => sourceValue.ExtendedData) ?? string.Empty;
            RequestMessage = To<object>(() => sourceValue.RequestMessage) ?? null!;
            ResponseMessage = To<object>(() => sourceValue.ResponseMessage) ?? null!;
            Cancel = To<Action>(() => sourceValue.Cancel) ?? (() => { });
        }

        /// <summary>
        /// Returns result of the delegate as the expected type, or default(T)
        ///
        /// This isolates any exceptions from the consumer.
        /// </summary>
        /// <param name="srcValue">A delegate that returns a value</param>
        /// <typeparam name="T">The desired output type</typeparam>
        /// <returns>The value from the function if the type is correct</returns>
        private T? To<T>(Func<T> srcValue)
        {
            try
            {
                var value = srcValue();
                if (value is null && typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
                {
                    // For non-nullable value types, return default
                    return default;
                }
                return value;
            }
            catch
            {
                return default;
            }
        }
    }
}
