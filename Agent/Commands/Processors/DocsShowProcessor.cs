// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Tools.Help;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

public sealed class DocsShowProcessor : DocsProcessorBase
{
    [ArgumentInfo(Required = true, Description = @"Command name to look up, e.g. 'vm create'.", SerializedName = @"name")]
    public string Name { get; set; } = string.Empty;

    protected override Task<string> ReadJsonAsync(HelpCatalogProvider provider) =>
        provider.GetJsonAsync(Name, Token);
}
