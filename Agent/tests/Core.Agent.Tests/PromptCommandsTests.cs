namespace Core.Agent.Tests;

public class PromptCommandsTests
{
    [Theory]
    [InlineData("oneshot")]
    [InlineData("inline")]
    public void LoadSystemPrompt_CliFlavor_UsesAzCommands(string entrypoint)
    {
        var prompt = ChatAgent.LoadSystemPrompt(AgentFlavor.AzureCli, entrypoint);

        Assert.DoesNotContain("{{", prompt);
        Assert.Contains("`az login --subscription-id <sub-id>`", prompt);
        Assert.Contains("`--use-device-code`", prompt);
        Assert.Contains("`az rest`", prompt);
        Assert.DoesNotContain("Invoke-AzRestMethod", prompt);
    }

    [Theory]
    [InlineData("oneshot")]
    [InlineData("inline")]
    public void LoadSystemPrompt_PowerShellFlavor_UsesCmdlets(string entrypoint)
    {
        var prompt = ChatAgent.LoadSystemPrompt("azure powershell", entrypoint);

        Assert.DoesNotContain("{{", prompt);
        Assert.Contains("`Connect-AzAccount -SubscriptionId <sub-id>`", prompt);
        Assert.Contains("`-UseDeviceCode`", prompt);
        Assert.Contains("`Invoke-AzRestMethod`", prompt);
        Assert.DoesNotContain("az rest", prompt);
        Assert.DoesNotContain("az login", prompt);
    }
}
