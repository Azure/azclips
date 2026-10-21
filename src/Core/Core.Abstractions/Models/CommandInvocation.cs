// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Represents a command invocation with extension name, command name, and parameters.
    /// </summary>
    /// <param name="ExtensionName">The name of the extension to invoke.</param>
    /// <param name="CommandName">The name of the command to execute.</param>
    /// <param name="Parameters">Dictionary of argument key-value pairs.</param>
    /// <param name="ModuleName">Resolved module name. Initialized by PowerShell and may also be populated for CLI invocations.</param>
    /// <param name="ModuleVersion">Resolved module version. Initialized by PowerShell and may also be populated for CLI invocations when resolved by the engine.</param>
    /// <param name="IsTransient">
    /// Indicates whether the hosting process exits after this invocation (true)
    /// or persists across multiple invocations (false).
    /// Transient: CLI single-command mode.
    /// Persistent: PowerShell, CLI interactive mode.
    /// Used to optimize resource management (e.g., flush telemetry eagerly in transient mode, reuse caches in persistent mode).
    /// </param>
    public record CommandInvocation(
        string ExtensionName,
        string CommandName,
        IDictionary<string, object> Parameters,
        bool IsTransient = false,
        string? ModuleName = null,
        string? ModuleVersion = null)
    {
        /// <summary>The installation selected by routing, or null for legacy name/version loads.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ExtensionInstallation? Installation { get; init; }
    }
}