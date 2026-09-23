// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class CancelAtManagementGroupScopeDeploymentsProcessor
    : ResourcesProcessorBase<object, object>
{
    private const string ApiVersion = "2025-04-01";

    public string ManagementGroupId { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"{ManagementGroupDeploymentUri.Resource(ManagementGroupId, DeploymentName)}/cancel?api-version={ApiVersion}";

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);
}
