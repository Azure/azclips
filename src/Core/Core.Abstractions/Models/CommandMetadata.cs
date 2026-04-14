// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    public record CommandMetadata(
        string Name,
        IList<string> Aliases,
        string FullName,
        string AssemblyName,
        Type CommandType,
        IDictionary<string, ParameterMetadata> Parameters,
        IDictionary<string, string> ParameterAliases);
}
