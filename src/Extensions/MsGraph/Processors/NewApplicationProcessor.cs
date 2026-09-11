// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    public class NewApplicationProcessor : GraphProcessorBase<Application, Application>
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

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() => "https://graph.microsoft.com/v1.0/applications";

        // Only settable properties are sent; null read-only fields (id/appId/...) are omitted by the
        // base's WhenWritingNull serialization.
        protected override Application? CreateRequestBody() => new Application
        {
            DisplayName = DisplayName,
            SignInAudience = string.IsNullOrWhiteSpace(SignInAudience) ? null : SignInAudience,
        };
    }
}
