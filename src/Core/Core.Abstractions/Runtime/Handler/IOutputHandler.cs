// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler
{
    /// <summary>
    /// Marker interface for output handlers.
    /// </summary>
    public interface IOutputHandler
    {
        OutputStreamDelegate WriteOutput { get; set; }
    }
}