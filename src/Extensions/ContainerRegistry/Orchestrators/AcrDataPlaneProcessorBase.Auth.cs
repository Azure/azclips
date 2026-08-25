using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

public partial class AcrDataPlaneProcessorBase<TResponse>
{
    private async partial Task AuthOverrideAsync()
    {
        string? tenantId = AuthHandler.Profile?.TenantId;
        Profile? profile = AuthHandler.Profile;
        string ArmAT = (await AuthHandler.AcquireTokenAsyncDefault(null, tenantId, false, default, profile)).AccessToken;

        ExchangeAadAccessTokenForAcrRefreshTokenProcessor refreshTokenProcessor = CreateExtendedProcessor<ExchangeAadAccessTokenForAcrRefreshTokenProcessor>(p =>
        {
            p.Tenant = tenantId;
            p.AccessToken = ArmAT;
        });

        using HttpResponseMessage refreshTokenResponse = await refreshTokenProcessor.SendAsync();
        AuthHandler.ApplyOverride = true;
        AuthHandler.AcquireTokenAsyncOverride = async (resource, tenant, forceRefresh, cancellationToken, profile) =>
        {
            return new AuthResult
            {
                AccessToken = (await refreshTokenResponse.Content.ReadFromJsonAsync<AcrRefreshToken>(JsonOptions))!.Refresh_token!,
                TenantId = tenantId ?? string.Empty,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>()
            };
        };
    }
}