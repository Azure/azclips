using System;
using System.IO;

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

    // Packaged: stage_agent copies the daemon publish's runtimes/ dir verbatim
    // to deps/runtimes/, so the native binary keeps its NuGet-native subpath
    // (runtimes/<rid>/native/). Dev: NuGet restore leaves it next to the exe.
    // Both therefore share the same runtimes/<rid>/native/ tail.
    public static string SdkNativeDir(string rid) => IsPackaged
        ? Path.Combine(AgentExtensionDir, "deps", "runtimes", rid, "native")
        : Path.Combine(BaseDir, "runtimes", rid, "native");
}
