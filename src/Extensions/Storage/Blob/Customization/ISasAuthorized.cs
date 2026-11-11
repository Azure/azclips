// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Opts a processor in to SAS auth as the alternative to Shared Key. The processor declares <see cref="SasToken"/>
    /// itself, so its dialect visibility is expressed on that class; the auth switch lives in the processor base.
    /// </summary>
    public interface ISasAuthorized
    {
        /// <summary>A shared access signature query string (with or without the leading '?'). Takes precedence over the account key.</summary>
        string? SasToken { get; set; }
    }
}
