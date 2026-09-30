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
/// Offline tests for <see cref="SaveResourceGroupDeploymentTemplateProcessor_Orchestrator"/>: the
/// ARM export call is faked, and the assertions are about the local file handling the orchestrator
/// adds on top of it — where the file lands and what happens when it is already there.
/// </summary>
public class SaveResourceGroupDeploymentTemplateProcessorTests : IDisposable
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private const string ExportResponse = """
        { "template": { "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#", "contentVersion": "1.0.0.0", "resources": [] } }
        """;

    private readonly string tempDirectory = Directory.CreateTempSubdirectory("azclips-save-template-tests-").FullName;

    public void Dispose() => Directory.Delete(tempDirectory, recursive: true);

    private static SaveResourceGroupDeploymentTemplateProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new SaveResourceGroupDeploymentTemplateProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "my-rg";
        processor.Name = "dep1";
        return processor;
    }

    private static FakeHttpPipeline Exporting() =>
        new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ExportResponse, Encoding.UTF8, "application/json"),
        });

    private static string WrittenPath(FakeOutputHandler output) =>
        Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(output.WrittenValues)).Path;

    [Fact]
    public async Task ProcessAsync_ExistingDirectoryPath_WritesDeploymentNameDotJsonInsideIt()
    {
        var output = new FakeOutputHandler();
        SaveResourceGroupDeploymentTemplateProcessor_Orchestrator processor = CreateProcessor(Exporting(), output);
        processor.Path = tempDirectory;

        await processor.ProcessAsync();

        string expected = Path.Combine(tempDirectory, "dep1.json");
        Assert.Equal(expected, WrittenPath(output));
        Assert.True(File.Exists(expected));
        Assert.Equal(
            "1.0.0.0",
            JsonDocument.Parse(File.ReadAllText(expected)).RootElement.GetProperty("contentVersion").GetString());
    }

    [Fact]
    public async Task ProcessAsync_FilePathWithoutAnExtension_GainsTheJsonExtension()
    {
        var output = new FakeOutputHandler();
        SaveResourceGroupDeploymentTemplateProcessor_Orchestrator processor = CreateProcessor(Exporting(), output);
        processor.Path = Path.Combine(tempDirectory, "exported");

        await processor.ProcessAsync();

        Assert.Equal(Path.Combine(tempDirectory, "exported.json"), WrittenPath(output));
    }

    [Fact]
    public async Task ProcessAsync_FilePathAlreadyEndingInJson_IsUsedAsIs()
    {
        var output = new FakeOutputHandler();
        SaveResourceGroupDeploymentTemplateProcessor_Orchestrator processor = CreateProcessor(Exporting(), output);
        string expected = Path.Combine(tempDirectory, "exported.json");
        processor.Path = expected;

        await processor.ProcessAsync();

        Assert.Equal(expected, WrittenPath(output));
    }

    [Fact]
    public async Task ProcessAsync_MissingIntermediateDirectory_IsCreated()
    {
        var output = new FakeOutputHandler();
        SaveResourceGroupDeploymentTemplateProcessor_Orchestrator processor = CreateProcessor(Exporting(), output);
        string expected = Path.Combine(tempDirectory, "nested", "deep", "exported.json");
        processor.Path = expected;

        await processor.ProcessAsync();

        Assert.True(File.Exists(expected));
    }

    [Fact]
    public async Task ProcessAsync_ExistingFileWithoutForce_Throws()
    {
        string existing = Path.Combine(tempDirectory, "exported.json");
        File.WriteAllText(existing, "original");
        var processor = CreateProcessor(Exporting(), new FakeOutputHandler());
        processor.Path = existing;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);
        Assert.Contains("-Force", error.Message);
        Assert.Equal("original", File.ReadAllText(existing));
    }

    [Fact]
    public async Task ProcessAsync_ExistingFileWithForce_Overwrites()
    {
        string existing = Path.Combine(tempDirectory, "exported.json");
        File.WriteAllText(existing, "original");
        var output = new FakeOutputHandler();
        SaveResourceGroupDeploymentTemplateProcessor_Orchestrator processor = CreateProcessor(Exporting(), output);
        processor.Path = existing;
        processor.Force = true;

        await processor.ProcessAsync();

        Assert.Equal(existing, WrittenPath(output));
        Assert.Contains("contentVersion", File.ReadAllText(existing));
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithoutATemplate_Throws()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        processor.Path = Path.Combine(tempDirectory, "exported.json");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);
        Assert.Contains("dep1", error.Message);
        Assert.False(File.Exists(Path.Combine(tempDirectory, "exported.json")));
    }
}
