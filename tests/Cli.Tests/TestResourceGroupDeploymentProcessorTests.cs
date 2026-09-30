// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>, covering the
/// user-facing input surface it shares with the create and what-if orchestrators: the optional
/// deployment name, the opaque validation level, the query-string rules, the .bicepparam template
/// source, the parameter token forms, and the client-side missing-parameter check. Nothing here
/// touches the network: a fake pipeline records the request URI and body.
/// </summary>
public class TestResourceGroupDeploymentProcessorTests : IDisposable
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private const string NoParameterTemplate = """
        { "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#", "contentVersion": "1.0.0.0", "resources": [] }
        """;

    // 'location' is required (no defaultValue), 'sku' has one, and 'tags' is nullable.
    private const string LocationTemplate = """
        {
          "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "parameters": {
            "location": { "type": "string" },
            "sku": { "type": "string", "defaultValue": "Standard_LRS" },
            "tags": { "type": "object", "nullable": true }
          },
          "resources": []
        }
        """;

    private readonly string tempDirectory = Directory.CreateTempSubdirectory("azclips-deployment-tests-").FullName;

    public void Dispose() => Directory.Delete(tempDirectory, recursive: true);

    private string WriteTempFile(string fileName, string content)
    {
        string path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static TestResourceGroupDeploymentProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new TestResourceGroupDeploymentProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "my-rg";
        return processor;
    }

    /// <summary>A pipeline that records every request and answers with an error-free validate result.</summary>
    private static FakeHttpPipeline Recording(List<string> uris, List<string> bodies) =>
        new((request, _) =>
        {
            uris.Add(request.RequestUri!.AbsoluteUri);
            bodies.Add(request.Content is null ? string.Empty : request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        });

    private static JsonElement DeploymentProperties(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("properties");

    // ── Deployment name ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithoutName_UsesAGeneratedGuidDeploymentName()
    {
        var uris = new List<string>();
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(uris, bodies), new FakeOutputHandler());
        processor.TemplateFile = WriteTempFile("empty.json", NoParameterTemplate);

        await processor.ProcessAsync();

        string path = new Uri(Assert.Single(uris)).AbsolutePath;
        string deploymentName = path.Split("/deployments/")[1].Split('/')[0];
        Assert.True(Guid.TryParse(deploymentName, out _), $"expected a GUID deployment name but got '{deploymentName}'");
    }

    [Fact]
    public async Task ProcessAsync_WithAnInvalidName_StillRejectsIt()
    {
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "not a valid name!";
        processor.TemplateFile = WriteTempFile("empty-invalid-name.json", NoParameterTemplate);

        await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
        Assert.Equal(0, pipeline.SendCount);
    }

    // ── Validation level ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_UnrecognizedValidationLevel_ReachesTheRequestBody()
    {
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("empty-level.json", NoParameterTemplate);
        processor.ValidationLevel = "SomeFutureLevel";

        await processor.ProcessAsync();

        Assert.Equal("SomeFutureLevel", DeploymentProperties(Assert.Single(bodies)).GetProperty("validationLevel").GetString());
    }

    // ── Query string ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_QueryStringWithoutTemplateUri_Throws()
    {
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("empty-qs.json", NoParameterTemplate);
        processor.ParametersFile = WriteTempFile("params-qs.json", """{ "parameters": { } }""");
        processor.QueryString = "sv=2021&sig=ABC";

        var error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
        Assert.Contains("TemplateUri", error.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_QueryStringWithTemplateUri_LandsOnTheTemplateLinkOnly()
    {
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateUri = "https://x/t.json";
        processor.ParametersUri = "https://x/p.json";
        processor.QueryString = "?sv=2021&sig=ABC";

        await processor.ProcessAsync();

        JsonElement properties = DeploymentProperties(Assert.Single(bodies));
        Assert.Equal("sv=2021&sig=ABC", properties.GetProperty("templateLink").GetProperty("queryString").GetString());
        Assert.Equal("https://x/t.json", properties.GetProperty("templateLink").GetProperty("uri").GetString());
        // The parameters link is never rewritten to carry the query string.
        Assert.Equal("https://x/p.json", properties.GetProperty("parametersLink").GetProperty("uri").GetString());
    }

    // ── Template sources ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_TemplateObject_ProducesTheSameBodyAsTheEquivalentTemplateFile()
    {
        var fileBodies = new List<string>();
        var fromFile = CreateProcessor(Recording(new List<string>(), fileBodies), new FakeOutputHandler());
        fromFile.Name = "dep";
        fromFile.TemplateFile = WriteTempFile("object-equivalent.json", NoParameterTemplate);
        await fromFile.ProcessAsync();

        var objectBodies = new List<string>();
        var fromObject = CreateProcessor(Recording(new List<string>(), objectBodies), new FakeOutputHandler());
        fromObject.Name = "dep";
        fromObject.TemplateObject = new System.Collections.Hashtable
        {
            ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            ["contentVersion"] = "1.0.0.0",
            ["resources"] = Array.Empty<object>(),
        };
        await fromObject.ProcessAsync();

        JsonElement fromFileTemplate = DeploymentProperties(Assert.Single(fileBodies)).GetProperty("template");
        JsonElement fromObjectTemplate = DeploymentProperties(Assert.Single(objectBodies)).GetProperty("template");
        Assert.Equal(
            fromFileTemplate.GetProperty("contentVersion").GetString(),
            fromObjectTemplate.GetProperty("contentVersion").GetString());
        Assert.Equal(
            fromFileTemplate.GetProperty("$schema").GetString(),
            fromObjectTemplate.GetProperty("$schema").GetString());
        Assert.Equal(JsonValueKind.Array, fromObjectTemplate.GetProperty("resources").ValueKind);
    }

    [Fact]
    public async Task ProcessAsync_NoTemplateSourceAtAll_Throws()
    {
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "dep";

        await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_BicepParamFileWithoutATemplate_TakesTheTemplateFromTheEnvelope()
    {
        // The .bicepparam names its template through 'using', so the 'bicep build-params' envelope
        // carries the compiled template. Needs the Bicep CLI; skipped where it is not installed.
        string bicepParam = Path.Combine(
            RepositoryRoot(), "tests", "Scripts", "Bicep", "sample-storage.bicepparam");
        Assert.True(File.Exists(bicepParam), $"fixture not found: {bicepParam}");

        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.ParametersFile = bicepParam;

        try
        {
            await processor.ProcessAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Bicep CLI not found"))
        {
            return;
        }

        JsonElement properties = DeploymentProperties(Assert.Single(bodies));
        Assert.Equal(JsonValueKind.Object, properties.GetProperty("template").ValueKind);
        Assert.Equal(
            "azclipsbicep0001",
            properties.GetProperty("parameters").GetProperty("storageAccountName").GetProperty("value").GetString());
    }

    // ── Parameter token forms ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_KeyValueParameter_IsTypedAndEnveloped()
    {
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("kv.json", NoParameterTemplate);
        processor.Parameter = ["name=contoso", "count=3"];

        await processor.ProcessAsync();

        JsonElement parameters = DeploymentProperties(Assert.Single(bodies)).GetProperty("parameters");
        Assert.Equal("contoso", parameters.GetProperty("name").GetProperty("value").GetString());
        Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task ProcessAsync_AtPrefixedParametersFile_IsMerged()
    {
        string paramsFile = WriteTempFile("at-form.json", """
            { "$schema": "x", "contentVersion": "1.0.0.0", "parameters": { "name": { "value": "from-file" } } }
            """);
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("at-form-template.json", NoParameterTemplate);
        processor.Parameter = ["@" + paramsFile];

        await processor.ProcessAsync();

        JsonElement parameters = DeploymentProperties(Assert.Single(bodies)).GetProperty("parameters");
        Assert.Equal("from-file", parameters.GetProperty("name").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ProcessAsync_BareParametersFilePath_IsMerged()
    {
        string paramsFile = WriteTempFile("bare-form.json", """
            { "parameters": { "name": { "value": "from-bare-path" } } }
            """);
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("bare-form-template.json", NoParameterTemplate);
        processor.Parameter = [paramsFile];

        await processor.ProcessAsync();

        JsonElement parameters = DeploymentProperties(Assert.Single(bodies)).GetProperty("parameters");
        Assert.Equal("from-bare-path", parameters.GetProperty("name").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ProcessAsync_RawJsonParameter_IsMerged()
    {
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("raw-json-template.json", NoParameterTemplate);
        processor.Parameter = ["""{ "name": { "value": "from-json" } }"""];

        await processor.ProcessAsync();

        JsonElement parameters = DeploymentProperties(Assert.Single(bodies)).GetProperty("parameters");
        Assert.Equal("from-json", parameters.GetProperty("name").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ProcessAsync_MixedParameterForms_MergeLeftToRight()
    {
        string paramsFile = WriteTempFile("merge.json", """
            { "parameters": { "name": { "value": "from-file" }, "region": { "value": "eastus" } } }
            """);
        var bodies = new List<string>();
        var processor = CreateProcessor(Recording(new List<string>(), bodies), new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("merge-template.json", NoParameterTemplate);
        // File, then raw JSON, then KEY=VALUE: each later token wins for the keys it names.
        processor.Parameter =
        [
            "@" + paramsFile,
            """{ "name": { "value": "from-json" } }""",
            "NAME=from-inline",
        ];

        await processor.ProcessAsync();

        JsonElement parameters = DeploymentProperties(Assert.Single(bodies)).GetProperty("parameters");
        // Case-insensitive match keeps the file's casing and a single key.
        Assert.Equal("from-inline", parameters.GetProperty("name").GetProperty("value").GetString());
        Assert.Equal("eastus", parameters.GetProperty("region").GetProperty("value").GetString());
        Assert.Equal(2, parameters.EnumerateObject().Count());
    }

    // ── Client-side missing-parameter check ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_RequiredTemplateParameterWithNoValue_FailsBeforeAnyRequest()
    {
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("missing.json", LocationTemplate);

        var error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
        Assert.Contains("location", error.Message);
        // Parameters carrying a defaultValue or marked nullable are not reported as missing.
        Assert.DoesNotContain("sku", error.Message);
        Assert.DoesNotContain("tags", error.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_RequiredTemplateParameterSuppliedInline_IsAccepted()
    {
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateFile = WriteTempFile("supplied.json", LocationTemplate);
        processor.Parameter = ["Location=eastus"];

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_TemplateLink_SkipsTheMissingParameterCheck()
    {
        // A TemplateUri is never downloaded here, so the template's parameters are unknown and the
        // check is left to ARM rather than guessed at.
        var pipeline = Recording(new List<string>(), new List<string>());
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Name = "dep";
        processor.TemplateUri = "https://x/t.json";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "azclips.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
