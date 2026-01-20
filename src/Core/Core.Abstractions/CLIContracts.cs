// File: IProcessor.cs - Interface approach (recommended)

/// <summary>
/// Delegate type for handling CLI request pipeline operations
// /// </summary>
// public delegate void NewCLIRequestPipelineDelegate(
//     System.Action<System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>> prepend,
//     System.Action<System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>> append
// );
using NewCLIRequestPipelineDelegate = global::System.Action<global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>, global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>>;

namespace Core.Abstractions;
/// <summary>
/// Interface that all processors must implement
/// </summary>
public interface IProcessor
{
    //Task ProcessAsync();
    // TODO: create wrapper for return object of IProcessor, this wrapper will handle serialization:
    // model => model.ToJson()
    // bool => "True"/"False"
    // null => "Null"
    // enumerable => serialize each item in enumerable
    object? Process();
    // TODO: consider pass serialization format as parameter
    string Process(bool Serialize);
}

/// <summary>
/// Optional abstract base class for common functionality
/// </summary>
public abstract class BaseProcessor : IProcessor
{
    //public abstract Task ProcessAsync();
    public abstract object Process();

    public abstract string Process(bool Serialize);

    protected virtual void LogMessage(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {GetType().Name}: {message}");
    }
}

//public interface IModule
//{
//    NewCLIRequestPipelineDelegate OnNewCLIRequest { get; set; }
//    void Init();
//}

public enum ArgumentAction
{
    Store,
    // StoreTrue,
    // Count,
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ArgumentInfoAttribute : Attribute
{
    public bool Required { get; set; } = false;
    public string Description { get; set; } = "";
    public string[] Enums { get; set; } = Array.Empty<string>();
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public ArgumentAction Action { get; set; } = ArgumentAction.Store;
    /// <summary>
    /// Indicates if the property is positional in the command line arguments.
    /// Each command could have one positional argument.
    /// </summary>
    // public bool Positional { get; set; } = false;
    // public string NumArgs { get; set; } = "1"; // Default to 1, can be set to '*' for variable length
    // public string? Group { get; set; } = null;
    public string SerializedName { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Class)]
public class CommandInfoAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
