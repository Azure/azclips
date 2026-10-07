namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

public abstract partial class KeyVaultDataPlaneProcessorBase
{
    private protected partial void EnsureExecutionAllowed()
    {
        throw new NotSupportedException(
            "Key Vault data-plane execution is not enabled. Authentication, endpoint validation, and diagnostic redaction must be implemented before exposing commands.");
    }
}