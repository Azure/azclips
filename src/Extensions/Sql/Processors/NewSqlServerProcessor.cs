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
    private const string ApiVersion = "2025-01-01";

    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AdministratorLogin { get; set; } = string.Empty;
    public string AdministratorLoginPassword { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public string? MinimalTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }

    /// <summary>Tag assignments in <c>key=value</c> form.</summary>
    // ponytail: string[] rather than a dictionary because the shared binder only passes through
    // exact CLR type matches (ArgumentParsingService uses Convert.ChangeType, which rejects
    // dictionaries). Move to Dictionary + a parser case when a second extension needs tags.
    public string[]? Tags { get; set; }

    private static Dictionary<string, string>? BuildTags(string[]? tags)
    {
        if (tags is null || tags.Length == 0)
        {
            return null;
        }

        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            int separator = tag.IndexOf('=');
            if (string.IsNullOrWhiteSpace(tag) || separator == 0)
            {
                throw new ArgumentException($"Tag '{tag}' is not valid. Use the format key[=value].");
            }

            // ponytail: a bare key means an empty value, matching `az --tags key[=value]`.
            string key = separator < 0 ? tag : tag[..separator];
            string value = separator < 0 ? string.Empty : tag[(separator + 1)..];
            result[key] = value;
        }

        return result;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Sql/servers/{Name}?api-version={ApiVersion}";
        SqlServerModel body = new()
        {
            Location = Location,
            Tags = BuildTags(Tags),
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
