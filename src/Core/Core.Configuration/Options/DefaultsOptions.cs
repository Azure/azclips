// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Options;

public sealed class DefaultsOptions
{
    public string? Group { get; set; }

    public string? Location { get; set; }

    public string? Web { get; set; }

    public string? Vm { get; set; }

    public string? Vmss { get; set; }

    public string? Acr { get; set; }
}
