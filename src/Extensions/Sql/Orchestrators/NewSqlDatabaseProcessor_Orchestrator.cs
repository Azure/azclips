// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Sql.Models;
using Microsoft.Azure.Azclips.Extensions.Sql.Processors;
using SqlServerModel = Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Orchestrators;

/// <summary>
/// <c>az sql db create</c> / <c>New-AzSqlDatabase</c>. Two API calls, so an orchestrator rather
/// than a processor: ARM requires <c>location</c> in the database PUT body, but neither shipping
/// client accepts it — both read it off the parent server. Step 1 GETs the server, step 2 PUTs
/// the database as a long-running operation.
/// </summary>
public class NewSqlDatabaseProcessor_Orchestrator : SqlProcessorBase<SqlDatabase>
{
    private const string ApiVersion = "2023-08-01";

    private string? _serverLocation;

    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string? Edition { get; set; }
    public string? ServiceObjective { get; set; }
    public string? Family { get; set; }
    public string? Collation { get; set; }
    public string? ElasticPoolName { get; set; }

    // ponytail: the argument binder goes through Convert.ChangeType, which cannot target
    // Nullable<T>, so value-typed parameters are non-nullable with a sentinel default meaning
    // "unset" (and therefore omitted from the PUT body) — same convention as NewVMProcessor.
    public int Capacity { get; set; }
    public long MaxSizeBytes { get; set; }
    public bool ZoneRedundant { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ResolveSubscriptionId();

        GetSqlServerProcessor getServer = CreateExtendedProcessor<GetSqlServerProcessor>(child =>
        {
            child.SubscriptionId = SubscriptionId;
            child.ResourceGroupName = ResourceGroupName;
            child.Name = ServerName;
        });

        using (HttpResponseMessage serverResponse = await getServer.SendAsync())
        {
            string rawBody = serverResponse.Content is null ? string.Empty : await serverResponse.Content.ReadAsStringAsync();
            SqlServerModel? server = string.IsNullOrWhiteSpace(rawBody)
                ? null
                : JsonSerializer.Deserialize<SqlServerModel>(rawBody, JsonOptions);

            _serverLocation = server?.Location;
            if (string.IsNullOrEmpty(_serverLocation))
            {
                throw new InvalidOperationException($"Could not resolve the location of SQL server '{ServerName}' in resource group '{ResourceGroupName}'. The database location is inherited from its server.");
            }
        }

        return await SendAsyncLRO();
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{ServerName}/databases/{Name}?api-version={ApiVersion}";

        SqlDatabase body = new()
        {
            Location = _serverLocation,
            Sku = BuildSku(),
            Properties = BuildProperties(),
        };

        HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    private SqlSku? BuildSku()
    {
        if (ServiceObjective is null && Edition is null && Family is null && Capacity == 0)
        {
            return null;
        }

        return new SqlSku
        {
            Name = ServiceObjective,
            Tier = Edition,
            Family = Family,
            Capacity = Capacity == 0 ? null : Capacity,
        };
    }

    private SqlDatabaseProperties? BuildProperties()
    {
        if (Collation is null && MaxSizeBytes == 0 && ElasticPoolName is null && !ZoneRedundant)
        {
            return null;
        }

        return new SqlDatabaseProperties
        {
            Collation = Collation,
            MaxSizeBytes = MaxSizeBytes == 0 ? null : MaxSizeBytes,
            // ponytail: ARM wants the full elastic pool resource id; the shipping clients take a
            // bare name, so compose it here rather than growing the parameter surface.
            ElasticPoolId = string.IsNullOrEmpty(ElasticPoolName)
                ? null
                : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{ServerName}/elasticPools/{ElasticPoolName}",
            ZoneRedundant = ZoneRedundant ? true : null,
        };
    }

    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");
}
