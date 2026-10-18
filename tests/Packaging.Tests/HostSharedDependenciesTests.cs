using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Azclips.Packaging;
using Core.Abstractions;

namespace Packaging.Tests;

public sealed class HostSharedDependenciesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"azclips-packaging-{Guid.NewGuid():N}");
    private string Publish => Path.Combine(_root, "publish");
    private string Source => Path.Combine(_root, "builtin", "Microsoft.Azure.Azclips.Extensions.Accounts");
    private string Destination => Path.Combine(Publish, "Extensions", Path.GetFileName(Source));

    public HostSharedDependenciesTests()
    {
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Destination);
    }

    [Theory]
    [InlineData("Core.Authentication", true)]
    [InlineData("Core.Abstractions", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions", true)]
    [InlineData("Microsoft.Azure.Azclips.ExtensionRuntime", false)]
    [InlineData("System.Security.Cryptography.Pkcs", false)]
    [InlineData("Newtonsoft.Json", false)]
    [InlineData(null, false)]
    public void Policy_MatchesExistingRuntimeOwnership(string? name, bool expected)
    {
        Assert.Equal(expected, HostSharedAssemblyPolicy.IsShared(name));
    }

    [Theory]
    [InlineData("Core.Authentication")]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
    public void Normalize_RemovesSharedDllsDespiteDifferentBuildBytes(string name)
    {
        var source = WriteAssembly(Source, name);
        var host = WriteAssembly(Publish, name);
        Assert.NotEqual(File.ReadAllBytes(source), File.ReadAllBytes(host));
        File.Copy(source, Path.Combine(Destination, $"{name}.dll"));

        HostSharedDependencies.Normalize(Publish, [Source]);

        Assert.False(File.Exists(Path.Combine(Destination, $"{name}.dll")));
        Assert.True(File.Exists(host));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Normalize_PreservesPrivateDependenciesAndMetadata()
    {
        foreach (var name in new[] { "Microsoft.Azure.Azclips.ExtensionRuntime", "System.Security.Cryptography.Pkcs", "Newtonsoft.Json" })
        {
            var source = WriteAssembly(Source, name);
            WriteAssembly(Publish, name);
            File.Copy(source, Path.Combine(Destination, $"{name}.dll"));
        }
        File.WriteAllText(Path.Combine(Destination, "Accounts.commands.json"), "{}");
        File.WriteAllText(Path.Combine(Destination, "Accounts.deps.json"), "{}");

        HostSharedDependencies.Normalize(Publish, [Source]);

        Assert.Equal(5, Directory.GetFiles(Destination).Length);
    }

    [Fact]
    public void Normalize_RejectsMissingHostDependencyBeforeDeletingAnything()
    {
        var source = WriteAssembly(Source, "Core.Authentication");
        var staged = Path.Combine(Destination, Path.GetFileName(source));
        File.Copy(source, staged);

        var error = Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));

        Assert.Contains("missing", error.Message);
        Assert.Contains("Core.Authentication", error.Message);
        Assert.True(File.Exists(staged));
    }

    [Fact]
    public void Normalize_ValidatesReferencesEvenWithoutCopyLocalDependencies()
    {
        WriteAssembly(Source, "Microsoft.Azure.Azclips.Extensions.Accounts", reference: "Core.Authentication");

        var error = Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));

        Assert.Contains("Core.Authentication", error.Message);
        WriteAssembly(Publish, "Core.Authentication");
        HostSharedDependencies.Normalize(Publish, [Source]);
    }

    [Fact]
    public void Normalize_RejectsOlderHostAssembly()
    {
        WriteAssembly(Source, "Core.Authentication", new Version(2, 0, 0, 0));
        WriteAssembly(Publish, "Core.Authentication", new Version(1, 0, 0, 0));

        var error = Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));

        Assert.Contains("Incompatible", error.Message);
    }

    [Fact]
    public void Normalize_AllowsNewerHostAssemblyLikeRuntimeResolution()
    {
        WriteAssembly(Source, "Core.Authentication", new Version(1, 0, 0, 0));
        WriteAssembly(Publish, "Core.Authentication", new Version(2, 0, 0, 0));

        HostSharedDependencies.Normalize(Publish, [Source]);
    }

    [Fact]
    public void Normalize_RejectsDifferentAssemblyNameBehindSameFilename()
    {
        WriteAssembly(Source, "Core.Authentication");
        var wrong = WriteAssembly(Publish, "Wrong.Assembly");
        File.Move(wrong, Path.Combine(Publish, "Core.Authentication.dll"));

        Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));
    }

    [Fact]
    public void Normalize_RejectsDifferentPublicKeyToken()
    {
        WriteAssembly(Source, "Core.Authentication", publicKey: typeof(object).Assembly.GetName().GetPublicKey());
        WriteAssembly(Publish, "Core.Authentication");

        Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));
    }

    [Fact]
    public void Normalize_RejectsDifferentCulture()
    {
        WriteAssembly(Source, "Core.Authentication", culture: "fr");
        WriteAssembly(Publish, "Core.Authentication");

        Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));
    }

    [Fact]
    public void Normalize_RemovesStaleSharedCopiesOnRepeatedRuns()
    {
        WriteAssembly(Publish, "Core.Authentication");
        var staged = WriteAssembly(Destination, "Core.Authentication");

        HostSharedDependencies.Normalize(Publish, [Source]);
        Assert.False(File.Exists(staged));
        HostSharedDependencies.Normalize(Publish, [Source]);
    }

    [Fact]
    public void Normalize_HandlesAgentStagingInPlace()
    {
        WriteAssembly(Publish, "Core.Authentication");
        WriteAssembly(Destination, "Core.Authentication");
        var probingDirectory = Path.Combine(Destination, "deps", "Core.Authentication", "1.0.0");
        Directory.CreateDirectory(probingDirectory);
        WriteAssembly(probingDirectory, "Core.Authentication");
        var tools = WriteAssembly(Destination, "Azclips.Agent.Tools", reference: "Core.Authentication");

        HostSharedDependencies.Normalize(Publish, [Destination]);

        Assert.Empty(Directory.GetFiles(Destination, "Core.*.dll", SearchOption.AllDirectories));
        Assert.True(File.Exists(tools));
    }

    [Fact]
    public void Normalize_AcceptsTrailingDirectorySeparators()
    {
        WriteAssembly(Source, "Core.Authentication");
        WriteAssembly(Publish, "Core.Authentication");
        WriteAssembly(Destination, "Core.Authentication");

        HostSharedDependencies.Normalize(Publish, [Source + Path.DirectorySeparatorChar]);

        Assert.Empty(Directory.GetFiles(Destination));
    }

    [Fact]
    public void Normalize_RejectsUnexpectedSharedSourceIdentity()
    {
        var source = WriteAssembly(Source, "Private.Dependency");
        File.Move(source, Path.Combine(Source, "Core.Authentication.dll"));
        WriteAssembly(Publish, "Private.Dependency");

        var error = Assert.Throws<InvalidOperationException>(() => HostSharedDependencies.Normalize(Publish, [Source]));

        Assert.Contains("unexpected assembly identity", error.Message);
    }

    [Fact]
    public void Normalize_RejectsInvalidHostAssembly()
    {
        WriteAssembly(Source, "Core.Authentication");
        File.WriteAllText(Path.Combine(Publish, "Core.Authentication.dll"), "not an assembly");

        Assert.Throws<BadImageFormatException>(() => HostSharedDependencies.Normalize(Publish, [Source]));
    }

    [Fact]
    public void Normalize_RequiresExistingDirectories()
    {
        Assert.Throws<DirectoryNotFoundException>(() => HostSharedDependencies.Normalize(Path.Combine(_root, "missing"), [Source]));
        Assert.Throws<DirectoryNotFoundException>(() => HostSharedDependencies.Normalize(Publish, [Path.Combine(_root, "missing")]));
    }

    [Fact]
    public async Task Injector_RemovesSharedCopiesAndPreservesPrivateAssetsOnRepeatedRuns()
    {
        WriteAssembly(Source, "Microsoft.Azure.Azclips.Extensions.Accounts", reference: "Core.Authentication");
        WriteAssembly(Source, "Core.Authentication");
        WriteAssembly(Publish, "Core.Authentication");
        var privateDll = WriteAssembly(Source, "Private.Dependency");
        WriteAssembly(Publish, "Private.Dependency");
        File.WriteAllText(Path.Combine(Source, "Accounts.commands.json"), "{}");
        File.WriteAllText(Path.Combine(Source, "Accounts.deps.json"), "{}");
        WriteAssembly(Destination, "Core.Obsolete");

        for (var run = 0; run < 2; run++)
        {
            var (exitCode, output) = await RunInjector();
            Assert.True(exitCode == 0, output);
            Assert.Empty(Directory.GetFiles(Destination, "Core.*.dll"));
            Assert.Equal(File.ReadAllBytes(privateDll), File.ReadAllBytes(Path.Combine(Destination, "Private.Dependency.dll")));
            Assert.True(File.Exists(Path.Combine(Destination, "Accounts.commands.json")));
            Assert.True(File.Exists(Path.Combine(Destination, "Accounts.deps.json")));
        }
    }

    [Fact]
    public async Task Injector_PropagatesMissingHostFailure()
    {
        WriteAssembly(Source, "Microsoft.Azure.Azclips.Extensions.Accounts", reference: "Core.Authentication");

        var (exitCode, output) = await RunInjector();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Core.Authentication", output);
        Assert.Contains("missing", output);
    }

    private async Task<(int ExitCode, string Output)> RunInjector()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "azclips.slnx")))
        {
            repository = repository.Parent;
        }
        Assert.NotNull(repository);
        var windows = OperatingSystem.IsWindows();
        var script = Path.Combine(repository.FullName, "devops", "scripts", "packaging", "common",
            windows ? "inject_builtin_extensions.ps1" : "inject_builtin_extensions.sh");
        var start = new ProcessStartInfo(windows ? "pwsh" : "bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repository.FullName
        };
        foreach (var argument in windows
            ? new[] { "-NoProfile", "-File", script, "-PublishDir", Publish, "-BuiltinRoot", Path.GetDirectoryName(Source)! }
            : new[] { script, "--publish-dir", Publish, "--builtin-root", Path.GetDirectoryName(Source)! })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["PACKAGE_PROFILE"] = "full";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string WriteAssembly(string directory, string name, Version? version = null, string? reference = null,
        byte[]? publicKey = null, string? culture = null)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString($"{name}.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), version ?? new Version(1, 0, 0, 0),
            culture is null ? default : metadata.GetOrAddString(culture),
            publicKey is null ? default : metadata.GetOrAddBlob(publicKey),
            publicKey is null ? 0 : AssemblyFlags.PublicKey, AssemblyHashAlgorithm.Sha256);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        if (reference is not null)
        {
            metadata.AddAssemblyReference(metadata.GetOrAddString(reference), new Version(1, 0, 0, 0), default, default, 0, default);
        }

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        var path = Path.Combine(directory, $"{name}.dll");
        File.WriteAllBytes(path, blob.ToArray());
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
