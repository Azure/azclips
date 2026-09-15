// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Options;

using Core.Abstractions;

[ConfigurationSection("defaults")]
public sealed class DefaultsOptions
{
    [ConfigurationKey("group", Description = "Default resource group for commands that support it.")]
    public string? Group { get; set; }

    [ConfigurationKey("location", Description = "Default location for commands that support it.")]
    public string? Location { get; set; }

    [ConfigurationKey("web", Description = "Default web app name for commands that support it.")]
    public string? Web { get; set; }

    [ConfigurationKey("vm", Description = "Default VM name for commands that support it.")]
    public string? Vm { get; set; }

    [ConfigurationKey("vmss", Description = "Default VMSS name for commands that support it.")]
    public string? Vmss { get; set; }

    [ConfigurationKey("acr", Description = "Default ACR name for commands that support it.")]
    public string? Acr { get; set; }
}
