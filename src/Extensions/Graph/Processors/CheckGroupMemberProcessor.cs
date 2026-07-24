// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    /// <summary>
    /// Checks whether a directory object (user, group, service principal, or device) is a
    /// transitive member of a specific group. Uses
    /// <c>POST /directoryObjects/{memberId}/checkMemberGroups</c> with body
    /// <c>{"groupIds":["&lt;groupId&gt;"]}</c>. Graph returns the subset of the queried group
    /// IDs the member is in; the processor projects that to <c>{"value": true|false}</c>.
    /// </summary>
    public class CheckGroupMemberProcessor : GraphProcessorBase<CheckMemberGroupsResponse>
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

        private string? _resolvedGroupId;

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(MemberId))
            {
                throw new InvalidOperationException("MemberId is required to check group membership.");
            }

            _resolvedGroupId = await ResolveGroupIdentifierAsync(Group);

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = $"{GraphBaseUri}/directoryObjects/{Uri.EscapeDataString(MemberId)}/checkMemberGroups";

            string body = JsonSerializer.Serialize(new { groupIds = new[] { _resolvedGroupId } }, JsonOptions);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(CheckMemberGroupsResponse body) =>
            body.Value is not null
                && body.Value.Any(id => string.Equals(id, _resolvedGroupId, StringComparison.OrdinalIgnoreCase));
    }
}
