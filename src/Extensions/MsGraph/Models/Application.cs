// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

public class Application
{
    public string? Id { get; set; }
    public string? AppId { get; set; }
    public string? DisplayName { get; set; }
    public string? SignInAudience { get; set; }
    public string? Description { get; set; }
    public string[]? IdentifierUris { get; set; }
    public DateTimeOffset? CreatedDateTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
