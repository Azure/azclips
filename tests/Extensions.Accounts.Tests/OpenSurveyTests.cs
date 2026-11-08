// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

namespace Extensions.Accounts.Tests;

public class OpenSurveyTests
{
    [Theory]
    [InlineData("2.80.0", "2.80.0")]
    [InlineData("1.0.0-preview+build &test", "1.0.0-preview%2Bbuild%20%26test")]
    public void CreateStartInfo_UsesServiceProductVersionAndSystemShell(string productVersion, string encodedVersion)
    {
        var service = new TestAssemblyLoadingService(productVersion);
        var startInfo = OpenSurveyProcessor.CreateStartInfo(service);
        Assert.Equal(
            $"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc={encodedVersion}",
            startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(1, service.ProductVersionReadCount);
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
        public int ProductVersionReadCount { get; private set; }

        public string GetProductVersion()
        {
            ProductVersionReadCount++;
            return productVersion;
        }

        public Assembly LoadExtension(string extensionName, string? extensionVersion = null) =>
            throw new NotSupportedException();

        public IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions() =>
            throw new NotSupportedException();
    }
}
