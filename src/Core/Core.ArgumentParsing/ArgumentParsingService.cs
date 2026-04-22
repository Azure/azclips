using Core.Abstractions;

namespace Core.ArgumentParsing
{
    public class ArgumentParsingService(PropertyParser propertyParser, ILoggingService loggingService) : IArgumentParsingService
    {
        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
        {
            IDictionary<string, object> parsedArgs = ParseArguments(args, parameters, parameterAliases);
            IList<string> validationErrors = ValidateArguments(parsedArgs, parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
            if (validationErrors != null && validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid arguments: {string.Join(", ", validationErrors)}");
            }
            return BindArgumentsToProcessor(processor, parsedArgs);
        }

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
            string strValue = preProcessArgument(source) is string[] arr ? arr[0] : throw new ArgumentException($"Invalid argument type for parameter '{key}': {source.GetType().FullName}");
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

        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> parameters)
        {
            parameters.ToList().ForEach(p => SetProperty(processor, p.Key, p.Value));
            return processor;
        }

        private void SetProperty(IProcessor processor, string propertyName, object? value)
        {
            var propertyInfo = processor.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"The property '{propertyName}' does not exist on processor '{processor.GetType().FullName}'.");
            }
            try
            {
                if (value != null)
                {
                    var convertedValue = Convert.ChangeType(value, propertyInfo.PropertyType);
                    propertyInfo.SetValue(processor, convertedValue);
                }
                else
                {
                    propertyInfo.SetValue(processor, null);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"The property '{propertyName}' on processor '{processor.GetType().FullName}' could not be set to value '{value}'.", e);
            }
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
}