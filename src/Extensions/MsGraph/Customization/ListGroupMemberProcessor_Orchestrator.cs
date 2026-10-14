// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator over the atomic <see cref="ListGroupMemberProcessor"/>. Accepts a
    /// group object ID, display-name prefix, or exact display name via <see cref="GroupDisplayName"/>,
    /// resolves it to a single object ID, then lists that group's members. The atomic itself only
    /// accepts an object ID.
    /// </summary>
    /// <remarks>
    /// Extends the atomic so the inherited <c>ProcessAsync</c> owns the whole lifecycle: Graph auth
    /// installs in <c>BeforeProcessAsync</c>, then resolution runs inside the overridden
    /// <see cref="SendAsync"/>, which the base wraps between the ProcessorProcessAsync start and end
    /// events. A resolution failure therefore still emits the paired end event, and the envelope
    /// unwrap plus @odata.nextLink pagination (inherited from the atomic) run unchanged.
    /// </remarks>
    public class ListGroupMemberProcessor_Orchestrator : ListGroupMemberProcessor
    {
        [ArgumentInfo(
            Required = false,
            Description = @"The group's object ID or display name (prefix works if there is a unique match).",
            SerializedName = @"Group")]
        public string? Group { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"The exact display name of the target group.",
            SerializedName = @"GroupDisplayName")]
        public string? GroupDisplayName { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            // Resolve inside the transport step so it runs within the base ProcessAsync start/end
            // lifecycle. Graph auth is already installed by the inherited BeforeProcessAsync, so the
            // resolution requests below acquire a Graph audience token.
            GroupId = await ResolveGroupAsync();
            return await base.SendAsync();
        }

        private async Task<string> ResolveGroupAsync()
        {
            if (!string.IsNullOrWhiteSpace(GroupDisplayName))
            {
                return await ResolveByExactDisplayNameAsync(GroupDisplayName);
            }
            if (!string.IsNullOrWhiteSpace(Group))
            {
                return await ResolveByIdentifierAsync(Group);
            }
            throw AzclipsErrorException.InvalidInput("A group identifier is required. Provide Group or GroupDisplayName.");
        }

        private async Task<string> ResolveByIdentifierAsync(string identifier)
        {
            if (Guid.TryParse(identifier, out _))
            {
                return identifier;
            }

            ListGroupProcessor list = CreateExtendedProcessor<ListGroupProcessor>(p =>
            {
                p.Filter = $"startswith(displayName,'{SelectClauseHelpers.EscapeODataString(identifier)}')";
                p.Select = ["id", "displayName"];
            });
            return await GroupResolver.PickSingleGroupIdAsync(list, JsonOptions, identifier);
        }

        private async Task<string> ResolveByExactDisplayNameAsync(string displayName)
        {
            ListGroupProcessor list = CreateExtendedProcessor<ListGroupProcessor>(p =>
            {
                p.Filter = $"displayName eq '{SelectClauseHelpers.EscapeODataString(displayName)}'";
                p.Select = ["id", "displayName"];
            });
            return await GroupResolver.PickSingleGroupIdAsync(list, JsonOptions, displayName);
        }
    }
}
