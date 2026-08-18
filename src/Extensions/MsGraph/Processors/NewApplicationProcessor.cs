// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    public class NewApplicationProcessor : GraphProcessorBase<Application>
    {
        [ArgumentInfo(
        Required = true,
        Description = @"The display name of the application registration.",
        SerializedName = @"DisplayName")]
        public string DisplayName { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"The Microsoft account types supported for sign-in (e.g. AzureADMyOrg, AzureADMultipleOrgs, AzureADandPersonalMicrosoftAccount).",
        SerializedName = @"SignInAudience")]
        public string SignInAudience { get; set; } = "";

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                throw new InvalidOperationException("DisplayName is required to create an application.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://graph.microsoft.com/v1.0/applications";

            // Build a minimal request body with only settable properties. Read-only fields such as
            // id/appId must not be sent on create, so we compose an explicit payload rather than
            // serializing the full model.
            Dictionary<string, object> body = new()
            {
                ["displayName"] = DisplayName,
            };
            if (!string.IsNullOrWhiteSpace(SignInAudience))
            {
                body["signInAudience"] = SignInAudience;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
