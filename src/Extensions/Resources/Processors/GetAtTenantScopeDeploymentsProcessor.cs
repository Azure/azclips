// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class GetAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<object, DeploymentExtended>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}?api-version={ApiVersion}";
}
