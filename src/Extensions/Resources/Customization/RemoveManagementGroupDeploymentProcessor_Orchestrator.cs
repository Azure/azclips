// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Customization;

public class RemoveManagementGroupDeploymentProcessor_Orchestrator
    : DeleteAtManagementGroupScopeDeploymentsProcessor
{
    public bool NoWait { get; set; }

    internal override Task<HttpResponseMessage> SendAsync() =>
        NoWait
            ? HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline)
            : base.SendAsync();
}
