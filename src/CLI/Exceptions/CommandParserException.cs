// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Exceptions;

public class CommandParserException : ParserException
{
    public CommandParserException(string message)
        : base(message)
    {
    }

    public CommandParserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}