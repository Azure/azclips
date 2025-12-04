namespace Core.Authentication;

using System.Threading;
using System.Threading.Tasks;
using Core.Abstractions;

public class AuthService : IAuthService
{
    public Task<string> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult("MOCKED_ACCESS_TOKEN");
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
