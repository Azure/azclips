// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Core.Agent.Tests;

/// <summary>
/// The real SDK connects only to this loopback JSON-RPC peer. No native daemon, credentials,
/// model, Azure endpoint, or SDK private/reflection API is involved.
/// </summary>
/// <remarks>
/// The receive loop runs on a background Task that nothing awaits until teardown. Every failure in
/// it therefore used to surface as "the server would not shut down", blamed on whichever test was
/// running at the time rather than the one whose traffic it could not parse. So the loop never
/// lets an exception escape: it records the failure with the payload that caused it, and
/// <see cref="ThrowIfFaulted"/> reports it against the test that asks.
/// </remarks>
internal sealed class OfflineCopilotServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _run;
    private readonly TaskCompletionSource _firstToolHandled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completed only on a fault path. Nothing else may complete it, or ThrowIfFaulted throws on
    // every test.
    private readonly TaskCompletionSource<Exception> _fault = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string _sessionId = "";
    private int _pending;

    internal OfflineCopilotServer()
    {
        _listener.Start();
        Url = $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _run = RunAsync();
    }

    internal string Url { get; }
    internal ToolCall[] Calls { get; init; } = [];
    internal List<JsonElement> ToolResults { get; } = [];
    internal bool RejectNextSend { get; set; }
    internal bool RejectCreate { get; init; }
    internal int CreateCount { get; private set; }
    internal int SendCount { get; private set; }
    internal Task FirstToolHandled => _firstToolHandled.Task;

    /// <summary>
    /// When set, a <c>session.error</c> event is emitted straight after a turn's tool calls are
    /// dispatched, while the send is still waiting for idle. The payload carries the two fields the
    /// SDK marks required (<c>errorType</c>, <c>message</c>); any other shape fails deserialisation
    /// and the event never reaches the host at all.
    /// </summary>
    internal string? SessionErrorAfterDispatch { get; init; }

    /// <summary>Completes with the first failure the receive loop could not handle.</summary>
    internal Task Faulted => _fault.Task;

    /// <summary>
    /// Surfaces a receive-loop failure against the caller. Tests call this explicitly at the end of
    /// the test body rather than having DisposeAsync throw: <c>await using</c> compiles to a
    /// try/finally, so a body that fails an assertion while dispose also throws would report only
    /// the dispose exception -- "the fake server crashed" in place of the real failure, which is a
    /// worse version of the misattribution this exists to fix.
    /// </summary>
    internal void ThrowIfFaulted()
    {
        if (_fault.Task.IsCompleted)
            throw new InvalidOperationException(
                "Offline SDK server faulted — see inner exception for the message it could not handle.",
                _fault.Task.Result);
    }

    private void Fault(string reason, Exception? cause = null) =>
        _fault.TrySetResult(new InvalidOperationException($"Offline SDK server: {reason}", cause));

    private async Task RunAsync()
    {
        string? handling = null;
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await using var stream = client.GetStream();
            while (!_stop.IsCancellationRequested)
            {
                // Cleared before the read, not just set after it. Left stale, a failure that is not
                // a dispatch failure -- a malformed next frame, a write that fails, a socket that
                // drops while _stop is not cancelled -- would be reported against the previous
                // message, which had already been answered correctly.
                handling = null;
                using var message = await ReadAsync(stream, _stop.Token);
                if (message is null)
                    return;
                var request = message.RootElement;
                handling = request.GetRawText();
                if (!request.TryGetProperty("method", out var methodElement))
                {
                    Fault($"request carried no 'method': {handling}");
                    continue;
                }
                var method = methodElement.GetString();
                if (!request.TryGetProperty("id", out var idElement))
                {
                    // A JSON-RPC notification: no id, no reply expected. The SDK sends these on the
                    // abort path, and this fake has nothing to do with any of them. Reading id
                    // unconditionally made one crash the receive loop, and that crash only surfaced
                    // at server shutdown -- the same trap session.detach fell into.
                    continue;
                }
                var id = idElement.Clone();
                object result;
                switch (method)
                {
                    case "connect":
                        result = new { ok = true, protocolVersion = 3, version = "offline-test" };
                        break;
                    case "session.create":
                        CreateCount++;
                        if (RejectCreate)
                        {
                            await WriteAsync(stream, new
                            {
                                jsonrpc = "2.0", id,
                                error = new { code = -32603, message = "offline create failure" },
                            });
                            continue;
                        }
                        if (request.TryGetProperty("params", out var createParams)
                            && createParams.TryGetProperty("sessionId", out var created)
                            && created.GetString() is { Length: > 0 } sessionId)
                            _sessionId = sessionId;
                        else
                            Fault($"session.create carried no params.sessionId: {handling}");
                        result = new { sessionId = _sessionId };
                        break;
                    case "session.send":
                        SendCount++;
                        if (RejectNextSend)
                        {
                            RejectNextSend = false;
                            await WriteAsync(stream, new
                            {
                                jsonrpc = "2.0", id,
                                error = new { code = -32603, message = "offline send failure" },
                            });
                            continue;
                        }
                        await ReplyAsync(stream, id, new { messageId = Guid.NewGuid().ToString() });
                        _pending = Calls.Length;
                        foreach (var call in Calls)
                        {
                            await EventAsync(stream, "external_tool.requested", new
                            {
                                sessionId = _sessionId,
                                requestId = Guid.NewGuid().ToString(),
                                toolCallId = Guid.NewGuid().ToString(),
                                toolName = call.Name,
                                arguments = call.Arguments,
                            });
                        }
                        if (SessionErrorAfterDispatch is { } errorMessage)
                        {
                            await EventAsync(stream, "session.error", new
                            {
                                errorType = "offline_test_error",
                                message = errorMessage,
                            });
                        }
                        if (_pending == 0)
                            await EventAsync(stream, "session.idle", new { });
                        continue;
                    case "session.tools.handlePendingToolCall":
                        if (request.TryGetProperty("params", out var toolParams))
                            ToolResults.Add(toolParams.Clone());
                        else
                            Fault($"session.tools.handlePendingToolCall carried no params: {handling}");
                        _firstToolHandled.TrySetResult();
                        await ReplyAsync(stream, id, new { });
                        // Guarded, because a _pending that goes negative never returns to zero:
                        // session.idle would then never fire again and the next send would hang to
                        // the test's budget, with nothing to say why.
                        if (_pending > 0 && --_pending == 0)
                            await EventAsync(stream, "session.idle", new { });
                        continue;
                    case "session.destroy":
                    case "session.abort":
                    // Teardown is not this test's subject, and the SDK's teardown sequence is not
                    // frozen: session.detach appeared here only once ChatAgent.DisposeAsync started
                    // cancelling the in-flight turn, which let disposal run to completion instead of
                    // timing out part-way through. Answering the teardown verbs generically keeps a
                    // span-parentage test from failing over the shape of a goodbye.
                    case "session.detach":
                        result = new { };
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected offline SDK request: {method} :: {handling}");
                }
                await ReplyAsync(stream, id, result);
            }
        }
        catch (Exception ex) when (!IsExpectedShutdown(ex))
        {
            Fault($"could not handle {handling ?? "<no message read>"}", ex);
        }
    }

    // Cancelling _stop tears the socket down under whatever read or write is in flight, so these
    // are the ordinary way this loop ends -- not something a test should be told about.
    private bool IsExpectedShutdown(Exception ex) =>
        _stop.IsCancellationRequested
        && ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException;

    private Task ReplyAsync(Stream stream, JsonElement id, object result) =>
        WriteAsync(stream, new { jsonrpc = "2.0", id, result });

    private Task EventAsync(Stream stream, string type, object data) =>
        WriteAsync(stream, new
        {
            jsonrpc = "2.0",
            method = "session.event",
            @params = new
            {
                sessionId = _sessionId,
                @event = new
                {
                    id = Guid.NewGuid().ToString(),
                    timestamp = DateTimeOffset.UtcNow,
                    parentId = (string?)null,
                    type,
                    data,
                },
            },
        });

    private async Task WriteAsync(Stream stream, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n"), _stop.Token);
        await stream.WriteAsync(bytes, _stop.Token);
        await stream.FlushAsync(_stop.Token);
    }

    private static async Task<JsonDocument?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new List<byte>();
        var next = new byte[1];
        while (header.Count < 8192)
        {
            if (await stream.ReadAsync(next, ct) == 0)
            {
                if (header.Count == 0)
                    return null;
                throw new EndOfStreamException("Incomplete offline RPC header.");
            }
            header.Add(next[0]);
            if (header.Count >= 4 && header.TakeLast(4).SequenceEqual("\r\n\r\n"u8.ToArray()))
                break;
        }
        var headerText = Encoding.ASCII.GetString(header.ToArray());
        // First, not Single: a peer is allowed to send other headers, and a second one would have
        // turned a framing detail into a receive-loop crash attributed to an unrelated test.
        // Content-Length is the one header this framing cannot recover from, so a missing or
        // unparseable value is the one case that still has to end the loop.
        var lengthHeader = headerText.Split("\r\n")
            .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Offline RPC header carried no Content-Length: '{headerText}'.");
        if (!int.TryParse(lengthHeader["Content-Length:".Length..], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var length) || length < 0)
            throw new InvalidOperationException($"Offline RPC header carried an unusable Content-Length: '{lengthHeader}'.");
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, ct);
        return JsonDocument.Parse(body);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            await _run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        // One predicate for both filters. They used to list different sets, so
        // ObjectDisposedException was "expected shutdown" in the loop -- deliberately not caught
        // there -- and then escaped from here, out of the `await using` finally, replacing whatever
        // the test body had actually failed on. TimeoutException is WaitAsync's own failure and
        // belongs to teardown alone.
        catch (Exception ex) when (IsExpectedShutdown(ex) || (_stop.IsCancellationRequested && ex is TimeoutException))
        {
        }
        finally
        {
            // Only once the loop has actually finished. _stop.Token is read throughout RunAsync,
            // so disposing the source while a read is still parked on it replaces a hung fake with
            // an ObjectDisposedException charged to whichever test runs next.
            if (_run.IsCompleted)
                _stop.Dispose();
        }
    }

    internal sealed record ToolCall(string Name, object Arguments);
}
