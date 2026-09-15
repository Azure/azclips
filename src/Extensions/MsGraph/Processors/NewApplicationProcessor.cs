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
        Description = @"The display name for the application. Supports $filter (eq, ne, NOT, ge, le, in, startsWith), $search, and $orderBy.",
        SerializedName = @"DisplayName")]
        public string DisplayName { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Specifies the Microsoft accounts that are supported for the current application. Supported values are: AzureADMyOrg, AzureADMultipleOrgs, AzureADandPersonalMicrosoftAccount, PersonalMicrosoftAccount. See more in the table below. Supports $filter (eq, ne, NOT).",
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
