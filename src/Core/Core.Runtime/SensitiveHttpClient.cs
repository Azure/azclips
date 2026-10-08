namespace Core.Runtime;

public sealed class SensitiveHttpClient(HttpMessageHandler handler, bool disposeHandler) : HttpClient(handler, disposeHandler)
{
	public SensitiveHttpClient(HttpMessageHandler handler) : this(handler, true) { }
}