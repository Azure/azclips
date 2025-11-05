namespace Core.SharedContracts;

public interface ICommand<TResult>
{
    public Task<TResult> ExecuteAsync();
}
