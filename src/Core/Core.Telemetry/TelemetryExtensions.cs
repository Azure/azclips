using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public static class TelemetryExtensions
{
    public static Activity? StartCLIActivity(this ITelemetryService service, string[] args)
    {
        var activity = service.ActivitySource.StartActivity("AzureCLI.Main", ActivityKind.Server);

        if (activity != null)
        {
            var commandBuilder = new StringBuilder("az");

            // Telemetry recording will be moved to engine soon.
            // Temporarily proceed with the parser here.
            var parameters = new StringBuilder();

            bool parsingOptions = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (!parsingOptions && !args[i].StartsWith("-"))
                {
                    commandBuilder.Append(" ").Append(args[i]);
                }
                else
                {
                    parsingOptions = true;
                    if (args[i].StartsWith("-"))
                    {
                        parameters.Append(args[i]);
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            parameters.Append(" ***");
                            i++;
                        }
                        parameters.Append(" ");
                    }
                    else
                    {
                        parameters.Append(args[i]).Append(" ");
                    }
                }
            }

            activity.SetTag("command", commandBuilder.ToString());
            activity.SetTag("parameter", parameters.ToString().Trim());
        }

        return activity;
    }

    public static Activity? StartPowerShellActivity(this ITelemetryService service, string commandName, IDictionary<string, object> parameters)
    {
        var activity = service.ActivitySource.StartActivity("AzurePS.Cmdlet", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("command", commandName);

            var paramBuilder = new StringBuilder();
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    paramBuilder.Append($"-{kvp.Key} *** ");
                }
            }
            activity.SetTag("parameter", paramBuilder.ToString().Trim());
        }

        return activity;
    }

    public static Activity GetRoot(this Activity activity)
    {
        while (activity.Parent is not null)
            activity = activity.Parent;
        return activity;
    }

    /// <summary>
    /// Tags the activity with the DevDiv-mandated <c>eventName</c> formatted as
    /// <c>{ProductName}.{Activity.OperationName}</c>. Null-safe and fluent: returns
    /// the same activity so it can be chained directly off <c>StartActivity(...)</c>.
    /// </summary>
    public static Activity? SetEventName(this Activity? activity, string eventName)
    {
        activity?.SetTag(
            CoreTelemetryProperties.EventNameTag,
            $"{CoreTelemetryProperties.ProductName}.{eventName}");
        return activity;
    }
}
