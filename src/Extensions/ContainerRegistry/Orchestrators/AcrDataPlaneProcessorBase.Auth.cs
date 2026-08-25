using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

public partial class AcrDataPlaneProcessorBase<TResponse>
{
    private async partial Task AuthOverrideAsync()
    {
        string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);
        var tokenProvider = new AcrTokenProvider(AuthHandler, JsonOptions);
        string refreshToken = await tokenProvider.AcquireAcrRefreshTokenAsync(loginServer);

        AuthHandler.ApplyOverride = true;
        AuthHandler.AcquireTokenAsyncOverride = (resource, tenant, forceRefresh, ct, profile) =>
            Task.FromResult(new AuthResult
            {
                AccessToken = refreshToken,
                TenantId = tenant ?? string.Empty,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>()
            });
    }
}