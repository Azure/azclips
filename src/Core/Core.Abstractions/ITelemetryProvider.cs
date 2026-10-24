// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Core.Abstractions;

public interface ITelemetryProvider
{
    public ActivitySource ActivitySource { get; }
    public TracerProvider TracerProvider { get; }
    public IReadOnlyCollection<KeyValuePair<string, object?>> ActivityTags => [];
}
