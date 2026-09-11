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
                throw AzclipsErrorException.UsageError($"Invalid arguments: {string.Join(", ", validationErrors)}");
            }

            // Yabo: the applying default need to work with future parameter sets.
            // when a parameter set doesn't have the key, the default value should not be applied
            profileService.GetProfileKeysForDefault().ForEach(key =>
            {
                // Apply default property when not in args and in processor
                if ((!parsedArgs.ContainsKey(key) || parsedArgs[key] is null) && processor.GetType().GetProperty(key) is {})
                {
                    // TODO(Yabo): To throw proper exceptions, provider need to return flavored argument names
                    // along with parsed args
                    if (profileService.GetProfileValueByKey(profile, key) is not {} value)
                    {
                        throw new ArgumentException($"Missing required argument '{key}' and no value found in profile.");
                    }
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