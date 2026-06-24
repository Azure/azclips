// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed class AgentOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "builtin";
    public BuiltinProviderOptions Builtin { get; set; } = new();
}

public sealed class BuiltinProviderOptions
{
    public string Endpoint { get; set; } = "https://azclis-copilot-apim-beta-eus.azure-api.net/cirrus/openai/";
    public string Scope { get; set; } = "https://management.core.windows.net//.default";
    public string ModelId { get; set; } = "gpt-5.4";
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}