// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

public interface ITelemetryActivityNameFilter
{
    /// <summary>
    /// Returns <c>true</c> when an activity with the given
    /// <paramref name="activityName"/> is safe to emit, <c>false</c> when the
    /// whole activity must be dropped before export. Null or empty names must
    /// return <c>false</c>.
    /// </summary>
    bool IsAllowed(string? activityName);
}
