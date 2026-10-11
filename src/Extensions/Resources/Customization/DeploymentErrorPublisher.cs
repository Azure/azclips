// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Legacy error text for failed ARM calls. The runtime publishes "{status} {reason} from {uri}: {body}" and then throws.
/// CLI: the record is rewritten to the azure-core ODataV4 text, or to the raw response body for the template commands
/// (legacy handle_template_based_exception). PS: the record is dropped and one exception with the legacy
/// ResourceManagerCloudException text is thrown, so the error shows once.
/// </summary>
public class DeploymentErrorPublisher : DispatchProxy
{
    private IEventPublisher _inner = null!;
    private bool _powerShell;
    private bool _rawCli;
    private bool _powerShellWhatIf;
    private HttpResponseMessage? _failed;
    private HttpResponseMessage? _last;

    /// <summary>The runtime LRO throws this without the body when a polled status is Failed.</summary>
    public static bool IsLroFailure(Exception ex) => ex.Message.StartsWith("Long running operation failed", StringComparison.Ordinal);

    /// <summary>The last response the runtime traced (the failed polling response after <see cref="IsLroFailure"/>).</summary>
    public static HttpResponseMessage? LastResponse(IEventPublisher publisher) =>
        publisher is DeploymentErrorPublisher proxy ? proxy._last : null;

    /// <summary>Legacy PS ExecuteDeploymentWhatIf rethrows HTTP failures as "\n{Code} - {Message}" plus details.</summary>
    public static void UsePowerShellWhatIfText(IEventPublisher publisher)
    {
        if (publisher is DeploymentErrorPublisher proxy)
        {
            proxy._powerShellWhatIf = true;
        }
    }

    /// <param name="rawCli">CLI create/what-if/validate: print the raw response body like legacy.</param>
    public static IEventPublisher Wrap(IEventPublisher publisher, bool powerShell, bool rawCli = false)
    {
        if (publisher is DeploymentErrorPublisher)
        {
            return publisher;
        }
        IEventPublisher proxy = Create<IEventPublisher, DeploymentErrorPublisher>();
        var self = (DeploymentErrorPublisher)(object)proxy;
        self._inner = publisher;
        self._powerShell = powerShell;
        self._rawCli = rawCli;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (args is [Events.TracingHttpResponseMessage, HttpResponseMessage response])
        {
            _last = response;
            _failed = response.IsSuccessStatusCode ? null : response;
        }
        else if (args is [Events.ResourceOperationFailed, string message] && _failed is { } failed)
        {
            string prefix = $"{(int)failed.StatusCode} {failed.ReasonPhrase} from {failed.RequestMessage?.RequestUri?.ToString() ?? "(unknown)"}";
            if (message.StartsWith(prefix, StringComparison.Ordinal))
            {
                _failed = null;
                string body = message.Length > prefix.Length + 2 ? message[(prefix.Length + 2)..] : string.Empty;
                return _powerShell
                    ? Task.FromException(new HttpRequestException(
                        _powerShellWhatIf ? Environment.NewLine + BuildCloudErrorMessage(ParseError(body)) : ToPowerShellText(failed, body), null, failed.StatusCode))
                    : _inner.Publish(Events.ResourceOperationFailed, _rawCli && body.Length > 0 ? body : ToCliText(failed, body));
            }
        }

        try
        {
            return method!.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }

    // Legacy azure-core ODataV4Format.__str__ (its spacing quirks included).
    internal static string ToODataV4String(ErrorResponse error)
    {
        StringBuilder text = new($"({error.Code}) {error.Message}\nCode: {error.Code}\nMessage: {error.Message}");
        if (!string.IsNullOrEmpty(error.Target))
        {
            text.Append($"\nTarget: {error.Target}");
        }
        if (error.Details is { Count: > 0 })
        {
            text.Append("\nException Details:");
            foreach (ErrorResponse detail in error.Details)
            {
                text.Append(string.Join('\n', ToODataV4String(detail).Split('\n').Select(line => "\t" + line)));
            }
        }
        return text.ToString();
    }

    // Legacy CLI: azure-core HttpResponseError with ARMErrorFormat.
    private static string ToCliText(HttpResponseMessage response, string body) =>
        ParseError(body) is { Code: not null, Message: not null } error
            ? ToODataV4String(error)
            : $"Operation returned an invalid status '{response.ReasonPhrase}'";

    // Legacy PS: track1 CloudException wrapped in ResourceManagerCloudException.
    private static string ToPowerShellText(HttpResponseMessage response, string body)
    {
        StringBuilder text = new(ParseError(body)?.Message ?? $"Operation returned an invalid status code '{response.StatusCode}'");
        text.AppendLine().Append($"StatusCode: {(int)response.StatusCode}");
        text.AppendLine().Append($"ReasonPhrase: {response.ReasonPhrase}");
        if (response.Headers.TryGetValues("x-ms-request-id", out IEnumerable<string>? ids))
        {
            text.AppendLine().Append($"OperationID : {ids.FirstOrDefault()}");
        }
        return text.ToString();
    }

    // Legacy PS NewResourceManagerSdkClient.BuildCloudErrorMessage.
    private static string BuildCloudErrorMessage(ErrorResponse? error) =>
        error is null ? string.Empty
            : string.Join(Environment.NewLine, (error.Details ?? []).Select(BuildCloudErrorMessage).Prepend($"{error.Code} - {error.Message}"));

    private static ErrorResponse? ParseError(string body)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(body);
            return (node?["error"] ?? node)?.Deserialize<ErrorResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
