// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class StopManagementGroupDeploymentProcessor_Orchestrator
    : ResourcesProcessorBase<object, object>
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        CancelAtManagementGroupScopeDeploymentsProcessor cancel =
            CreateExtendedProcessor<CancelAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
            });
        return await cancel.SendAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);
}
