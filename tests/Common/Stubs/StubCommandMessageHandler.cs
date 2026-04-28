// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Tests.Common.Stubs;

public class StubCommandMessageHandler : ICommandHttpMessageHandler
{
    public SendAsyncStep SendAsync { get; } = (request, callback, next) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
}
