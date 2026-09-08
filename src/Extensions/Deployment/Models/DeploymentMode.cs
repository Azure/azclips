// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Deployment.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeploymentMode
{
    Incremental,
    Complete,
}
