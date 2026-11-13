// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>Delete File answers 202 with headers only, so the generated pipeline writes no output.</summary>
    public partial class DeleteFileProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>File path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));
    }
}
