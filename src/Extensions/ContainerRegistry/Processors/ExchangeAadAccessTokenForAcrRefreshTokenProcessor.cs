// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors
{
    public class ExchangeAadAccessTokenForAcrRefreshTokenProcessor : AcrDataPlaneProcessorBase<AcrRefreshToken>
    {
        public string LoginUri { get; set; } = string.Empty;
        public PostContentSchemaGrantType GrantType { get; set; } = PostContentSchemaGrantType.AccessToken;
        public string? Tenant { get; set; } = string.Empty;
        public string? AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; } = string.Empty;

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://{LoginUri}/oauth2/exchange?api-version=2021-07-01";
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", GrantType.ToString()),
                new KeyValuePair<string, string>("service", LoginUri)
            };
            if (!string.IsNullOrEmpty(Tenant))
            {
                values.Add(new KeyValuePair<string, string>("tenant", Tenant));
            }
            if (!string.IsNullOrEmpty(AccessToken))
            {
                values.Add(new KeyValuePair<string, string>("access_token", AccessToken));
            }
            if (!string.IsNullOrEmpty(RefreshToken))
            {
                values.Add(new KeyValuePair<string, string>("refresh_token", RefreshToken));
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new FormUrlEncodedContent(values)
            };
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        /*
        Yabo:
            swagger says:         
            "x-ms-long-running-operation-options": {
                "final-state-via": "location"
            }
            but got nothing, fallback to no finaluri
        */
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        // TODO: Yabo to add config in codegen to empty ProcessAsync for operations. 
        // Maybe when Hide processors also empty its ProcessAsync.
        public override async Task ProcessAsync() {}
    }
}



