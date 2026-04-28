using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Abstractions;

public record ProcessorContext
    (IRuntimeHttpPipeline HttpPipeline,
     IEventListener EventListener,
     IAuthHandler AuthHandler,
     IOutputHandler OutputHandler);