// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    public class GetApplicationProcessor : GraphProcessorBase<object, Application>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of application",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://graph.microsoft.com/v1.0/applications/{Uri.EscapeDataString(Id)}";
    }
}
