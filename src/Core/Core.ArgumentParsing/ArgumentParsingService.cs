// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing
{
    public class ArgumentParsingService(IArgumentParsingProvider provider, IProfileService profileService) : IArgumentParsingService
    {
        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, CommandMetadata command, Profile? profile)
        {
            IDictionary<string, ParameterMetadata> parameters = command.Parameters;
            IDictionary<string, string>? parameterAliases = command.ParameterAliases;
            IDictionary<string, ParameterSetMetadata>? parameterSets = command.ParameterSets;
            IDictionary<string, (string, object)> parsedArgs = provider.ParseArguments(args, parameters, parameterAliases);
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

            // Yabo: the applying default need to work with future parameter sets.
            // when a parameter set doesn't have the key, the default value should not be applied
            profileService.GetProfileKeysForDefault().ForEach(key =>
            {
                (string invocationName, object? value) = parsedArgs[key];
                // Apply default property when not in args and in processor
                if ((!parsedArgs.ContainsKey(key) || value is null) && processor.GetType().GetProperty(key) is {})
                {
                    // TODO(Yabo): To throw proper exceptions, provider need to return flavored argument names
                    // along with parsed args
                    if (profileService.GetProfileValueByKey(profile, key) is not {} profileValue)
                    {
                        throw new ArgumentException($"Missing required argument '{invocationName}' and no value found in profile.");
                    }
                    parsedArgs[key] = (invocationName, profileValue);
                }
            });

            parsedArgs.ToList().ForEach(p => SetProperty(processor, p.Key, p.Value));
            return processor;
        }

        private IList<string>? ValidateParameterSets(IDictionary<string, (string, object)> parsedArgs, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, ParameterSetMetadata>? parameterSets)
        {
            if (parameterSets is null)
            {
                return null;
            }

            List<string> errors = new List<string>();
            HashSet<string> candidates = new HashSet<string>();
            parsedArgs.Select(kvp => parameters[kvp.Key]).ToList().ForEach(parameter =>
            {
                if (parameter.ParameterSets is null)
                {
                    return;
                }
                if (!HasValidParameterSet(parameter.ParameterSets.Keys.ToHashSet(), candidates))
                {
                    foreach (string candidate in candidates)
                    {
                        string[] members = parameterSets[candidate].Parameters!.Keys.Select(k =>
                        {
                            var (invocationName, value) = parsedArgs[k];
                            return invocationName;
                        }).ToArray();
                        errors.Add($"Parameter: {parameter.InvocationName} cannot be used with candidate: [{string.Join(", ", members)}]");
                    }
                }

            });
            if (errors.Count > 0)
            {
                return errors;
            }

            HashSet<ParameterMetadata> input = parsedArgs.Keys.Select(k => parameters[k]).ToHashSet();
            candidates.Select(name => parameterSets[name]).ToHashSet().RemoveWhere(candidate =>
            {
                if (IsValidCandidate(input, candidate))
                {
                    return false;
                }
                string[] members = candidate.Parameters!.Keys.Select(k =>
                {
                    var (invocationName, value) = parsedArgs[k];
                    return invocationName;
                }).ToArray();
                errors.Add($"Input: [{string.Join(", ", input.Select(p => p.InvocationName))}] is not valid for candidate: [{string.Join(", ", members)}]");
                return true;
            });
            return candidates.Count == 1 ? null : errors;
        }

        private bool HasValidParameterSet(HashSet<string> parameterSetNames, HashSet<string> candidates)
        {
            // parameter sets of first accepted parameter are all added in candidates
            if (candidates.Count == 0)
            {
                parameterSetNames.ToList().ForEach(name => candidates.Add(name));
                return true;
            }

            int intersected = 0;
            parameterSetNames.ToList().ForEach(name =>
            {
                if (candidates.Contains(name))
                {
                    intersected += 1;
                }
                else
                {
                    parameterSetNames.Remove(name);
                }
            });
            
            // intersected == 0 means current parameter does not have a shared parameter set with candidate parameters
            if (intersected == 0)
            {
                return false;
            }

            // Update candidates to only include intersected parameter sets
            // So that the candidates are guaranteed to support all parameters accepted by far
            candidates.IntersectWith(parameterSetNames);
            return true;
        }

        private bool IsValidCandidate(HashSet<ParameterMetadata> parameters, ParameterSetMetadata candidate)
        {
            // filter 1. validate whether args have all required parameters for candidate
            // filter 2. validate whether args have any extra parameters not defined in the candidate
            // for example: ps1->[subid, rg, name]; ps2->[subid, rg]; input {subid, rg, name} survives filter 1, will be filtered in filter 2
            // filter 3. validate whether there is only one candidate left
            foreach (ParameterReference pr in candidate.Parameters!.Values)
            {
                if (pr.IsMandatory && !parameters.Any(a => a.Name == pr.Name))
                {
                    return false;
                }
            }

            foreach (var parameter in parameters)
            {
                if (!candidate.Parameters!.ContainsKey(parameter.Name))
                {
                    return false;
                }
            }
            
            return true;
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