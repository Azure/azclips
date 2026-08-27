// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="GetGroupProcessor"/>. Adds:
    /// <list type="bullet">
    ///   <item>display-name / display-name-prefix resolution to an object ID,</item>
    ///   <item>the <c>AppendSelected</c> switch that merges the caller's <c>Select</c>
    ///         with the default group property set.</item>
    /// </list>
    /// The atomic <see cref="GetGroupProcessor"/> stays a pure projection of
    /// <c>GET /groups/{id}</c> and knows nothing about resolution or default merging.
    /// </summary>
    public class GetGroupProcessor_Orchestrator : GraphProcessorBase<Group>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"The group's object ID or display name (prefix works if there is a unique match).",
            SerializedName = @"Group")]
        public string Group { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return. Combine with AppendSelected to include the default properties as well.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response, for example members.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set with Select, include the default property set alongside the values in Select rather than replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            EnsureRuntimeContext();
            if (string.IsNullOrWhiteSpace(Group))
            {
                throw new InvalidOperationException("Group is required.");
            }

            string groupId = await ResolveGroupAsync(Group);
            string[]? mergedSelect = SelectClauseHelpers.MergeGroupSelect(Select, AppendSelected);

            GetGroupProcessor atomic = CreateExtendedProcessor<GetGroupProcessor>(p =>
            {
                p.Id = groupId;
                p.Select = mergedSelect;
                p.Expand = Expand;
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
