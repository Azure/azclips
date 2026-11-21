// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;

namespace Core.Engine.Tests;

public sealed class SurveyCommandTrackerTests : IDisposable
{
    private const string CliVersion = "0.1.5";
    private const string PowerShellVersion = "0.1.2";
    private const string CliMessage = "[Survey] Tell us what you think of Azure CLI. This survey should take about 2 minutes. Run 'az survey' to open in browser.";
    private const string PowerShellMessage = "[Survey] Tell us what you think of Azure PowerShell. This survey should take about 2 minutes. Run 'Open-AzSurvey' to open in browser.";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"azclips-survey-{Guid.NewGuid():N}");
    private readonly ConcurrentQueue<string> _warnings = new();
    private bool _enabled = true;
    private string StatePath => Path.Combine(_directory, "azclips-survey.json");

    private SurveyCommandTracker CreateTracker()
    {
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop,
            (value, _) => { _warnings.Enqueue((string)value); return Task.CompletedTask; }, noop, noop);
        return new SurveyCommandTracker(new OutputStreamAdapter(streams), () => _enabled, StatePath);
    }

    private async Task<long> ReadCountAsync(string client)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(StatePath));
        return document.RootElement.GetProperty("commandcount").GetProperty(client).GetInt64();
    }

    private async Task<string?> ReadVersionAsync(string client)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(StatePath));
        return document.RootElement.GetProperty("version").GetProperty(client).GetString();
    }

    [Theory]
    [InlineData(ClientType.CLI, 1, 0)]
    [InlineData(ClientType.PowerShell, 0, 1)]
    public async Task FirstCommand_CreatesDirectoryAndFile_WithIndependentCounters(
        ClientType clientType, long cliCount, long powerShellCount)
    {
        await CreateTracker().RecordCommandAsync(clientType, clientType == ClientType.CLI ? CliVersion : PowerShellVersion);

        Assert.Equal(cliCount, await ReadCountAsync("cli"));
        Assert.Equal(powerShellCount, await ReadCountAsync("powershell"));
        Assert.Equal(clientType == ClientType.CLI ? CliVersion : null, await ReadVersionAsync("cli"));
        Assert.Equal(clientType == ClientType.PowerShell ? PowerShellVersion : null, await ReadVersionAsync("powershell"));
        Assert.Empty(_warnings);
    }

    [Fact]
    public async Task Clients_CountIndependently_AndPromptOnceOnTheirSixthCommand()
    {
        for (int i = 0; i < 5; i++)
        {
            await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);
            await CreateTracker().RecordCommandAsync(ClientType.PowerShell, PowerShellVersion);
        }
        Assert.Empty(_warnings);

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);
        Assert.Equal(CliMessage, Assert.Single(_warnings));
        Assert.Equal(6, await ReadCountAsync("cli"));
        Assert.Equal(5, await ReadCountAsync("powershell"));

        await CreateTracker().RecordCommandAsync(ClientType.PowerShell, PowerShellVersion);
        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);
        await CreateTracker().RecordCommandAsync(ClientType.PowerShell, PowerShellVersion);

        Assert.Equal(7, await ReadCountAsync("cli"));
        Assert.Equal(7, await ReadCountAsync("powershell"));
        Assert.Equal(2, _warnings.Count);
        Assert.Contains(CliMessage, _warnings);
        Assert.Contains(PowerShellMessage, _warnings);
    }

    [Theory]
    [InlineData(ClientType.CLI, "cli", "0.1.5", "0.1.6")]
    [InlineData(ClientType.PowerShell, "powershell", "0.1.2", "0.1.3")]
    public async Task DisabledReminder_StillCountsAndDoesNotAppearRetroactively(
        ClientType clientType, string client, string version, string newVersion)
    {
        _enabled = false;
        for (int i = 0; i < 6; i++)
            await CreateTracker().RecordCommandAsync(clientType, version);

        Assert.Equal(6, await ReadCountAsync(client));
        Assert.Empty(_warnings);

        _enabled = true;
        await CreateTracker().RecordCommandAsync(clientType, version);
        Assert.Equal(7, await ReadCountAsync(client));
        Assert.Empty(_warnings);

        for (int i = 0; i < 6; i++)
            await CreateTracker().RecordCommandAsync(clientType, newVersion);

        Assert.Equal(6, await ReadCountAsync(client));
        Assert.Equal(clientType == ClientType.CLI ? CliMessage : PowerShellMessage, Assert.Single(_warnings));
    }

    [Fact]
    public async Task ConcurrentCommands_DoNotLoseIncrements()
    {
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            CreateTracker().RecordCommandAsync(
                i % 2 == 0 ? ClientType.CLI : ClientType.PowerShell,
                i % 2 == 0 ? CliVersion : PowerShellVersion)));

        Assert.Equal(10, await ReadCountAsync("cli"));
        Assert.Equal(10, await ReadCountAsync("powershell"));
        Assert.Equal(2, _warnings.Count);
        Assert.Contains(CliMessage, _warnings);
        Assert.Contains(PowerShellMessage, _warnings);
    }

    [Fact]
    public async Task ChangedVersion_ResetsOnlyItsClientAndStartsAtOne()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath,
            """{"commandcount":{"cli":5,"powershell":4},"version":{"cli":"0.1.4","powershell":"0.1.2"}}""");

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);

        Assert.Equal(1, await ReadCountAsync("cli"));
        Assert.Equal(4, await ReadCountAsync("powershell"));
        Assert.Equal(CliVersion, await ReadVersionAsync("cli"));
        Assert.Equal(PowerShellVersion, await ReadVersionAsync("powershell"));
        Assert.Empty(_warnings);

        await CreateTracker().RecordCommandAsync(ClientType.PowerShell, "0.1.3");

        Assert.Equal(1, await ReadCountAsync("powershell"));
        Assert.Equal("0.1.3", await ReadVersionAsync("powershell"));
        Assert.Equal(1, await ReadCountAsync("cli"));
    }

    [Fact]
    public async Task LegacyState_ResetsEachClientOnItsFirstVersionedCommand()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath, """{"commandcount":{"cli":5,"powershell":3}}""");

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);
        Assert.Equal(1, await ReadCountAsync("cli"));
        Assert.Equal(3, await ReadCountAsync("powershell"));
        Assert.Null(await ReadVersionAsync("powershell"));

        await CreateTracker().RecordCommandAsync(ClientType.PowerShell, PowerShellVersion);
        Assert.Equal(1, await ReadCountAsync("powershell"));
        Assert.Equal(PowerShellVersion, await ReadVersionAsync("powershell"));
        Assert.Empty(_warnings);
    }

    [Fact]
    public async Task SameVersion_ContinuesExistingCountAndPromptsOnSixth()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath,
            """{"commandcount":{"cli":5,"powershell":3},"version":{"cli":"0.1.5","powershell":"0.1.2"}}""");

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);

        Assert.Equal(6, await ReadCountAsync("cli"));
        Assert.Equal(3, await ReadCountAsync("powershell"));
        Assert.Equal(CliMessage, Assert.Single(_warnings));
    }

    [Fact]
    public async Task MissingClientVersion_WarnsWithoutChangingFile()
    {
        Directory.CreateDirectory(_directory);
        var json = """{"commandcount":{"cli":5,"powershell":3}}""";
        await File.WriteAllTextAsync(StatePath, json);

        await CreateTracker().RecordCommandAsync(ClientType.CLI, "");

        Assert.StartsWith("Unable to update survey command count", Assert.Single(_warnings));
        Assert.Equal(json, await File.ReadAllTextAsync(StatePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("custom-home")]
    public void DefaultPath_UsesSameDirectoryAsConfig(string? userProfilePath)
    {
        var configDirectory = Path.GetDirectoryName(HomePaths.GetDefaultConfigFilePath(userProfilePath))!;

        Assert.Equal(Path.Combine(configDirectory, "azclips-survey.json"), HomePaths.GetDefaultSurveyFilePath(userProfilePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"commandcount":null}""")]
    [InlineData("""{"commandcount":{"cli":1}}""")]
    [InlineData("""{"commandcount":{"cli":-1,"powershell":0}}""")]
    [InlineData("""{"commandcount":{"cli":"five","powershell":0}}""")]
    [InlineData("""{"commandcount":{"cli":9223372036854775807,"powershell":0},"version":{"cli":"0.1.5","powershell":null}}""")]
    [InlineData("""{"commandcount":{"cli":1,"powershell":0},"version":"invalid"}""")]
    public async Task InvalidState_WarnsWithoutOverwritingFile(string json)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath, json);

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);

        Assert.StartsWith("Unable to update survey command count", Assert.Single(_warnings));
        Assert.Equal(json, await File.ReadAllTextAsync(StatePath));
    }

    [Fact]
    public async Task StorageFailure_WarnsWithoutThrowing()
    {
        Directory.CreateDirectory(StatePath);

        await CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion);

        Assert.StartsWith("Unable to update survey command count", Assert.Single(_warnings));
    }

    [Fact]
    public async Task Cancellation_DoesNotCreateState()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTracker().RecordCommandAsync(ClientType.CLI, CliVersion, cancellation.Token));

        Assert.False(File.Exists(StatePath));
        Assert.Empty(_warnings);
    }

    [Fact]
    public async Task OtherClients_DoNotCreateState()
    {
        await CreateTracker().RecordCommandAsync(ClientType.TUI, CliVersion);

        Assert.False(File.Exists(StatePath));
        Assert.Empty(_warnings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
