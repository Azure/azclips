namespace Core.Agent;

/// <summary>
/// The command flavor that the azclips host passes in <c>AZURECLI_FLAVOR</c>:
/// <c>"azure cli"</c> (the default, plan steps are <c>az</c> commands) or
/// <c>"azure powershell"</c> (plan steps are cmdlets). Compare it only here.
/// </summary>
internal static class AgentFlavor
{
    public const string AzureCli = "azure cli";

    public static bool IsAzureCli(string? flavor) =>
        string.Equals(flavor, AzureCli, StringComparison.OrdinalIgnoreCase);
}
