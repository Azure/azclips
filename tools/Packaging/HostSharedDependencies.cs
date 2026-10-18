using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Core.Abstractions;

namespace Azclips.Packaging;

public static class HostSharedDependencies
{
    public static void Normalize(string publishDirectory, IEnumerable<string> extensionSourceDirectories)
    {
        var publishDir = Path.GetFullPath(publishDirectory);
        if (!Directory.Exists(publishDir))
        {
            throw new DirectoryNotFoundException($"Publish directory not found: {publishDir}");
        }

        var sources = extensionSourceDirectories
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .ToArray();
        var destinations = new List<string>();
        foreach (var source in sources)
        {
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"Extension source directory not found: {source}");
            }

            var destination = Path.Combine(publishDir, "Extensions", Path.GetFileName(source));
            if (!Directory.Exists(destination))
            {
                throw new DirectoryNotFoundException($"Injected extension directory not found: {destination}");
            }

            destinations.Add(destination);
            foreach (var dll in Directory.EnumerateFiles(source, "*.dll", SearchOption.AllDirectories))
            {
                using var stream = File.OpenRead(dll);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata)
                {
                    continue;
                }

                var metadata = pe.GetMetadataReader();
                if (HostSharedAssemblyPolicy.IsShared(Path.GetFileNameWithoutExtension(dll)))
                {
                    var identity = AssemblyName.GetAssemblyName(dll);
                    if (!string.Equals(identity.Name, Path.GetFileNameWithoutExtension(dll), StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Shared DLL '{dll}' has an unexpected assembly identity: '{identity.FullName}'.");
                    }
                    ValidateHost(publishDir, identity, dll);
                }

                // References still need validation when Private=false excluded the shared DLL itself.
                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetAssemblyReference(handle);
                    var name = metadata.GetString(reference.Name);
                    if (!HostSharedAssemblyPolicy.IsShared(name))
                    {
                        continue;
                    }

                    var required = new AssemblyName
                    {
                        Name = name,
                        Version = reference.Version,
                        CultureName = metadata.GetString(reference.Culture)
                    };
                    var key = metadata.GetBlobBytes(reference.PublicKeyOrToken);
                    if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
                    {
                        required.SetPublicKey(key);
                    }
                    else
                    {
                        required.SetPublicKeyToken(key);
                    }

                    ValidateHost(publishDir, required, dll);
                }
            }
        }

        // Validate every extension before modifying staging, including on repeat packaging runs.
        foreach (var destination in destinations.Distinct(StringComparer.Ordinal))
        {
            foreach (var dll in Directory.EnumerateFiles(destination, "*.dll", SearchOption.AllDirectories))
            {
                if (HostSharedAssemblyPolicy.IsShared(Path.GetFileNameWithoutExtension(dll)))
                {
                    File.Delete(dll);
                    Console.WriteLine($"  HOST-SHARED {dll}");
                }
            }
        }
    }

    private static void ValidateHost(string publishDir, AssemblyName required, string source)
    {
        var hostPath = Path.Combine(publishDir, $"{required.Name}.dll");
        if (!File.Exists(hostPath))
        {
            throw new InvalidOperationException($"Host-shared dependency '{required.FullName}' required by '{source}' is missing at '{hostPath}'.");
        }

        var host = AssemblyName.GetAssemblyName(hostPath);
        if (!string.Equals(host.Name, required.Name, StringComparison.Ordinal) ||
            host.Version < required.Version ||
            !string.Equals(host.CultureName ?? "", required.CultureName ?? "", StringComparison.OrdinalIgnoreCase) ||
            !(host.GetPublicKeyToken() ?? []).AsSpan().SequenceEqual(required.GetPublicKeyToken() ?? []))
        {
            throw new InvalidOperationException(
                $"Incompatible host-shared dependency required by '{source}': requested '{required.FullName}', host provides '{host.FullName}' at '{hostPath}'.");
        }
    }
}
