// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

public static class AccountsLinks
{
    public static readonly string Survey = $"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc={Uri.EscapeDataString(GetProjectVersion())}";
    private static string GetProjectVersion()
    {
        Assembly assembly = typeof(AccountsLinks).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        int buildMetadataStart = version.IndexOf('+');
        return buildMetadataStart >= 0 ? version[..buildMetadataStart] : version;
    }
}
