// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Net;
using System.Net.Http;
using System.Threading;

public sealed class RequestAttemptState
{
    public static readonly HttpRequestOptionsKey<RequestAttemptState> OptionsKey = new("Azclips.RequestAttemptState");
    private int attempts;
    private int ambiguousResponse;

    public int Attempts => Volatile.Read(ref attempts);
    public bool HadAmbiguousResponse => Volatile.Read(ref ambiguousResponse) != 0;

    public void RecordAttempt() => Interlocked.Increment(ref attempts);

    public void RecordResponse(HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.RequestTimeout ||
            statusCode >= HttpStatusCode.InternalServerError &&
            statusCode is not (HttpStatusCode.NotImplemented or HttpStatusCode.HttpVersionNotSupported))
            Interlocked.Exchange(ref ambiguousResponse, 1);
    }
}
