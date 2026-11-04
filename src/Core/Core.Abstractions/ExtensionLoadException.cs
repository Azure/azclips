// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>An expected failure to locate an extension or discover its command index.</summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/>, which loaders threw before, so existing
/// catch blocks still handle it. It lets the Agent help catalog tell a load failure apart from other errors.
/// </remarks>
public sealed class ExtensionLoadException : InvalidOperationException
{
    public ExtensionLoadException(string message) : base(message) { }

    public ExtensionLoadException(string message, Exception? innerException) : base(message, innerException) { }
}
