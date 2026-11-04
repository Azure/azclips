// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageDirectoriesProcessor_Orchestrator : ListStorageFilesProcessor_Orchestrator
{
    protected override bool DirectoriesOnly => true;
}
