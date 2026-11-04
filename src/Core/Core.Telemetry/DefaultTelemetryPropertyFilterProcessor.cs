// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

public sealed class DefaultTelemetryPropertyFilterProcessor : BaseProcessor<Activity>
{
    private readonly ITelemetryPropertyFilter _filter;
    private long _droppedCount;

    public DefaultTelemetryPropertyFilterProcessor(ITelemetryPropertyFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    /// <summary>
    /// Total number of tags dropped across all processed activities. Exposed for
    /// tests and diagnostics; not surfaced anywhere user-visible because logging
    /// dropped keys could itself leak the existence of a sensitive tag.
    /// </summary>
    internal long DroppedCount => Interlocked.Read(ref _droppedCount);

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        // Status descriptions are free-form error text and can contain URLs, hostnames, or
        // other sensitive values. Preserve the structured status code only.
        if (data.StatusDescription is not null)
        {
            data.SetStatus(data.Status);
        }

        // Snapshot keys first — SetTag mutates the underlying collection so we
        // can't enumerate TagObjects and remove at the same time.
        List<string>? toRemove = null;
        foreach (var tag in data.TagObjects)
        {
            if (!_filter.IsAllowed(tag.Key))
            {
                (toRemove ??= new List<string>()).Add(tag.Key);
            }
        }

        if (toRemove is null) return;

        foreach (var key in toRemove)
        {
            // Activity treats SetTag(key, null) as a delete — see
            // System.Diagnostics.Activity.SetTag in dotnet/runtime.
            data.SetTag(key, null);
        }

        Interlocked.Add(ref _droppedCount, toRemove.Count);
    }
}
