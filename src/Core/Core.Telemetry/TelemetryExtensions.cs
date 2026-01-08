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
        var activity = service.StartActivity("AzureCLI.Main", ActivityKind.Server);
        
        if (activity != null)
        {
            var commandName = args.Length > 0 ? args[0] : "none";
            activity.SetTag("command", commandName);

            var parameters = new StringBuilder();
            for (int i = 1; i < args.Length; i++)
            {
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
            activity.SetTag("parameter", parameters.ToString().Trim());
        }

        return activity;
    }

    public static Activity? StartPowerShellActivity(this ITelemetryService service, string commandName, IDictionary<string, object> parameters)
    {
        var activity = service.StartActivity("AzurePS.Cmdlet", ActivityKind.Server);

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
}
