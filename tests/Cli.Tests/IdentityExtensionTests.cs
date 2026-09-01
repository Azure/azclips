// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Identity;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;
using Microsoft.Azure.Azclips.Extensions.Identity.Processors;

namespace Cli.Tests;

public class IdentityExtensionTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public void CliMetadata_ExposesDocumentedIdentityCommandSet()
    {
        var index = new ExtensionIndex();

        Assert.Equal(
            ["identity create", "identity list", "identity show", "identity update", "identity delete"],
            index.GetCommandKeys(ClientType.CLI).ToArray());
    }

    [Fact]
    public void CliMetadata_UsesLearnParameterNames_NotPowerShellNames()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("identity create", ClientType.CLI);

        Assert.Contains("resource-group", metadata.Parameters.Keys);
        Assert.Contains("resource-name", metadata.Parameters["name"].Aliases);
        Assert.Contains("subscription", metadata.Parameters.Keys);
        Assert.DoesNotContain("ResourceGroupName", metadata.Parameters.Keys);
        Assert.DoesNotContain("SubscriptionId", metadata.Parameters.Keys);
        Assert.Equal("ResourceGroup", metadata.Parameters["resource-group"].Name);
        Assert.Equal("ResourceName", metadata.Parameters["name"].Name);
    }

    [Fact]
    public void CliBinding_BindsDocumentedAliases_ToRefactoredProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("identity create", ClientType.CLI);
        var processor = (CreateIdentityProcessor_Orchestrator)metadata.GetInstance();
        var logging = new ConsoleLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging), new ProfileService(new ScopedLoggingService()));

        binder.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["g"] = new List<List<string>> { new() { "rg1" } },
                ["resource-name"] = new List<List<string>> { new() { "id1" } },
                ["location"] = new List<List<string>> { new() { "eastus" } },
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile(){SubscriptionId = "sub-1"});

        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus", processor.Location);
    }

    [Fact]
    public async Task CreateProcessAsync_UsesPreviewApi_AndSerializesIdentityProperties()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            return Json("""
                {
                  "id": "/subscriptions/s/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
                  "name": "id1",
                  "location": "eastus",
                  "properties": { "isolationScope": "Regional" }
                }
                """);
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<CreateIdentityProcessor>(pipeline, output);
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Location = "eastus";
        processor.IsolationScope = "Regional";
        processor.ResourceRestriction = ["""{"providers":["Microsoft.Compute"]}"""];
        processor.Tags = ["env=test"];

        await processor.ProcessAsync();

        var request = Assert.Single(sentRequests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1?api-version=2025-05-31-preview",
            request.RequestUri!.ToString());
        string body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("eastus", doc.RootElement.GetProperty("location").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal("Regional", doc.RootElement.GetProperty("properties").GetProperty("isolationScope").GetString());
        Assert.Equal("Microsoft.Compute", doc.RootElement.GetProperty("properties").GetProperty("assignmentRestrictions").GetProperty("providers")[0].GetString());
    }

    [Fact]
    public async Task CreateOrchestrator_ResolvesLocationFromResourceGroup_WhenLocationOmitted()
    {
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            if (request.Method == HttpMethod.Get)
            {
                return Json("""{ "location": "westus" }""");
            }

            return Json("""{ "name": "id1", "location": "westus" }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<CreateIdentityProcessor_Orchestrator>(pipeline, output);
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/rg1?api-version=2025-04-01",
            sentUris[0]);
        Assert.Equal("westus", processor.Location);
    }

    [Fact]
    public async Task ListProcessAsync_UnwrapsValueEnvelope()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""
            {
              "value": [
                { "name": "id1", "location": "eastus" },
                { "name": "id2", "location": "westus" }
              ]
            }
            """));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<ListIdentityProcessor>(pipeline, output);
        processor.ResourceGroup = "rg1";

        await processor.ProcessAsync();

        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
    }

    [Fact]
    public async Task ShowProcessAsync_ParsesIds_WithoutAmbientSubscription()
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<ShowIdentityProcessor>(pipeline, output, profile: null);
        processor.Ids = [$"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1"];

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1?api-version=2025-05-31-preview",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ShowProcessAsync_AllowsMultipleIds_AndOutputsIdentityArray()
    {
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            string name = request.RequestUri!.ToString().Contains("/id1?", StringComparison.Ordinal)
                ? "id1"
                : "id2";
            return Json($$"""{ "name": "{{name}}", "location": "eastus", "properties": { "isolationScope": "Regional" } }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<ShowIdentityProcessor>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Contains("/resourceGroups/rg1/", sentUris[0], StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg2/", sentUris[1], StringComparison.Ordinal);
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
        Assert.All(identities, identity => Assert.Equal("Regional", identity.Properties!.IsolationScope));
    }

    [Fact]
    public async Task UpdateProcessAsync_SerializesResourceRestrictionAndIsolationScope()
    {
        HttpRequestMessage? sentRequest = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequest = CloneRequest(request).GetAwaiter().GetResult();
            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<UpdateIdentityProcessor>(pipeline, output);
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.IsolationScope = "None";
        processor.ResourceRestriction = ["""{"providers":["Microsoft.Storage"]}"""];

        await processor.ProcessAsync();

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Patch, sentRequest!.Method);
        string body = await sentRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("None", doc.RootElement.GetProperty("properties").GetProperty("isolationScope").GetString());
        Assert.Equal("Microsoft.Storage", doc.RootElement.GetProperty("properties").GetProperty("assignmentRestrictions").GetProperty("providers")[0].GetString());
    }

    [Fact]
    public async Task UpdateOrchestrator_AppliesGenericUpdateOperations_ToCurrentIdentity()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            if (request.Method == HttpMethod.Get)
            {
                return Json("""
                    {
                      "name": "id1",
                      "location": "eastus",
                      "tags": { "old": "remove-me", "env": "dev" },
                      "properties": {
                        "assignmentRestrictions": { "providers": ["Microsoft.Compute"] },
                        "isolationScope": "None"
                      }
                    }
                    """);
            }

            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output);
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Tags = ["env=prod"];
        processor.Set = ["properties.isolationScope=Regional"];
        processor.Add = ["properties.assignmentRestrictions.providers", "Microsoft.KeyVault"];
        processor.Remove = ["tags.old"];

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Get, HttpMethod.Patch], sentRequests.Select(request => request.Method).ToArray());
        string body = await sentRequests[1].Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("tags").TryGetProperty("old", out _));
        Assert.Equal("prod", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal("Regional", doc.RootElement.GetProperty("properties").GetProperty("isolationScope").GetString());
        Assert.Equal(
            ["Microsoft.Compute", "Microsoft.KeyVault"],
            doc.RootElement.GetProperty("properties").GetProperty("assignmentRestrictions").GetProperty("providers")
                .EnumerateArray()
                .Select(provider => provider.GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task UpdateOrchestrator_AllowsMultipleIds_AndOutputsIdentityArray()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            string name = request.RequestUri!.ToString().Contains("/id1?", StringComparison.Ordinal)
                ? "id1"
                : "id2";

            if (request.Method == HttpMethod.Get)
            {
                return Json($$"""
                    {
                      "name": "{{name}}",
                      "location": "eastus",
                      "tags": { "env": "dev" },
                      "properties": { "isolationScope": "None" }
                    }
                    """);
            }

            return Json($$"""{ "name": "{{name}}", "location": "eastus", "properties": { "isolationScope": "Regional" } }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];
        processor.Set = ["properties.isolationScope=Regional"];

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Get, HttpMethod.Patch, HttpMethod.Get, HttpMethod.Patch], sentRequests.Select(request => request.Method).ToArray());
        Assert.Contains("/resourceGroups/rg1/", sentRequests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg1/", sentRequests[1].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg2/", sentRequests[2].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg2/", sentRequests[3].RequestUri!.ToString(), StringComparison.Ordinal);
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
        Assert.All(identities, identity => Assert.Equal("Regional", identity.Properties!.IsolationScope));
    }

    [Fact]
    public void JsonNodeExtensions_ParseJsonKeyValue_ParsesJsonFirstValues()
    {
        (string key, var value) = """enabled={"value":true}""".ParseJsonKeyValue();

        Assert.Equal("enabled", key);
        Assert.True(value!["value"]!.GetValue<bool>());
    }

    [Fact]
    public async Task DeleteProcessAsync_AllowsMultipleIds()
    {
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor<DeleteIdentityProcessor>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Contains("/resourceGroups/rg1/", sentUris[0], StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg2/", sentUris[1], StringComparison.Ordinal);
    }

    private static T CreateProcessor<T>(FakeHttpPipeline pipeline, FakeOutputHandler output, Profile? profile = null)
        where T : IProcessor, new()
    {
        profile ??= new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new T();
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            string body = await request.Content.ReadAsStringAsync();
            clone.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return clone;
    }
}
