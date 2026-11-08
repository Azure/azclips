// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

public static class AccountsLinks
{
    public static string CreateSurveyUrl(string productVersion) =>
        $"https://aka.ms/azclipsfeedback?rea2af1a1b73d4b38969cd14f4757cabc=" +
        Uri.EscapeDataString(productVersion);
}
