using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Provider;

namespace Core.Runtime
{
    public class CommandContext : ICommandContext
    {
        public ICommandAuthProvider CommandAuthProvider { get; set; } = null!;
        public IRuntimeHttpPipeline HttpPipeline { get; set; } = null!;
    }
}