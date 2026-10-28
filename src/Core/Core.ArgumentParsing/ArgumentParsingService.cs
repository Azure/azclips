// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing
{
    public class ArgumentParsingService(IArgumentParsingProvider provider, IProfileService profileService) : IArgumentParsingService
    {
        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases, Profile? profile)
        {
            IDictionary<string, object> parsedArgs = provider.ParseArguments(args, parameters, parameterAliases);
            IList<string>? validationErrors = provider.ValidateArguments(parsedArgs, parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
            if (validationErrors != null && validationErrors.Count > 0)
            {
                var message = $"Invalid arguments: {string.Join(", ", validationErrors)}";
                if (parameters.Values.Any(parameter => parameter.IsRequired && !parsedArgs.ContainsKey(parameter.Name)))
                {
                    throw AzclipsErrorException.MissingRequiredParameters(message);
                }
                throw AzclipsErrorException.UsageError(message);
            }

            profileService.GetProfileKeysForDefault().ForEach(key =>
            {
                if ((!parsedArgs.ContainsKey(key) || parsedArgs[key] is null)
                    && parameters.Values.Any(parameter => parameter.Name == key && parameter.ApplyProfileDefault)
                    && processor.GetType().GetProperty(key) is { }
                    && profileService.GetProfileValueByKey(profile, key) is { } value
                    && !string.IsNullOrWhiteSpace(value))
                {
                    parsedArgs[key] = value;
                }
            });

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
                    // Values already assignable to the target type (e.g. a string[] for a collection
                    // parameter such as --scope/-Scope) must be assigned directly: Convert.ChangeType
                    // only handles IConvertible scalars and throws for arrays and other collections.
                    var convertedValue = propertyInfo.PropertyType.IsInstanceOfType(value)
                        ? value
                        : Convert.ChangeType(value, propertyInfo.PropertyType);
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