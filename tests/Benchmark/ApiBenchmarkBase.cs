// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.DependencyInjection;

namespace Benchmark;

/// <summary>
/// Base class for live API benchmarks that hit Azure.
/// Handles authentication, resource group lifecycle, and cleanup.
/// </summary>
public abstract class ApiBenchmarkBase : BenchmarkBase
{
    protected string AccessToken { get; private set; } = null!;
    protected string InstanceId { get; private set; } = null!;

    protected readonly List<string> CreatedResourceGroups = new();

    protected const string TestRgPrefix = "benchmark-rg-";
    protected const string ApiVersion = "2025-04-01";
    protected const string TestLocation = "eastus";

    /// <summary>
    /// Initialize for live benchmarks: sets up authentication and finds repo root.
    /// </summary>
    protected void InitializeLive(ClientType clientType = ClientType.CLI)
    {
        CirrusServiceProvider.Initialize(clientType);

        InstanceId = $"{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..4]}";

        RepoRoot = RepoLocator.GetRepoRoot();

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
    /// Clean up all created resource groups in Azure.
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
}
