// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

public interface ITelemetryPropertyFilter
{
    /// <summary>
    /// Returns <c>true</c> when a tag with the given <paramref name="key"/> is
    /// safe to emit, <c>false</c> when it must be dropped before export. Null
    /// or empty keys must return <c>false</c>.
    /// </summary>
    bool IsAllowed(string? key);
}
