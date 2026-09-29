// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

// Currently resolves Linux OS profile settings owned by the vm create customization.
// When --image support lands, this resolver can also choose between Linux and Windows
// configuration based on the selected image/OS.
internal static class OSProfileResolver
{
    internal static LinuxConfiguration? Resolve(
        IReadOnlyList<string> sshKeyValues,
        string adminUsername,
        string adminPassword)
    {
        if (sshKeyValues.Count == 0)
        {
            return null;
        }

        var authorizedKeysPath = $"/home/{adminUsername}/.ssh/authorized_keys";
        return new LinuxConfiguration
        {
            DisablePasswordAuthentication = string.IsNullOrEmpty(adminPassword),
            Ssh = new SshConfiguration
            {
                PublicKeys = sshKeyValues.Select(value => new SshPublicKey
                {
                    Path = authorizedKeysPath,
                    KeyData = ResolveSshKeyValue(value)
                }).ToList()
            }
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
