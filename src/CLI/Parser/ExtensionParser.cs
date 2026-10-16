// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Parser;

/// <summary>
/// Parses raw CLI token arrays into command names and parameter key-value pairs.
/// </summary>
public class ExtensionParser
{
    public (string, IDictionary<string, object>) Parse(string[] args)
    {
        Dictionary<string, List<List<string>>> parameters = [];
        int i = 0;
        
        // Find where parameters start (first argument beginning with -)
        while (i < args.Length && !args[i].StartsWith("-"))
        {
            i++;
        }
        
        // Everything before the first - is the command name
        string commandName = string.Join(" ", args[..i]);
        
        var currentProperty = string.Empty;
        var currentValues = new List<string>();

        foreach (var arg in args[i..])
        {
            if (arg.StartsWith("-"))
            {
                // If we were collecting values for a previous property, save it
                if (!string.IsNullOrEmpty(currentProperty))
                {
                    if (!parameters.ContainsKey(currentProperty))
                    {
                        parameters[currentProperty] = new List<List<string>>();
                    }
                    parameters[currentProperty].Add(currentValues);
                }

                // Start a new property (strip leading dashes, CLI-specific syntax)
                currentProperty = arg.TrimStart('-').ToLowerInvariant();
                currentValues = new List<string>();
            }
            else
            {
                if (string.IsNullOrEmpty(currentProperty))
                {
                    // If no property is set, treat this as a positional argument
                    parameters[string.Empty] = parameters.GetValueOrDefault(string.Empty, []);
                    parameters[string.Empty].Add([arg]);
                }
                else
                {
                    // Collect values for the current property
                    currentValues.Add(arg);
                }
            }
        }

        // Save the last property collected
        if (!string.IsNullOrEmpty(currentProperty))
        {
            if (!parameters.ContainsKey(currentProperty))
            {
                parameters[currentProperty] = [];
            }
            parameters[currentProperty].Add(currentValues);
        }
        
        return (commandName, parameters.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
    }
}
