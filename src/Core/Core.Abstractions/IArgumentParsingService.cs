// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Service for parsing and binding arguments to processor instances.
    /// Handles CLI argument parsing, type conversion, validation, and property binding
    /// based on provided <see cref="ParameterMetadata"/> definitions and aliases.
    /// </summary>
    public interface IArgumentParsingService
    {
        /// <summary>
        /// Parses and binds arguments to a processor instance.
        /// Validates required parameters, converts types, and sets properties.
        /// </summary>
        /// <param name="processor">The processor instance to bind arguments to.</param>
        /// <param name="parameters">Dictionary of argument key-value pairs.</param>
        /// <param name="parameterAliases">Dictionary of parameter aliases.</param>
        /// <param name="profile">The profile to use for default values.</param>
        /// <returns>The processor instance with bound arguments.</returns>
        /// <exception cref="ArgumentException">Thrown when argument parsing or binding fails.</exception>
        IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases, Profile? profile);
    }
}
