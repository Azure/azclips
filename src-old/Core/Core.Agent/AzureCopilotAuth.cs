using Azure.Core;
using Azure.Identity;

namespace Core.Agent;

/// <summary>
/// Authentication helper for Azure Copilot Agent.
/// Provides multiple authentication methods with interactive browser login as fallback.
/// </summary>
public static class AzureCopilotAuth
{
    private const string CopilotResourceId = "api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3";
    
    /// <summary>
    /// Gets a TokenCredential with multiple authentication methods.
    /// Tries in order: Environment variables, Azure CLI, Device code flow (user-friendly for dev containers).
    /// </summary>
    public static TokenCredential GetCredential()
    {
        string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        
        // Create a chain of credentials (tries each in order until one succeeds):
        // 1. Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)
        // 2. Azure CLI (if already logged in with 'az login')
        // 3. Device code flow (displays a code and URL for user to login in browser)
        var credentials = new List<TokenCredential>
        {
            new EnvironmentCredential(),
            CreateAzureCliCredential(tenantId),
            CreateDeviceCodeCredential(tenantId)
        };
        
        return new ChainedTokenCredential(credentials.ToArray());
    }
    
    /// <summary>
    /// Gets a TokenCredential that ONLY uses Azure CLI.
    /// </summary>
    public static TokenCredential GetAzureCliCredential()
    {
        string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        return CreateAzureCliCredential(tenantId);
    }
    
    private static AzureCliCredential CreateAzureCliCredential(string? tenantId)
    {
        var options = new AzureCliCredentialOptions();
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            options.TenantId = tenantId;
        }
        
        return new AzureCliCredential(options);
    }
    
    /// <summary>
    /// Gets a TokenCredential that ONLY uses device code flow.
    /// Perfect for headless/dev container environments.
    /// </summary>
    public static TokenCredential GetDeviceCodeCredential()
    {
        string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        return CreateDeviceCodeCredential(tenantId);
    }
    
    private static DeviceCodeCredential CreateDeviceCodeCredential(string? tenantId)
    {
        var options = new DeviceCodeCredentialOptions
        {
            // Disable automatic persistence to avoid issues in dev containers
            DisableInstanceDiscovery = true,
            DeviceCodeCallback = (code, cancellation) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  AZURE AUTHENTICATION REQUIRED");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine($"  Please visit: {code.VerificationUri}");
                Console.WriteLine();
                Console.WriteLine($"  And enter code: {code.UserCode}");
                Console.WriteLine();
                Console.WriteLine($"  Scope: api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3/.default");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();
                return Task.CompletedTask;
            }
        };
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            options.TenantId = tenantId;
        }
        
        return new DeviceCodeCredential(options);
    }
    
    /// <summary>
    /// Validates that the credential can successfully get a token for the Copilot API.
    /// </summary>
    public static async Task<bool> ValidateCredentialAsync(TokenCredential credential)
    {
        try
        {
            var tokenContext = new TokenRequestContext(new[] { $"{CopilotResourceId}/.default" });
            var token = await credential.GetTokenAsync(tokenContext, CancellationToken.None);
            return !string.IsNullOrEmpty(token.Token);
        }
        catch
        {
            return false;
        }
    }
}
