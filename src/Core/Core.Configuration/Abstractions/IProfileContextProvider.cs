namespace Core.Configuration.Abstractions;

public interface IProfileContextProvider
{
    ActiveProfileContext? GetActiveProfile();
}

public sealed record ActiveProfileContext(string Name, string ConfigFilePath);
