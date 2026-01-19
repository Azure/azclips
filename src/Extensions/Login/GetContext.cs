using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class GetContextCommand : IProcessor
    {
        public object? Process()
        {
            var authContextService = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                .GetRequiredService<IAuthContextService>();
            return authContextService.GetCurrentContextAsync().GetAwaiter().GetResult();
        }

        public string Process(bool Serialize)
        {
            var result = (AuthContext?)Process();
            if (result == null)
            {
                return string.Empty; // todo: handle null case properly
            }
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }
    }
}
