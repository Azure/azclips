// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public const string DelayBeforePolling = nameof(DelayBeforePolling);
        public const string Polling = nameof(Polling);
        public const string Progress = nameof(Progress);

        public const string CommandInitiated = nameof(CommandInitiated);
        public const string CommandProcessAsyncStart = nameof(CommandProcessAsyncStart);
        public const string CommandProcessAsyncEnd = nameof(CommandProcessAsyncEnd);
        public const string CommandException = nameof(CommandException);
        public const string CommandBeforeAPICall = nameof(CommandBeforeAPICall);
        public const string CommandAfterAPICall = nameof(CommandAfterAPICall);

        public const string SessionCreated = nameof(SessionCreated);
        public const string SessionDisposed = nameof(SessionDisposed);
        public const string EngineLoadingAssembly = nameof(EngineLoadingAssembly);
        public const string EngineBindingCommand = nameof(EngineBindingCommand);

        public const string AuthBeforeTokenAcquisition = nameof(AuthBeforeTokenAcquisition);
        public const string AuthAfterTokenAcquisition = nameof(AuthAfterTokenAcquisition);

        public const string Verbose = nameof(Verbose);
        public const string Debug = nameof(Debug);
        public const string Information = nameof(Information);
        public const string Error = nameof(Error);
        public const string Warning = nameof(Warning);

        /// <summary>
        /// Events that fire on the runspace thread (engine/session lifecycle).
        /// These are safe to route directly to ILoggingService → PS streams.
        /// All other events fire on the worker thread and must go through the channel.
        /// </summary>
        public static readonly HashSet<string> RunspaceThreadEvents = new(StringComparer.Ordinal)
        {
            SessionCreated,
            SessionDisposed,
            EngineLoadingAssembly,
            EngineBindingCommand,
        };

        public static bool IsRunspaceThread(string eventId) => RunspaceThreadEvents.Contains(eventId);
    }
}
