using System;
using Xunit;
using AzTool;
using SharedContracts;
using System.Threading.Tasks;

namespace AzTool.Tests
{
    public class HelpDisplayTests
    {
        [CommandInfo(Name = "storage account create", Description = "Create a new storage account in Azure.")]
        public class MockStorageAccountCreateCommand : BaseProcessor
        {
            [ArgumentInfo(Required = true, Description = "The name of the storage account.")]
            public string Name { get; set; } = string.Empty;

            [ArgumentInfo(Description = "The name of the resource group.", Aliases = new[] { "g" })]
            public string ResourceGroupName { get; set; } = string.Empty;

            [ArgumentInfo(Description = "The location where the storage account will be created.", Aliases = new[] { "l", "location" })]
            public string Location { get; set; } = string.Empty;

            [ArgumentInfo(Required = true, Description = "The subscription ID for the storage account.")]
            public string SubscriptionId { get; set; } = string.Empty;

            [ArgumentInfo(Description = "The performance tier of the storage account. Allowed values: Standard, Premium.", Enums = new[] { "Standard", "Premium" })]
            public string PerformanceTier { get; set; } = "Standard";

            [ArgumentInfo(Description = "Enable verbose output for debugging purposes.")]
            public bool Verbose { get; set; } = false;

            public override Task Process()
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldGenerateCorrectHelp()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockStorageAccountCreateCommand);
            uint? terminalWidth = 100;

            // Act
            var result = template.Render(commandType, terminalWidth);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            
            // Check that the command section is present
            Assert.Contains("Command", result);
            
            // Check that the command name and description are included
            var commandLine = Array.Find(result, line => line.Contains("storage account create"));
            Assert.NotNull(commandLine);
            Assert.Contains("Create a new storage account in Azure", commandLine);
            
            // Check that the Arguments section is present
            Assert.Contains("Arguments", result);
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldIncludeRequiredArguments()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockStorageAccountCreateCommand);

            // Act
            var result = template.Render(commandType, null);

            // Assert
            var helpText = string.Join("\n", result);
            
            // Check for required arguments
            Assert.Contains("name", helpText);
            Assert.Contains("subscription-id", helpText);
            Assert.Contains("[Required]", helpText);
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldIncludeAliases()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockStorageAccountCreateCommand);

            // Act
            var result = template.Render(commandType, null);

            // Assert
            var helpText = string.Join("\n", result);
            
            // Check for aliases
            Assert.Contains("g", helpText); // Short alias
            Assert.Contains("location", helpText); // Long alias
            Assert.Contains("l", helpText); // Short alias
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldHandleNullTerminalWidth()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockStorageAccountCreateCommand);

            // Act & Assert
            var result = template.Render(commandType, null);
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldRespectTerminalWidth()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockStorageAccountCreateCommand);
            uint terminalWidth = 80;

            // Act
            var result = template.Render(commandType, terminalWidth);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            
            // Verify no line exceeds the terminal width (allowing for some flexibility)
            foreach (var line in result)
            {
                Assert.True(line.Length <= terminalWidth, $"Line exceeds terminal width: {line}");
            }
        }

        [Fact]
        public void HelpCellContent_LimitWidth_ShouldWrapLongText()
        {
            // Arrange
            var content = new HelpCellContent("This is a very long description that should be wrapped when it exceeds the specified width limit");
            
            // Act
            var newContent = content.LimitWidth(20);
            
            // Assert
            Assert.True(newContent.Value.Length > 1, "Content should be split into multiple lines");
            foreach (var line in newContent.Value)
            {
                Assert.True(line.Length <= 20, $"Line exceeds width limit: {line}");
            }
        }

        [Fact]
        public void HelpCellContent_LimitWidth_ShouldWrapCorrectSpace()
        {
            // Arrange
            var content = new HelpCellContent(" : ");

            // Act
            var newContent = content.LimitWidth(3);

            // Assert
            Assert.True(newContent.Value.Length == 1, "Content should not be split");
            Assert.Equal(" : ", newContent.Value[0]);
        }

        [Fact]
        public void HelpTableLine_RenderTable_ShouldCreateFormattedTable()
        {
            // Arrange
            var lines = new[]
            {
                new HelpTableLine
                {
                    Short = new HelpCellContent("n"),
                    Names = new HelpCellContent("name"),
                    Tags = new HelpCellContent("[Required]"),
                    Description = new HelpCellContent("The storage account name")
                },
                new HelpTableLine
                {
                    Short = new HelpCellContent("g"),
                    Names = new HelpCellContent("resource-group-name"),
                    Tags = new HelpCellContent(),
                    Description = new HelpCellContent("The resource group name")
                }
            };

            // Act
            var result = HelpTableLine.RenderTable(lines, 4, 4, 100);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            
            var tableText = string.Join("\n", result);
            Assert.Contains("name", tableText);
            Assert.Contains("resource-group-name", tableText);
            Assert.Contains("[Required]", tableText);
            Assert.Contains("The storage account name", tableText);
        }

        [CommandInfo(Name = "simple-command", Description = "A simple test command.")]
        public class MockSimpleCommand : BaseProcessor
        {
            [ArgumentInfo(Description = "A simple parameter.")]
            public string SimpleParam { get; set; } = string.Empty;

            public override Task Process()
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void CommandHelpTemplate_Render_ShouldHandleSimpleCommand()
        {
            // Arrange
            var template = new CommandHelpTemplate();
            var commandType = typeof(MockSimpleCommand);

            // Act
            var result = template.Render(commandType, 80);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            
            var helpText = string.Join("\n", result);
            Assert.Contains("simple-command", helpText);
            Assert.Contains("A simple test command", helpText);
            Assert.Contains("simple-param", helpText);
        }
    }
}