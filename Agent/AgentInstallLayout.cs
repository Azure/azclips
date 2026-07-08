using System.Text.Json;

namespace Core.Agent;

/// <summary>
/// Single source of truth for "where do this daemon's assets live". The
/// packaged install layout (launcher siblings share the runtime pack; every
/// agent-only asset lives under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/)
/// and the dev-loop layout (dotnet build output — prompts next to the exe,
/// copilot native under NuGet's default runtimes/&lt;rid&gt;/native/) differ, so
/// callers that need an asset path must route through here rather than
/// probing the filesystem each time.
///
/// <para>The packaged/dev decision is a compile-time constant baked in by the
/// .csproj: packaging scripts publish with <c>/p:IsPackagedBuild=true</c>,
/// dev builds don't. Each dll carries its own flavor — an MSI-installed
/// daemon and a locally-built one on the same box each look only at their
/// own <c>#if</c>, so filesystem coincidences (both layouts existing) can
/// never confuse them.</para>
/// </summary>
internal static class AgentInstallLayout
{
#if AGENT_DEV_BUILD
    public const bool IsPackaged = false;
#else
    public const bool IsPackaged = true;
#endif

    private static string BaseDir => AppContext.BaseDirectory;

    public static string AgentExtensionDir { get; } = Path.Combine(BaseDir,
        "Extensions", "Microsoft.Azure.Azclips.Extensions.Agent");

    public static string PromptsDir { get; } = IsPackaged
        ? Path.Combine(AgentExtensionDir, "prompts")
        : Path.Combine(BaseDir, "prompts");

    public static string SdkNativeDir(string rid) => IsPackaged
        ? Path.Combine(AgentExtensionDir, "deps", "github.copilot.sdk",
            ReferencedSdkVersionOrThrow(), "runtimes", rid, "native")
        : Path.Combine(BaseDir, "runtimes", rid, "native");

    private static string? _cachedSdkVersion;
    private static string ReferencedSdkVersionOrThrow()
    {
        if (_cachedSdkVersion is not null) return _cachedSdkVersion;
        var depsJson = Path.Combine(BaseDir, "azclips-agent.deps.json");
        if (!File.Exists(depsJson))
            throw new InvalidOperationException(
                $"azclips-agent.deps.json not found at {depsJson}; cannot resolve Copilot SDK version.");
        using var doc = JsonDocument.Parse(File.ReadAllText(depsJson));
        foreach (var tfm in doc.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (var lib in tfm.Value.EnumerateObject())
            {
                var parts = lib.Name.Split('/', 2);
                if (parts.Length == 2 &&
                    parts[0].Equals("GitHub.Copilot.SDK", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedSdkVersion = parts[1];
                    return _cachedSdkVersion;
                }
            }
        }
        throw new InvalidOperationException(
            $"GitHub.Copilot.SDK entry not found in {depsJson}.");
    }
}
