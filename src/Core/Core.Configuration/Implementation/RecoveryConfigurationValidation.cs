// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

internal static class RecoveryConfigurationValidation
{
    internal const string TriggerKey = "agent.recovery";

    internal static bool IsRecoveryKey(string key) =>
        string.Equals(key, TriggerKey, StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith(TriggerKey + ".", StringComparison.OrdinalIgnoreCase);

    internal static void ValidateShape(JsonObject root, string configFilePath)
    {
        foreach (var agent in root.Where(entry => string.Equals(entry.Key, "agent", StringComparison.OrdinalIgnoreCase)))
        {
            if (agent.Value is not JsonObject agentObject)
                throw new InvalidOperationException("Configuration 'agent' must be an object.");
            if (agentObject.Any(entry => entry.Key.Replace(':', '.').StartsWith("recovery.", StringComparison.OrdinalIgnoreCase)))
                throw FlatRecoveryShape();
            foreach (var recovery in agentObject.Where(entry => string.Equals(entry.Key, "recovery", StringComparison.OrdinalIgnoreCase)))
            {
                if (recovery.Value is not JsonObject)
                    throw new InvalidOperationException(
                        "Configuration 'agent.recovery' must be stored as an object, for example { \"trigger\": \"manual\", \"style\": \"oneshot\", \"historySize\": 10 }. " +
                        $"Edit '{configFilePath}' directly to replace or remove the invalid 'agent.recovery' node; config commands cannot repair this file shape.");
            }
        }

        if (root.Any(entry => IsRecoveryKey(entry.Key.Replace(':', '.'))))
            throw FlatRecoveryShape();
    }

    internal static void ValidateValues(IEnumerable<KeyValuePair<string, object?>> values, IConfigurationSchema schema, IConfigurationValidator validator)
    {
        foreach (var (key, value) in values.Where(entry => IsRecoveryKey(entry.Key)))
        {
            if (!schema.IsKnownKey(key))
                throw new ArgumentException($"Unknown recovery configuration key '{key}'.");
            var validation = validator.ValidateValue(key, value);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);
        }
    }

    private static InvalidOperationException FlatRecoveryShape() =>
        new("Store recovery in the nested 'agent.recovery' object with a 'trigger' leaf, not as a flat or scalar 'agent.recovery' value.");

}
