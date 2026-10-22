// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Benchmark.PS;

namespace Cli.Tests;

public class PSManifestStagerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("azclips-ps-manifest-tests-").FullName;

    [Theory]
    [InlineData("RestResponse.format.ps1xml")]
    [InlineData("views/RestResponse.format.ps1xml")]
    public void StageModule_CopiesFormattingAssetsAndDependencies(string formatPath)
    {
        WriteModuleFile("Resources", "Resources.psd1",
            $"@{{ RequiredModules = @(@{{ ModuleName = 'Accounts'; ModuleVersion = '1.0' }}); FormatsToProcess = @('{formatPath}') }}");
        WriteModuleFile("Resources", formatPath, "<Configuration>resources</Configuration>");
        WriteModuleFile("Accounts", "Accounts.psd1",
            "@{ RequiredModules = @(); FormatsToProcess = @('Accounts.format.ps1xml') }");
        WriteModuleFile("Accounts", "Accounts.format.ps1xml", "<Configuration>accounts</Configuration>");

        string manifest = PSManifestStager.StageModule(_root, "Resources");

        string stagedRoot = Path.Combine(_root, "artifacts", "Benchmark", "PS");
        Assert.Equal(Path.Combine(stagedRoot, "Resources", "Resources.psd1"), manifest);
        Assert.Contains($"FormatsToProcess = @('{formatPath}')", File.ReadAllText(manifest));
        Assert.Contains("ModuleName = '../Accounts/Accounts.psd1'", File.ReadAllText(manifest));
        Assert.Equal("<Configuration>resources</Configuration>",
            File.ReadAllText(Path.Combine(stagedRoot, "Resources", formatPath)));
        Assert.Equal("<Configuration>accounts</Configuration>",
            File.ReadAllText(Path.Combine(stagedRoot, "Accounts", "Accounts.format.ps1xml")));
    }

    [Fact]
    public void StageModule_RefreshesExistingFormattingAssets()
    {
        WriteModuleFile("Resources", "Resources.psd1",
            "@{ RequiredModules = @(); FormatsToProcess = @('RestResponse.format.ps1xml') }");
        WriteModuleFile("Resources", "RestResponse.format.ps1xml", "original");
        PSManifestStager.StageModule(_root, "Resources");

        WriteModuleFile("Resources", "RestResponse.format.ps1xml", "updated");
        string manifest = PSManifestStager.StageModule(_root, "Resources");

        Assert.Equal("updated",
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifest)!, "RestResponse.format.ps1xml")));
    }

    private void WriteModuleFile(string moduleName, string relativePath, string content)
    {
        string path = Path.Combine(_root, "src", "PS", moduleName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
