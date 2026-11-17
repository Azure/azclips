// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public sealed class FailureHistoryException : Exception
{
    public string Code { get; }

    public FailureHistoryException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
