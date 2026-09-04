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
    public class DeleteApplicationProcessor : GraphProcessorBase<object, object>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of application",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

        protected override HttpMethod Method => HttpMethod.Delete;

        protected override string CreateRequestUri() =>
            $"https://graph.microsoft.com/v1.0/applications/{Uri.EscapeDataString(Id)}";
    }
}
