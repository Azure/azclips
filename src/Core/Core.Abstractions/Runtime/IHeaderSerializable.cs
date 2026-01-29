using System.Net.Http.Headers;

namespace Core.Abstractions.Runtime
{
    public interface IHeaderSerializable
    {
        void ReadHeaders(HttpResponseHeaders headers);
    }
}