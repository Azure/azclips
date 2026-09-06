// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions.Runtime
{
    public static class Events
    {
        public const string Log = nameof(Log);
        public const string Validation = nameof(Validation);
        public const string ValidationWarning = nameof(ValidationWarning);
        public const string AfterValidation = nameof(AfterValidation);
        public const string RequestCreated = nameof(RequestCreated);
        public const string ResponseCreated = nameof(ResponseCreated);
        public const string URLCreated = nameof(URLCreated);
        public const string Finally = nameof(Finally);
        public const string HeaderParametersAdded = nameof(HeaderParametersAdded);
        public const string BodyContentSet = nameof(BodyContentSet);
        public const string BeforeCall = nameof(BeforeCall);
        public const string BeforeResponseDispatch = nameof(BeforeResponseDispatch);
        public const string FollowingNextLink = nameof(FollowingNextLink);
        public const string Progress = nameof(Progress);

        // processor
        public const string ProcessorContextCreated = nameof(ProcessorContextCreated);
        public const string ProcessorInitiated = nameof(ProcessorInitiated);
        public const string ProcessorAcquired = nameof(ProcessorAcquired);
        public const string ProcessorProcessAsyncStart = nameof(ProcessorProcessAsyncStart);
        public const string ProcessorProcessAsyncEnd = nameof(ProcessorProcessAsyncEnd);
        public const string ProcessorException = nameof(ProcessorException);
        public const string ProcessorBeforeAPICall = nameof(ProcessorBeforeAPICall);
        public const string ProcessorAfterAPICall = nameof(ProcessorAfterAPICall);
        public const string ProcessorBeforeHttpRequestCreate = nameof(ProcessorBeforeHttpRequestCreate);
        public const string ProcessorAfterHttpRequestCreate = nameof(ProcessorAfterHttpRequestCreate);

        // session
        public const string SessionCreated = nameof(SessionCreated);
        public const string SessionDisposed = nameof(SessionDisposed);
        public const string EngineLoadingAssembly = nameof(EngineLoadingAssembly);
        public const string EngineBindingProcessor = nameof(EngineBindingProcessor);

        // http pipeline
        public const string TracingHttpRequestMessage = nameof(TracingHttpRequestMessage);
        public const string TracingHttpResponseMessage = nameof(TracingHttpResponseMessage);

        // polling
        public const string DelayPolling = nameof(DelayPolling);
        public const string BeforePollingCall = nameof(BeforePollingCall);
        public const string AfterPollingCall = nameof(AfterPollingCall);
        public const string BeforeFinalPollingCall = nameof(BeforeFinalPollingCall);
        public const string AfterFinalPollingCall = nameof(AfterFinalPollingCall);
        public const string PollingFinished = nameof(PollingFinished);
        public const string PollingFailed = nameof(PollingFailed);

        // Response
        public const string ResponseNoContent = nameof(ResponseNoContent);
        public const string ResponseEmptyBody = nameof(ResponseEmptyBody);
        public const string ResponseInvalidJson = nameof(ResponseInvalidJson);
        public const string ResourceOperationFailed = nameof(ResourceOperationFailed);

        // Pagination
        public const string PaginationNextPage = nameof(PaginationNextPage);

        // Auth
        public const string AuthBeforeTokenAcquisition = nameof(AuthBeforeTokenAcquisition);
        public const string AuthAfterTokenAcquisition = nameof(AuthAfterTokenAcquisition);

        // Generic Events
        public const string ActionStart = nameof(ActionStart);
        public const string ActionEnd = nameof(ActionEnd);

        public const string Verbose = nameof(Verbose);
        public const string Debug = nameof(Debug);
        public const string Information = nameof(Information);
        public const string Error = nameof(Error);
        public const string Warning = nameof(Warning);
    }
}
