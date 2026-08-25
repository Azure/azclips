// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using SqlServerModel = Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Processors;

/// <summary>
/// Atomic GET on <c>Microsoft.Sql/servers/{name}</c>. Not advertised in
/// <see cref="ExtensionIndex"/> — it exists because <c>sql db create</c> has to resolve the
/// parent server's location, which neither the Azure CLI nor Azure PowerShell asks the user for.
/// </summary>
public class GetSqlServerProcessor : SqlProcessorBase<SqlServerModel>
{
    private const string ApiVersion = "2023-08-01";

    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{Name}?api-version={ApiVersion}";
        HttpRequestMessage request = new(HttpMethod.Get, uri);

        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
