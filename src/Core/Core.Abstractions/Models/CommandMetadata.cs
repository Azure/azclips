// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Describes a single command that an extension exposes, including how it is invoked and which
    /// processor and parameters back it. Instances are declared in an extension's <c>ExtensionIndex</c>
    /// and consumed by the engine to discover, bind, and execute commands, and by the CLI to render
    /// help and completions.
    /// </summary>
    /// <param name="Name">
    /// The internal, human-readable name of the command, typically the processor's type name. Used for
    /// diagnostics and to correlate the command with its backing processor rather than for invocation.
    /// Example: <c>"NewResourceGroupProcessor"</c>.
    /// </param>
    /// <param name="InvocationName">
    /// The client-facing invocation token that end users type to run the command, and the unique key under
    /// which the command is registered in the discovered command table. Commands are keyed by this value
    /// (for example, the discovery session emits a <c>Dictionary&lt;InvocationName, CommandMetadata&gt;</c>),
    /// so it must be unique within an extension.
    /// Kept as an explicit field so an <c>ExtensionIndex</c> can be serialized without relying on the CLR type.
    /// Example: <c>"New-AzResourceGroup"</c> (PowerShell verb-noun) or <c>"Get-AzResourceGroup_List"</c>.
    /// </param>
    /// <param name="Aliases">
    /// Alternate invocation names that also resolve to this command. Use an empty list when there are
    /// no aliases. Example: <c>["new-rg"]</c>.
    /// </param>
    /// <param name="FullName">
    /// The fully-qualified type name of the processor that implements the command, including its namespace.
    /// Example: <c>"Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor"</c>.
    /// </param>
    /// <param name="AssemblyName">
    /// The name of the assembly that contains the processor. Used to locate and load the command at runtime.
    /// Example: <c>"Microsoft.Azure.Azclips.Extensions.Resources"</c>.
    /// </param>
    /// <param name="ProcessorType">
    /// The CLR <see cref="System.Type"/> of the processor that executes the command. Used to instantiate the
    /// processor. Example: <c>typeof(Processors.NewResourceGroupProcessor)</c>.
    /// </param>
    /// <param name="Parameters">
    /// The command's parameters keyed by their canonical parameter name. Each entry carries the parameter's
    /// type, description, whether it is required, and allowed values.
    /// Example: <c>["Name"] = new("Name", "Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: [])</c>.
    /// </param>
    /// <param name="ParameterAliases">
    /// A map from an alias to the canonical parameter name it resolves to, used during argument parsing so
    /// that alternate spellings bind to the correct parameter. Use an empty dictionary when there are none.
    /// Example: <c>["-g"] = "ResourceGroupName"</c>.
    /// </param>
    /// <param name="ParameterSets">
    /// The command's parameter sets keyed by their canonical parameter set name. Each entry carries the parameter set's
    /// name, whether it is the default set, and the parameters it contains.
    /// Example: <c>["Default"] = new("Default", IsDefault: true, Parameters: ["Name" = new("Name", IsMandatory: true, Position: 0)])</c>.
    /// </param>
    public record CommandMetadata(
        string Name,
        string InvocationName,
        IList<string> Aliases,
        string FullName,
        string AssemblyName,
        Type ProcessorType,
        IDictionary<string, ParameterMetadata> Parameters,
        IDictionary<string, string> ParameterAliases,
        IDictionary<string, ParameterSetReference>? ParameterSets = null);

    public record ParameterSetMetadata(
        string Name,
        bool IsDefault = false,
        IDictionary<string, ParameterReference>? Parameters = null);

    public record ParameterReference(
        string Name,
        bool IsMandatory = false,
        int Position = -1
    );
}
