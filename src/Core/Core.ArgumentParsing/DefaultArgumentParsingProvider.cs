// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing;

public class DefaultArgumentParsingProvider() : IArgumentParsingProvider
{
    public IDictionary<string, (string, object)> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases) => args;
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