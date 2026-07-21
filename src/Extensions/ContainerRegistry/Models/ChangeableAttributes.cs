// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

public class ChangeableAttributes
{
    public bool? DeleteEnabled { get; set; }
    public bool? WriteEnabled { get; set; }
    public bool? ListEnabled { get; set; }
    public bool? ReadEnabled { get; set; }
}
