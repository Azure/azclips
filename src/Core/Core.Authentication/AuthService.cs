namespace Core.Authentication;

using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;
using Microsoft.Identity.Client;


public class DefaultAuthService : IAuthService
{
    public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        string[] scopes = [".default"];

        // todo: what should be the client id here?s
        var app = PublicClientApplicationBuilder.Create("1950a258-227b-4e31-a9cf-717495945fc2")
            .WithDefaultRedirectUri()
            .Build();

        var accounts = await app.GetAccountsAsync();

        AuthenticationResult result;
        try
        {
            result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            result = await app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false).ExecuteAsync();
        }
        return result.AccessToken;
    }
}
