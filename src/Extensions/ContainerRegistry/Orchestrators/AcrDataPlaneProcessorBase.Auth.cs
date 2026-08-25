using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

public partial class AcrDataPlaneProcessorBase<TResponse>
{
    // Auth is handled by each processor's ProcessAsync via AcrTokenProvider.SetupAuthAsync.
    private partial Task AuthOverrideAsync() => Task.CompletedTask;
}