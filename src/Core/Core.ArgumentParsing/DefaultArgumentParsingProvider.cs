// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing;

public class DefaultArgumentParsingProvider() : IArgumentParsingProvider
{
    public IDictionary<string, (string, object)> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases) 
    {
        var parsedArgs = new Dictionary<string, (string, object)>();
        foreach (var kvp in args)
        {
            var key = kvp.Key;
            if (parameterAliases != null && parameterAliases.TryGetValue(key, out var alias))
            {
                key = alias;
            }
            parsedArgs[key] = (key, kvp.Value);
        }
        return args.Select(kvp => (parameters[kvp.Key].Name, (kvp.Key, kvp.Value))).ToDictionary(kvp => kvp.Item1, kvp => kvp.Item2);
    }
    public bool ValidateArguments(IDictionary<string, (string, object)> args, IDictionary<string, ParameterMetadata> parameters, out IList<string>? errors)
    {
        errors = null;
        return true;
    }
    public string? ResolveParameterSet(IDictionary<string, (string, object)> parsedArgs, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, ParameterSetMetadata>? parameterSets, Func<IDictionary<string, (string, object)>, IDictionary<string, ParameterMetadata>, IDictionary<string, ParameterSetMetadata>?, (string?, IList<string>?)> defaultResolve, out IList<string>? errors)
    {
        var (resolved, errorList) = defaultResolve(parsedArgs, parameters, parameterSets);
        errors = errorList;
        return resolved;
    }
}