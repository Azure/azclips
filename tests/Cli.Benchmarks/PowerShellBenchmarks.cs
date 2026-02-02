using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using IEngine = Core.Abstractions.IEngine;
using EngineFactory = Core.Engine.EngineFactory;

namespace Cli.Benchmarks;

/// <summary>
/// E2E Live benchmarks for PowerShell cmdlet operations.
/// Compares:
/// 1. Pure HTTP Pipeline time
/// 2. Full PSEngine execution time (same as PowerShell cmdlets use)
/// Requires: 'azv2 login' authentication before running.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(0)]  // Skip warmup for live API calls
[IterationCount(3)]
[SimpleJob(RunStrategy.ColdStart, iterationCount: 3)]
public class PowerShellBenchmarks
{
    private IEngine _psEngine = null!;
    private string _subscriptionId = null!;
    private readonly List<string> _createdResourceGroups = new();
    private int _rgCounter = 0;
    private string _instanceId = null!;
    private string _accessToken = null!;
    private string _psModulePath = null!;

    private const string TestLocation = "eastus";
    private const string TestRgPrefix = "benchmark-rg-";

    [GlobalSetup]
    public void Setup()
    {
        // Unique instance ID to avoid conflicts between runs
        _instanceId = $"{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..4]}";

        // Find repo root for module path
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? FindRepoRoot(Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Cannot find repository root (looking for cirrus.slnx)");

        _psModulePath = Path.Combine(repoRoot, "src", "PS", "Resources", "Resources.psd1");
        Console.WriteLine($"[Setup] PS Module path: {_psModulePath}");

        // Get PSEngine from DI
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        var engineFactory = serviceProvider.GetRequiredService<EngineFactory>();
        _psEngine = engineFactory.GetEngine(ClientType.PowerShell);

        // Get auth context
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

        Console.WriteLine($"[Setup] SubscriptionId: {_subscriptionId}");
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
        // Clean up all created resource groups
        Console.WriteLine($"\n[Cleanup] Deleting {_createdResourceGroups.Count} test resource groups...");
        foreach (var rgName in _createdResourceGroups)
        {
            try
            {
                Console.WriteLine($"[Cleanup] Deleting {rgName}...");
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Delete,
                    $"https://management.azure.com/subscriptions/{_subscriptionId}/resourcegroups/{rgName}?api-version=2025-04-01");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                httpClient.SendAsync(request).GetAwaiter().GetResult();
                Console.WriteLine($"[Cleanup] Delete initiated for {rgName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Failed to delete {rgName}: {ex.Message}");
            }
        }

        (_psEngine as IDisposable)?.Dispose();
    }

    // ==================== PSEngine (Same as PowerShell cmdlets) ====================

    [Benchmark(Description = "PSEngine: List RG")]
    public void PSEngine_ListResourceGroups()
    {
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", _subscriptionId }
        };

        var invocation = new PSCommandInvocation("Resources", "Get-ResourceGroup_List", parameters);
        var outputList = new List<object>();

        _psEngine.Process(invocation, (value, ct) =>
        {
            outputList.Add(value);
            return Task.CompletedTask;
        });
    }

    [Benchmark(Description = "PSEngine: Create RG")]
    public void PSEngine_CreateResourceGroup()
    {
        var rgName = $"{TestRgPrefix}{_instanceId}-ps-{Interlocked.Increment(ref _rgCounter)}";
        _createdResourceGroups.Add(rgName);

        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", _subscriptionId },
            { "Name", rgName },
            { "Location", TestLocation }
        };

        var invocation = new PSCommandInvocation("Resources", "New-ResourceGroup", parameters);
        var outputList = new List<object>();

        _psEngine.Process(invocation, (value, ct) =>
        {
            outputList.Add(value);
            return Task.CompletedTask;
        });
    }

    [Benchmark(Description = "PSEngine: Get RG")]
    public void PSEngine_GetResourceGroup()
    {
        // Ensure we have a resource group to get
        var rgName = $"{TestRgPrefix}{_instanceId}-ps-get";
        if (!_createdResourceGroups.Contains(rgName))
        {
            var createParams = new Dictionary<string, object>
            {
                { "SubscriptionId", _subscriptionId },
                { "Name", rgName },
                { "Location", TestLocation }
            };
            var createInvocation = new PSCommandInvocation("Resources", "New-ResourceGroup", createParams);
            _psEngine.Process(createInvocation, (value, ct) => Task.CompletedTask);
            _createdResourceGroups.Add(rgName);
        }

        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", _subscriptionId },
            { "Name", rgName }
        };

        var invocation = new PSCommandInvocation("Resources", "Get-ResourceGroup_Get", parameters);
        var outputList = new List<object>();

        _psEngine.Process(invocation, (value, ct) =>
        {
            outputList.Add(value);
            return Task.CompletedTask;
        });
    }

    // ==================== PowerShell Process (Full pwsh.exe) ====================

    [Benchmark(Description = "pwsh.exe: New-ResourceGroup")]
    public int Pwsh_NewResourceGroup()
    {
        var rgName = $"{TestRgPrefix}{_instanceId}-pwsh-{Interlocked.Increment(ref _rgCounter)}";
        _createdResourceGroups.Add(rgName);

        var script = $@"
            Import-Module '{_psModulePath}' -Force
            New-ResourceGroup -SubscriptionId '{_subscriptionId}' -Name '{rgName}' -Location '{TestLocation}'
        ";
        return RunPowerShellScript(script);
    }

    [Benchmark(Description = "pwsh.exe: Get-ResourceGroup")]
    public int Pwsh_GetResourceGroup()
    {
        var script = $@"
            Import-Module '{_psModulePath}' -Force
            Get-ResourceGroup -SubscriptionId '{_subscriptionId}'
        ";
        return RunPowerShellScript(script);
    }

    private int RunPowerShellScript(string script)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"",
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
