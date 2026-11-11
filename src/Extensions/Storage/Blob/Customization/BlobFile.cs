// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>Local file I/O shared by upload and download. Request bodies are byte arrays, so reads are bounded by the caller's chunk size.</summary>
    internal static class BlobFile
    {
        public static FileStream OpenRead(string path) =>
            new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        /// <summary>Reads the whole remaining stream; the caller bounds its length.</summary>
        public static byte[] ReadAll(FileStream file)
        {
            byte[] data = new byte[file.Length - file.Position];
            file.ReadExactly(data);
            return data;
        }

        /// <summary>
        /// Reads the stream in <paramref name="chunkSize"/> chunks. A full chunk reuses one buffer,
        /// so each chunk must be consumed before the next is requested.
        /// </summary>
        public static IEnumerable<byte[]> ReadChunks(FileStream file, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];
            int read;
            while ((read = file.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)) > 0)
            {
                yield return read == buffer.Length ? buffer : buffer[..read];
            }
        }

        /// <summary>
        /// Streams <paramref name="content"/> into a new file. An existing file is never overwritten, and a
        /// partly written file is removed if the copy fails.
        /// </summary>
        public static async Task<long> WriteNewAsync(string path, Stream content, CancellationToken token)
        {
            FileStream file;
            try
            {
                file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException ex) when (File.Exists(path))
            {
                throw new IOException($"'{path}' already exists.", ex);
            }

            try
            {
                await using (file)
                {
                    await content.CopyToAsync(file, token);
                    return file.Length;
                }
            }
            catch
            {
                File.Delete(path);
                throw;
            }
        }
    }
}
