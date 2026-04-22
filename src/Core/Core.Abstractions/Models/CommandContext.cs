using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Abstractions;

public record CommandContext
    (IRuntimeHttpPipeline HttpPipeline,
     IEventListener EventListener,
     ICommandAuthHandler CommandAuthHandler,
     ICommandOutputHandler CommandOutputHandler);