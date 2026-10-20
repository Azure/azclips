// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>An expected failure to locate an extension or discover its command index.</summary>
public sealed class ExtensionLoadException : InvalidOperationException
{
    public ExtensionLoadException(string message) : base(message) { }

    public ExtensionLoadException(string message, Exception? innerException) : base(message, innerException) { }
}
