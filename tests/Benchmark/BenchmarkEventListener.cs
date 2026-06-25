// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Benchmark;

public class BenchmarkEventPublisher : IEventPublisher
{
    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => () => { };
    public Task Publish(string id, EventData message) => Task.CompletedTask;
    public Task Publish(string id, Uri uri) => Task.CompletedTask;
    public Task Publish(string id, CancellationToken token) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, EventData message) => throw new NotImplementedException();
    public Task Publish(string id, Func<object> createMessage) => throw new NotImplementedException();
    public Task Publish(string id) => throw new NotImplementedException();
    public Task Publish(string id, string messageText) => throw new NotImplementedException();
    public Task Publish(string id, string messageText, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, string messageText, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, string messageText, double magnitude) => throw new NotImplementedException();
    public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, HttpRequestMessage request) => throw new NotImplementedException();
    public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, HttpResponseMessage response) => throw new NotImplementedException();
    public Task Publish(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
}
