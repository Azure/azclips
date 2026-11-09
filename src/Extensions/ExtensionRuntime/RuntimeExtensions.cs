// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Core.Abstractions.Runtime;
using Core.Abstractions;
using System.Text.RegularExpressions;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

public static class RuntimeExtensions
{
    public static async Task AssertNotNull(this IEventPublisher instance, string parameterName, object value)
    {
        if (value == null)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' should not be null", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertMinimumLength(this IEventPublisher instance, string parameterName, string value, int length)
    {
        if (value != null && value.Length < length)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Length of '{parameterName}' is less than {length}", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertMaximumLength(this IEventPublisher instance, string parameterName, string value, int length)
    {
        if (value != null && value.Length > length)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Length of '{parameterName}' is greater than {length}", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }

    public static async Task AssertRegEx(this IEventPublisher instance, string parameterName, string value, string regularExpression)
    {
        if (value != null && !System.Text.RegularExpressions.Regex.Match(value, regularExpression).Success)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' does not validate against pattern /{regularExpression}/", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertEnum(this IEventPublisher instance, string parameterName, string value, params string[] values)
    {
        if (!values.Any(each => each.Equals(value)))
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' is not one of ({values.Aggregate((c, e) => $"'{e}',{c}")}", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertObjectIsValid(this IEventPublisher instance, string parameterName, object inst)
    {
        if (inst is IValidates validatable)
        {
            await validatable.Validate(instance);
        }
    }

    public static async Task AssertIsLessThan<T>(this IEventPublisher instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
    {
        if (null != value && ((T)value).CompareTo(max) >= 0)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be less than {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertIsGreaterThan<T>(this IEventPublisher instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
    {
        if (null != value && ((T)value).CompareTo(max) <= 0)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be greater than {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertIsLessThanOrEqual<T>(this IEventPublisher instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
    {
        if (null != value && ((T)value).CompareTo(max) > 0)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be less than or equal to {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertIsGreaterThanOrEqual<T>(this IEventPublisher instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
    {
        if (null != value && ((T)value).CompareTo(max) < 0)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be greater than or equal to {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertIsMultipleOf(this IEventPublisher instance, string parameterName, Int64? value, Int64 multiple)
    {
        if (null != value && value % multiple != 0)
        {
            await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
        }
    }
    public static async Task AssertIsMultipleOf(this IEventPublisher instance, string parameterName, double? value, double multiple)
    {
        if (null != value)
        {
            var i = (Int64)(value / multiple);
            if (i != value / multiple)
            {
                await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
    }
    public static async Task AssertIsMultipleOf(this IEventPublisher instance, string parameterName, decimal? value, decimal multiple)
    {
        if (null != value)
        {
            var i = (Int64)(value / multiple);
            if (i != value / multiple)
            {
                await instance.Publish(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
    }

    public static T[] SubArray<T>(this T[] array, int offset, int length)
    {
        return new ArraySegment<T>(array, offset, length)
                    .ToArray();
    }

    public static T ReadHeaders<T>(this T instance, global::System.Net.Http.Headers.HttpResponseHeaders headers) where T : class
    {
        (instance as IHeaderSerializable)?.ReadHeaders(headers);
        return instance;
    }

    public static bool If<T>(T input, out T output)
    {
        if (null == input)
        {
            output = default!;
            return false;
        }
        output = input;
        return true;
    }

    /// <summary>
    /// Returns the first header value as a string from an HttpReponseMessage. 
    /// </summary>
    /// <param name="response">the HttpResponseMessage to fetch a header from</param>
    /// <param name="headerName">the header name</param>
    /// <returns>the first header value as a string from an HttpReponseMessage. string.empty if there is no header value matching</returns>
    public static string GetFirstHeader(this System.Net.Http.HttpResponseMessage response, string headerName) => response.Headers.FirstOrDefault(each => string.Equals(headerName, each.Key, System.StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// Sets the Synchronization Context to null, and returns an IDisposable that when disposed, 
    /// will restore the synchonization context to the original value.
    /// 
    /// This is used a less-invasive means to ensure that code in the library that doesn't 
    /// need to be continued in the original context doesn't have to have <c>ConfigureAwait(false)</c> 
    /// on every single <c>await</c>
    /// 
    /// If the <c>SynchronizationContext</c> is <c>null</c> when this is used, the resulting IDisposable
    /// will not do anything (this prevents excessive re-setting of the <c>SynchronizationContext</c>)
    /// <example>
    /// Usage:
    /// <code>
    /// using(NoSynchronizationContext) {
    ///     await SomeAsyncOperation();
    ///     await SomeOtherOperation();
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <returns>An IDisposable that will return the SynchronizationContext to original state</returns>
    public static IDisposable NoSynchronizationContext => SynchronizationContext.Current == null ? Dummy : new NoSyncContext();

    /// <summary>
    /// An instance of the Dummy IDispoable.
    /// </summary>
    /// <returns></returns>
    public static System.IDisposable Dummy = new DummyDisposable();

    /// <summary>
    /// An IDisposable that does absolutely nothing. 
    /// </summary>
    public class DummyDisposable : System.IDisposable
    {
        public void Dispose()
        {
        }
    }
    /// <summary>
    /// An IDisposable that saves the SynchronizationContext,sets it to <c>null</c> and 
    /// restores it to the original upon <c>Dispose()</c>. 
    /// 
    /// NOTE: This is designed to be less invasive than using <c>.ConfigureAwait(false)</c> 
    /// on every single <c>await</c> in library code (ie, places where we know we don't need
    /// to continue in the same context as we went async)
    /// </summary>
    public class NoSyncContext : System.IDisposable
    {
        private SynchronizationContext original = SynchronizationContext.Current!;
        public NoSyncContext()
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
        }
        public void Dispose() => System.Threading.SynchronizationContext.SetSynchronizationContext(original);
    }

    public static HttpRequestMessage CloneAndDispose(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
    {
        using (original)
        {
            return original.Clone(requestUri, method);
        }
    }

    public static Task<HttpRequestMessage> CloneWithContentAndDispose(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
    {
        using (original)
        {
            return original.CloneWithContent(requestUri, method);
        }
    }

    /// <summary>
    /// Clones an HttpRequestMessage (without the content)
    /// </summary>
    /// <param name="original">Original HttpRequestMessage (Will be diposed before returning)</param>
    /// <param name="requestUri"></param>
    /// <param name="method"></param>
    /// <returns>A clone of the HttpRequestMessage</returns>
    public static HttpRequestMessage Clone(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
    {
        var clone = new HttpRequestMessage
        {
            Method = method ?? original.Method,
            RequestUri = requestUri ?? original.RequestUri,
            Version = original.Version,
        };

        foreach (KeyValuePair<string, object?> prop in original.Options)
        {
            clone.Options.TryAdd(prop.Key, prop.Value);
        }

        var createsNewRequest = requestUri is not null || method is not null;
        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            /*
            **temporarily skip cloning telemetry related headers**
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            */
            if (!"x-ms-unique-id".Equals(header.Key) &&
                !(createsNewRequest && "x-ms-client-request-id".Equals(header.Key, StringComparison.OrdinalIgnoreCase)) &&
                !"CommandName".Equals(header.Key) && !"FullCommandName".Equals(header.Key) && !"ParameterSetName".Equals(header.Key) && !"User-Agent".Equals(header.Key))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    /// <summary>
    /// Clones an HttpRequestMessage (including the content stream and content headers) 
    /// </summary>
    /// <param name="original">Original HttpRequestMessage (Will be diposed before returning)</param>
    /// <param name="requestUri"></param>
    /// <param name="method"></param>
    /// <returns>A clone of the HttpRequestMessage</returns>
    public static async Task<HttpRequestMessage> CloneWithContent(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
    {
        var clone = original.Clone(requestUri, method);
        if (original.Content != null)
        {
            if (original.Content is IReplayableHttpContent replayable)
            {
                clone.Content = replayable.CreateReplay();
            }
            else
            {
                var stream = new System.IO.MemoryStream();
                await original.Content.CopyToAsync(stream).ConfigureAwait(false);
                stream.Position = 0;
                clone.Content = new StreamContent(stream);
            }
            if (original.Content.Headers != null)
            {
                foreach (var h in original.Content.Headers)
                {
                    clone.Content.Headers.Add(h.Key, h.Value);
                }
            }
        }
        return clone;
    }

    public static string FormatString(string content)
    {
        // this is only used to determine cutoff for streams (not xml or json).
        // TODO(Yabo): get this from configservice: engine get this property from configservice and set through processor context
        if (string.IsNullOrEmpty(content))
        {
            return content ?? string.Empty;
        }

        // Pretty-print JSON best-effort. Tracing/logging must tolerate ANY body and never throw:
        // non-JSON bodies (text/plain, xml, html error pages, octet-stream, etc.) fall back to the
        // raw content unchanged. Matches Azure PowerShell's Invoke-AzRestMethod behavior.
        try
        {
            content = JsonNode.Parse(content)?.ToString() ?? content;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Not valid JSON - keep the raw content. Fatal conditions (OOM) are allowed to propagate.
        }

        const int StreamCutOffSize = 10 * 1024; //10KB
        return content.Length <= StreamCutOffSize ? 
                content : 
                content.Substring(0, StreamCutOffSize) + "\r\nDATA TRUNCATED DUE TO SIZE\r\n";
    }

    public static async Task<string> GetLog(this HttpRequestMessage request)
    {
        return await GetLog(request, null);
    }

    public static async Task<string> GetLog(this HttpResponseMessage response, IList<Regex>? matchers = null)
    {
        if (response == null)
        {
            return string.Empty;
        }

        if (response.RequestMessage.HasSensitiveDiagnostics())
        {
            return $"HTTP response: {(int)response.StatusCode}. Sensitive details omitted.";
        }

        string body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync();
        body = string.IsNullOrEmpty(body) ? string.Empty
            : FormatString(body);

        return GetHttpResponseLog(
            response.StatusCode.ToString(),
            response.Headers,
            body,
            matchers);
    }

    public static async Task<string> GetLog(this HttpResponseMessage response)
    {
        return await GetLog(response, null);
    }

    public static async Task<string> GetLog(this HttpRequestMessage request, IList<Regex>? matchers = null)
    {
        if (request == null)
        {
            return string.Empty;
        }

        if (request.HasSensitiveDiagnostics())
        {
            return $"HTTP request: {request.Method}. Sensitive details omitted.";
        }

        string body = request.Content == null ? string.Empty
            : FormatString(await request.Content.ReadAsStringAsync());

        return GetHttpRequestLog(
            request.Method.ToString(),
            request.RequestUri == null ? string.Empty : request.RequestUri.ToString(),
            request.Headers,
            body,
            matchers);
    }

    public static string GetHttpRequestLog(string method, string requestUri, HttpHeaders headers, string body, IList<Regex>? matchers = null)
    {
        return GetHttpRequestLog(method, requestUri, ConvertHttpHeadersToWebHeaderCollection(headers), body, matchers);
    }

    public static string GetHttpRequestLog(
        string method,
        string requestUri,
        IDictionary<string, IEnumerable<string>> headers,
        string body,
        IList<Regex>? matchers = null)
    {
        StringBuilder httpRequestLog = new StringBuilder();
        httpRequestLog.AppendLine(string.Format("============================ HTTP REQUEST ============================{0}", Environment.NewLine));
        httpRequestLog.AppendLine(string.Format("HTTP Method:{0}{1}{0}", Environment.NewLine, method));
        httpRequestLog.AppendLine(string.Format("Absolute Uri:{0}{1}{0}", Environment.NewLine, requestUri));
        httpRequestLog.AppendLine(string.Format("Headers:{0}{1}", Environment.NewLine, MessageHeadersToString(headers)));
        httpRequestLog.AppendLine(string.Format("Body:{0}{1}{0}", Environment.NewLine, TransformBody(body, matchers)));

        return httpRequestLog.ToString();
    }

    public static string GetHttpResponseLog(string statusCode, HttpHeaders headers, string body, IList<Regex>? matchers = null)
    {
        return GetHttpResponseLog(statusCode, ConvertHttpHeadersToWebHeaderCollection(headers), body, matchers);
    }

    public static string GetHttpResponseLog(string statusCode, IDictionary<string, IEnumerable<string>> headers, string body, IList<Regex>? matchers = null)
    {
        StringBuilder httpResponseLog = new StringBuilder();
        httpResponseLog.AppendLine($"============================ HTTP RESPONSE ============================{Environment.NewLine}");
        httpResponseLog.AppendLine($"Status Code:{Environment.NewLine}{statusCode}{Environment.NewLine}");
        httpResponseLog.AppendLine($"Headers:{Environment.NewLine}{MessageHeadersToString(headers)}");
        httpResponseLog.AppendLine($"Body:{Environment.NewLine}{TransformBody(body, matchers)}{Environment.NewLine}");
        return httpResponseLog.ToString();
    }

    public static string TransformBody(string inBody)
    {
        IList<Regex> matchers = new List<Regex>();
        Regex matcher = new Regex("(\\s*\"access_token\"\\s*:\\s*)\"[^\"]+\"");
        matchers.Add(matcher);
        return TransformBody(inBody, matchers);
    }

    public static string TransformBody(string inBody, IList<Regex>? matchers)
    {
        if (matchers != null) 
        {
            foreach (Regex match in matchers)
            {
                inBody = match.Replace(inBody, "$1\"<redacted>\"");
            }
        }
        return inBody;
    }

    // TODO(Yabo): get this from configservice: engine get this property from configservice and set through processor context
    private static List<string> AuthorizationHeaderNames = new List<string>() { "Authorization" };

    private static IDictionary<string, IEnumerable<string>> ConvertHttpHeadersToWebHeaderCollection(HttpHeaders headers)
    {
        IDictionary<string, IEnumerable<string>> webHeaders = new Dictionary<string, IEnumerable<string>>();
        foreach (KeyValuePair<string, IEnumerable<string>> pair in headers)
        {
            if (AuthorizationHeaderNames.Any(h => h.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
            {
                // Skip adding the authorization header
                continue;
            }

            webHeaders.Add(pair.Key, pair.Value);
        }

        return webHeaders;
    }

    private static string MessageHeadersToString(IDictionary<string, IEnumerable<string>> headers)
    {
        string[] keys = headers.Keys.ToArray();
        StringBuilder result = new StringBuilder();

        foreach (string key in keys)
        {
            result.AppendLine(string.Format(
                "{0,-30}: {1}",
                key,
                headers[key].ToArray().ArrayToString(",")));
        }

        return result.ToString();
    }

    /// <summary>
    /// Convert the given array into string.
    /// </summary>
    /// <typeparam name="T">The type of the object array is holding</typeparam>
    /// <param name="array">The collection</param>
    /// <param name="delimiter">The used delimiter between array elements</param>
    /// <returns>The array into string representation</returns>
    public static string? ArrayToString<T>(this T[] array, string delimiter)
    {
        return (array == null) ? null : (array.Length == 0) ? string.Empty
            : array.Skip(1).Aggregate(new StringBuilder(array[0]!.ToString()),
            (s, i) => s.Append(delimiter).Append(i), s => s.ToString());
    }

    // Reads ARM error body and publishes ResourceOperationFailed so the message reaches
    // the error stream, then throws to stop the orchestration. Callers that need to keep
    // ownership of the response (i.e. return it to a caller) are responsible for disposing
    // it when this throws.
    public static Task EnsureSuccessAsync(this HttpResponseMessage response, IEventPublisher publisher) =>
        response.EnsureSuccessAsync(publisher, reportFailure: true);

    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, IEventPublisher publisher, bool reportFailure)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.RequestMessage.HasSensitiveDiagnostics())
        {
            string safeMessage = SensitiveHttpDiagnostics.FailureMessage(response.StatusCode);
            if (reportFailure) await publisher.Publish(Events.ResourceOperationFailed, safeMessage);
            var exception = new HttpRequestException(safeMessage, inner: null, statusCode: response.StatusCode);
            if (!reportFailure) exception.Data["FailureDiagnosticsDeferred"] = true;
            foreach (var (header, key) in new[]
            {
                ("x-ms-error-code", "ServiceErrorCode"),
                ("x-ms-copy-source-error-code", "CopySourceErrorCode")
            })
            {
                if (response.Headers.TryGetValues(header, out var codes))
                {
                    string[] values = codes.Take(2).ToArray();
                    if (values.Length == 1 && System.Text.RegularExpressions.Regex.IsMatch(values[0], "\\A[A-Za-z][A-Za-z0-9]{0,127}\\z"))
                        exception.Data[key] = values[0];
                }
            }
            if (response.Headers.TryGetValues("x-ms-copy-source-status-code", out var statuses))
            {
                string[] values = statuses.Take(2).ToArray();
                if (values.Length == 1 && values[0].Length == 3 &&
                    int.TryParse(values[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                        out int status) && status is >= 100 and <= 599)
                    exception.Data["CopySourceStatusCode"] = status;
            }
            if (response.Headers.TryGetValues("x-ms-request-id", out var ids))
            {
                string[] values = ids.Take(2).ToArray();
                if (values.Length == 1 && Guid.TryParse(values[0], out var requestId))
                    exception.Data["ServiceRequestId"] = requestId.ToString();
            }
            throw exception;
        }
        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        string uri = response.RequestMessage?.RequestUri?.ToString() ?? "(unknown)";
        string message = $"{(int)response.StatusCode} {response.ReasonPhrase} from {uri}{(string.IsNullOrWhiteSpace(body) ? string.Empty : ": " + body)}";
        if (reportFailure) await publisher.Publish(Events.ResourceOperationFailed, message);
        var failure = new HttpRequestException(message, inner: null, statusCode: response.StatusCode);
        if (!reportFailure) failure.Data["FailureDiagnosticsDeferred"] = true;
        throw failure;
    }
}
