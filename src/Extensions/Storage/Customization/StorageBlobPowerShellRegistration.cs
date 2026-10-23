// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public static class StorageBlobPowerShellRegistration
{
    public static void AddPowerShell(Dictionary<string, CommandMetadata> table)
    {
        Add<SetStorageBlobContentPowerShellCommand>("Set-AzStorageBlobContent");
        Add<GetStorageBlobContentPowerShellCommand>("Get-AzStorageBlobContent");
        Add<StartStorageBlobCopyPowerShellCommand>("Start-AzStorageBlobCopy");

        void Add<T>(string name) where T : IProcessor
        {
            ParameterMetadata[] parameters =
            [
                new("Plan", "Plan", typeof(StorageBlobPowerShellPlan), "Locally validated and frozen Blob operation.", true, [], []),
                new("Profile", "Profile", typeof(string), "Use the named connection profile.", false, [], [])
            ];
            table.Add(name, new(typeof(T).Name, name, [], typeof(T).FullName!, typeof(T).Assembly.GetName().Name!, typeof(T),
                parameters.ToDictionary(parameter => parameter.InvocationName, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        }
    }
}
