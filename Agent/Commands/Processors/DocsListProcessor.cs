// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Agent.Tools.Help;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

public sealed class DocsListProcessor : DocsProcessorBase
{
    protected override Task<string> ReadJsonAsync(HelpCatalogProvider provider) =>
        provider.ListJsonAsync(Token);
}
