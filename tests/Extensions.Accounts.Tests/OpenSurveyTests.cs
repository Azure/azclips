// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

namespace Extensions.Accounts.Tests;

public class OpenSurveyTests
{
    [Fact]
    public void CreateStartInfo_UsesSharedSurveyUrlAndSystemShell()
    {
        var startInfo = OpenSurveyProcessor.CreateStartInfo();
        string projectVersion = AssemblyVersionProvider.GetVersion(typeof(AccountsLinks).Assembly);

        Assert.Equal($"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc={Uri.EscapeDataString(projectVersion)}", AccountsLinks.Survey);
        Assert.Equal(AccountsLinks.Survey, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
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
}
