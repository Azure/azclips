// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Request bodies the generated JSON default gets wrong for blob: data is sent as raw bytes, and the block
    /// list as the XML <c>BlockList</c> document.
    /// </summary>
    public abstract partial class ProcessorBase
    {
        partial void OverrideCreateRequestContentBase(object body, ref HttpContent? content)
        {
            content = body switch
            {
                byte[] data => new ByteArrayContent(data),
                BlockLookupList blocks => BlockListContent(blocks),
                _ => content,
            };
        }

        private static ByteArrayContent BlockListContent(BlockLookupList blocks)
        {
            static IEnumerable<XElement> Ids(string name, IList<byte[]>? ids) =>
                (ids ?? []).Select(id => new XElement(name, Convert.ToBase64String(id)));

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("BlockList",
                    Ids("Committed", blocks.Committed),
                    Ids("Uncommitted", blocks.Uncommitted),
                    Ids("Latest", blocks.Latest)));

            using var stream = new MemoryStream();
            document.Save(stream, SaveOptions.DisableFormatting);
            var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };
            return content;
        }
    }
}
