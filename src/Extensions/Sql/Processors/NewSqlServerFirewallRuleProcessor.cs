// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Sql.Models;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Processors;

/// <summary>
/// <c>az sql server firewall-rule create</c> / <c>New-AzSqlServerFirewallRule</c>. A single
/// synchronous PUT: ARM answers 200/201 directly, so unlike the server and database creates this
/// is not a long-running operation and must not go through <see cref="SqlProcessorBase.SendAsyncLRO"/>.
/// A firewall rule is a proxy resource, so the body carries no location.
/// </summary>
public class NewSqlServerFirewallRuleProcessor : SqlProcessorBase<SqlFirewallRule>
{
    private const string ApiVersion = "2023-08-01";

    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string StartIpAddress { get; set; } = string.Empty;
    public string EndIpAddress { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{ServerName}/firewallRules/{Name}?api-version={ApiVersion}";

        SqlFirewallRule body = new()
        {
            Properties = new SqlFirewallRuleProperties
            {
                StartIpAddress = StartIpAddress,
                EndIpAddress = EndIpAddress,
            }
        };

        HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
