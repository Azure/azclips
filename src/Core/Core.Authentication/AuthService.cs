namespace Core.Authentication;

using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;
using Microsoft.Identity.Client;

public class MockAuthService : IAuthService
{
    public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default) => "MOCKED_ACCESS_TOKEN";
}

public class DefaultAuthService : IAuthService
{
    public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        string[] scopes = new string[] { ".default" };

        var app = PublicClientApplicationBuilder.Create("1950a258-227b-4e31-a9cf-717495945fc2")
            .WithDefaultRedirectUri()
            .Build();

        var accounts = await app.GetAccountsAsync();

        AuthenticationResult result;
        try
        {
            result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
            .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            result = await app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false).ExecuteAsync();
        }
        return result.AccessToken;
    }
}


// public class AuthService
// {
//     public string GetAccessTokenRaw()
//     {
//         return string.Empty;
//     }

//     public AccessToken GetAccessTokenRecord()
//     {
//         return new AccessToken();
//     }

//     public IAuthorizable GetAccessTokenInterface()
//     {
//         return new Authorizable();
//     }
// }

// internal class Authorizable : IAuthorizable
// {
//     public void Authorize(HttpRequestMessage request) => throw new NotImplementedException();
// }

// public interface IAuthorizable
// {
//     public void Authorize(HttpRequestMessage request);
// }

// public record AccessToken
// {
// }
