using Core.Abstractions;
using Login.Business;
using Login.Business.Models;

namespace Login.Commands;

/// <summary>
/// CLI adapter for login command.
/// Handles login, logout, and show context based on action parameter.
/// </summary>
public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        var action = options.ContainsKey("action") ? options["action"] : "login";

        try
        {
            if (action == "logout")
            {
                AuthenticationManager.Logout();
                Console.WriteLine("Logged out successfully.");
                return;
            }

            if (action == "show")
            {
                var context = AuthenticationManager.GetCurrentContext();
                if (context == null)
                {
                    Console.WriteLine("Not authenticated. Run 'azv3 login' first.");
                    return;
                }

                Console.WriteLine($"Subscription ID: {context.SubscriptionId}");
                Console.WriteLine($"Tenant ID: {context.TenantId}");
                return;
            }

            // Login action
            var request = new LoginRequest
            {
                TenantId = options.ContainsKey("tenant") ? options["tenant"] : null,
                SubscriptionId = options.ContainsKey("subscription") ? options["subscription"] : null
            };

            Console.WriteLine("Authenticating...");
            var result = await AuthenticationManager.LoginAsync(request, cancellationToken);
            
            Console.WriteLine("Authentication successful!");
            Console.WriteLine($"Subscription: {result.SubscriptionId}");
            Console.WriteLine($"Tenant: {result.TenantId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Authentication failed: {ex.Message}");
            throw;
        }
    }
}
