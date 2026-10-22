// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

/// <summary>Thrown when a parameter-set definition is itself invalid (for example, two sets that are
/// not mutually exclusive, or a set with no mandatory parameter to distinguish it).</summary>
internal sealed class InvalidParameterSetDefinitionException : Exception
{
    public InvalidParameterSetDefinitionException(string message)
        : base(message)
    {
    }

    public InvalidParameterSetDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when the supplied parameters cannot be resolved to exactly one parameter set.</summary>
internal sealed class ParameterSetResolutionException : Exception
{
    public ParameterSetResolutionException(string message)
        : base(message)
    {
    }

    public ParameterSetResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
