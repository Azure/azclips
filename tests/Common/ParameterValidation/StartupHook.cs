// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;

internal static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("AZCLIPS_TEST_PARAMETER_VALIDATION") != "1")
        {
            return;
        }

        var directory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Tests.Common.ParameterValidation.ValidationHook.Register();
        Environment.SetEnvironmentVariable("AZCLIPS_TEST_PARAMETER_VALIDATION_ACTIVE", "1");
    }
}
