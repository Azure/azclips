using System.Net;
using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

[BenchmarkExtension("KeyVault")]
public class KeyVaultBenchmarks : PSBenchmarkBase
{
    protected override string[] AuthenticationScopes => ["https://vault.azure.net/.default"];

    [Benchmark(Description = "PS: Set secret")]
    public object? SetSecret()
    {
        using var value = new NetworkCredential(string.Empty, "synthetic-benchmark-value").SecurePassword;
        return RunPowerShellCommand("Set-AzKeyVaultSecret",
            ("VaultName", "bench-vault"), ("Name", "bench-secret"), ("SecretValue", value), ("Confirm", false));
    }

    [Benchmark(Description = "PS: Get secret")]
    public object? GetSecret() =>
        RunPowerShellCommand("Get-AzKeyVaultSecret", ("VaultName", "bench-vault"), ("Name", "bench-secret"));

    [Benchmark(Description = "PS: List secrets")]
    public object? ListSecrets() =>
        RunPowerShellCommand("Get-AzKeyVaultSecret", ("VaultName", "bench-vault"), ("MaxResults", 1));

    [Benchmark(Description = "PS: Create certificate")]
    public object? CreateCertificate() =>
        RunPowerShellCommand("Add-AzKeyVaultCertificate",
            ("VaultName", "bench-vault"), ("Name", "bench-certificate"), ("Policy", "{\"issuer\":{\"name\":\"Self\"}}"), ("Wait", true), ("Confirm", false));

    [Benchmark(Description = "PS: Get certificate")]
    public object? GetCertificate() =>
        RunPowerShellCommand("Get-AzKeyVaultCertificate", ("VaultName", "bench-vault"), ("Name", "bench-certificate"));

    [Benchmark(Description = "PS: List certificates")]
    public object? ListCertificates() =>
        RunPowerShellCommand("Get-AzKeyVaultCertificate", ("VaultName", "bench-vault"), ("MaxResults", 1));
}