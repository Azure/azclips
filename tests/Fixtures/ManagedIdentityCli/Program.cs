// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Core.Cli;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 2)
{
    throw new ArgumentException("Expected the CLI artifacts directory followed by command arguments.");
}

using var httpClient = new HttpClient(new SubscriptionHandler());
AzclipsServiceProvider.OverrideRegistration(services =>
{
    services.AddSingleton<ISubscriptionService>(sp =>
        new SubscriptionService(httpClient, sp.GetRequiredService<ILoggingService>()));
    services.AddSingleton<IExtensionLoader>(sp => new BundledExtensionLoader(
        sp.GetRequiredService<ILoggingService>(),
        sp.GetRequiredService<IConfigurationService>(),
        args[0]));
});

return await Core.Cli.Program.Main(args[1..]);

internal sealed class SubscriptionHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string subscriptionId = "a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d";
        if (request.Method != HttpMethod.Get
            || request.RequestUri?.AbsoluteUri != $"https://management.azure.com/subscriptions/{subscriptionId}?api-version=2022-12-01"
            || request.Headers.Authorization is not { Scheme: "Bearer", Parameter.Length: > 0 })
        {
            throw new InvalidOperationException($"Unexpected subscription request: {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"subscriptionId":"{{subscriptionId}}","displayName":"Test","state":"Enabled"}""",
                Encoding.UTF8, "application/json"),
        });
    }
}
