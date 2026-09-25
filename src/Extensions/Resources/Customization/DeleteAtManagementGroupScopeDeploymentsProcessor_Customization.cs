// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class DeleteAtManagementGroupScopeDeploymentsProcessor_Customization
    : DeleteAtManagementGroupScopeDeploymentsProcessor
{
    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);
}
