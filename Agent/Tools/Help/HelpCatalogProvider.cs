// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Agent.Tools.Shell;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Reads validated help artifacts in process. Missing, stale, or corrupt artifacts
/// require one regeneration; a failed regeneration is an error, never an empty catalog.
/// </summary>
public sealed class HelpCatalogProvider
{
    public const string ManifestFileName = "HelpManifest.json";
    public const string IndexFileName = "HelpIndex.json";

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions s_read = new(s_json)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    private readonly string _azExePath;
    private readonly IConfigurationService _configuration;
    private readonly string _cacheDirectory;
    private readonly Func<CancellationToken, Task<string>> _regenerate;
    private readonly SemaphoreSlim _generationGate = new(1, 1);

    public HelpCatalogProvider(
        string azExePath,
        IConfigurationService configuration,
        string? cacheDirectory = null,
        Func<CancellationToken, Task<string>>? regenerate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azExePath);
        ArgumentNullException.ThrowIfNull(configuration);
        _azExePath = azExePath;
        _configuration = configuration;
        _cacheDirectory = cacheDirectory ?? HomePaths.GetDefaultAgentHelpDirectory();
        _regenerate = regenerate ?? (ct => ShellOutHelper.InvokeAzAsync(
            _azExePath, ["agent", "tool", "help", "generate"], ct));
    }

    public async Task<string> ListJsonAsync(CancellationToken ct = default) =>
        (await ListAsync(ct).ConfigureAwait(false)).Json;

    public async Task<string> GetJsonAsync(string name, CancellationToken ct = default) =>
        (await GetAsync(name, ct).ConfigureAwait(false)).Json;

    internal async Task<HelpLookupResult> ListAsync(CancellationToken ct = default)
    {
        try
        {
            var document = await ReadFreshAsync<HelpIndexDocument>(IndexFileName, d => d.Header,
                d => ValidIndex(d.Index), ct).ConfigureAwait(false);
            return new(JsonSerializer.Serialize(document.Index, s_json), HelpLookupKind.Found);
        }
        catch (Exception ex) when (ex is CatalogException || IsExpectedReadFailure(ex))
        {
            ct.ThrowIfCancellationRequested();
            return new(ErrorJson(ex), HelpLookupKind.Error);
        }
    }

    internal async Task<HelpLookupResult> GetAsync(string name, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                return new("{\"error\":\"missing_name\",\"hint\":\"Pass cliName or psName.\"}", HelpLookupKind.Error);

            var document = await ReadFreshAsync<HelpManifestDocument>(ManifestFileName, d => d.Header,
                d => ValidManifests(d.Manifests), ct).ConfigureAwait(false);
            var command = HelpLookup.FindCommand(document.Manifests, name);
            return command is null
                ? new(JsonSerializer.Serialize(new { error = "not_found", name, suggestions = HelpLookup.FindSuggestions(document.Manifests, name) }, s_json), HelpLookupKind.NotFound)
                : new(JsonSerializer.Serialize(command, s_json), HelpLookupKind.Found, command);
        }
        catch (Exception ex) when (ex is CatalogException || IsExpectedReadFailure(ex))
        {
            ct.ThrowIfCancellationRequested();
            return new(ErrorJson(ex), HelpLookupKind.Error);
        }
    }

    private async Task<T> ReadFreshAsync<T>(
        string fileName, Func<T, HelpCatalogHeader> header, Func<T, bool> valid, CancellationToken ct) where T : class
    {
        var stamp = InstalledExtensionSet.Read(_azExePath, _configuration, ct);
        var document = await TryReadAsync(fileName, stamp, header, valid, ct).ConfigureAwait(false);
        if (document is not null) return document;

        await _generationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Another tool call may have regenerated while this caller waited.
            stamp = InstalledExtensionSet.Read(_azExePath, _configuration, ct);
            document = await TryReadAsync(fileName, stamp, header, valid, ct).ConfigureAwait(false);
            if (document is not null) return document;

            string result;
            try
            {
                result = await _regenerate(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsExpectedReadFailure(ex) || ex is ExtensionLoadException or BadImageFormatException or TypeLoadException)
            {
                ct.ThrowIfCancellationRequested();
                throw new CatalogException("help_catalog_regeneration_failed");
            }
            ct.ThrowIfCancellationRequested();
            if (!GenerationSucceeded(result))
                throw new CatalogException("help_catalog_regeneration_failed");

            stamp = InstalledExtensionSet.Read(_azExePath, _configuration, ct);
            return await TryReadAsync(fileName, stamp, header, valid, ct).ConfigureAwait(false)
                ?? throw new CatalogException("help_catalog_unavailable");
        }
        finally
        {
            _generationGate.Release();
        }
    }

    private async Task<T?> TryReadAsync<T>(
        string fileName, HelpCatalogHeader stamp, Func<T, HelpCatalogHeader> header,
        Func<T, bool> valid, CancellationToken ct) where T : class
    {
        foreach (var directory in ArtifactDirectories())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(Path.Combine(directory, fileName), FileMode.Open, FileAccess.Read,
                    FileShare.Read | FileShare.Delete, bufferSize: 4096, useAsync: true);
                var document = await JsonSerializer.DeserializeAsync<T>(stream, s_read, ct).ConfigureAwait(false);
                if (document is not null && header(document) == stamp && valid(document) &&
                    InstalledExtensionSet.Read(_azExePath, _configuration, ct) == stamp)
                    return document;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Try other layouts, then regenerate. Never serve the invalid file.
            }
        }

        return null;
    }

    private IEnumerable<string> ArtifactDirectories()
    {
        var cliDirectory = Path.GetDirectoryName(CliInstallation.ResolvePath(_azExePath))!;
        return new[]
        {
            _cacheDirectory,
            cliDirectory,
            Path.Combine(cliDirectory, "Extensions", "Microsoft.Azure.Azclips.Extensions.Agent"),
            Path.GetFullPath(Path.Combine(cliDirectory, "..", "Core.Agent")),
        }.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool GenerationSucceeded(string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                !document.RootElement.TryGetProperty("error", out _) &&
                document.RootElement.TryGetProperty("generated", out var generated) &&
                generated.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ValidIndex(HelpIndex index) =>
        index.Groups.All(g => g is not null) && index.Commands.All(c => c is not null);

    private static bool ValidManifests(IList<HelpManifest> manifests) =>
        manifests.All(m => m is not null && m.Commands.All(c => c is not null &&
            c.Parameters.All(p => p is not null && p.Aliases.All(a => a is not null) && p.AllowedValues.All(v => v is not null)) &&
            c.Examples.Cli.All(e => e is not null) && c.Examples.PowerShell.All(e => e is not null)));

    private static bool IsExpectedReadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException ||
        // ConfigurationService reports invalid JSON as InvalidInput; ConfigurationBinder
        // wraps invalid setting values. Other exceptions of these types are defects.
        ((exception is InvalidInputException || exception.GetType() == typeof(InvalidOperationException)) &&
         exception.InnerException is FormatException or InvalidDataException or JsonException);

    private static string ErrorJson(Exception exception) => JsonSerializer.Serialize(new
    {
        error = exception is CatalogException catalog ? catalog.Code : "help_catalog_read_failed",
        hint = "Run 'az agent tool help generate' to rebuild the help catalog.",
    }, s_json);

    private sealed class CatalogException(string code) : Exception
    {
        public string Code { get; } = code;
    }
}

internal enum HelpLookupKind { Found, NotFound, Error }

/// <summary>Catalog JSON, the lookup outcome, and the matched command for a found get.</summary>
internal sealed record HelpLookupResult(string Json, HelpLookupKind Kind, HelpCommand? Command = null);
