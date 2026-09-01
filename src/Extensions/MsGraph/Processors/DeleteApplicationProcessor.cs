// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Deletes an application registration. Microsoft Graph returns <c>204 No Content</c>, so there is
    /// no response body to project; <see cref="object"/> is used as the response type and the base
    /// pipeline simply completes once the request succeeds.
    /// </summary>
    public class DeleteApplicationProcessor : GraphProcessorBase<object>
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
                throw new InvalidOperationException("Id is required to delete an application.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://graph.microsoft.com/v1.0/applications/{Uri.EscapeDataString(Id)}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
