// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Options;

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
    public string? ApiVersionOverride { get; set; }
}
