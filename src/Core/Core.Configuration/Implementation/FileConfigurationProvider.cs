// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class FileConfigurationProvider : IConfigurationProvider
{
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const int SharingViolationHResult = unchecked((int)0x80070020);
    private const int LockViolationHResult = unchecked((int)0x80070021);

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public string GetConfigFilePath()
    {
        return HomePaths.GetDefaultConfigFilePath();
    }

    public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default)
    {
        var path = GetConfigFilePath();
        if (!File.Exists(path))
        {
            return [];
        }

        string json;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (IOException ex) when (ShouldRetryFileAccess(ex, stopwatch.Elapsed))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) as JsonObject;
            return parsed ?? [];
        }
        catch (JsonException ex)
        {
            throw AzclipsErrorException.InvalidInput(
                $"The configuration file '{path}' contains invalid JSON. Correct the file contents or delete the file and try again.",
                innerException: ex);
        }
    }

    public async Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var path = GetConfigFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(directory ?? Path.GetTempPath(), $"config.{Guid.NewGuid():N}.tmp");

        try
        {
            var json = configuration.ToJsonString(_serializerOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

            if (File.Exists(path))
            {
                await ReplaceAsync(tempPath, path, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

    }

    private static async Task ReplaceAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            // MoveFileEx can report access denied as well as sharing violations for an open reader.
            // Keep the mutation lock while retrying; persistent permission errors still propagate.
            catch (Exception ex) when (ShouldRetryFileAccess(ex, stopwatch.Elapsed, replacing: true))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static bool ShouldRetryFileAccess(Exception exception, TimeSpan elapsed, bool replacing = false) =>
        OperatingSystem.IsWindows() &&
        elapsed < TimeSpan.FromSeconds(10) &&
        ((exception is IOException &&
          exception.HResult is SharingViolationHResult or LockViolationHResult) ||
         (replacing && exception is UnauthorizedAccessException &&
          exception.HResult == AccessDeniedHResult));
}
