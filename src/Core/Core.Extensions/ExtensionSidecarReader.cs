// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Core.Abstractions;
using NuGet.Versioning;

namespace Core.Extensions;

/// <summary>Reads routing metadata without loading the extension assembly.</summary>
public static class ExtensionSidecarReader
{
    public static bool TryRead(
        InstalledExtensionRef installation,
        [NotNullWhen(true)] out ExtensionRoutingInfo? routing,
        out string reason)
    {
        routing = null;
        if (string.IsNullOrWhiteSpace(installation.Location))
        {
            reason = "installation has no assembly location";
            return false;
        }

        var packageId = Path.GetFileNameWithoutExtension(installation.Location);
        var sidecarPath = Path.ChangeExtension(installation.Location, ".commands.json");
        try
        {
            using var stream = File.OpenRead(sidecarPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1)
            {
                reason = "unsupported or missing schemaVersion (expected 1)";
                return false;
            }

            if (!root.TryGetProperty("packageId", out var identity) ||
                identity.ValueKind != JsonValueKind.String ||
                !string.Equals(identity.GetString(), packageId, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"packageId does not match assembly '{packageId}'";
                return false;
            }

            if (!root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                !NuGetVersion.TryParse(version.GetString(), out var sidecarVersion))
            {
                reason = "missing or invalid package version";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(installation.Version) &&
                (!NuGetVersion.TryParse(installation.Version, out var installedVersion) ||
                 VersionComparer.VersionRelease.Compare(sidecarVersion, installedVersion) != 0))
            {
                reason = $"sidecar version '{sidecarVersion}' does not match installation '{installation.Version}'";
                return false;
            }

            if (!TryReadStrings(root, "verbs", out var verbs) ||
                !TryReadStrings(root, "leaves", out var leaves))
            {
                reason = "verbs and leaves must be arrays of nonempty strings";
                return false;
            }

            var declaredVerbs = new HashSet<string>(verbs, StringComparer.OrdinalIgnoreCase);
            if (verbs.Any(verb => verb.Any(char.IsWhiteSpace)) ||
                leaves.Any(leaf => leaf.Split(' ').Any(string.IsNullOrEmpty) ||
                                   leaf.Any(c => char.IsWhiteSpace(c) && c != ' ') ||
                                   !declaredVerbs.Contains(leaf.Split(' ')[0])))
            {
                reason = "invalid command keys or leaves without declared verbs";
                return false;
            }

            // Keep the loader's version, including an unknown version: metadata must not
            // change which installation the execution loader would select.
            routing = new ExtensionRoutingInfo(packageId, installation.Version, verbs, leaves, installation.Location);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            reason = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadStrings(JsonElement root, string property, out string[] values)
    {
        values = [];
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var strings = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                item.GetString() is not { } value || string.IsNullOrWhiteSpace(value) ||
                value != value.Trim())
            {
                return false;
            }
            strings.Add(value);
        }
        values = strings.ToArray();
        return true;
    }
}
