namespace Core.AssemblyLoading;

using System.Reflection;
using Core.Abstractions;

public class DefaultAssemblyLoadingProvider(): IAssemblyLoadingProvider
{
    private const string azclipsPrefix = "Microsoft.Azure.Azclips.Extensions.";
    private const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";
    private static readonly string[] s_extensionPackagePrefixes = [azclipsPrefix, cirrusPrefix];

    //Yabo: Default implementation of assembly loading.
    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        var failures = new List<Exception>();
        foreach (var prefix in s_extensionPackagePrefixes)
        {
            var assemblyName = prefix + extensionName;
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch (System.IO.FileNotFoundException)
            {
                failures.Add(new System.IO.FileNotFoundException($"Extension assembly '{assemblyName}' was not found."));
            }
            catch (System.BadImageFormatException)
            {
                failures.Add(new System.BadImageFormatException($"Extension assembly '{assemblyName}' is not a valid assembly."));
            }
        }

        throw new InvalidOperationException(
            $"Extension '{extensionName}' could not be found by any extension loader.",
            new AggregateException(failures));
    }

    public IList<Tuple<string, string?>>? GetAllInstalledExtensions()
    {
        //Yabo: Default implementation does not support extension discovery, return null to indicate no extensions found.
        return null;
    }
}