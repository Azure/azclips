// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing
{
    public class ArgumentParsingService(IArgumentParsingProvider provider) : IArgumentParsingService
    {
        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
        {
            IDictionary<string, object> parsedArgs = provider.ParseArguments(args, parameters, parameterAliases);
            IList<string>? validationErrors = provider.ValidateArguments(parsedArgs, parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
            if (validationErrors != null && validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid arguments: {string.Join(", ", validationErrors)}");
            }
            parsedArgs.ToList().ForEach(p => SetProperty(processor, p.Key, p.Value));
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
    }
}