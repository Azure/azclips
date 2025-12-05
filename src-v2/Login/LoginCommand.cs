using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Core.Abstractions;

namespace Login;

public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        var action = options.GetValueOrDefault("action", "login");

        if (action == "logout")
        {
            AzureAuthenticationContext.Reset();
            Console.WriteLine("Logged out successfully.");
            return;
        }

        if (action == "show")
        {
            if (!AzureAuthenticationContext.IsAuthenticated)
            {
                Console.WriteLine("Not authenticated. Run 'azv2 login' or 'Connect-AzV2Account' first.");
                return;
            }

            Console.WriteLine($"Subscription ID: {AzureAuthenticationContext.SubscriptionId ?? "Not set"}");
            Console.WriteLine($"Tenant ID: {AzureAuthenticationContext.TenantId ?? "Not set"}");
            return;
        }

        // Login action
        try
        {
            var tenantId = options.GetValueOrDefault("tenant");
            var subscriptionId = options.GetValueOrDefault("subscription");

            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeInteractiveBrowserCredential = false
            };

            if (!string.IsNullOrEmpty(tenantId))
            {
                credentialOptions.TenantId = tenantId;
                AzureAuthenticationContext.TenantId = tenantId;
            }

            var credential = new DefaultAzureCredential(credentialOptions);
            AzureAuthenticationContext.Credential = credential;

            // Test authentication by trying to get subscriptions
            var armClient = new ArmClient(credential);
            var subscriptions = armClient.GetSubscriptions();

            Console.WriteLine("Authentication successful!");
            
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                AzureAuthenticationContext.SubscriptionId = subscriptionId;
                Console.WriteLine($"Using subscription: {subscriptionId}");
            }
            else
            {
                // List available subscriptions
                Console.WriteLine("\nAvailable subscriptions:");
                await foreach (var sub in subscriptions.GetAllAsync(cancellationToken))
                {
                    Console.WriteLine($"  - {sub.Data.DisplayName} ({sub.Data.SubscriptionId})");
                    
                    // Use first subscription as default
                    if (string.IsNullOrEmpty(AzureAuthenticationContext.SubscriptionId))
                    {
                        AzureAuthenticationContext.SubscriptionId = sub.Data.SubscriptionId;
                        Console.WriteLine($"\nUsing subscription: {sub.Data.SubscriptionId}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Authentication failed: {ex.Message}");
            throw;
        }
    }
}
