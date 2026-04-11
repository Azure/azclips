// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Service for parsing and binding arguments to command instances.
    /// Handles CLI argument parsing, type conversion, validation, and property binding
    /// based on ArgumentInfoAttribute metadata.
    /// </summary>
    public interface IArgumentParsingService
    {
        /// <summary>
        /// Parses and binds arguments to a command instance.
        /// Validates required parameters, converts types, and sets properties.
        /// </summary>
        /// <param name="command">The command instance to bind arguments to.</param>
        /// <param name="parameters">Dictionary of argument key-value pairs.</param>
        /// <returns>The command instance with bound arguments.</returns>
        /// <exception cref="ArgumentException">Thrown when argument parsing or binding fails.</exception>
        IProcessor BindArgumentsToCommand(IProcessor command, IDictionary<string, object> parameters);

        /// <summary>
        /// Parses raw CLI argument strings into a structured format.
        /// </summary>
        /// <param name="args">Raw command-line arguments.</param>
        /// <param name="parameterMetadata">Metadata for command parameters.</param>
        /// <param name="parameterAliases">Aliases for command parameters.</param>
        /// <returns>Dictionary of parsed argument key-value pairs.</returns>
        IDictionary<string, object> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases);

        /// <summary>
        /// Validates that all required arguments are present and all provided arguments are valid.
        /// </summary>
        /// <param name="args">Dictionary of argument key-value pairs to validate.</param>
        /// <param name="parameters">Metadata for command parameters.</param>
        /// <returns>A list of validation error messages. Empty if all arguments are valid.</returns>
        IList<string> ValidateArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters);
    }
}
