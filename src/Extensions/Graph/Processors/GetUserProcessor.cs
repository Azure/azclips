// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    public class GetUserProcessor : GraphProcessorBase<User>
    {
        [ArgumentInfo(
        Required = true,
        Description = @"The object ID or user principal name (UPN) of the user.",
        SerializedName = @"Id")]
        public string Id { get; set; } = "";

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Id is required to get a user.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"{GraphBaseUri}/users/{Uri.EscapeDataString(Id)}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
