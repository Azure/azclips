// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>POST /directoryObjects/{memberId}/checkMemberGroups</c>. Takes
    /// only what the Microsoft Graph endpoint accepts: the member's object ID and the group
    /// object ID to test against. Graph returns the subset of the queried group IDs that the
    /// member is a transitive member of; the processor projects that to a single boolean.
    /// Convenience features (display-name resolution for the group) live in
    /// <c>Orchestrators/CheckGroupMemberProcessor_Orchestrator</c>.
    /// </summary>
    /// <remarks>
    /// Hand-authored atomic: the <c>/directoryObjects</c> path is not defined in the
    /// azure-powershell v1.0 OpenAPI spec cut this extension targets for codegen. Once codegen
    /// consumes the upstream <c>msgraph-metadata</c> spec (which does document this action) or
    /// the azure-powershell spec is extended, this class becomes a codegen candidate and the
    /// <c>SelectOutput</c> boolean reduction below moves to the orchestrator.
    /// </remarks>
    public class CheckGroupMemberProcessor : GraphProcessorBase<CheckMemberGroupsResponse>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"The object ID of the group to test against.",
            SerializedName = @"GroupId")]
        public string GroupId { get; set; } = "";

        [ArgumentInfo(
            Required = true,
            Description = @"The object ID of the member to test.",
            SerializedName = @"MemberId")]
        public string MemberId { get; set; } = "";

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(GroupId))
            {
                throw new InvalidOperationException("GroupId is required to check group membership.");
            }
            if (string.IsNullOrWhiteSpace(MemberId))
            {
                throw new InvalidOperationException("MemberId is required to check group membership.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = $"https://graph.microsoft.com/v1.0/directoryObjects/{Uri.EscapeDataString(MemberId)}/checkMemberGroups";
            string body = JsonSerializer.Serialize(new { groupIds = new[] { GroupId } }, JsonOptions);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(CheckMemberGroupsResponse body) =>
            body.Value is not null
                && body.Value.Any(id => string.Equals(id, GroupId, StringComparison.OrdinalIgnoreCase));
    }
}
