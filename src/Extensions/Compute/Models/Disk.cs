// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Azure.Cirrus.Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

/// <summary>
/// Minimal managed-disk resource representation. Phase 9 multi-namespace stub: the Compute
/// extension contributes leaves under both <c>vm</c> and <c>disk</c> verbs to prove the
/// multi-namespace dispatch model end-to-end.
/// </summary>
public class Disk
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
