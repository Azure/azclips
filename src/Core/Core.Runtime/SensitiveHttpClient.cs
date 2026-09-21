namespace Core.Runtime;

public sealed class SensitiveHttpClient(HttpMessageHandler handler) : HttpClient(handler);