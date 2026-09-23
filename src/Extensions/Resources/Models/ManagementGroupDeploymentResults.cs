// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models;

public class DeploymentListResult
{
    public IList<DeploymentExtended> Value { get; set; } = [];

    public string? NextLink { get; set; }
}

public class DeploymentExportResult
{
    public object? Template { get; set; }
}

public class DeploymentValidateResult
{
    public ErrorResponse? Error { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Type { get; set; }

    public DeploymentPropertiesExtended? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
