// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    // Yabo: Should these interfaces be in SharedContract or merge SharedContract to Core.Abstraction?
    public interface ICommandInvocation
    {
        string ExtensionName { get; }
        string CommandName { get; }
        IDictionary<string, object> Parameters { get; }
        ClientType ClientType { get; }
    }
}
