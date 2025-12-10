using Login.Business.Models;

namespace Login.Business;

/// <summary>
/// Core business logic for Azure authentication operations.
/// No knowledge of CLI or PowerShell - just pure authentication operations.
/// MOCKUP: Returns simulated responses without actual Azure authentication.
/// </summary>
public static class AuthenticationManager
{
    private static AuthenticationInfo? _currentContext;

    public static Task<AuthenticationInfo> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // MOCKUP: Simulate successful authentication
        var subscriptionId = request.SubscriptionId ?? "00000000-0000-0000-0000-000000000000";
        var tenantId = request.TenantId ?? "11111111-1111-1111-1111-111111111111";

        _currentContext = new AuthenticationInfo
        {
            SubscriptionId = subscriptionId,
            TenantId = tenantId,
            IsAuthenticated = true
        };

        return Task.FromResult(_currentContext);
    }

    public static void Logout()
    {
        // MOCKUP: Clear authentication context
        _currentContext = null;
    }

    public static AuthenticationInfo? GetCurrentContext()
    {
        // MOCKUP: Return current context or null
        return _currentContext;
    }
}
