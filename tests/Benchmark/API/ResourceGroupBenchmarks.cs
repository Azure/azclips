// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Json;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Benchmark.API;

/// <summary>
/// E2E Live benchmarks for Resource Group operations using HTTP Pipeline.
/// Tests pure API request time without CLI/PS overhead.
/// Requires: 'azv2 login' authentication before running.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(1)]  // 1 warmup iteration to stabilize connections
[IterationCount(5)]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 5)]
public class ResourceGroupBenchmarks : ApiBenchmarkBase
{
    private IRuntimeHttpPipeline _httpPipeline = null!;
    private BenchmarkEventListener _eventListener = null!;
    private string _getRgName = null!;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [GlobalSetup]
    public void Setup()
    {
        InitializeLive();

        // Initialize HttpPipeline
        _httpPipeline = new HttpPipeline(new HttpClientFactory());
        _eventListener = new BenchmarkEventListener();

        // Warmup: do a List RG request to establish connection
        var warmupUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version={ApiVersion}";
        using var warmupRequest = new HttpRequestMessage(HttpMethod.Get, warmupUrl);
        warmupRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        _httpPipeline.SendAsync(warmupRequest, _eventListener).GetAwaiter().GetResult();

        // Pre-create a resource group for Get benchmark
        _getRgName = $"{TestRgPrefix}{InstanceId}-api-get";
        var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{_getRgName}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new ResourceGroup(TestLocation, _getRgName), options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        _httpPipeline.SendAsync(request, _eventListener).GetAwaiter().GetResult();
        CreatedResourceGroups.Add(_getRgName);

        Console.WriteLine($"[Setup] API benchmarks ready");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CleanupResourceGroups();
    }

    [Benchmark(Description = "API: List RG")]
    public async Task<object> ListResourceGroups()
    {
        var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResourceGroupListResult>(_jsonOptions);
        return result?.Value ?? Array.Empty<ResourceGroup>();
    }

    [Benchmark(Description = "API: Create RG")]
    public async Task<object> CreateResourceGroup()
    {
        var rgName = GenerateRgName("api-");

        var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new ResourceGroup(TestLocation, rgName), options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
    }

    [Benchmark(Description = "API: Get RG")]
    public async Task<object> GetResourceGroup()
    {
        var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{_getRgName}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
    }
}
