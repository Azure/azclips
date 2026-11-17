// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Tests.Common.Stubs;

/// <summary>
/// Benchmark/test HTTP terminator: short-circuits every outgoing request with a
/// minimal, structurally-valid ARM response (resolved from <see cref="StubResponseMappings"/>)
/// so the CLI/PS pipeline never calls Azure. A <c>Retry-After: 0</c> header neutralises
/// long-running-operation polling delays. Used only by the Benchmark project.
/// </summary>
public class StubHttpTerminateHandler : IHttpTerminateHandler
{
    public SendAsyncStep SendAsync { get; } = (request, callback, next) =>
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StubResponseMappings.ResolveBody(path, request.RequestUri?.Query, request.Method), Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return Task.FromResult(response);
    };
}
