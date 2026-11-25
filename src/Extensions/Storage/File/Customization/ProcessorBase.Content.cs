// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Request bodies the generated JSON default gets wrong for file: range and file data is sent as raw bytes.
    /// </summary>
    public abstract partial class ProcessorBase
    {
        partial void OverrideCreateRequestContentBase(object body, ref HttpContent? content)
        {
            content = body switch
            {
                byte[] data => new ByteArrayContent(data),
                _ => content,
            };
        }
    }
}
