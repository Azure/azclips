//using System.Reflection;

//namespace Core.Cli.Parser;

//public class CommandParser: IParser
//{
//    public Type CommandType { get; }
//    public CommandInfoAttribute CommandInfo { get; }

//    /// <summary>
//    /// Initializes a new instance of the <see cref="CommandParser"/> class.
//    /// </summary>
//    /// <param name="commandType">The type of the command. Must implement <see cref="ICommand{TResult}"/> and have a <see cref="CommandInfoAttribute"/>.</param>
//    /// <exception cref="ArgumentNullException">Thrown when commandType is null.</exception>
//    /// <exception cref="ArgumentException">Thrown when commandType does not implement ICommand{TResult} or does not have CommandInfoAttribute.</exception>
//    public CommandParser(Type commandType)
//    {
//        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType), "Command type cannot be null.");
//        var commandInterface = commandType.GetInterfaces()
//            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)) ?? throw new ArgumentException("Provided type does not implement ICommand<> interface.", nameof(commandType));
//        CommandInfo = commandType.GetCustomAttribute<CommandInfoAttribute>()
//            ?? throw new ArgumentException("Provided command type does not have CommandInfoAttribute.", nameof(commandType));
//    }

//    public Task<ICommand<TResult>> ParseAsync<TResult>(string[] args)
//    {
//        // Parsing logic to create an instance of ICommand<TResult> from args
//        throw new NotImplementedException();
//    }
//}
