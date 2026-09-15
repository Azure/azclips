// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Options;

using Core.Abstractions;

[ConfigurationSection("extensions")]
public sealed class ExtensionsOptions
{
    public ResourcesOptions Resources { get; set; } = new();
}

public sealed class ResourcesOptions
{
    public ArmOptions Arm { get; set; } = new();
}

public sealed class ArmOptions
{
    [ConfigurationKey("apiVersionOverride", Description = "Override ARM API version.")]
    public string? ApiVersionOverride { get; set; }
}
