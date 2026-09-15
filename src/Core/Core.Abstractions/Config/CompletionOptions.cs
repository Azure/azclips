// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("completion")]
public sealed class CompletionOptions
{
    [ConfigurationKey("enabled", Description = "Enable or disable az shell tab-completion.")]
    public bool Enabled { get; set; } = true;
}
