// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Extensions;

/// <summary>
/// Reads the <see cref="AssemblyInformationalVersionAttribute"/> off an extension's primary DLL
/// via <see cref="MetadataLoadContext"/> + <see cref="CustomAttributeData"/>. Used as a fallback
/// when the on-disk <c>.nupkg</c> has been stripped (packaged installs) but the DLL still carries
/// version metadata emitted by the SDK's <c>GenerateAssemblyInfo</c> step.
/// </summary>
/// <remarks>
/// Mirrors the safety posture of <see cref="CompatibilityGuard"/>: never executes extension code,
/// swallows IO/reflection failures and returns an empty string so a corrupt or locked DLL cannot
/// take down <c>az extension list</c>.
/// </remarks>
public static class AssemblyVersionReader
{
    private static readonly string s_attributeFullName = typeof(AssemblyInformationalVersionAttribute).FullName!;

    /// <summary>
    /// Returns the SemVer core (without the SourceLink <c>+&lt;sha&gt;</c> build metadata suffix)
    /// from the <see cref="AssemblyInformationalVersionAttribute"/> on
    /// <paramref name="packageDir"/>/<c>{packageId}.dll</c>, or an empty string if the DLL is
    /// missing, the attribute is absent, or reflection fails.
    /// </summary>
    public static string TryReadInformationalVersion(string packageDir, string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        if (!Directory.Exists(packageDir))
        {
            return string.Empty;
        }

        var primaryDll = Path.Combine(packageDir, packageId + ".dll");
        if (!File.Exists(primaryDll))
        {
            return string.Empty;
        }

        try
        {
            using var mlc = ExtensionMetadataContext.Create(packageDir, out _);
            var assembly = mlc.LoadFromAssemblyPath(primaryDll);
            var attr = assembly.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.FullName == s_attributeFullName);
            if (attr is null || attr.ConstructorArguments.Count == 0)
            {
                return string.Empty;
            }

            var raw = attr.ConstructorArguments[0].Value as string;
            return StripBuildMetadata(raw);
        }
        catch
        {
            // Defensive: same posture as CompatibilityGuard — never let a bad DLL break list.
            return string.Empty;
        }
    }

    private static string StripBuildMetadata(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var plus = raw.IndexOf('+');
        return plus < 0 ? raw : raw[..plus];
    }
}
