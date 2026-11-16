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

    // Commands the system prompts name directly, by flavor. Prompts use {{key}}.
    public static IReadOnlyDictionary<string, string> PromptCommands(string? flavor) =>
        IsAzureCli(flavor)
            ? new Dictionary<string, string>
            {
                ["login_command"] = "az login --subscription-id <sub-id>",
                ["device_code_flag"] = "--use-device-code",
                ["rest_command"] = "az rest",
            }
            : new Dictionary<string, string>
            {
                ["login_command"] = "Connect-AzAccount -SubscriptionId <sub-id>",
                ["device_code_flag"] = "-UseDeviceCode",
                ["rest_command"] = "Invoke-AzRestMethod",
            };
}
