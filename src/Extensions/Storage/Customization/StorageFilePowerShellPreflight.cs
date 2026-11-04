// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageFilePowerShellPreflight : FileShareWorkflowBase
{
    public StorageFileDirectory GetDirectoryTarget()
    {
        if (Context is null && ShareObject is null && DirectoryObject is null)
            throw new ArgumentException("Specify a native Storage context or Files resource.");
        return ResolveDirectory(Path);
    }

    protected override Task ExecuteAsync() => throw new NotSupportedException("Use the synchronous preflight method.");
}
