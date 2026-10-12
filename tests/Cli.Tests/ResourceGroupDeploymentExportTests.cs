// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline processor tests for deployment export (Save-AzResourceGroupDeploymentTemplate / az deployment group export): the request sent, response
/// handling, errors and output, against a fake HTTP pipeline.
/// </summary>
public class ResourceGroupDeploymentExportTests
{
    private const string ExportBody = """{"template":{"contentVersion":"1.0.0.0","resources":[]}}""";

    private static SaveResourceGroupDeploymentTemplateProcessor_Orchestrator Save(
        FakeHttpPipeline pipeline, FakeOutputHandler output, string path, bool force = false) =>
        Configure(new SaveResourceGroupDeploymentTemplateProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
            p.Path = path;
            p.Force = force;
        });

    private static string TempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "azclips-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public async Task Export_PostsExportTemplate_SavesTemplateFile_AndWritesPath()
    {
        string directory = TempDirectory();
        try
        {
            var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, ExportBody));

            await Save(pipeline, output, Path.Combine(directory, "out")).ProcessAsync();

            var request = Assert.Single(sent);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(DeploymentUri + "/exportTemplate?api-version=2025-04-01", request.Uri);

            string expected = Path.Combine(directory, "out.json");
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(output.WrittenValues));
            Assert.Equal(expected, saved.Path);
            var template = JsonDocument.Parse(File.ReadAllText(expected)).RootElement;
            Assert.Equal("1.0.0.0", template.GetProperty("contentVersion").GetString());
            Assert.False(template.TryGetProperty("template", out _), "The ARM envelope must be unwrapped.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_DirectoryPath_SavesAsDeploymentNameJson()
    {
        string directory = TempDirectory();
        try
        {
            var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, ExportBody));

            await Save(pipeline, output, directory).ProcessAsync();

            Assert.True(File.Exists(Path.Combine(directory, "dep.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_MissingParentDirectory_IsCreated()
    {
        string directory = TempDirectory();
        try
        {
            var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, ExportBody));

            await Save(pipeline, output, Path.Combine(directory, "a", "b", "out")).ProcessAsync();

            Assert.True(File.Exists(Path.Combine(directory, "a", "b", "out.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_ExistingFileWithoutForce_ThrowsAndKeepsFile_ForceOverwrites()
    {
        string directory = TempDirectory();
        try
        {
            string file = Path.Combine(directory, "existing.json");
            File.WriteAllText(file, "original");

            var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, ExportBody));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(pipeline, output, file).ProcessAsync());
            Assert.Contains("already exists", error.Message);
            Assert.Equal("original", File.ReadAllText(file));
            Assert.Empty(output.WrittenValues);

            var (forcePipeline, forceOutput, _) = Fake(_ => Status(HttpStatusCode.OK, ExportBody));
            await Save(forcePipeline, forceOutput, file, force: true).ProcessAsync();
            Assert.Contains("contentVersion", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_ResponseWithoutTemplate_ThrowsAndWritesNoFile()
    {
        string directory = TempDirectory();
        try
        {
            var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, "{}"));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(pipeline, output, Path.Combine(directory, "out")).ProcessAsync());

            Assert.Contains("did not contain a template", error.Message);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Export_ArmNotFound_ThrowsAndWritesNoFile()
    {
        string directory = TempDirectory();
        try
        {
            var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.NotFound, """{"error":{"code":"DeploymentNotFound","message":"missing"}}"""));

            var error = await Assert.ThrowsAsync<HttpRequestException>(() => Save(pipeline, output, Path.Combine(directory, "out")).ProcessAsync());

            Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
