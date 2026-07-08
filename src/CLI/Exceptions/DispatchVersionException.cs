// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Version;

namespace Core.Cli.Exceptions;

/// <summary>
/// Control-flow signal raised by <see cref="Parser.ExtensionDispatcher"/> when the user asks for
/// version information (<c>az version</c> or <c>az --version</c>). Mirrors
/// <see cref="DispatchHelpException"/>: version is handled by the CLI host on the cold-path — no
/// engine session, no extension load — so the parser short-circuits and <c>Program</c> renders via
/// <see cref="RuntimeVersionDispatcher"/>.
/// </summary>
public sealed class DispatchVersionException : ParserException
{
    internal DispatchVersionException(VersionOutputFormat format)
        : base("Dispatch version requested.")
    {
        Format = format;
    }

    internal VersionOutputFormat Format { get; }
}
