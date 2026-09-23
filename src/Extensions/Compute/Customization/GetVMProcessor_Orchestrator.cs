// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared read orchestrator behind <c>az vm show</c>, <c>Get-AzVM_Get</c> and
    /// <c>Get-AzVM_Id</c>. It owns the client-facing switches and reduces them to a single ARM
    /// <c>$expand</c>; <c>ExtensionIndex</c> decides which of them each client exposes.
    /// </summary>
    public class GetVMProcessor_Orchestrator : GetVMProcessor
    {
        public bool IncludeUserData { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = ResolveExpand();
            await base.ProcessAsync();
        }

        /// <summary>The one place the switches turn into a request. Mirrors legacy `az vm show`.</summary>
        private string? ResolveExpand() => IncludeUserData ? "userData" : null;
    }
}
