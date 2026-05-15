using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using System.Diagnostics;
using Microsoft.Azure.Cirrus.Extensions.Runtime;

namespace Core.Engine
{
    public partial class Engine
    {
        // session subscribes messages first, then each service 
        private InvocationSession SubscribeAll(InvocationSession session)
        {
            SubscribeMessages(session);
            SubscribeActivity(session);
            SubscribeMetric(session);
            return session;
        }

        // Messages for events before processor execution start (inclusive) goes to ILoggingService
        // Messages for events after processor execution start goes to session.channel
        // session made this wired loggingService to channel during process, all messages subscribe loggingService
        private InvocationSession SubscribeMessages(InvocationSession session)
        {
            // Print every event id for debugging purpose, 
            // SubscribeAll() subscribers are guaranteed executed before everyone else 
            // No need to print event id for subsequent events
            session.SubscribeAll(msg =>
            {
                var evt = (EventData)msg;
                string id = evt.Id;
                loggingService.Verbose($"[{id}]");
            });

            session.Subscribe(Events.SessionCreated, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.SessionDisposed, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.ProcessorInitiated, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.ProcessorBeforeHttpRequestCreate, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.ProcessorAfterHttpRequestCreate, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.TracingHttpRequestMessage, async (data) =>
            {
                var evt = (EventData)data;
                HttpRequestMessage request = (HttpRequestMessage)evt.RequestMessage;
                loggingService.Debug(await request.GetLog());
            });
            session.Subscribe(Events.TracingHttpResponseMessage, async (data) =>
            {
                var evt = (EventData)data;
                HttpResponseMessage response = (HttpResponseMessage)evt.ResponseMessage;
                loggingService.Debug(await response.GetLog());
            });

            session.Subscribe(Events.DelayPolling, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.BeforePollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.AfterPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.BeforeFinalPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.AfterFinalPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            session.Subscribe(Events.PollingFinished, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            session.Subscribe(Events.PollingFailed, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Error(evt.Message);
            });

            session.Subscribe(Events.ResponseNoContent, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            session.Subscribe(Events.ResponseEmptyBody, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            session.Subscribe(Events.ResponseInvalidJson, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });
 
            return session;
        }

        // Paired stages used for paired events like processor before/after processor api calls
        // Sequential stages used for sequential events
        // Paired always check stack: if (pairedStages.TryPop(out var activity)) activity.Dispose();
        // Sequential always check previous: sequentialStage?.Dispose(); sequentialStage = telemetryService.StartActivity(...);
        private InvocationSession SubscribeActivity(InvocationSession session)
        {
            Stack<Activity> pairedStages = new Stack<Activity>();
            Activity? sequentialStage = null;

            string sessionId = session.Id;

            // Set activity root
            session.Subscribe(Events.SessionCreated, (data) =>
            {
                Activity? activity = Activity.Current;
                if (activity != null)
                {
                    // Walk up to the root activity so module tags appear on the top-level span
                    while (activity.Parent is not null)
                        activity = activity.Parent;
                    loggingService.Debug($"Root activity={activity.DisplayName}, setting tags...");
                    activity.SetTag("ClientType", clientType);
                    // NOTE: On the CLI side "module.name" currently maps to the extension name.
                    // The tag key may be renamed to "extension.name" in the future — TBD.
                    if (!string.IsNullOrEmpty(session.Invocation.ModuleName))
                        activity.SetTag("module.name", session.Invocation.ModuleName);
                    if (!string.IsNullOrEmpty(session.Invocation.ModuleVersion))
                        activity.SetTag("module.version", session.Invocation.ModuleVersion);
                    loggingService.Debug($"Tags set. Verifying: module.name={activity.GetTagItem("module.name")}, module.version={activity.GetTagItem("module.version")}");
                }
            });

            // Paired: SessionCreated - SessionDisposed
            // Session dispose will ensure all activities are disposed
            session.Subscribe(Events.SessionCreated, (data) =>
            {
                var evt = (EventData)data;
                var activity = telemetryService.StartActivity(evt.Id, ActivityKind.Internal);
                if (activity != null)
                    pairedStages.Push(activity);
            });
            session.Subscribe(Events.SessionDisposed, (data) =>
            {
                while (pairedStages.TryPop(out var activity))
                    activity.Dispose();
                pairedStages.Clear();
                sequentialStage?.Dispose();
            });

            // Paired: AuthBeforeTokenAcquisition - AuthAfterTokenAcquisition
            session.Subscribe(Events.AuthBeforeTokenAcquisition, (data) =>
            {
                var evt = (EventData)data;
                var activity = telemetryService.StartActivity(evt.Id, ActivityKind.Internal);
                if (activity != null)
                    pairedStages.Push(activity);
            });
            session.Subscribe(Events.AuthAfterTokenAcquisition, (data) =>
            {
                if (pairedStages.TryPop(out var activity))
                    activity.Dispose();
            });

            session.Subscribe(Events.ProcessorProcessAsyncStart, (data) =>
            {
                var evt = (EventData)data;
                sequentialStage = telemetryService.StartActivity(evt.Id, ActivityKind.Internal);
            });
            session.Subscribe(Events.ProcessorProcessAsyncEnd, (data) =>
            {
                sequentialStage?.Dispose();
                sequentialStage = null;
            });

            //TODO(Yabo): Observability Squad to subscribe more events for telemetry
            return session;
        }

        // TODO(Yabo): Observability squad to implement metrics subscriptions, e.g. CPU/memory usage
        // Snapshot GC.GetTotalMemory() at some stages — detect memory-heavy extension loads
        private InvocationSession SubscribeMetric(InvocationSession session)
        {
            return session;
        }
    }
}




