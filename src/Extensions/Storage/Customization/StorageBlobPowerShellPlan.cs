// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

/// <summary>A locally validated, frozen operation carried across an invocation-thread confirmation.</summary>
public sealed class StorageBlobPowerShellPlan
{
    public string Target { get; }
    public string Action { get; }
    internal StorageBlobContainer Container { get; }
    internal string Name { get; }
    internal string Kind { get; }
    internal bool Force { get; }
    internal string? ETag { get; }
    internal string? CreateDirectory { get; }
    internal Action<IBlobTargetOptions> Configure { get; }

    internal StorageBlobPowerShellPlan(StorageBlobContainer container, string name, string target, string action,
        string kind, bool force, Action<IBlobTargetOptions> configure, string? etag = null, string? createDirectory = null)
    {
        Container = container; Name = name; Target = target; Action = action;
        Kind = kind; Force = force; Configure = configure; ETag = etag; CreateDirectory = createDirectory;
    }

    public StorageBlobPowerShellPlan ApproveOverwrite() =>
        new(Container, Name, Target, Action, Kind, true, Configure, ETag, CreateDirectory);
}
