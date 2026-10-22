// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>Optional processor hook applied by the runtime before host output handling.</summary>
public interface IOutputProjection
{
    /// <summary>Returns the value to emit, or null to suppress output. The runtime applies this once.</summary>
    object? GetOutputValue(object value);
}
