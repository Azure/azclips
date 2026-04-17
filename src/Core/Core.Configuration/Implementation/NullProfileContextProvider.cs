using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class NullProfileContextProvider : IProfileContextProvider
{
    public ActiveProfileContext? GetActiveProfile() => null;
}
