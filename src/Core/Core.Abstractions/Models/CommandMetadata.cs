// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    public record CommandMetadata(
        string Name,
        IList<string> Aliases,
        string FullName,
        string AssemblyName,
        Type ProcessorType,
        IDictionary<string, ParameterMetadata> Parameters,
        IDictionary<string, string> ParameterAliases,
        string Summary = "",
        string Description = "",
        IList<CommandExample>? Examples = null);

    public record CommandExample(string Title, string Command);
}
