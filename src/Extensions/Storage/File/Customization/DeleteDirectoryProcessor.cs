// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Delete Directory answers 202 with headers only, so the generated pipeline writes no output.
    /// The service refuses a directory that is not empty.
    /// </summary>
    public partial class DeleteDirectoryProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>Directory path within the share, e.g. <c>parent/child</c>.</summary>
        public string? DirectoryPath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(DirectoryPath), DirectoryPath?.Trim('/')));
    }
}
