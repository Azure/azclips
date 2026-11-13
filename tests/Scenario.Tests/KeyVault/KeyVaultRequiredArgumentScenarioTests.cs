using System.Management.Automation;
using System.Security;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.KeyVault;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class KeyVaultRequiredArgumentScenarioTests
{
    public static TheoryData<string[], string[]> CliCommandsMissingRequiredArguments => new()
    {
        { ["secret", "set", "--value", "fixture-value"], ["--name"] },
        { ["secret", "show"], ["--name"] },
        { ["secret", "list-versions"], ["--name"] },
        { ["secret", "set-attributes", "--enabled", "true"], ["--name"] },
        { ["secret", "delete", "--yes"], ["--name"] },
        { ["certificate", "create", "--policy", "{}"], ["--name"] },
        { ["certificate", "import", "--name", "test"], ["--file"] },
        { ["certificate", "import"], ["--name", "--file"] },
        { ["certificate", "show"], ["--name"] },
        { ["certificate", "list-versions"], ["--name"] },
        { ["certificate", "set-attributes", "--enabled", "true"], ["--name"] },
        { ["certificate", "download", "--name", "test"], ["--file"] },
        { ["certificate", "delete", "--yes"], ["--name"] },
    };

    [Theory]
    [MemberData(nameof(CliCommandsMissingRequiredArguments))]
    public void CliRejectsMissingRequiredArgumentsBeforeSending(string[] command, string[] missingFlags)
    {
        using var backend = new CountingBackend();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);

        var thrown = Assert.ThrowsAny<Exception>(() => host.RunCli(["keyvault", .. command, "--vault-name", "fixture"]));

        var usageError = FindUsageError(thrown);
        Assert.Equal(2, usageError.ExitCode);
        foreach (string flag in missingFlags) Assert.Contains($"'{flag}'", usageError.Message);
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public void PowerShellSetSecretRequiresName() =>
        AssertMissingMandatory<SetAzKeyVaultSecretCmdlet>("Name", new() { ["SecretValue"] = Secure("fixture-value") });

    [Fact]
    public void PowerShellSetSecretRequiresSecretValue() =>
        AssertMissingMandatory<SetAzKeyVaultSecretCmdlet>("SecretValue", new() { ["Name"] = "test" });

    [Fact]
    public void PowerShellGetSecretVersionsRequiresName() =>
        AssertMissingMandatory<GetAzKeyVaultSecretCmdlet>("Name", new() { ["IncludeVersions"] = true });

    [Fact]
    public void PowerShellUpdateSecretRequiresName() =>
        AssertMissingMandatory<UpdateAzKeyVaultSecretCmdlet>("Name", new() { ["Enabled"] = true });

    [Fact]
    public void PowerShellRemoveSecretRequiresName() =>
        AssertMissingMandatory<RemoveAzKeyVaultSecretCmdlet>("Name", new() { ["Confirm"] = false });

    [Fact]
    public void PowerShellUndoSecretRemovalRequiresName() =>
        AssertMissingMandatory<UndoAzKeyVaultSecretRemovalCmdlet>("Name", new() { ["Confirm"] = false });

    [Fact]
    public void PowerShellAddCertificateRequiresName() =>
        AssertMissingMandatory<AddAzKeyVaultCertificateCmdlet>("Name", new() { ["Policy"] = "{}" });

    [Fact]
    public void PowerShellImportCertificateRequiresFilePath() =>
        AssertMissingMandatory<ImportAzKeyVaultCertificateCmdlet>("FilePath", new() { ["Name"] = "test" });

    [Fact]
    public void PowerShellGetCertificateVersionsRequiresName() =>
        AssertMissingMandatory<GetAzKeyVaultCertificateCmdlet>("Name", new() { ["IncludeVersions"] = true });

    [Fact]
    public void PowerShellUpdateCertificateRequiresName() =>
        AssertMissingMandatory<UpdateAzKeyVaultCertificateCmdlet>("Name", new() { ["Enabled"] = true });

    [Fact]
    public void PowerShellExportCertificateRequiresName() =>
        AssertMissingMandatory<ExportAzKeyVaultCertificateCmdlet>("Name", new() { ["FilePath"] = "fixture.pem" });

    [Fact]
    public void PowerShellExportCertificateRequiresFilePath() =>
        AssertMissingMandatory<ExportAzKeyVaultCertificateCmdlet>("FilePath", new() { ["Name"] = "test" });

    [Fact]
    public void PowerShellRemoveCertificateRequiresName() =>
        AssertMissingMandatory<RemoveAzKeyVaultCertificateCmdlet>("Name", new() { ["Confirm"] = false });

    [Fact]
    public void PowerShellUndoCertificateRemovalRequiresName() =>
        AssertMissingMandatory<UndoAzKeyVaultCertificateRemovalCmdlet>("Name", new() { ["Confirm"] = false });

    private static void AssertMissingMandatory<TCmdlet>(string missingParameter, Dictionary<string, object> parameters)
        where TCmdlet : PSCmdlet
    {
        using var backend = new CountingBackend();
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        parameters["VaultName"] = "fixture";

        var thrown = Assert.Throws<ParameterBindingException>(() => host.RunPowerShellCmdlet<TCmdlet>(parameters));

        Assert.Contains("missing mandatory parameters", thrown.Message);
        Assert.Contains(missingParameter, thrown.Message);
        Assert.Equal(0, backend.Calls);
    }

    private static UsageErrorException FindUsageError(Exception thrown)
    {
        for (Exception? current = thrown; current is not null; current = current.InnerException)
            if (current is UsageErrorException usageError) return usageError;
        throw new Xunit.Sdk.XunitException($"Expected a usage error but got: {thrown}");
    }

    private static SecureString Secure(string value)
    {
        var secure = new SecureString();
        foreach (char character in value) secure.AppendChar(character);
        secure.MakeReadOnly();
        return secure;
    }

    private sealed class CountingBackend : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { RequestMessage = request });
        }
    }
}
