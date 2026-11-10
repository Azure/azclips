// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AccountsModuleInitializer = Microsoft.Azure.Azclips.Cmdlets.Accounts.ModuleInitializer;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class TenantDeploymentCmdletPathTests
{
    [Theory]
    [InlineData("New-AzTenantDeployment", true, true)]
    [InlineData("New-AzTenantDeployment", true, false)]
    [InlineData("New-AzTenantDeployment", false, true)]
    [InlineData("New-AzTenantDeployment", false, false)]
    [InlineData("Test-AzTenantDeployment", true, true)]
    [InlineData("Test-AzTenantDeployment", true, false)]
    [InlineData("Test-AzTenantDeployment", false, true)]
    [InlineData("Test-AzTenantDeployment", false, false)]
    [InlineData("Get-AzTenantDeploymentWhatIfResult", true, true)]
    [InlineData("Get-AzTenantDeploymentWhatIfResult", true, false)]
    [InlineData("Get-AzTenantDeploymentWhatIfResult", false, true)]
    [InlineData("Get-AzTenantDeploymentWhatIfResult", false, false)]
    public void TemplateInputs_ResolveAfterSetLocation(string command, bool relativeTemplate, bool relativeParameters)
    {
        string processDirectory = Directory.GetCurrentDirectory();
        string directory = Path.Combine(Path.GetTempPath(), $"azclips-tenant-paths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        AzclipsServiceProvider.Dispose();
        try
        {
            string templateFile = Path.Combine(directory, "template.json");
            string parametersFile = Path.Combine(directory, "parameters.json");
            const string template = """{"contentVersion":"1.0.0.0","resources":[]}""";
            const string parameters = """{"parameters":{"message":{"value":"from-file"}}}""";
            File.WriteAllText(templateFile, template);
            File.WriteAllText(parametersFile, parameters);
            var session = new Mock<ISession>();
            var engine = new Mock<IEngine>();
            CommandInvocation? captured = null;
            engine.Setup(engine => engine.OpenExecutionSession(
                    It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
                .Returns((CommandInvocation invocation, OutputStreams streams, CancellationToken token) =>
                {
                    captured = invocation;
                    return session.Object;
                });
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(Mock.Of<ITelemetryService>());
            });
            new AccountsModuleInitializer().OnImport();
            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("New-AzTenantDeployment", typeof(NewTenantDeploymentCmdlet), null));
            state.Commands.Add(new SessionStateCmdletEntry("Test-AzTenantDeployment", typeof(TestTenantDeploymentCmdlet), null));
            state.Commands.Add(new SessionStateCmdletEntry("Get-AzTenantDeploymentWhatIfResult", typeof(GetTenantDeploymentWhatIfResultCmdlet), null));
            using var runspace = RunspaceFactory.CreateRunspace(state);
            runspace.Open();
            using var shell = PowerShell.Create();
            shell.Runspace = runspace;
            shell.AddCommand("Set-Location").AddParameter("LiteralPath", directory).Invoke();
            Assert.Empty(shell.Streams.Error);
            shell.Commands.Clear();
            shell.AddCommand(command)
                .AddParameter("DeploymentName", "path-regression")
                .AddParameter("Location", "westus")
                .AddParameter("TemplateFile", relativeTemplate ? @".\template.json" : templateFile)
                .AddParameter("ParametersFile", relativeParameters ? @".\parameters.json" : parametersFile);

            Assert.Empty(shell.Invoke());

            Assert.Empty(shell.Streams.Error);
            Assert.NotNull(captured);
            Assert.Equal("Resources", captured.ExtensionName);
            Assert.Equal(command, captured.CommandName);
            string resolvedTemplate = Assert.IsType<string>(captured.Parameters["TemplateFile"]);
            string resolvedParameters = Assert.IsType<string>(captured.Parameters["ParametersFile"]);
            Assert.Equal(templateFile, resolvedTemplate);
            Assert.Equal(parametersFile, resolvedParameters);
            Assert.Equal(template, File.ReadAllText(resolvedTemplate));
            Assert.Equal(parameters, File.ReadAllText(resolvedParameters));
            Assert.Equal(processDirectory, Directory.GetCurrentDirectory());
            session.Verify(session => session.Run(), Times.Once);
            session.Verify(session => session.Dispose(), Times.Once);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
