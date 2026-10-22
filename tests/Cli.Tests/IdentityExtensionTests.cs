// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;
using Moq;

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
            [.. index.GetCommandKeys(ClientType.CLI)]);
    }

    [Fact]
    public void PowerShellMetadata_ExposesIdentityCmdletCommandSet()
    {
        var index = new ExtensionIndex();

        Assert.Equal(
            ["New-AzUserAssignedIdentity", "Get-AzUserAssignedIdentity_List", "Get-AzUserAssignedIdentity_Get", "Update-AzUserAssignedIdentity", "Remove-AzUserAssignedIdentity"],
            [.. index.GetCommandKeys(ClientType.PowerShell)]);
    }

    [Fact]
    public void PowerShellMetadata_MapsCmdletParameters_ToProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Update-AzUserAssignedIdentity", ClientType.PowerShell);

        Assert.Equal(typeof(UpdateIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("ResourceGroup", metadata.Parameters["ResourceGroupName"].Name);
        Assert.Equal("ResourceName", metadata.Parameters["Name"].Name);
        Assert.Equal("Location", metadata.Parameters["Location"].Name);
        Assert.Equal("ResourceRestrictionProviders", metadata.Parameters["ResourceRestriction"].Name);
        Assert.Equal(typeof(List<string>), metadata.Parameters["ResourceRestriction"].ParameterType);
        Assert.Equal(typeof(Dictionary<string, string>), metadata.Parameters["Tags"].ParameterType);
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
        Assert.Equal("TagsInput", metadata.Parameters["tags"].Name);
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
                ["tags"] = new List<List<string>> { new() { "env=test" } },
                ["resource-restriction"] = new List<List<string>> { new() { """{"providers":["Microsoft.Compute"]}""" } },
            },
            metadata,
            new Profile(){SubscriptionId = "sub-1"});

        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus", processor.Location);
        Assert.NotNull(processor.TagsInput);
        Assert.Equal(["env=test"], processor.TagsInput);
        Assert.NotNull(processor.ResourceRestriction);
        Assert.Equal(["""{"providers":["Microsoft.Compute"]}"""], processor.ResourceRestriction);
    }

    [Fact]
    public async Task CreateProcessAsync_UsesPreviewApi_AndSerializesIdentityProperties()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
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
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<CreateIdentityProcessor>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Location = "eastus";
        processor.IsolationScope = "Regional";
        processor.ResourceRestrictionProviders = ["Microsoft.Compute"];
        processor.Tags = new Dictionary<string, string> { ["env"] = "test" };

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
    public async Task CreateOrchestrator_ParsesCliTagsAndResourceRestriction()
    {
        HttpRequestMessage? sentRequest = null;
        var pipeline = CreatePipeline((request, _) =>
        {
            sentRequest = CloneRequest(request).GetAwaiter().GetResult();
            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<CreateIdentityProcessor_Orchestrator>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Location = "eastus";
        processor.TagsInput = ["env=test"];
        processor.ResourceRestriction = ["""{"providers":["Microsoft.Compute"]}"""];

        await processor.ProcessAsync();

        string body = await sentRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("test", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal("Microsoft.Compute", doc.RootElement.GetProperty("properties").GetProperty("assignmentRestrictions").GetProperty("providers")[0].GetString());
    }

    [Fact]
    public async Task ListProcessAsync_UnwrapsValueEnvelope()
    {
        var pipeline = CreatePipeline((_, _) => Json("""
            {
              "value": [
                { "name": "id1", "location": "eastus" },
                { "name": "id2", "location": "westus" }
              ]
            }
            """));
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<ListIdentityProcessor>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";

        await processor.ProcessAsync();

        var identities = Assert.IsType<List<UserAssignedIdentity>>(Assert.Single(writtenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
    }

    [Fact]
    public async Task ListProcessAsync_FollowsNextLink()
    {
        var sentUris = new List<string>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count switch
            {
                1 => Json("""
                    {
                      "value": [
                        { "name": "id1", "location": "eastus" },
                        { "name": "id2", "location": "eastus" }
                      ],
                      "nextLink": "https://management.azure.com/page2"
                    }
                    """),
                2 => Json("""
                    {
                      "value": [
                        { "name": "id3", "location": "westus" },
                        { "name": "id4", "location": "westus" }
                      ],
                      "nextLink": "https://management.azure.com/page3"
                    }
                    """),
                _ => Json("""
                    {
                      "value": [
                        { "name": "id5", "location": "centralus" }
                      ]
                    }
                    """)
            };
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<ListIdentityProcessor>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";

        await processor.ProcessAsync();

        Assert.Equal(3, sentUris.Count);
        Assert.Equal("https://management.azure.com/page2", sentUris[1]);
        Assert.Equal("https://management.azure.com/page3", sentUris[2]);
        var identities = Assert.IsType<List<UserAssignedIdentity>>(Assert.Single(writtenValues));
        Assert.Equal(["id1", "id2", "id3", "id4", "id5"], identities.Select(identity => identity.Name!).ToArray());
    }

    [Fact]
    public async Task ShowProcessAsync_ParsesIds_WithoutAmbientSubscription()
    {
        Uri? sentUri = null;
        var pipeline = CreatePipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<ShowIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids = [$"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1"];

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1?api-version=2025-05-31-preview",
            sentUri!.ToString());
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        var identity = Assert.Single(identities);
        Assert.Equal("id1", identity.Name);
    }

    [Fact]
    public async Task ShowProcessAsync_AllowsMultipleIds_AndOutputsIdentityArray()
    {
        var sentUris = new List<string>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            string name = request.RequestUri!.ToString().Contains("/id1?", StringComparison.Ordinal)
                ? "id1"
                : "id2";
            return Json($$"""{ "name": "{{name}}", "location": "eastus", "properties": { "isolationScope": "Regional" } }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<ShowIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        Assert.Contains("/resourceGroups/rg1/", sentUris[0], StringComparison.Ordinal);
        Assert.Contains("/resourceGroups/rg2/", sentUris[1], StringComparison.Ordinal);
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
        Assert.All(identities, identity => Assert.Equal("Regional", identity.Properties!.IsolationScope));
    }

    [Fact]
    public async Task ShowProcessAsync_AllowsMultipleIds_AndSkipsNoContentResponses()
    {
        var sentUris = new List<string>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count == 1
                ? Json("""{ "name": "id1", "location": "eastus" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<ShowIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];

        await processor.ProcessAsync();

        Assert.Equal(2, sentUris.Count);
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        var identity = Assert.Single(identities);
        Assert.Equal("id1", identity.Name);
    }

    [Fact]
    public async Task UpdateProcessAsync_SerializesResourceRestrictionAndIsolationScope()
    {
        HttpRequestMessage? sentRequest = null;
        var pipeline = CreatePipeline((request, _) =>
        {
            sentRequest = CloneRequest(request).GetAwaiter().GetResult();
            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<UpdateIdentityProcessor>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Location = "westus2";
        processor.IsolationScope = "None";
        processor.ResourceRestrictionProviders = ["Microsoft.Storage"];

        await processor.ProcessAsync();

        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Patch, sentRequest!.Method);
        string body = await sentRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("westus2", doc.RootElement.GetProperty("location").GetString());
        Assert.Equal("None", doc.RootElement.GetProperty("properties").GetProperty("isolationScope").GetString());
        Assert.Equal("Microsoft.Storage", doc.RootElement.GetProperty("properties").GetProperty("assignmentRestrictions").GetProperty("providers")[0].GetString());
    }

    [Fact]
    public async Task UpdateOrchestrator_AppliesGenericUpdateOperations_ToCurrentIdentity()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            if (request.Method == HttpMethod.Get)
            {
                return Json("""
                    {
                      "id": "/subscriptions/s/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
                      "name": "id1",
                      "type": "Microsoft.ManagedIdentity/userAssignedIdentities",
                      "location": "eastus",
                      "tags": { "old": "remove-me", "env": "dev" },
                      "properties": {
                        "assignmentRestrictions": { "providers": ["Microsoft.Compute"] },
                        "isolationScope": "None",
                        "tenantId": "tenant-1",
                        "principalId": "principal-1",
                        "clientId": "client-1"
                      },
                      "systemData": {}
                    }
                    """);
            }

            return Json("""{ "name": "id1", "location": "eastus" }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.TagsInput = ["env=prod"];
        processor.ResourceRestriction = ["""{"providers":["Microsoft.Storage"]}"""];
        processor.Set = ["properties.isolationScope=Regional"];
        processor.Add = ["properties.assignmentRestrictions.providers", "Microsoft.KeyVault"];
        processor.Remove = ["tags.old"];

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Get, HttpMethod.Patch], sentRequests.Select(request => request.Method).ToArray());
        string body = await sentRequests[1].Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("id", out _));
        Assert.False(doc.RootElement.TryGetProperty("name", out _));
        Assert.False(doc.RootElement.TryGetProperty("type", out _));
        Assert.False(doc.RootElement.TryGetProperty("systemData", out _));
        Assert.False(doc.RootElement.GetProperty("tags").TryGetProperty("old", out _));
        Assert.Equal("prod", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        JsonElement properties = doc.RootElement.GetProperty("properties");
        Assert.False(properties.TryGetProperty("tenantId", out _));
        Assert.False(properties.TryGetProperty("principalId", out _));
        Assert.False(properties.TryGetProperty("clientId", out _));
        Assert.Equal("Regional", properties.GetProperty("isolationScope").GetString());
        Assert.Equal(
            ["Microsoft.Storage", "Microsoft.KeyVault"],
            properties.GetProperty("assignmentRestrictions").GetProperty("providers")
                .EnumerateArray()
                .Select(provider => provider.GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task UpdateOrchestrator_AllowsMultipleIds_AndOutputsIdentityArray()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
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
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
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
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        Assert.Equal(["id1", "id2"], identities.Select(identity => identity.Name!).ToArray());
        Assert.All(identities, identity => Assert.Equal("Regional", identity.Properties!.IsolationScope));
    }

    [Fact]
    public async Task UpdateOrchestrator_MultipleIdsWithGenericUpdate_BuildsPatchBodyPerChildInstance()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
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
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];
        processor.Set = ["properties.isolationScope=Regional"];

        await processor.ProcessAsync();

        HttpRequestMessage[] patchRequests = [.. sentRequests.Where(request => request.Method == HttpMethod.Patch)];
        Assert.Equal(2, patchRequests.Length);
        foreach (HttpRequestMessage patchRequest in patchRequests)
        {
            string body = await patchRequest.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("Regional", doc.RootElement.GetProperty("properties").GetProperty("isolationScope").GetString());
            Assert.Equal("dev", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        }
    }

    [Fact]
    public async Task UpdateOrchestrator_AllowsMultipleIds_AndSkipsNoContentResponses()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            return sentRequests.Count == 1
                ? Json("""{ "name": "id1", "location": "eastus" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids =
        [
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1",
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/rg2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id2",
        ];
        processor.IsolationScope = "Regional";

        await processor.ProcessAsync();

        Assert.Equal(2, sentRequests.Count);
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        var identity = Assert.Single(identities);
        Assert.Equal("id1", identity.Name);
    }

    [Fact]
    public async Task UpdateOrchestrator_AllowsSingleId_AndOutputsIdentityArray()
    {
        var sentRequests = new List<HttpRequestMessage>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentRequests.Add(CloneRequest(request).GetAwaiter().GetResult());
            if (request.Method == HttpMethod.Get)
            {
                return Json("""{ "name": "id1", "location": "eastus", "properties": { "isolationScope": "None" } }""");
            }

            return Json("""{ "name": "id1", "location": "eastus", "properties": { "isolationScope": "Regional" } }""");
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
        processor.Ids = [$"/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id1"];
        processor.Set = ["properties.isolationScope=Regional"];

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Get, HttpMethod.Patch], sentRequests.Select(request => request.Method).ToArray());
        var identities = Assert.IsType<UserAssignedIdentity[]>(Assert.Single(writtenValues));
        var identity = Assert.Single(identities);
        Assert.Equal("id1", identity.Name);
        Assert.Equal("Regional", identity.Properties!.IsolationScope);
    }

    [Fact]
    public async Task DeleteProcessAsync_AllowsMultipleIds()
    {
        var sentUris = new List<string>();
        var pipeline = CreatePipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var writtenValues = new List<object>();
        var output = CreateOutputHandler(writtenValues);
        var processor = CreateProcessor<DeleteIdentityProcessor_Orchestrator>(pipeline, output, profile: null);
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

    private static T CreateProcessor<T>(IRuntimeHttpPipeline pipeline, IOutputHandler output, Profile? profile = null)
        where T : IProcessor, new()
    {
        profile ??= new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new T();
        var context = new ProcessorContext(
            pipeline,
            CreateEventPublisher(),
            CreateAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        return processor;
    }

    private static IRuntimeHttpPipeline CreatePipeline(Func<HttpRequestMessage, IEventPublisher, HttpResponseMessage> responder)
    {
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline
            .Setup(p => p.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns<HttpRequestMessage, IEventPublisher>((request, publisher) => Task.FromResult(responder(request, publisher)));
        return pipeline.Object;
    }

    private static IOutputHandler CreateOutputHandler(List<object> writtenValues)
    {
        var output = new Mock<IOutputHandler>();
        OutputStreamDelegate writeOutput = (value, _) =>
        {
            writtenValues.Add(value);
            return Task.CompletedTask;
        };

        output.SetupGet(o => o.WriteOutput).Returns(writeOutput);
        return output.Object;
    }

    private static IEventPublisher CreateEventPublisher()
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetupGet(p => p.Token).Returns(CancellationToken.None);
        publisher.SetupGet(p => p.Cancel).Returns(() => { });
        publisher.SetReturnsDefault(Task.CompletedTask);
        return publisher.Object;
    }

    private static IAuthHandler CreateAuthHandler(Profile? profile)
    {
        var auth = new Mock<IAuthHandler>();
        auth.SetupProperty(a => a.Profile, profile);
        auth.SetupProperty(a => a.ApplyOverride);
        auth.SetupProperty(a => a.AcquireTokenAsyncOverride);
        auth.SetupGet(a => a.AcquireTokenAsyncDefault).Returns((_, _, _, _, _) => throw new NotSupportedException("Token acquisition is not exercised in these offline tests."));
        auth.SetupGet(a => a.SendAsync).Returns((request, callback, next) => next.SendAsync(request, callback));
        return auth.Object;
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
