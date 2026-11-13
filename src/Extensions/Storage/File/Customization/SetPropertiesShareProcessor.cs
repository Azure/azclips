// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>Set Share Properties answers 200 with headers only, so the generated pipeline writes no output.</summary>
    public partial class SetPropertiesShareProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName));
    }
}
