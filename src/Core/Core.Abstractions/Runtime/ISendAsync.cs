namespace Core.Abstractions.Runtime
{
    /// <summary>
    /// The interface for sending an HTTP request across the wire.
    /// </summary>
    public interface ISendAsync
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventListener callback);
    }

    /// <summary>
    /// Factory interface for creating terminal ISendAsync steps.
    /// </summary>
    public interface ISendAsyncTerminalFactory
    {
        ISendAsync Create();
    }

    /// <summary>
    /// Factory interface for creating ISendAsync pipeline steps.
    /// </summary>
    public interface ISendAsyncFactory
    {
        ISendAsync Create(ISendAsync next);
    }
    
    public interface IRuntimeHttpPipeline : ISendAsync
    {
        public IRuntimeHttpPipeline Prepend(ISendAsyncFactory item);

        public IRuntimeHttpPipeline Append(SendAsyncStep item);

        public IRuntimeHttpPipeline Prepend(SendAsyncStep item);

        public IRuntimeHttpPipeline Append(IEnumerable<SendAsyncStep> items);

        public IRuntimeHttpPipeline Prepend(IEnumerable<SendAsyncStep> items);

        public IRuntimeHttpPipeline Append(ISendAsyncFactory item);

        public IRuntimeHttpPipeline Prepend(IEnumerable<ISendAsyncFactory> items);

        public IRuntimeHttpPipeline Append(IEnumerable<ISendAsyncFactory> items);

        public IRuntimeHttpPipeline Append(SendAsyncStepExpand item);

        public IRuntimeHttpPipeline Prepend(SendAsyncStepExpand item);
    }
}
