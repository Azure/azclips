using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

[BenchmarkExtension("KeyVault")]
public class KeyVaultBenchmarks : CLIBenchmarkBase
{
    protected override string[] AuthenticationScopes => ["https://vault.azure.net/.default"];

    [Benchmark(Description = "CLI: Set secret")]
    public int SetSecret() =>
        RunCli("keyvault", "secret", "set", "--vault-name", "bench-vault", "--name", "bench-secret", "--value", "synthetic-benchmark-value");

    [Benchmark(Description = "CLI: Get secret")]
    public int GetSecret() =>
        RunCli("keyvault", "secret", "show", "--vault-name", "bench-vault", "--name", "bench-secret");

    [Benchmark(Description = "CLI: List secrets")]
    public int ListSecrets() =>
        RunCli("keyvault", "secret", "list", "--vault-name", "bench-vault", "--maxresults", "1");

    [Benchmark(Description = "CLI: Create certificate")]
    public int CreateCertificate() =>
        RunCli("keyvault", "certificate", "create", "--vault-name", "bench-vault", "--name", "bench-certificate", "--policy", "{\"issuer\":{\"name\":\"Self\"}}");

    [Benchmark(Description = "CLI: Get certificate")]
    public int GetCertificate() =>
        RunCli("keyvault", "certificate", "show", "--vault-name", "bench-vault", "--name", "bench-certificate");

    [Benchmark(Description = "CLI: List certificates")]
    public int ListCertificates() =>
        RunCli("keyvault", "certificate", "list", "--vault-name", "bench-vault", "--maxresults", "1");
}