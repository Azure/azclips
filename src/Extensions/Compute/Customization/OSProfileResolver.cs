// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal static class OSProfileResolver
{
    internal static ResolvedLinuxProfile? Resolve(
        IReadOnlyList<string> sshKeyValues,
        string adminUsername,
        string? adminPassword)
    {
        if (sshKeyValues.Count == 0)
        {
            return null;
        }

        var authorizedKeysPath = $"/home/{adminUsername}/.ssh/authorized_keys";
        return new ResolvedLinuxProfile
        {
            DisablePasswordAuthentication = string.IsNullOrWhiteSpace(adminPassword),
            SshPublicKeyPaths = sshKeyValues.Select(_ => authorizedKeysPath).ToList(),
            SshPublicKeyData = sshKeyValues.Select(ResolveSshKeyValue).ToList()
        };
    }

    private static string ResolveSshKeyValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SSH public key value cannot be empty.", nameof(value));
        }

        var trimmedValue = value.Trim();
        var keyData = File.Exists(trimmedValue)
            ? File.ReadAllText(trimmedValue).Trim()
            : trimmedValue;

        return keyData;
    }
}

internal sealed class ResolvedLinuxProfile
{
    public bool DisablePasswordAuthentication { get; init; }
    public List<string> SshPublicKeyPaths { get; init; } = [];
    public List<string> SshPublicKeyData { get; init; } = [];
}
