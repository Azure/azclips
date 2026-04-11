using Core.Abstractions;

namespace Core.ArgumentParsing
{
    public class ArgumentParsingService(PropertyParser propertyParser, ILoggingService loggingService) : IArgumentParsingService
    {
        public IDictionary<string, object> ParseArguments(IDictionary<string, object> args, IDictionary<string, IParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
        {
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
                    parsedArguments[key] = ParseArgument(key, value, parameterMetadata.ParameterType);
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
            preProcessArgument();
            return source is string strValue
                ? propertyParser.ConvertToType(key, strValue, targetType)
                : throw new ArgumentException($"Invalid argument type for parameter '{key}': {source.GetType().FullName}");
        }

        private object preProcessArgument()
        {
            throw new NotImplementedException("This method is a placeholder for any future pre-processing logic that may be needed for arguments before parsing. It can be used to handle special cases, perform transformations, or apply any necessary adjustments to the raw input before it is processed by the main parsing logic.");  
        }

        public IProcessor BindArgumentsToCommand(IProcessor command, IDictionary<string, object> parameters)
        {
            parameters.ToList().ForEach(p => SetProperty(command, p.Key, p.Value));
            return command;
        }

        private void SetProperty(IProcessor processor, string propertyName, object? value)
        {
            var propertyInfo = processor.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"The property '{propertyName}' does not exist on command '{processor.GetType().FullName}'.");
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
                throw new ArgumentException($"The property '{propertyName}' on command '{processor.GetType().FullName}' could not be set to value '{value}'.", e);
            }
        }

        // PowerShell should have validated most of cases
        // This method is mainly for CLI required, allowed values validation
        public IList<string> ValidateArguments(IDictionary<string, object> args, IDictionary<string, IParameterMetadata> parameters)
        {
            var errors = new List<string>();

            // Check for unknown arguments and allowed values
            foreach (var kvp in args)
            {
                if (!parameters.TryGetValue(kvp.Key, out var metadata))
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
            foreach (var kvp in parameters)
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