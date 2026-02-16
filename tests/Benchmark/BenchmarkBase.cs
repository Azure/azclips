// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmark;

/// <summary>
/// Base class providing common setup for all benchmarks.
/// Handles authentication, resource group cleanup, and utility methods.
/// </summary>
public abstract class BenchmarkBase
{
    protected string SubscriptionId { get; private set; } = null!;
    protected string AccessToken { get; private set; } = null!;
    protected string InstanceId { get; private set; } = null!;
    protected string RepoRoot { get; private set; } = null!;
    
    protected readonly List<string> CreatedResourceGroups = new();
    protected int RgCounter = 0;
    
    protected const string TestLocation = "eastus";
    protected const string TestRgPrefix = "benchmark-rg-";
    protected const string ApiVersion = "2025-04-01";

    /// <summary>
    /// Initialize common setup: authentication, instance ID, repo root.
    /// </summary>
    protected void InitializeCommon()
    {
        // Unique instance ID to avoid conflicts between runs
        InstanceId = $"{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..4]}";

        // Find repo root
        RepoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? FindRepoRoot(Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Cannot find repository root (looking for cirrus.slnx)");

        // Get auth token
        var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
        var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
        var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");

        var authResult = authService.AcquireTokenSilentlyAsync(
            scopes: null,
            tenantId: authContext.TenantId,
            promptOnError: false).GetAwaiter().GetResult();

        AccessToken = authResult.AccessToken;
        SubscriptionId = authContext.SubscriptionId;

        Console.WriteLine($"[Setup] SubscriptionId: {SubscriptionId}");
    }

    /// <summary>
    /// Clean up all created resource groups.
    /// </summary>
    protected void CleanupResourceGroups()
    {
        Console.WriteLine($"\n[Cleanup] Deleting {CreatedResourceGroups.Count} test resource groups...");
        foreach (var rgName in CreatedResourceGroups)
        {
            try
            {
                Console.WriteLine($"[Cleanup] Deleting {rgName}...");
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Delete,
                    $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{rgName}?api-version={ApiVersion}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
                httpClient.SendAsync(request).GetAwaiter().GetResult();
                Console.WriteLine($"[Cleanup] Delete initiated for {rgName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Failed to delete {rgName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generate a unique resource group name.
    /// </summary>
    protected string GenerateRgName(string suffix = "")
    {
        var name = $"{TestRgPrefix}{InstanceId}-{suffix}{Interlocked.Increment(ref RgCounter)}";
        CreatedResourceGroups.Add(name);
        return name;
    }

    /// <summary>
    /// Run an external process and return exit code.
    /// </summary>
    protected static int RunProcess(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
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
}

/// <summary>
/// Simple IEventListener implementation for benchmarks.
/// </summary>
public class BenchmarkEventListener : IEventListener
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
