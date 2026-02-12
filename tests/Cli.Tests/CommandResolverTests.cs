using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Engine;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for CommandResolver — verifies default command resolution,
/// help flag handling, and subcommand routing.
/// Uses a testable subclass to inject fake extension indexes.
/// </summary>
public class CommandResolverTests
{
    #region Test infrastructure

    [CommandInfo(Name = "", Description = "Default test command.")]
    private class FakeDefaultCommand { }

    [CommandInfo(Name = "sub", Description = "Sub test command.")]
    private class FakeSubCommand { }

    [CommandInfo(Name = "leaf", Description = "Leaf command.")]
    private class FakeLeafCommand { }

    private class TestCommandMetadata(string name, Type commandType) : ICommandMetadata
    {
        public string Name => name;
        public string FullName => commandType.FullName!;
        public string AssemblyName => commandType.Assembly.GetName().Name!;
        public Type CommandType => commandType;
    }

    /// <summary>
    /// Extension index WITH a default ("") command — like Login.
    /// </summary>
    private class ExtensionIndexWithDefault : IExtensionIndex
    {
        private readonly Dictionary<string, ICommandMetadata> _commands = new()
        {
            [""] = new TestCommandMetadata("FakeDefaultCommand", typeof(FakeDefaultCommand)),
            ["sub"] = new TestCommandMetadata("FakeSubCommand", typeof(FakeSubCommand)),
        };

        public ICommandMetadata GetCommandMetaData(string name, ClientType clientType)
            => _commands.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException(name);

        public IEnumerable<ICommandMetadata> GetAllCommands(ClientType clientType) => _commands.Values;
    }

    /// <summary>
    /// Extension index WITHOUT a default command — like Resources (group).
    /// </summary>
    private class ExtensionIndexWithoutDefault : IExtensionIndex
    {
        private readonly Dictionary<string, ICommandMetadata> _commands = new()
        {
            ["leaf"] = new TestCommandMetadata("FakeLeafCommand", typeof(FakeLeafCommand)),
        };

        public ICommandMetadata GetCommandMetaData(string name, ClientType clientType)
            => _commands.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException(name);

        public IEnumerable<ICommandMetadata> GetAllCommands(ClientType clientType) => _commands.Values;
    }

    /// <summary>
    /// Testable subclass of CommandResolver that bypasses real assembly loading
    /// and returns the provided IExtensionIndex for any extension.
    /// </summary>
    private class TestableCommandResolver(IExtensionIndex extensionIndex)
        : CommandResolver(Array.Empty<IExtensionLoader>())
    {
        protected override (Assembly assembly, IExtensionIndex index) LoadExtensionIndex(string extensionName)
            => (typeof(CommandResolverTests).Assembly, extensionIndex);
    }

    #endregion

    // ── Default command tests (the bug scenario) ─────────────────────────

    [Fact]
    public void Resolve_ShouldExecuteDefaultCommand_WhenGroupHasDefault()
    {
        // "azv2 login" → CommandArgs is empty, but Login has a default ("") command
        var resolver = new TestableCommandResolver(new ExtensionIndexWithDefault());

        var result = resolver.Resolve(["login"]);

        Assert.Equal(typeof(FakeDefaultCommand), result.CommandType);
    }

    [Fact]
    public void Resolve_ShouldShowGroupHelp_WhenGroupHasNoDefault()
    {
        // "azv2 group" → CommandArgs is empty and Resources has no default command
        var resolver = new TestableCommandResolver(new ExtensionIndexWithoutDefault());

        Assert.Throws<CommandGroupHelpException>(() => resolver.Resolve(["group"]));
    }

    // ── Help flag tests ─────────────────────────────────────────────────

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Resolve_ShouldShowCommandHelp_WhenHelpFlagOnly_WithDefault(string helpFlag)
    {
        // "azv2 login --help" should show command-level help for the default command
        // (not group help), since the default command is resolved first
        var resolver = new TestableCommandResolver(new ExtensionIndexWithDefault());

        Assert.Throws<CommandHelpException>(() => resolver.Resolve(["login", helpFlag]));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Resolve_ShouldShowGroupHelp_WhenHelpFlagOnly_WithoutDefault(string helpFlag)
    {
        // "azv2 group --help" should show group help
        var resolver = new TestableCommandResolver(new ExtensionIndexWithoutDefault());

        Assert.Throws<CommandGroupHelpException>(() => resolver.Resolve(["group", helpFlag]));
    }

    // ── Subcommand tests ────────────────────────────────────────────────

    [Fact]
    public void Resolve_ShouldResolveSubcommand_WhenSubcommandProvided()
    {
        // "azv2 login sub" should resolve to the sub command, not the default
        var resolver = new TestableCommandResolver(new ExtensionIndexWithDefault());

        var result = resolver.Resolve(["login", "sub"]);

        Assert.Equal(typeof(FakeSubCommand), result.CommandType);
    }

    [Fact]
    public void Resolve_ShouldShowCommandHelp_WhenSubcommandWithHelpFlag()
    {
        // "azv2 login sub --help" should show command-level help for sub
        var resolver = new TestableCommandResolver(new ExtensionIndexWithDefault());

        Assert.Throws<CommandHelpException>(() => resolver.Resolve(["login", "sub", "--help"]));
    }
}
