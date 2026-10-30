// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public static class ProductReleaseInfo
{
    // (Added by @DANIEL)
    // Introducing this until there is a shared/dynamic source for product release info (Ie Preview/GA). 
    // This is required for MsLearn/Help tool tagging of commands/etc, with tags.
    public const HelpTag Maturity = HelpTag.Preview;
}
