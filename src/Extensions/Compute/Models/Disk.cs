// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

/// <summary>
/// Minimal managed-disk resource representation. The Compute extension contributes leaves
/// under both <c>vm</c> and <c>disk</c> verbs to exercise the multi-namespace dispatch model.
/// </summary>
public class Disk
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
