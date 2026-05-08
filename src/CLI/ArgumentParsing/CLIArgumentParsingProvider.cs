namespace Core.Cli;

using Core.Abstractions;

public class CLIArgumentParsingProvider(PropertyParser propertyParser, ILoggingService loggingService) : IArgumentParsingProvider
{
    public IDictionary<string, object> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
    {
        //TODO: handle positional arguemnts for CLI
        Dictionary<string, object> parsedArguments = new(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in args)
        {
            string key = kvp.Key;
            object value = kvp.Value;

            // Resolve aliases
            if (!parameters.ContainsKey(key) && parameterAliases != null && parameterAliases.TryGetValue(key, out var canonicalKey))
            {
                loggingService.Info($"Resolving alias '{key}' to canonical parameter '{canonicalKey}'");
                key = canonicalKey;
            }

            if (!parameters.TryGetValue(key, out var parameterMetadata))
            {
                throw new ArgumentException($"Unable to find parameter: {key}");
            }
            //less likely to be null, but we should still check
            if (parameterMetadata == null)
            {
                throw new InvalidOperationException($"Parameter metadata is null for: {key}");
            }
            try
            {
                parsedArguments[parameterMetadata.Name] = ParseArgument(parameterMetadata.Name, value, parameterMetadata.ParameterType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parameter '{key}': {ex.Message}", ex);
            }
        }
        return parsedArguments;
    }

    private object ParseArgument(string key, object source, Type targetType)
    {
        // PS argument are already expected type
        // otherwise plain string arguments need no type conversion
        if (targetType.IsInstanceOfType(source))
        {
            return source;
        }
        // Yabo: arbitrarily treat argument from CLI as string for now
        // Need CLI expert to polish this logic if necessary, especially for array and complex object parsing
        if (preProcessArgument(source) is not string[] arr)
        {
            throw new ArgumentException($"Invalid argument type for parameter '{key}': {source.GetType().FullName}");
        }

        if (arr.Length == 0)
        {
            if (targetType == typeof(bool))
            {
                return true;
            }

            throw new ArgumentException($"Parameter '{key}' requires a value.");
        }

        string strValue = arr[0];
        return propertyParser.ConvertToType(key, strValue, targetType);
    }

    private object preProcessArgument(object source)
    {
        // Yabo: invocation.Parameters from CLI is currently <string, List<List<string>>>
        // copied over from legacy codebase, convert value
        // List<List<string>> to string[]
        // Need CLI expert to polish this logic if necessary
        return (source is List<List<string>> listOfLists
                ? listOfLists
                : source is List<string> list
                    ? [list]
                    : [new List<string> { source?.ToString() ?? string.Empty }]
                )
                .SelectMany(innerList => innerList)
                .ToArray();
    }

    // PowerShell should have validated most of cases
    // This method is mainly for CLI required, allowed values validation
    public IList<string> ValidateArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters)
    {
        var errors = new List<string>();

        // Build lookup by original C# property name (args keys are already resolved to original names)
        var byName = parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        // Check for unknown arguments and allowed values
        foreach (var kvp in args)
        {
            if (!byName.TryGetValue(kvp.Key, out var metadata))
            {
                errors.Add($"Unknown parameter: '{kvp.Key}'");
                continue;
            }

            if (metadata.AllowedValues is { Length: > 0 }
                && kvp.Value is string strValue
                && !metadata.AllowedValues.Contains(strValue, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Parameter '{kvp.Key}' value '{strValue}' is not allowed. Allowed values: {string.Join(", ", metadata.AllowedValues)}");
            }
        }

        // Check for missing required arguments
        foreach (var kvp in byName)
        {
            if (kvp.Value.IsRequired && !args.ContainsKey(kvp.Key))
            {
                errors.Add($"Required parameter '{kvp.Key}' is missing.");
            }
        }

        return errors;
    }
}