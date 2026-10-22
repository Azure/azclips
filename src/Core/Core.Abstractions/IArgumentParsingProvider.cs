// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IArgumentParsingProvider
{
    /// <summary>
    /// Parses invocations to extension parameters
    /// </summary>
    /// <param name="args">Dictionary of invocation names to their corresponding values.</param>
    /// <param name="parameters">Dictionary of Invocation names to their corresponding parameter metadata.</param>
    /// <param name="parameterAliases">Optional dictionary of parameter aliases.</param>
    /// <returns>Dictionary of parsed arguments: <canonical key, (invocation name, parsed value)></returns>
    IDictionary<string, (string, object)> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases);
    /// <summary>
    /// Validates that all required arguments are present and all provided arguments are valid.
    /// </summary>
    /// <param name="args">Dictionary of argument key-value pairs to validate.</param>
    /// <param name="parameters">Metadata for command parameters.</param>
    /// <param name="errors">Output list of validation error messages.</param>
    /// <returns>True if all arguments are valid, false otherwise.</returns>
    bool ValidateArguments(IDictionary<string, (string, object)> args, IDictionary<string, ParameterMetadata> parameters, out IList<string>? errors);
    /// <summary>
    /// Validates that the provided arguments conform to the defined parameter sets.
    /// </summary>
    /// <param name="parsedArgs">Dictionary of parsed arguments: <canonical key, (invocation name, parsed value)></param>
    /// <param name="parameters">Dictionary of canonical names to their corresponding parameter metadata.</param>
    /// <param name="parameterSets">Dictionary of parameter set metadata.</param>
    /// <param name="defaultResolve">Optional function to provide a default resolution for the parameter set.</param>
    /// <param name="errors">Output list of validation error messages.</param>
    /// <returns>Resolved parameter set name. Null if not resolved</returns>
    string? ResolveParameterSet(IDictionary<string, (string, object)> parsedArgs, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, ParameterSetMetadata>? parameterSets, Func<IDictionary<string, (string, object)>, IDictionary<string, ParameterMetadata>, IDictionary<string, ParameterSetMetadata>?, (string?, IList<string>?)> defaultResolve, out IList<string>? errors);
}