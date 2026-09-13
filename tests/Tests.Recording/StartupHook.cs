// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;

// The runtime requires the startup hook type in the global namespace. Injected into a spawned
// az or pwsh process via DOTNET_STARTUP_HOOKS, so the product carries no test awareness; the
// hook is inert unless the recording environment variables are set.
internal static class StartupHook
{
    public static void Initialize()
    {
        // The host process resolves this assembly's dependencies from its own app base first;
        // anything missing there (the pwsh case) is served from this assembly's directory.
        var hookDirectory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(hookDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        Tests.Recording.RecordingHook.TryRegister();
    }
}
