using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime
{
    public class CommandContext : ICommandContext
    {
        public ICommandAuthHandler CommandAuthHandler { get; set; } = null!;
        public IRuntimeHttpPipeline HttpPipeline { get; set; } = null!;
    }
}