// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IArgumentParsingProvider
{
    IDictionary<string, (string, object)> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases);
    /// <summary>
    /// Validates that all required arguments are present and all provided arguments are valid.
    /// </summary>
    /// <param name="args">Dictionary of argument key-value pairs to validate.</param>
    /// <param name="parameters">Metadata for command parameters.</param>
    /// <returns>A list of validation error messages. Empty if all arguments are valid.</returns>
    IList<string>? ValidateArguments(IDictionary<string, (string, object)> args, IDictionary<string, ParameterMetadata> parameters);
}