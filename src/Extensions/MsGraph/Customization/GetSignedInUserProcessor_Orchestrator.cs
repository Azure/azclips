// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="GetSignedInUserProcessor"/>. Adds
    /// the <c>AppendSelected</c> switch which merges the caller's <c>Select</c> with a default
    /// user property set.
    /// </summary>
    public class GetSignedInUserProcessor_Orchestrator : GraphProcessorBase<object, User>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return. Combine with AppendSelected to include the default properties as well.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response, for example memberOf.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set with Select, include the default property set alongside the values in Select rather than replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            string[]? mergedSelect = SelectClauseHelpers.MergeUserSelect(Select, AppendSelected);
            GetSignedInUserProcessor atomic = CreateExtendedProcessor<GetSignedInUserProcessor>(p =>
            {
                p.Select = mergedSelect;
                p.Expand = Expand;
            });
            return await atomic.SendAsync();
        }
    }
}
