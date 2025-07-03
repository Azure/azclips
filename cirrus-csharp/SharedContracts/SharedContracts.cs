// File: IProcessor.cs - Interface approach (recommended)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedContracts
{
    /// <summary>
    /// Delegate type for handling CLI request pipeline operations
    // /// </summary>
    // public delegate void NewCLIRequestPipelineDelegate(
    //     System.Action<System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>> prepend,
    //     System.Action<System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, System.Action, System.Func<string, System.Threading.CancellationToken, System.Func<System.EventArgs>, System.Threading.Tasks.Task>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>> append
    // );
    using NewCLIRequestPipelineDelegate = global::System.Action<global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>, global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>>;
    
    public abstract record InvocationInfo;

    public record NoopInvocationInfo : InvocationInfo;

    public record CLIInvocationInfo : InvocationInfo
    {
        /// <summary>
        /// The raw command line arguments passed to the CLI.
        /// </summary>
        public string[] RawCommand { get; init; } = Array.Empty<string>();
        /// <summary>
        /// The name of the module being invoked.
        /// </summary>
        /// <remarks>
        /// This is typically the first argument in the command line and the length should be 1.
        /// </remarks>
        public string[] ModuleName { get; init; } = Array.Empty<string>();
        public string[] CommandName { get; init; } = Array.Empty<string>();
        public bool ShowHelp { get; init; } = false;
        public bool ShowVersion { get; init; } = false;
        public string[] PositionalArguments { get; init; } = Array.Empty<string>();
        public (string arg, string[] values)[] Arguments { get; init; } = Array.Empty<(string, string[])>();
    }

    /// <summary>
    /// Interface that all processors must implement
    /// </summary>
    public interface IProcessor
    {
        Task Process(InvocationInfo invocationInfo);
    }

    /// <summary>
    /// Optional abstract base class for common functionality
    /// </summary>
    public abstract class BaseProcessor : IProcessor
    {
        public abstract Task Process(InvocationInfo invocationInfo);

        protected virtual void LogMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {GetType().Name}: {message}");
        }
    }

    public interface IModule
    {
        NewCLIRequestPipelineDelegate OnNewCLIRequest { get; set; }
        void Init();
    }

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

}