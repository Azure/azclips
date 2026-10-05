// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.ArgumentParsing;

public class DefaultArgumentParsingProvider() : IArgumentParsingProvider
{
    public string GetParameterDisplayName(ParameterMetadata parameter) => parameter.Name;
    public IDictionary<string, object> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases) => args;
    public IList<string>? ValidateArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters) => null;
}