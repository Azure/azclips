// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Web;

namespace Tests.Common.Stubs;

/// <summary>
/// Explicit mapping from an ARM resource-type path segment to the minimal JSON
/// fixture returned for it by <see cref="StubHttpTerminateHandler"/>. Bodies are
/// loaded from the fixture files once and cached; per-request cost is an in-memory
/// lookup. Add a resource by adding one entry here plus one fixture file under
/// <c>Stubs/Fixtures</c>.
/// </summary>
internal static class StubResponseMappings
{
    /// <summary>Body returned for any request that matches no mapping.</summary>
    public const string DefaultBody = "{}";

    // Resource-type path segment -> fixture file name. The request path carries a
    // varying resource name, so the stable type segment is matched as a substring.
    private static readonly IReadOnlyList<(string Segment, string File)> s_fixtureFiles = new[]
    {
        ("/virtualNetworks/", "virtualNetwork.json"),
        ("/virtualMachines/", "virtualMachine.json"),
        ("/networkInterfaces/", "networkInterface.json"),
        ("/networkSecurityGroups/", "networkSecurityGroup.json"),
    };

    private static readonly IReadOnlyList<(string Segment, string Body)> s_bodies = LoadFixtures();
    private static readonly IReadOnlyDictionary<string, string> s_keyVaultBodies = LoadKeyVaultFixtures();

    /// <summary>
    /// Returns the minimal JSON body for the request path, or <see cref="DefaultBody"/>
    /// when the path matches no mapped resource type.
    /// </summary>
    public static string ResolveBody(string path, string? query = null)
    {
        string key = path.TrimEnd('/');
        if (s_keyVaultBodies.TryGetValue(key, out var keyVaultBody))
        {
            if (HttpUtility.ParseQueryString(query ?? string.Empty)["skiptoken"] == "benchmark-next" &&
                s_keyVaultBodies.TryGetValue(key + "?skiptoken=benchmark-next", out var nextPage)) return nextPage;
            return keyVaultBody;
        }

        foreach (var (segment, body) in s_bodies)
        {
            if (path.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return body;
            }
        }
        return DefaultBody;
    }

    private static IReadOnlyDictionary<string, string> LoadKeyVaultFixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Stubs", "Fixtures", "keyVault.json")));
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
    }

    private static List<(string, string)> LoadFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Stubs", "Fixtures");
        var list = new List<(string, string)>(s_fixtureFiles.Count);
        foreach (var (segment, file) in s_fixtureFiles)
        {
            var full = Path.Combine(dir, file);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException(
                    $"Stub fixture not found: '{full}'. Ensure Stubs/Fixtures/*.json are copied to output.",
                    full);
            }
            list.Add((segment, File.ReadAllText(full)));
        }
        return list;
    }
}
