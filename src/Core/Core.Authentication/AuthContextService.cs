// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Text.Json;

namespace Core.Authentication;

public class AuthContextService(ILoggingService Logger, ITelemetryService TelemetryService) : IAuthContextService // todo: testability, consider injecting file system abstractions
{
    private static readonly string ContextDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(ContextDirectory, "context.json");

    internal static string BaseDirectory => ContextDirectory;
    private AuthContext? _currentContext;

    public async Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(AuthContextService)}.{nameof(SaveContextAsync)}");
        try
        {
            Directory.CreateDirectory(ContextDirectory);
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var json = JsonSerializer.Serialize(authContext, options);
            Logger.Verbose($"[{nameof(AuthContextService)}] Saving authentication context to: {ContextFilePath}");
            await File.WriteAllTextAsync(ContextFilePath, json, cancellationToken);
            _currentContext = authContext;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save authentication context.", ex);
        }
    }

    public async Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(AuthContextService)}.{nameof(GetCurrentContextAsync)}");
        _currentContext = await LoadContextAsync(cancellationToken);  // todo: proper cache management (synchronization, multi-account, etc.) instead of hard-reloading
        TelemetryService.AddTag(TelemetryTags.SubscriptionId, _currentContext?.SubscriptionId);
        TelemetryService.AddTag(TelemetryTags.TenantId, _currentContext?.TenantId);
        TelemetryService.AddTag(TelemetryTags.Mode, _currentContext?.Mode.ToString());
        return _currentContext;
    }

    private async Task<AuthContext?> LoadContextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(ContextFilePath))
            {
                Logger.Verbose($"[{nameof(AuthContextService)}] Loading persisted authentication context from: {ContextFilePath}");
                var json = await File.ReadAllTextAsync(ContextFilePath, cancellationToken);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<AuthContext>(json, options);
            }
            else
            {
                Logger.Verbose($"[{nameof(AuthContextService)}] No persisted authentication context found.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load authentication context from file.", ex);
        }
        return null;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(AuthContextService)}.{nameof(ClearContextAsync)}");
        _currentContext = null;
        if (File.Exists(ContextFilePath))
        {
            Logger.Verbose($"[{nameof(AuthContextService)}] Deleting persisted authentication context: {ContextFilePath}");
            File.Delete(ContextFilePath);
        }
        return Task.CompletedTask;
    }
}
