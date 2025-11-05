using Core.SharedContracts;

namespace Core.Cli.Parser;

public interface IParser
{
    public Task<ICommand<TResult>> ParseAsync<TResult>(string[] args);
}
