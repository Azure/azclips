// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    public class GetApplicationProcessor : GraphProcessorBase<Application>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of application",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Id is required to get an application.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://graph.microsoft.com/v1.0/applications/{Uri.EscapeDataString(Id)}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
