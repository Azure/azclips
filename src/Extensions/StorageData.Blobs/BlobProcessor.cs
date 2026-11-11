// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.StorageData.Blobs;

public sealed class BlobProcessor : IProcessor
{
    private ProcessorContext _context = null!;

    public IProcessor Init(ProcessorContext context)
    {
        _context = context;
        return this;
    }

    public Task ProcessAsync() => _context.OutputHandler.WriteOutput(new
    {
        isMock = true,
        command = "storage blob",
        assembly = typeof(BlobProcessor).Assembly.GetName().Name,
        message = "Mock blob command: no Azure requests were made."
    }, CancellationToken.None);
}
