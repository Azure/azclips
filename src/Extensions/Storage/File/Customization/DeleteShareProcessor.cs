// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Delete Share answers 202 with headers only, so the generated pipeline writes no output.
    /// A share with snapshots needs <see cref="DeleteSnapshots"/> (<c>include</c> or <c>include-leased</c>).
    /// </summary>
    public partial class DeleteShareProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName));
    }
}
