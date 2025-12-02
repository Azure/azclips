using Core.Engine;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var commandEngine = await Bootstrapper.StartAsync();
        await commandEngine.ExecuteCommandAsync(args, default);
    }
}
