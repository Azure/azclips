// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Sql.Models;
// ponytail: alias avoids the collision with the ambient Microsoft.SqlServer namespace.
using SqlServerModel = Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Processors;

public class NewSqlServerProcessor : SqlProcessorBase<SqlServerModel>
{
    private const string ApiVersion = "2023-08-01";

    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AdministratorLogin { get; set; } = string.Empty;
    public string AdministratorLoginPassword { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public string? MinimalTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{Name}?api-version={ApiVersion}";
        SqlServerModel body = new()
        {
            Location = Location,
            Properties = new SqlServerProperties
            {
                AdministratorLogin = AdministratorLogin,
                AdministratorLoginPassword = AdministratorLoginPassword,
                Version = ServerVersion,
                MinimalTlsVersion = MinimalTlsVersion,
                PublicNetworkAccess = PublicNetworkAccess,
            }
        };

        HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
