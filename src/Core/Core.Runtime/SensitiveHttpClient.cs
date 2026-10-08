namespace Core.Runtime;

public sealed class SensitiveHttpClient(HttpMessageHandler handler, bool disposeHandler = true) : HttpClient(handler, disposeHandler);