// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface ICommandMetadata
    {
        string Name { get; }
        string FullName { get; }
        string AssemblyName { get; }
        Type CommandType { get; }
    }
}
