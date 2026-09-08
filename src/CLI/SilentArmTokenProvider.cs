// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Cli;

/// <summary>
/// Attempts to silently acquire a cached ARM access token for the active profile's tenant.
/// Used by the <c>--upload-telemetry</c> background process to populate the data-boundary
/// cache without prompting the user.
/// </summary>
internal static class SilentArmTokenProvider
{
    private const string ArmDefaultScope = "https://management.azure.com/.default";
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";

    /// <summary>
    /// Returns a cached ARM access token, or <c>null</c> if none is available.
    /// Never prompts the user or makes interactive requests.
    /// </summary>
    internal static string? TryAcquire()
    {
        try
        {
            var cacheDirectory = HomePaths.GetDefaultLocalAppDataRoot();
            var app = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
                .Build();

            RegisterCacheSync(app, cacheDirectory);

            var accounts = app.GetAccountsAsync().GetAwaiter().GetResult();
            var account = accounts.FirstOrDefault();
            if (account is null)
                return null;

            var result = app.AcquireTokenSilent(new[] { ArmDefaultScope }, account)
                .ExecuteAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            return result.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    private static void RegisterCacheSync(IPublicClientApplication app, string cacheDirectory)
    {
        var storageProperties = new StorageCreationPropertiesBuilder(
                "MSAL.cache.plaintext", cacheDirectory)
            .WithUnprotectedFile()
            .Build();

        var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties)
            .GetAwaiter()
            .GetResult();

        cacheHelper.RegisterCache(app.UserTokenCache);
    }
}
