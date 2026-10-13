// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="CheckGroupMemberProcessor"/>.
    /// Accepts a group object ID or a display-name prefix and resolves the prefix to a single
    /// object ID before delegating to the atomic. The atomic itself only accepts an object ID.
    /// </summary>
    public class CheckGroupMemberProcessor_Orchestrator : GraphProcessorBase<CheckMemberGroupsRequest, CheckMemberGroupsResponse>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"The group's object ID or display name (prefix works if there is a unique match).",
            SerializedName = @"Group")]
        public string Group { get; set; } = "";

        [ArgumentInfo(
            Required = true,
            Description = @"The object ID of the member to test.",
            SerializedName = @"MemberId")]
        public string MemberId { get; set; } = "";

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            if (string.IsNullOrWhiteSpace(Group))
            {
                throw AzclipsErrorException.InvalidInput("Group is required.");
            }
            if (string.IsNullOrWhiteSpace(MemberId))
            {
                throw AzclipsErrorException.InvalidInput("MemberId is required to check group membership.");
            }

            string groupId = await ResolveGroupAsync(Group);

            CheckGroupMemberProcessor atomic = CreateExtendedProcessor<CheckGroupMemberProcessor>(p =>
            {
                p.GroupId = groupId;
                p.MemberId = MemberId;
            });
            return await atomic.SendAsync();
        }

        private async Task<string> ResolveGroupAsync(string identifier)
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
    }
}