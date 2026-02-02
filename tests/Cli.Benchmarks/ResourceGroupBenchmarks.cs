using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Core.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cli.Benchmarks;

/// <summary>
/// E2E Live benchmarks for Resource Group operations.
/// Compares:
/// 1. Pure HTTP request time (using HttpPipeline like commands do)
/// 2. Full CLI command execution time (azv2.exe group list)
/// Requires: 'azv2 login' authentication before running.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(0)]  // Skip warmup for live API calls
[IterationCount(3)]
[SimpleJob(RunStrategy.ColdStart, iterationCount: 3)]  // ColdStart is better for E2E tests
public class ResourceGroupBenchmarks
{
    private IRuntimeHttpPipeline _httpPipeline = null!;
    private BenchmarkEventListener _eventListener = null!;
    private string _accessToken = null!;
    private string _subscriptionId = null!;
    private readonly List<string> _createdResourceGroups = new();
    private int _rgCounter = 0;
    private string _instanceId = null!;
    private string _cliPath = null!;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string TestLocation = "eastus";
    private const string TestRgPrefix = "benchmark-rg-";
    private const string ApiVersion = "2025-04-01";

    [GlobalSetup]
    public void Setup()
    {
        // Unique instance ID to avoid conflicts between runs
        _instanceId = $"{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..4]}";
        
        // CLI executable path - use absolute path from repo root
        // BenchmarkDotNet runs from a temp directory, so we need to find the repo root
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory) 
            ?? FindRepoRoot(Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Cannot find repository root (looking for cirrus.slnx)");
        
        _cliPath = Path.Combine(repoRoot, "artifacts", "Debug", "Core.Cli", "azv2.exe");
        if (!File.Exists(_cliPath))
        {
            throw new FileNotFoundException($"CLI executable not found at: {_cliPath}. Run 'dotnet build' first.");
        }
        Console.WriteLine($"[Setup] CLI path: {_cliPath}");
        
        // Initialize HttpPipeline (same as commands use)
        _httpPipeline = new HttpPipeline(new HttpClientFactory());
        _eventListener = new BenchmarkEventListener();
        
        // Get auth token
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        var authService = serviceProvider.GetRequiredService<IAuthService>();
        var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
        var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");

        var authResult = authService.AcquireTokenSilentlyAsync(
            scopes: null,
            tenantId: authContext.TenantId,
            promptOnError: false).GetAwaiter().GetResult();

        _accessToken = authResult.AccessToken;
        _subscriptionId = authContext.SubscriptionId;
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cirrus.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Clean up all created resource groups (fire and forget, don't wait)
        Console.WriteLine($"\n[Cleanup] Deleting {_createdResourceGroups.Count} test resource groups...");
        var deleteTasks = _createdResourceGroups.Select(async rgName =>
        {
            try
            {
                Console.WriteLine($"[Cleanup] Deleting {rgName}...");
                using var request = new HttpRequestMessage(HttpMethod.Delete, 
                    $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                await _httpPipeline.SendAsync(request, _eventListener);
                Console.WriteLine($"[Cleanup] Delete initiated for {rgName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Failed to delete {rgName}: {ex.Message}");
            }
        }).ToArray();

        Task.WhenAll(deleteTasks).GetAwaiter().GetResult();
    }

    // ==================== HTTP Pipeline (Same as Commands) ====================

    [Benchmark(Description = "Pipeline: List RG")]
    public async Task<object> Pipeline_ListResourceGroups()
    {
        var url = $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        
        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResourceGroupListResult>(_jsonOptions);
        return result?.Value ?? Array.Empty<ResourceGroup>();
    }

    [Benchmark(Description = "Pipeline: Create RG")]
    public async Task<object> Pipeline_CreateResourceGroup()
    {
        var rgName = $"{TestRgPrefix}{_instanceId}-{Interlocked.Increment(ref _rgCounter)}";
        _createdResourceGroups.Add(rgName);
        
        var url = $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new ResourceGroup(TestLocation, rgName), options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        
        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();
        
        return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
    }

    [Benchmark(Description = "Pipeline: Get RG")]
    public async Task<object> Pipeline_GetResourceGroup()
    {
        var rgName = $"{TestRgPrefix}{_instanceId}-get";
        if (!_createdResourceGroups.Contains(rgName))
        {
            // Create the resource group first
            var createUrl = $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}";
            using var createRequest = new HttpRequestMessage(HttpMethod.Put, createUrl)
            {
                Content = JsonContent.Create(new ResourceGroup(TestLocation, rgName), options: _jsonOptions)
            };
            createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            var createResponse = await _httpPipeline.SendAsync(createRequest, _eventListener);
            createResponse.EnsureSuccessStatusCode();
            _createdResourceGroups.Add(rgName);
        }
        
        var url = $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        
        var response = await _httpPipeline.SendAsync(request, _eventListener);
        response.EnsureSuccessStatusCode();
        
        return (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
    }

    // ==================== CLI E2E (Full azv2.exe command) ====================

    [Benchmark(Description = "CLI: group list")]
    public int Cli_GroupList()
    {
        return RunCliCommand("group", "list");
    }

    [Benchmark(Description = "CLI: group create")]
    public int Cli_GroupCreate()
    {
        var rgName = $"{TestRgPrefix}{_instanceId}-cli-{Interlocked.Increment(ref _rgCounter)}";
        _createdResourceGroups.Add(rgName);
        return RunCliCommand("group", "create", "--Name", rgName, "--Location", TestLocation);
    }

    [Benchmark(Description = "CLI: group show")]
    public int Cli_GroupShow()
    {
        // Ensure we have a resource group to show
        var rgName = $"{TestRgPrefix}{_instanceId}-cli-show";
        if (!_createdResourceGroups.Contains(rgName))
        {
            RunCliCommand("group", "create", "--Name", rgName, "--Location", TestLocation);
            _createdResourceGroups.Add(rgName);
        }
        return RunCliCommand("group", "show", "--Name", rgName);
    }

    private int RunCliCommand(params string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        
        // Must read stdout/stderr to prevent buffer deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        
        return process.ExitCode;
    }
}

/// <summary>
/// Simple IEventListener implementation for benchmarks.
/// </summary>
internal class BenchmarkEventListener : IEventListener
{
    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => () => { };
    public StartActivityDelegate StartActivity { get; set; } = (name, kind) => null!;
    
    public Task Signal(string id, CancellationToken token, Func<object> createMessage)
    {
        // No-op for benchmarks
        return Task.CompletedTask;
    }
}
