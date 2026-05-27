using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Core.Telemetry;
using System.Diagnostics;
using Microsoft.Azure.Cirrus.ExtensionRuntime;

namespace Core.Engine
{
    public partial class Engine
    {
        // session subscribes messages first, then each service 
        private IEventSubscriber SubscribeAll(IEventSubscriber subscriber)
        {
            SubscribeMessages(subscriber);
            SubscribeActivity(subscriber);
            SubscribeMetric(subscriber);
            return subscriber;
        }

        // Messages for events before processor execution start (inclusive) goes to ILoggingService
        // Messages for events after processor execution start goes to session.channel
        // session made this wired loggingService to channel during process, all messages subscribe loggingService
        private IEventSubscriber SubscribeMessages(IEventSubscriber subscriber)
        {
            // Print every event id for debugging purpose,
            // SubscribeAll() subscribers are guaranteed executed before everyone else
            // No need to print event id for subsequent events
            subscriber.SubscribeAll(msg =>
            {
                var evt = (EventData)msg;
                string id = evt.Id;
                loggingService.Verbose($"[{id}]");
            });

            subscriber.Subscribe(Events.SessionCreated, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.SessionDisposed, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.ProcessorInitiated, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.ProcessorBeforeHttpRequestCreate, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.ProcessorAfterHttpRequestCreate, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.TracingHttpRequestMessage, async (data) =>
            {
                var evt = (EventData)data;
                HttpRequestMessage request = (HttpRequestMessage)evt.RequestMessage;
                loggingService.Debug(await request.GetLog());
            });
            subscriber.Subscribe(Events.TracingHttpResponseMessage, async (data) =>
            {
                var evt = (EventData)data;
                HttpResponseMessage response = (HttpResponseMessage)evt.ResponseMessage;
                loggingService.Debug(await response.GetLog());
            });

            subscriber.Subscribe(Events.DelayPolling, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.BeforePollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.AfterPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.BeforeFinalPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.AfterFinalPollingCall, (data) =>
            {
                EventData evt = (EventData)data;
                loggingService.Debug(evt.Message);
            });

            subscriber.Subscribe(Events.PollingFinished, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            subscriber.Subscribe(Events.PollingFailed, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Error(evt.Message);
            });

            subscriber.Subscribe(Events.ResponseNoContent, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            subscriber.Subscribe(Events.ResponseEmptyBody, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });

            subscriber.Subscribe(Events.ResponseInvalidJson, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });
 
            return subscriber;
        }

        // Paired stages used for paired events like processor before/after processor api calls
        // Sequential stages used for sequential events
        // Paired always check stack: if (pairedStages.TryPop(out var activity)) activity.Dispose();
        // Sequential always check previous: sequentialStage?.Dispose(); sequentialStage = telemetryService.StartActivity(...);
        private IEventSubscriber SubscribeActivity(IEventSubscriber subscriber)
        {
            Stack<Activity> pairedStages = new();
            Activity? sequentialStage = null;

            // Set activity root
            subscriber.Subscribe(Events.SessionCreated, (data) =>
            {
                var evt = (EventData)data;
                Activity? activity = Activity.Current;
                if (activity == null) return;
                var root = activity.GetRoot();

                //todo: fix this post merge
                // ApplyBaseTelemetryProperties(root, session);

                loggingService.Debug($"Tags set. Verifying: module.name={activity.GetTagItem("module.name")}, module.version={activity.GetTagItem("module.version")}");
            });

            // Paired: SessionCreated - SessionDisposed
            // Session dispose will ensure all activities are disposed
            subscriber.Subscribe(Events.SessionCreated, (data) =>
            {
                var evt = (EventData)data;
                var activity = telemetryService.StartActivity(evt.Id, ActivityKind.Internal).ApplyEvent(evt.Id);
                if (activity != null)
                    pairedStages.Push(activity);
            });
            subscriber.Subscribe(Events.SessionDisposed, (data) =>
            {
                Activity? currentActivity = Activity.Current;
                if (currentActivity != null)
                {
                    var root = currentActivity.GetRoot();
                    // Activity.Duration only populates after Stop(); compute manually while
                    // the root is still live so totalDuration reflects the full invocation.
                    var elapsed = root.Duration > TimeSpan.Zero
                        ? root.Duration
                        : DateTime.UtcNow - root.StartTimeUtc;
                    root.ApplyTotalDuration(elapsed);
                }

                while (pairedStages.TryPop(out var activity))
                    activity.Dispose();
                pairedStages.Clear();
                sequentialStage?.Dispose();
            });

            // Paired: AuthBeforeTokenAcquisition - AuthAfterTokenAcquisition
            subscriber.Subscribe(Events.AuthBeforeTokenAcquisition, (data) =>
            {
                var evt = (EventData)data;
                var activity = telemetryService.StartActivity(evt.Id, ActivityKind.Internal).ApplyEvent(evt.Id);
                if (activity != null)
                    pairedStages.Push(activity);
            });
            subscriber.Subscribe(Events.AuthAfterTokenAcquisition, (data) =>
            {
                if (pairedStages.TryPop(out var activity))
                    activity.Dispose();
            });

            subscriber.Subscribe(Events.ProcessorProcessAsyncStart, (data) =>
            {
                var evt = (EventData)data;
                sequentialStage = telemetryService.StartActivity(evt.Id, ActivityKind.Internal).ApplyEvent(evt.Id);
            });
            subscriber.Subscribe(Events.ProcessorProcessAsyncEnd, (data) =>
            {
                sequentialStage?.Dispose();
                sequentialStage = null;
            });

            // Map processor lifecycle to root-span Status. Signaled by InvocationSession
            // around the single processor.ProcessAsync() call site so CLI and PowerShell
            // produce identical Status semantics without host-specific plumbing.
            subscriber.Subscribe(Events.ProcessorCompleted, (data) =>
            {
                Activity? activity = Activity.Current;
                if (activity == null) return;
                var root = activity.GetRoot();
                root.SetStatus(ActivityStatusCode.Ok);
            });
            subscriber.Subscribe(Events.ProcessorException, (data) =>
            {
                var evt = (EventData)data;
                Activity? activity = Activity.Current;
                if (activity == null) return;

                var root = activity.GetRoot();
                root.SetStatus(ActivityStatusCode.Error, evt.ExtendedData);
                root.AddException(new Exception(evt.ExtendedData));
            });

            return subscriber;
        }

        // TODO(Yabo): Observability squad to implement metrics subscriptions, e.g. CPU/memory usage
        // Snapshot GC.GetTotalMemory() at some stages — detect memory-heavy extension loads
        private IEventSubscriber SubscribeMetric(IEventSubscriber subscriber)
        {
            return subscriber;
        }
    }
}
