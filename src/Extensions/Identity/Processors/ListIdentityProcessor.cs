// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class ListIdentityProcessor : IdentityProcessorBase<UserAssignedIdentityListResult>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string? ResourceGroup { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Total number of items to return in the command's output.",
        SerializedName = @"MaxItems")]
        public int MaxItems { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Token to specify where to start paginating.",
        SerializedName = @"NextToken")]
        public string? NextToken { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = BuildIdentityListUri(ResourceGroup, NextToken);

            HttpRequestMessage request = new(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(UserAssignedIdentityListResult body)
        {
            var values = body.Value ?? [];
            return MaxItems > 0 ? values.Take(MaxItems).ToArray() : [.. values];
        }
    }
}
