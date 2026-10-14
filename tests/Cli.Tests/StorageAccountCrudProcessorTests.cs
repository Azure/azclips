// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for the storage account read and delete commands. These pin the request each
/// processor issues (verb, URI, api-version) and, for list, the branch between the two API
/// operations plus the flattening of paged responses into one array.
/// </summary>
public class StorageAccountCrudProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string AccountName = "mystorageaccount";
    private const string ApiVersion = "api-version=2026-04-01";

    private const string SingleAccount = """
        {
          "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/mystorageaccount",
          "name": "mystorageaccount",
          "type": "Microsoft.Storage/storageAccounts",
          "location": "eastus",
          "kind": "StorageV2",
          "sku": { "name": "Standard_LRS", "tier": "Standard" },
          "properties": { "provisioningState": "Succeeded" }
        }
        """;

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ProcessorContext Context(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        return new ProcessorContext(pipeline, new FakeEventPublisher(), new FakeAuthHandler(profile), output, Profile: profile);
    }

    private static string ListPage(string nextLink, params string[] names)
    {
        string items = string.Join(",", names.Select(n => $$"""
            { "name": "{{n}}", "location": "eastus", "kind": "StorageV2" }
            """));
        string next = string.IsNullOrEmpty(nextLink) ? "" : $", \"nextLink\": \"{nextLink}\"";
        return $$"""{ "value": [{{items}}]{{next}} }""";
    }

    // ── Show ──

    [Fact]
    public async Task Show_IssuesAGetAgainstTheAccountResourceUri()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) => { captured ??= request; return Json(SingleAccount); });
        var output = new FakeOutputHandler();

        var processor = new ShowStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}"
            + $"/providers/Microsoft.Storage/storageAccounts/{AccountName}?{ApiVersion}",
            captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Show_WithoutExpand_OmitsTheQueryOption()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) => { captured ??= request; return Json(SingleAccount); });

        var processor = new ShowStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.DoesNotContain("$expand", captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Show_WithExpand_AppendsTheQueryOption()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) => { captured ??= request; return Json(SingleAccount); });

        var processor = new ShowStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
            Expand = "geoReplicationStats",
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.Contains("$expand=geoReplicationStats", Uri.UnescapeDataString(captured!.RequestUri!.AbsoluteUri));
    }

    [Fact]
    public async Task Show_DeserializesGeoReplicationStats()
    {
        const string withStats = """
            {
              "name": "mystorageaccount",
              "location": "eastus",
              "kind": "StorageV2",
              "properties": {
                "provisioningState": "Succeeded",
                "geoReplicationStats": {
                  "status": "Live",
                  "lastSyncTime": "2026-09-08T00:00:00Z",
                  "canFailover": true,
                  "canPlannedFailover": false,
                  "postFailoverRedundancy": "Standard_LRS",
                  "postPlannedFailoverRedundancy": "Standard_GRS"
                }
              }
            }
            """;
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json(withStats));

        var processor = new ShowStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
            Expand = "geoReplicationStats",
        };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        var account = Assert.IsType<StorageAccount>(Assert.Single(output.WrittenValues));
        var stats = account.Properties!.GeoReplicationStats;
        Assert.NotNull(stats);
        Assert.Equal("Live", stats!.Status);
        Assert.True(stats.CanFailover);
        Assert.False(stats.CanPlannedFailover);
        Assert.Equal("Standard_GRS", stats.PostPlannedFailoverRedundancy);
    }

    // ── List ──

    [Fact]
    public async Task List_WithoutResourceGroup_TargetsTheSubscriptionOperation()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) => { captured ??= request; return Json(ListPage("", "a", "b")); });

        var processor = new ListStorageAccountProcessor_Orchestrator { SubscriptionId = TestSubscriptionId };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}"
            + $"/providers/Microsoft.Storage/storageAccounts?{ApiVersion}",
            captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_WithResourceGroup_TargetsTheResourceGroupOperation()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) => { captured ??= request; return Json(ListPage("", "a")); });

        var processor = new ListStorageAccountProcessor_Orchestrator
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}"
            + $"/providers/Microsoft.Storage/storageAccounts?{ApiVersion}",
            captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_WritesTheAccountsAsOneFlatArray()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json(ListPage("", "first", "second")));

        var processor = new ListStorageAccountProcessor_Orchestrator { SubscriptionId = TestSubscriptionId };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        var accounts = Assert.IsType<List<StorageAccount>>(Assert.Single(output.WrittenValues));
        Assert.Collection(accounts,
            a => Assert.Equal("first", a.Name),
            a => Assert.Equal("second", a.Name));
    }

    [Fact]
    public async Task List_FollowsNextLinkAndConcatenatesThePages()
    {
        // The service pages this operation. Without following nextLink a caller silently sees only
        // the first page, so the delegated processor has to carry pagination too.
        string secondPageUri = $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Storage/storageAccounts?{ApiVersion}&skipToken=abc";
        var uris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            uris.Add(request.RequestUri!.AbsoluteUri);
            return Json(uris.Count == 1 ? ListPage(secondPageUri, "first") : ListPage("", "second"));
        });
        var output = new FakeOutputHandler();

        var processor = new ListStorageAccountProcessor_Orchestrator
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
        };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        Assert.Equal(2, uris.Count);
        Assert.Equal(secondPageUri, uris[1]);
        var accounts = Assert.IsType<List<StorageAccount>>(Assert.Single(output.WrittenValues));
        Assert.Collection(accounts,
            a => Assert.Equal("first", a.Name),
            a => Assert.Equal("second", a.Name));
    }

    [Fact]
    public async Task List_EmptyResult_WritesAnEmptyCollection()
    {
        // An empty resource group is ordinary, and the command must still emit a collection so a
        // caller piping the output sees zero items rather than a missing result.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));

        var processor = new ListStorageAccountProcessor_Orchestrator
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
        };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        var accounts = Assert.IsType<List<StorageAccount>>(Assert.Single(output.WrittenValues));
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task List_ResultWithoutValue_WritesAnEmptyCollection()
    {
        // The envelope's value member is optional in the schema, so a body that omits it must not
        // fault or silently write nothing.
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) => Json("{ }"));

        var processor = new ListStorageAccountProcessor_Orchestrator { SubscriptionId = TestSubscriptionId };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        var accounts = Assert.IsType<List<StorageAccount>>(Assert.Single(output.WrittenValues));
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task List_SubscriptionScope_AlsoFollowsNextLink()
    {
        // The resource-group branch delegates to a second processor while this one runs on the
        // customization itself, so pagination has to be proven on both paths.
        string secondPageUri = $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Storage/storageAccounts?{ApiVersion}&skipToken=abc";
        var uris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            uris.Add(request.RequestUri!.AbsoluteUri);
            return Json(uris.Count == 1 ? ListPage(secondPageUri, "first") : ListPage("", "second"));
        });
        var output = new FakeOutputHandler();

        var processor = new ListStorageAccountProcessor_Orchestrator { SubscriptionId = TestSubscriptionId };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        Assert.Equal(2, uris.Count);
        var accounts = Assert.IsType<List<StorageAccount>>(Assert.Single(output.WrittenValues));
        Assert.Equal(2, accounts.Count);
    }

    // ── Delete ──

    [Fact]
    public async Task Delete_IssuesADeleteAgainstTheAccountResourceUri()
    {
        HttpRequestMessage? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            captured ??= request;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var processor = new DeleteStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Delete, captured!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}"
            + $"/providers/Microsoft.Storage/storageAccounts/{AccountName}?{ApiVersion}",
            captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Delete_DoesNotPoll()
    {
        // The operation is not long running (x-ms-long-running-operation is false and azure-cli
        // binds the synchronous delete), so a 200 must end the command rather than start polling.
        int requests = 0;
        var pipeline = new FakeHttpPipeline((_, _) =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var processor = new DeleteStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));
        await processor.ProcessAsync();

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Delete_OnNoContent_WritesNothing()
    {
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((_, _) =>
            new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent("") });

        var processor = new DeleteStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, output));
        await processor.ProcessAsync();

        Assert.Empty(output.WrittenValues);
    }

    // ── Failure surfacing ──

    [Fact]
    public async Task Show_OnNotFound_Throws()
    {
        // Reading a missing account has to fail rather than write an empty result, otherwise a
        // script cannot tell "absent" from "present but empty".
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{ "error": { "code": "ResourceNotFound", "message": "The resource was not found." } }""",
                Encoding.UTF8,
                "application/json"),
        });

        var processor = new ShowStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));

        await Assert.ThrowsAnyAsync<Exception>(processor.ProcessAsync);
    }

    [Fact]
    public async Task Delete_OnNotFound_Throws()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{ "error": { "code": "ResourceNotFound", "message": "The resource was not found." } }""",
                Encoding.UTF8,
                "application/json"),
        });

        var processor = new DeleteStorageAccountProcessor
        {
            SubscriptionId = TestSubscriptionId,
            ResourceGroupName = ResourceGroupName,
            AccountName = AccountName,
        };
        processor.Init(Context(pipeline, new FakeOutputHandler()));

        await Assert.ThrowsAnyAsync<Exception>(processor.ProcessAsync);
    }
}
