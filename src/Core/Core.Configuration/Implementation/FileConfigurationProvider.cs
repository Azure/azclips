// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class FileConfigurationProvider : IConfigurationProvider
{
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

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonNode.Parse(json) as JsonObject;
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
                File.Move(tempPath, path, overwrite: true);
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
}
