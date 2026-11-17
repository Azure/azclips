// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Accounts.Tests;

[CollectionDefinition(nameof(OpenSurveyTests), DisableParallelization = true)]
public sealed class OpenSurveyTestsCollection { }

[Collection(nameof(OpenSurveyTests))]
public class OpenSurveyTests : IDisposable
{
    public OpenSurveyTests() => AzclipsServiceProvider.Dispose();

    public void Dispose() => AzclipsServiceProvider.Dispose();

    [Theory]
    [InlineData("2.80.0", "2.80.0")]
    [InlineData("1.0.0-preview+build &test", "1.0.0-preview%2Bbuild%20%26test")]
    public void CreateStartInfo_UsesServiceProductVersionAndSystemShell(string productVersion, string encodedVersion)
    {
        var service = new TestAssemblyLoadingService(productVersion);
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton<IAssemblyLoadingService>(service));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        var startInfo = OpenSurveyProcessor.CreateStartInfo();
        Assert.Equal(
            $"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc={encodedVersion}",
            startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(1, service.ProductVersionReadCount);
        Assert.Equal(AccountsLinks.Survey, startInfo.FileName);
    }

    [Fact]
    public void Survey_ReadsServiceVersionOnEachAccess()
    {
        var service = new TestAssemblyLoadingService("2.80.0");
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton<IAssemblyLoadingService>(service));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"));

        Assert.EndsWith("=2.80.0", AccountsLinks.Survey);
        service.ProductVersion = "0.1.4";
        Assert.EndsWith("=0.1.4", AccountsLinks.Survey);
        Assert.Equal(2, service.ProductVersionReadCount);
    }

    [Theory]
    [InlineData(ClientType.CLI, "survey")]
    [InlineData(ClientType.PowerShell, "Open-AzSurvey")]
    public void CommandSurface_UsesOpenSurveyProcessor(ClientType clientType, string commandName)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata(commandName, clientType);

        Assert.Equal(typeof(OpenSurveyProcessor), metadata.ProcessorType);
        Assert.Empty(metadata.Parameters);
    }

    private sealed class TestAssemblyLoadingService(string productVersion) : IAssemblyLoadingService
    {
        public string ProductVersion { get; set; } = productVersion;
        public int ProductVersionReadCount { get; private set; }

        public string GetProductVersion()
        {
            ProductVersionReadCount++;
            return ProductVersion;
        }

        public Assembly LoadExtension(string extensionName, string? extensionVersion = null) =>
            throw new NotSupportedException();

        public IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions() =>
            throw new NotSupportedException();
    }
}
