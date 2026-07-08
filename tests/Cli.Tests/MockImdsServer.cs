// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Cli.Tests;

/// <summary>Minimal fake IMDS endpoint for managed identity E2E tests.</summary>
internal sealed class MockImdsServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _serverTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _accessToken;
    private readonly string _defaultClientId;

    /// <summary>Base URL for the fake IMDS endpoint.</summary>
    public string Url { get; }

    public MockImdsServer(string accessToken, string defaultClientId)
    {
        _accessToken = accessToken;
        _defaultClientId = defaultClientId;

        // bind right after choosing a free port; retry on races
        _listener = StartOnFreePort(out string boundUrl);
        Url = boundUrl;

        _serverTask = Task.Run(() => RunAsync(_cts.Token));
    }

    private static HttpListener StartOnFreePort(out string url)
    {
        const int maxAttempts = 10;
        for (int attempt = 1; ; attempt++)
        {
            int port = GetFreeTcpPort();
            url = $"http://127.0.0.1:{port}";
            var listener = new HttpListener();
            listener.Prefixes.Add($"{url}/");
            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException) when (attempt < maxAttempts)
            {
                // port was grabbed; try again
                listener.Close();
            }
        }
    }

    private static int GetFreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.Url?.AbsolutePath.Contains("/metadata/identity/oauth2/token") == true)
                {
                    HandleImdsTokenRequest(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException)
            {
                // listener stopped
                break;
            }
            catch (Exception)
            {
                // ignore shutdown races
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                throw;
            }
        }
    }

    private void HandleImdsTokenRequest(HttpListenerContext context)
    {
        // require the IMDS metadata header
        var metadataHeader = context.Request.Headers["Metadata"];
        if (metadataHeader != "true")
        {
            context.Response.StatusCode = 400;
            var errorBytes = Encoding.UTF8.GetBytes("Metadata header required");
            context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
            return;
        }

        var query = context.Request.Url?.Query;
        var resource = GetQueryParam(query, "resource") ?? "https://management.azure.com";
        var clientId = GetQueryParam(query, "client_id");

        var expiresOn = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var notBefore = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var response = new
        {
            access_token = _accessToken,
            client_id = clientId ?? _defaultClientId,
            expires_in = "3600",
            expires_on = expiresOn.ToString(),
            ext_expires_in = "3600",
            not_before = notBefore.ToString(),
            resource,
            token_type = "Bearer"
        };

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        context.Response.OutputStream.Write(jsonBytes, 0, jsonBytes.Length);
        context.Response.Close();
    }

    private static string? GetQueryParam(string? query, string paramName)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        var pairs = query.TrimStart('?').Split('&');
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=');
            if (keyValue.Length == 2 && keyValue[0] == paramName)
            {
                return Uri.UnescapeDataString(keyValue[1]);
            }
        }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();

        try
        {
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        _listener.Close();
        _cts.Dispose();
    }
}
