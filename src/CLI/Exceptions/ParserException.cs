// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Exceptions;

public class ParserException : Exception
{
    public ParserException(string message)
        : base(message)
    {
    }

    public ParserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}