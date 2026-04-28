namespace Core.Runtime
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Core.Abstractions.Runtime;
    using Core.Abstractions;

    internal static class RuntimeExtensions
    {
        public static async Task AssertNotNull(this IEventListener instance, string parameterName, object value)
        {
            if (value == null)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' should not be null", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertMinimumLength(this IEventListener instance, string parameterName, string value, int length)
        {
            if (value != null && value.Length < length)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Length of '{parameterName}' is less than {length}", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertMaximumLength(this IEventListener instance, string parameterName, string value, int length)
        {
            if (value != null && value.Length > length)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Length of '{parameterName}' is greater than {length}", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }

        public static async Task AssertRegEx(this IEventListener instance, string parameterName, string value, string regularExpression)
        {
            if (value != null && !System.Text.RegularExpressions.Regex.Match(value, regularExpression).Success)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' does not validate against pattern /{regularExpression}/", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertEnum(this IEventListener instance, string parameterName, string value, params string[] values)
        {
            if (!values.Any(each => each.Equals(value)))
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"'{parameterName}' is not one of ({values.Aggregate((c, e) => $"'{e}',{c}")}", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertObjectIsValid(this IEventListener instance, string parameterName, object inst)
        {
            if (inst is IValidates validatable)
            {
                await validatable.Validate(instance);
            }
        }

        public static async Task AssertIsLessThan<T>(this IEventListener instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
        {
            if (null != value && ((T)value).CompareTo(max) >= 0)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be less than {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertIsGreaterThan<T>(this IEventListener instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
        {
            if (null != value && ((T)value).CompareTo(max) <= 0)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be greater than {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertIsLessThanOrEqual<T>(this IEventListener instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
        {
            if (null != value && ((T)value).CompareTo(max) > 0)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be less than or equal to {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertIsGreaterThanOrEqual<T>(this IEventListener instance, string parameterName, T? value, T max) where T : struct, System.IComparable<T>
        {
            if (null != value && ((T)value).CompareTo(max) < 0)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be greater than or equal to {max} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertIsMultipleOf(this IEventListener instance, string parameterName, Int64? value, Int64 multiple)
        {
            if (null != value && value % multiple != 0)
            {
                await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
            }
        }
        public static async Task AssertIsMultipleOf(this IEventListener instance, string parameterName, double? value, double multiple)
        {
            if (null != value)
            {
                var i = (Int64)(value / multiple);
                if (i != value / multiple)
                {
                    await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
                }
            }
        }
        public static async Task AssertIsMultipleOf(this IEventListener instance, string parameterName, decimal? value, decimal multiple)
        {
            if (null != value)
            {
                var i = (Int64)(value / multiple);
                if (i != value / multiple)
                {
                    await instance.Signal(Events.ValidationWarning, instance.Token, () => new EventData { Id = Events.ValidationWarning, Message = $"Value of '{parameterName}' must be multiple of {multiple} (value is {value})", Parameter = parameterName, Cancel = instance.Cancel });
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

        internal static bool If<T>(T input, out T output)
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
        internal static string GetFirstHeader(this System.Net.Http.HttpResponseMessage response, string headerName) => response.Headers.FirstOrDefault(each => string.Equals(headerName, each.Key, System.StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? string.Empty;

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
        internal static IDisposable NoSynchronizationContext => SynchronizationContext.Current == null ? Dummy : new NoSyncContext();

        /// <summary>
        /// An instance of the Dummy IDispoable.
        /// </summary>
        /// <returns></returns>
        internal static System.IDisposable Dummy = new DummyDisposable();

        /// <summary>
        /// An IDisposable that does absolutely nothing. 
        /// </summary>
        internal class DummyDisposable : System.IDisposable
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
        internal class NoSyncContext : System.IDisposable
        {
            private SynchronizationContext original = SynchronizationContext.Current!;
            internal NoSyncContext()
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            }
            public void Dispose() => System.Threading.SynchronizationContext.SetSynchronizationContext(original);
        }

        internal static HttpRequestMessage CloneAndDispose(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
        {
            using (original)
            {
                return original.Clone(requestUri, method);
            }
        }

        internal static Task<HttpRequestMessage> CloneWithContentAndDispose(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
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
        internal static HttpRequestMessage Clone(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
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

            foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
            {
                /*
                **temporarily skip cloning telemetry related headers**
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
                */
                if (!"x-ms-unique-id".Equals(header.Key) && !"x-ms-client-request-id".Equals(header.Key) && !"CommandName".Equals(header.Key) && !"FullCommandName".Equals(header.Key) && !"ParameterSetName".Equals(header.Key) && !"User-Agent".Equals(header.Key))
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
        internal static async Task<HttpRequestMessage> CloneWithContent(this HttpRequestMessage original, System.Uri? requestUri = null, System.Net.Http.HttpMethod? method = null)
        {
            var clone = original.Clone(requestUri, method);
            var stream = new System.IO.MemoryStream();
            if (original.Content != null)
            {
                await original.Content.CopyToAsync(stream).ConfigureAwait(false);
                stream.Position = 0;
                clone.Content = new StreamContent(stream);
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
    }
}
