using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Core.Telemetry;
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

            // Set activity root: single tagging point for all DevDiv-mandated core
            // properties plus per-invocation tags (ClientType, module info, EventName,
            // DisplayName). Squads should not tag the root activity directly; use
            // ITelemetryService.AddProperty / AddMeasure instead.
            session.Subscribe(Events.SessionCreated, _ =>
            {
                Activity? activity = Activity.Current;
                if (activity == null) return;

                var root = TelemetryUtils.GetActivityRoot(activity);
                ApplyBaseProperties(root, session);

                loggingService.Debug($"Root activity tagged: DisplayName={root.DisplayName}");
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
                // Tag root with totalDuration before disposing the paired session activity
                // so the value lands on the same span that carries the DevDiv core properties.
                Activity? currentActivity = Activity.Current;
                if (currentActivity != null)
                {
                    var root = TelemetryUtils.GetActivityRoot(currentActivity);
                    // Activity.Duration only populates after Stop(); compute manually while
                    // the root is still live so totalDuration reflects the full invocation.
                    var elapsed = root.Duration > TimeSpan.Zero
                        ? root.Duration
                        : DateTime.UtcNow - root.StartTimeUtc;
                    root.SetTag("totalDuration", (long)elapsed.TotalMilliseconds);
                }

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

            // Map processor lifecycle to root-span Status. Signaled by InvocationSession
            // around the single processor.ProcessAsync() call site so CLI and PowerShell
            // produce identical Status semantics without host-specific plumbing.
            session.Subscribe(Events.ProcessorCompleted, (data) =>
            {
                Activity? root = Activity.Current;
                if (root == null) return;
                TelemetryUtils.GetActivityRoot(root);
                root.SetStatus(ActivityStatusCode.Ok);
            });
            session.Subscribe(Events.ProcessorException, (data) =>
            {
                var evt = (EventData)data;
                Activity? root = Activity.Current;
                if (root == null) return;
                TelemetryUtils.GetActivityRoot(root);
                root.SetStatus(ActivityStatusCode.Error, evt.ExtendedData);
                root.AddException(new Exception(evt.ExtendedData));
            });

            return session;
        }

        // TODO(Yabo): Observability squad to implement metrics subscriptions, e.g. CPU/memory usage
        // Snapshot GC.GetTotalMemory() at some stages — detect memory-heavy extension loads
        private InvocationSession SubscribeMetric(InvocationSession session)
        {
            return session;
        }

        private void ApplyBaseProperties(Activity root, InvocationSession session)
        {
            root.SetTag("ClientType", clientType);

            // NOTE: On the CLI side "module.name" currently maps to the extension name.
            // The tag key may be renamed to "extension.name" in the future — TBD.
            if (!string.IsNullOrEmpty(session.Invocation.ModuleName))
                root.SetTag("module.name", session.Invocation.ModuleName);
            if (!string.IsNullOrEmpty(session.Invocation.ModuleVersion))
                root.SetTag("module.version", session.Invocation.ModuleVersion);

            CoreTelemetryProperties.ApplyTo(root);

            var extension = session.Invocation.ExtensionName;
            var command = session.Invocation.CommandName;
            root.SetTag("EventName", $"cirrus.{extension}.{command}.executed");
            root.DisplayName = $"cirrus {extension} {command}";
        }
    }
}
