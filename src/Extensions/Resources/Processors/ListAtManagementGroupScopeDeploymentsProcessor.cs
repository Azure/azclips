// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ListAtManagementGroupScopeDeploymentsProcessor
    : ResourcesProcessorBase<object, DeploymentListResult>
{
    private const string ApiVersion = "2025-04-01";

    public string ManagementGroupId { get; set; } = string.Empty;

    public string? Filter { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        string uri = $"{ManagementGroupDeploymentUri.Collection(ManagementGroupId)}?api-version={ApiVersion}";
        return string.IsNullOrWhiteSpace(Filter)
            ? uri
            : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(
            page => page.Value,
            page => page.NextLink,
            CreateRequestMessage,
            WriteOutput,
            Publisher,
            HttpPipeline,
            JsonOptions);
        return true;
    }
}
