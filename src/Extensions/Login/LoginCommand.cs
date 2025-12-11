using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Login.Commands;

/// <summary>
/// CLI adapter for login command.
/// Handles login, logout, and show context based on action parameter.
/// </summary>
public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        var action = options.ContainsKey("action") ? options["action"] : "login";
        IAuthService authService = serviceProvider.GetRequiredService<IAuthService>();

        try
        {
            if (action == "logout")
            {
                await authService.LogoutAsync(cancellationToken);
                Console.WriteLine("Logged out successfully.");
                return;
            }

            if (action == "show")
            {
                var context = authService.GetCurrentContext();
                if (context == null)
                {
                    Console.WriteLine("Not authenticated. Run 'azv2 login' first.");
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
            var result = await authService.LoginAsync(request, cancellationToken);

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
