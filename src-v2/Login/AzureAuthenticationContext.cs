using Azure.Core;
using Azure.Identity;
using Core.Abstractions;

namespace Login;

/// <summary>
/// Global authentication context shared across all commands
/// </summary>
public static class AzureAuthenticationContext
{
    private static TokenCredential? _credential;
    private static string? _subscriptionId;
    private static string? _tenantId;

    public static TokenCredential Credential
    {
        get
        {
            if (_credential == null)
            {
                // Default to DefaultAzureCredential which tries multiple auth methods
                _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeVisualStudioCodeCredential = false,
                    ExcludeAzureCliCredential = false,
                    ExcludeEnvironmentCredential = false,
                    ExcludeManagedIdentityCredential = false,
                    ExcludeSharedTokenCacheCredential = false,
                    ExcludeInteractiveBrowserCredential = false
                });
            }
            return _credential;
        }
        set => _credential = value;
    }

    public static string? SubscriptionId
    {
        get => _subscriptionId;
        set => _subscriptionId = value;
    }

    public static string? TenantId
    {
        get => _tenantId;
        set => _tenantId = value;
    }

    public static bool IsAuthenticated => _credential != null;

    public static void Reset()
    {
        _credential = null;
        _subscriptionId = null;
        _tenantId = null;
    }
}
