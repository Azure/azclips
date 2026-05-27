using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Core.Abstractions;

namespace Core.Telemetry;

public static class TelemetryExtensions
{
    // Todo: hoist this to the host
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

            activity.ApplyCommandLine(commandBuilder.ToString(), parameters.ToString().Trim());
        }

        return activity;
    }

    public static Activity? StartPowerShellActivity(this ITelemetryService service, string commandName, IDictionary<string, object> parameters)
    {
        var activity = service.ActivitySource.StartActivity("AzurePS.Cmdlet", ActivityKind.Server);

        if (activity != null)
        {
            var paramBuilder = new StringBuilder();
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    paramBuilder.Append($"-{kvp.Key} *** ");
                }
            }
            activity.ApplyCommandLine(commandName, paramBuilder.ToString().Trim());
        }

        return activity;
    }
}
