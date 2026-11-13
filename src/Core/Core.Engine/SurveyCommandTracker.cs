// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Engine;

internal sealed class SurveyCommandTracker(ILoggingService loggingService, Func<bool> isEnabled, string? filePath = null)
{
    private const string CLIInterceptMessage = "[Survey] Tell us what you think of Azure CLI. This survey should take about 2 minutes. Run 'az survey' to open in browser.";
    private const string PowerShellInterceptMessage = "[Survey] Tell us what you think of Azure PowerShell. This survey should take about 2 minutes. Run 'Open-AzSurvey' to open in browser.";

    internal async Task RecordCommandAsync(ClientType clientType, string version, CancellationToken cancellationToken = default)
    {
        if (clientType is not (ClientType.CLI or ClientType.PowerShell))
            return;

        var path = filePath ?? HomePaths.GetDefaultSurveyFilePath();
        if (string.IsNullOrWhiteSpace(version))
        {
            loggingService.Warn($"Unable to update survey command count in '{path}': client version is unavailable.");
            return;
        }

        bool shouldPrompt;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fileLock = await AcquireLockAsync(path, cancellationToken);
            var state = File.Exists(path)
                ? JsonSerializer.Deserialize<SurveyState>(await File.ReadAllTextAsync(path, cancellationToken))
                    ?? throw new JsonException("Survey state must be a JSON object.")
                : new SurveyState { CommandCount = new CommandCounts { Cli = 0, PowerShell = 0 } };

            if (state.CommandCount is null || state.CommandCount.Cli < 0 || state.CommandCount.PowerShell < 0)
                throw new JsonException("Survey command counts must be non-negative integers.");

            state.Version ??= new ClientVersions();
            string? storedVersion = clientType == ClientType.CLI ? state.Version.Cli : state.Version.PowerShell;
            long previousCount = string.Equals(storedVersion, version, StringComparison.Ordinal)
                ? clientType == ClientType.CLI ? state.CommandCount.Cli : state.CommandCount.PowerShell
                : 0;
            long nextCount = checked(previousCount + 1);
            if (clientType == ClientType.CLI)
            {
                state.Version.Cli = version;
                state.CommandCount.Cli = nextCount;
            }
            else
            {
                state.Version.PowerShell = version;
                state.CommandCount.PowerShell = nextCount;
            }

            var tempPath = path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(state), cancellationToken);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                File.Delete(tempPath);
            }

            shouldPrompt = previousCount == 5;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or OverflowException)
        {
            loggingService.Warn($"Unable to update survey command count in '{path}': {ex.Message}");
            return;
        }

        if (shouldPrompt && isEnabled())
            loggingService.Warn(clientType == ClientType.CLI ? CLIInterceptMessage : PowerShellInterceptMessage);
    }

    private static async Task<FileStream> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = Environment.TickCount64 + 2000;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (Environment.TickCount64 < deadline)
            {
                await Task.Delay(20, cancellationToken);
            }
        }
    }

    private sealed class SurveyState
    {
        [JsonPropertyName("commandcount")]
        public required CommandCounts CommandCount { get; init; }

        [JsonPropertyName("version")]
        public ClientVersions? Version { get; set; }
    }

    private sealed class ClientVersions
    {
        [JsonPropertyName("cli")]
        public string? Cli { get; set; }

        [JsonPropertyName("powershell")]
        public string? PowerShell { get; set; }
    }

    private sealed class CommandCounts
    {
        [JsonPropertyName("cli")]
        public required long Cli { get; set; }

        [JsonPropertyName("powershell")]
        public required long PowerShell { get; set; }
    }
}
