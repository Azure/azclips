// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime
{
    using System.Net.Http;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using System;
    using Core.Abstractions.Runtime;
    using Microsoft.Azure.Azclips.ExtensionRuntime;
    
    public class HttpPipeline : IRuntimeHttpPipeline
    {
        private const int DefaultMaxRetry = 3;
        private const int DefaultMaxRetryFor429 = 5;
        private ISendAsync pipeline = null!;
        private readonly object _pipelineLock = new();
        private ISendAsyncTerminalFactory terminal = null!;
        private List<ISendAsyncFactory> steps = new List<ISendAsyncFactory>();
        private readonly int _maxRetry;
        private readonly int _maxRetryFor429;

        public int PollingInterval { get; set; } = 30;

        public HttpPipeline(ISendAsyncTerminalFactory terminalStep) : this(
            terminalStep,
            DefaultMaxRetry,
            DefaultMaxRetryFor429)
        {
        }

        public HttpPipeline(ISendAsyncTerminalFactory terminalStep, int maxRetry, int maxRetryFor429)
        {
            if (terminalStep == null)
            {
                throw new System.ArgumentNullException(nameof(terminalStep), "Terminal Step Factory in HttpPipeline may not be null");
            }
            TerminalFactory = terminalStep;
            _maxRetry = maxRetry;
            _maxRetryFor429 = maxRetryFor429;
        }

        /// <summary>
        /// Returns an HttpPipeline with the current state of this pipeline.
        /// </summary>
        public HttpPipeline Clone() => new HttpPipeline(terminal, _maxRetry, _maxRetryFor429) { steps = this.steps.ToList(), pipeline = this.pipeline };

        private bool shouldRetry429(HttpResponseMessage response)
        {
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                var retryAfter = response.Headers.RetryAfter;
                if (retryAfter != null && retryAfter.Delta.HasValue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The step to handle 429 response with retry-after header.
        /// </summary>
        public async Task<HttpResponseMessage> Retry429(HttpRequestMessage request, IEventPublisher callback, ISendAsync next)
        {
            var cloneRequest = await request.CloneWithContent();
            var response = await SendTrackedAsync(request, callback, next);
            int count = 0;
            while (shouldRetry429(response) && count++ < _maxRetryFor429)
            {
                request = await cloneRequest.CloneWithContent();
                var retryAfter = response.Headers.RetryAfter;
                TimeSpan Delta = retryAfter?.Delta ?? TimeSpan.FromSeconds(0);
                await Task.Delay(Delta, callback.Token);
                await callback.Publish("Debug", $"Start to retry {count} time(s) on status code 429 after waiting {Delta.TotalSeconds} seconds.");
                response = await SendTrackedAsync(request, callback, next);
            }
            return response;
        }

        private static async Task<HttpResponseMessage> SendTrackedAsync(HttpRequestMessage request, IEventPublisher callback, ISendAsync next)
        {
            request.Options.TryGetValue(RequestAttemptState.OptionsKey, out var state);
            state?.RecordAttempt();
            var response = await next.SendAsync(request, callback);
            state?.RecordResponse(response.StatusCode);
            return response;
        }

        private bool shouldRetryError(HttpResponseMessage response)
        {
            if (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotImplemented &&
                    response.StatusCode != System.Net.HttpStatusCode.HttpVersionNotSupported)
                {
                    return true;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                return true;
            }
            else if (response.StatusCode == (System.Net.HttpStatusCode)429 && response.Headers.RetryAfter == null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if status code in HttpRequestExceptionWithStatus exception is greater 
        /// than or equal to 500 and not NotImplemented (501) or HttpVersionNotSupported (505).
        /// Or it's 429 (TOO MANY REQUESTS) without Retry-After header.
        /// </summary>
        public async Task<HttpResponseMessage> RetryError(HttpRequestMessage request, IEventPublisher callback, ISendAsync next)
        {
            var cloneRequest = await request.CloneWithContent();
            var response = await next.SendAsync(request, callback);
            int count = 0;
            while (shouldRetryError(response) && count++ < _maxRetry)
            {
                await callback.Publish("Debug", $"Start to retry {count} time(s) on status code {response.StatusCode}");
                request = await cloneRequest.CloneWithContent();
                response = await next.SendAsync(request, callback);
            }
            return response;
        }

        public ISendAsyncTerminalFactory TerminalFactory
        {
            get => terminal;
            set
            {
                if (value == null)
                {
                    throw new System.ArgumentNullException("TerminalFactory in HttpPipeline may not be null");
                }
                terminal = value;
            }
        }

        public ISendAsync Pipeline
        {
            get
            {
                // Fast path: pipeline already built and not invalidated.
                // Volatile read via local copy keeps the value stable across the null check
                // and avoids racing with concurrent writers in Prepend/Append.
                var current = Volatile.Read(ref this.pipeline);
                if (current != null)
                {
                    return current;
                }

                // Slow path: build under lock with double-check so concurrent first-callers
                // don't each build (and abandon) their own chain.
                lock (_pipelineLock)
                {
                    current = this.pipeline;
                    if (current != null)
                    {
                        return current;
                    }

                    // create the pipeline from scratch.
                    var next = terminal.Create();
                    if (Convert.ToBoolean(@"true"))
                    {
                        next = new SendAsyncFactory(Retry429).Create(next) ?? next;
                        next = new SendAsyncFactory(RetryError).Create(next) ?? next;
                    }
                    foreach (var factory in steps)
                    {
                        // skip factories that return null.
                        next = factory.Create(next) ?? next;
                    }
                    Volatile.Write(ref this.pipeline, next);
                    return next;
                }
            }
        }

        public int Count => steps.Count;

        public IRuntimeHttpPipeline Prepend(ISendAsyncFactory item)
        {
            if (item != null)
            {
                steps.Add(item);
                pipeline = null!;
            }
            return this;
        }

        public IRuntimeHttpPipeline Append(SendAsyncStep item)
        {
            if (item != null)
            {
                Append(new SendAsyncFactory(item));
            }
            return this;
        }

        public IRuntimeHttpPipeline Prepend(SendAsyncStep item)
        {
            if (item != null)
            {
                Prepend(new SendAsyncFactory(item));
            }
            return this;
        }

        public IRuntimeHttpPipeline Append(IEnumerable<SendAsyncStep> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    Append(new SendAsyncFactory(item));
                }
            }
            return this;
        }

        public IRuntimeHttpPipeline Prepend(IEnumerable<SendAsyncStep> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    Prepend(new SendAsyncFactory(item));
                }
            }
            return this;
        }

        public IRuntimeHttpPipeline Append(ISendAsyncFactory item)
        {
            if (item != null)
            {
                steps.Insert(0, item);
                pipeline = null!;
            }
            return this;
        }

        public IRuntimeHttpPipeline Prepend(IEnumerable<ISendAsyncFactory> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    Prepend(item);
                }
            }
            return this;
        }

        public IRuntimeHttpPipeline Append(IEnumerable<ISendAsyncFactory> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    Append(item);
                }
            }
            return this;
        }

        public IRuntimeHttpPipeline Append(SendAsyncStepExpand item)
        {
            if (item != null)
            {
                Append(new SendAsyncFactory(item));
            }
            return this;
        }

        public IRuntimeHttpPipeline Prepend(SendAsyncStepExpand item)
        {
            if (item != null)
            {
                Prepend(new SendAsyncFactory(item));
            }
            return this;
        }

        // you can use this as the ISendAsync Implementation
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) => Pipeline.SendAsync(request, callback);
    }
}



