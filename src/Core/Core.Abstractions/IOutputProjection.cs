// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>Lets a host select a command result's output view before querying or rendering.</summary>
public interface IOutputProjection
{
    /// <summary>Returns the value to emit, or null to suppress output. Hosts apply this once.</summary>
    object? GetOutputValue();
}
