// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

public static class AccountsLinks
{
    public static readonly string Survey =
        $"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc=" +
        Uri.EscapeDataString(AssemblyVersionProvider.GetVersion(typeof(AccountsLinks).Assembly, "0.0.0"));
}
