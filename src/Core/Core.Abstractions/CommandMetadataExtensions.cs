// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public static class CommandMetadataExtensions
{
    /// <summary>
    /// Adds a default set to commands without explicit sets, preserving parameter-level requiredness.
    /// Call after injecting any shared parameters, and only on the intended client's table.
    /// </summary>
    public static void AddDefaultParameterSets(this IDictionary<string, CommandMetadata> commands)
    {
        foreach (var key in commands.Keys.ToArray())
        {
            var command = commands[key];
            if (command.ParameterSets is { Count: > 0 })
            {
                continue;
            }

            commands[key] = command with
            {
                ParameterSets = new Dictionary<string, ParameterSetMetadata>
                {
                    ["Default"] = new("Default", IsDefault: true, Parameters: command.Parameters.Values.ToDictionary(
                        parameter => parameter.Name,
                        parameter => new ParameterReference(parameter.Name, IsRequired: parameter.IsRequired))),
                },
            };
        }
    }
}
