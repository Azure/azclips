using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SharedContracts;

namespace AzTool
{
    public class HelpCellContent
    {
        public string[] Value { get; set; } = Array.Empty<string>();

        public HelpCellContent() { }

        public HelpCellContent(string content)
        {
            Value = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public HelpCellContent(string[] content)
        {
            Value = content;
        }

        public HelpCellContent LimitWidth(uint? width)
        {
            if (width == null)
            {
                return this;
            }
            var newLines = new List<string>();
            foreach (var line in Value)
            {
                var words = line.Split(' ');
                var currentLine = string.Empty;
                foreach (var word in words[..^1]) // Exclude the last word to handle it separately
                {
                    if (!string.IsNullOrEmpty(currentLine) && currentLine.Length + word.Length + 1 > width)
                    {
                        newLines.Add(currentLine);
                        currentLine = string.Empty;
                    }
                    currentLine += word + " ";
                }
                // Handle the last word separately
                if (currentLine.Length + words[^1].Length > width)
                {
                    newLines.Add(currentLine.Trim());
                    currentLine = words[^1];
                }
                else
                {
                    currentLine += words[^1];
                }
                if (!string.IsNullOrEmpty(currentLine))
                {
                    newLines.Add(currentLine);
                }
            }
            return new HelpCellContent(newLines.ToArray());
        }

        public uint Width { get { return Value.Length > 0 ? (uint)Value.Select(line => line.Length).Max() : 0; } }
    }

    public class HelpTableLine
    {
        public HelpCellContent Short { get; set; } = new HelpCellContent();
        public HelpCellContent Names { get; set; } = new HelpCellContent();
        public HelpCellContent Tags { get; set; } = new HelpCellContent();
        public HelpCellContent Description { get; set; } = new HelpCellContent();
        public HelpCellContent Detail { get; set; } = new HelpCellContent();
        public string? Group { get; set; } = null;

        public static string[] RenderTable(
            HelpTableLine[] lines,
            uint indent = 0,
            uint offset = 0,
            uint? terminalWidth = null)
        {
            if (lines == null || lines.Length == 0)
            {
                return Array.Empty<string>();
            }
            var renderedLines = new List<string>();

            var shortWidth = lines.Max(line => line.Short.Width);
            var namesWidth = lines.Max(line => line.Names.Width);
            var tagsWidth = lines.Max(line => line.Tags.Width);

            var usedWidth = offset + shortWidth + namesWidth + tagsWidth + 3; // 3 for " : "
            if (shortWidth > 0) usedWidth += 1;
            if (tagsWidth > 0) usedWidth += 1;

            int? descriptionWidth = terminalWidth != null ? (int)terminalWidth - (int)usedWidth : null;

            foreach (var line in lines)
            {
                var cells = new List<DisplayTableCell> { new() { Width = offset } };

                if (shortWidth > 0)
                {
                    cells.Add(new DisplayTableCell { Content = line.Short, Width = shortWidth });
                    cells.Add(new DisplayTableCell { Width = 1 });
                }
                if (namesWidth > 0)
                {
                    cells.Add(new DisplayTableCell { Content = line.Names, Width = namesWidth });
                }
                if (tagsWidth > 0)
                {
                    cells.Add(new DisplayTableCell { Width = 1 });
                    cells.Add(new DisplayTableCell { Content = line.Tags, Width = tagsWidth });
                }
                cells.Add(new DisplayTableCell { Content = new HelpCellContent(" : ") });
                if (descriptionWidth == null || descriptionWidth > 0)
                {
                    cells.Add(new DisplayTableCell { Content = line.Description, Width = (uint?)descriptionWidth });
                    renderedLines.AddRange(DisplayTableCell.Render(cells.ToArray()));
                }
                else
                {
                    renderedLines.AddRange(DisplayTableCell.Render(cells.ToArray()));
                    var indentStr = new string(' ', (int)(offset + indent));
                    renderedLines.AddRange(line.Description.Value.Select(descriptionLine => indentStr + descriptionLine));
                }

                if (line.Detail != null && line.Detail.Value.Length > 0)
                {
                    var indentStr = new string(' ', (int)(offset + indent));
                    renderedLines.AddRange(line.Detail.Value.Select(detailLine => indentStr + detailLine));
                }
            }
            return renderedLines.ToArray();
        }
    }

    public class DisplayTableCell
    {
        public HelpCellContent Content { get; set; } = new HelpCellContent();
        public uint? Width { get; set; } = null;
        public bool VerticalSeparator { get; set; } = false;

        private HelpCellContent? _limitedContent;

        public HelpCellContent LimitedContent
        {
            get
            {
                _limitedContent ??= Content.LimitWidth(Width);
                return _limitedContent;
            }
        }

        public static string[] Render(DisplayTableCell[] cells)
        {
            var renderedLines = new ArrayList();
            var maxHeight = cells.Select(cell => cell.LimitedContent.Value.Length).Max();

            for (int i = 0; i < maxHeight; i++)
            {
                var lineParts = new ArrayList();
                foreach (var cell in cells)
                {
                    string part = string.Empty;
                    if (i < cell.LimitedContent.Value.Length)
                    {
                        part = cell.LimitedContent.Value[i];
                    }
                    else if (cell.VerticalSeparator && cell.LimitedContent.Value.Length > 0)
                    {
                        part = cell.LimitedContent.Value[i % cell.LimitedContent.Value.Length];
                    }
                    lineParts.Add(part.PadRight((int)(cell.Width ?? cell.LimitedContent.Width)));
                }
                renderedLines.Add(string.Join("", lineParts.ToArray()));
            }
            return (string[])renderedLines.ToArray(typeof(string));
        }
    }

    public class CommandHelpTemplate
    {
        public uint Indent { get; set; } = 4;

        public string[] Render(
            Type commandType,
            uint? terminalWidth
        )
        {
            var renderedLines = new List<string>();

            var commandInfo = commandType.GetCustomAttribute<CommandInfoAttribute>() ?? throw new InvalidOperationException($"Command type {commandType.Name} does not have CommandInfoAttribute.");
            renderedLines.Add($"Command");
            var commandName = commandInfo.Name;
            // Convert PascalCase to "lower case"
            // commandName = System.Text.RegularExpressions.Regex.Replace(commandName, "([A-Z])", " $1").Trim().ToLower();
            uint? descriptionWidth = null;
            if (terminalWidth.HasValue)
            {
                var usedWidth = Indent + commandName.Length + 3; // 3 for " : "
                if (usedWidth < terminalWidth)
                {
                    descriptionWidth = (uint)(terminalWidth - usedWidth);
                }
            }
            renderedLines.AddRange(DisplayTableCell.Render(new DisplayTableCell[]
            {
                new() { Width = Indent, },
                new() { Content = new HelpCellContent(commandName)},
                new() { Content = new HelpCellContent(" : ") },
                new() { Content = new HelpCellContent(commandInfo.Description), Width = descriptionWidth }
            }));
            renderedLines.Add("");
            renderedLines.Add("Arguments");

            var lines = new List<HelpTableLine>();
            foreach (var prop in commandType.GetProperties())
            {
                var argAttr = prop.GetCustomAttribute<ArgumentInfoAttribute>();
                if (argAttr == null) { continue; }
                var aliases = argAttr.Aliases.Where(alias => !string.IsNullOrEmpty(alias)).ToArray();
                if (aliases == null || aliases.Length == 0)
                {
                    aliases = new[] { PropertyParser.GetPropertyRawAlias(prop) };
                }
                var tags = new List<string>();
                if (argAttr.Required)
                {
                    tags.Add("[Required]");
                }

                var line = new HelpTableLine
                {
                    Short = new HelpCellContent(aliases.Where(alias => alias.Length == 1).Select(alias => $"-{alias}").ToArray()),
                    Names = new HelpCellContent(aliases.Where(alias => alias.Length > 1).Select(alias => $"--{alias}").ToArray()),
                    Tags = new HelpCellContent(tags.ToArray()),
                    Description = new HelpCellContent(argAttr.Description),
                };
                lines.Add(line);
            }
            renderedLines.AddRange(
                HelpTableLine.RenderTable(
                    lines.ToArray(),
                    Indent,
                    Indent,
                    terminalWidth
                )
            );
            return renderedLines.ToArray();
        }
    }

    public class ModuleHelpTemplate
    {
        public uint Indent { get; set; } = 4;

        public string[] Render(
            Assembly moduleAssembly,
            uint? terminalWidth
        )
        {
            var renderedLines = new List<string>
            {
                "Module",
                $"{new string(' ', (int)Indent)}Name: {moduleAssembly.GetName().Name}",
                $"{new string(' ', (int)Indent)}Version: {moduleAssembly.GetName().Version}",
                "",
                "Commands"
            };

            var lines = new List<HelpTableLine>();
            foreach (var type in moduleAssembly.GetTypes())
            {
                var commandInfo = type.GetCustomAttribute<CommandInfoAttribute>();
                if (commandInfo == null) { continue; }
                lines.Add(new HelpTableLine
                {
                    Names = new HelpCellContent(commandInfo.Name),
                    Description = new HelpCellContent(commandInfo.Description),
                });
            }
            renderedLines.AddRange(
                HelpTableLine.RenderTable(
                    lines.ToArray(),
                    Indent,
                    Indent,
                    terminalWidth
                )
            );
            renderedLines.Add("");
            renderedLines.Add("Use 'az <command> --help' to see command details.");

            return renderedLines.ToArray();
        }
    }

    public class AppHelpTemplate
    {
        public uint Indent { get; set; } = 4;

        public string[] Render(
            IEnumerable<(string name, ModuleInfoAttribute moduleInfo)> moduleInfos,
            uint? terminalWidth
        )
        {
            var indent = new string(' ', (int)Indent);
            var renderedLines = new List<string>
            {
                "Az Tool - Dynamic DLL Processor v1.0",
                "================================",
                "",
                "Usage",
                $"{indent}az [extension] [resource] [subresource...] [action] [options]",
                "",
                "Parameters",
                $"{indent}extension      Name of the extension (determines DLL to load)",
                $"{indent}resource       Primary resource name",
                $"{indent}subresource    Optional sub-resource names",
                $"{indent}action         Action to perform (e.g., show, create, delete)",
                $"{indent}--help         Show this help message",
                "",
                "Options",
                $"{indent}--[property]   Set property value on the target class instance",
                "",
                "Examples",
                $"{indent}az databricks workspace show -resourceName \"myworkspace\" -Name \"test\"",
                $"{indent}az databricks workspace cluster create -Name \"mycluster\" -Size \"Standard_D3_v2\"",
                $"{indent}az storage account blob list -accountName \"mystorage\" -containerName \"data\"",
                "",
                "Description",
                $"{indent}This tool loads a .NET DLL file based on the extension name, searches for classes",
                $"{indent}that implement the IProcessor interface and match the resource pattern, creates",
                $"{indent}instances of those classes, sets specified properties, and calls their Process() method.",
                "",
                "Class Name Resolution",
                $"{indent}The tool searches for classes named: [resource]_[subresource1]_[subresource2]..._[action]",
                $"{indent}Example: 'workspace show' becomes 'WorkspaceShow', 'workspace cluster create' becomes 'WorkspaceClusterCreate'",
                "",
                "Modules"
            };

            var lines = new List<HelpTableLine>();
            foreach (var (name, moduleInfo) in moduleInfos)
            {
                var description = moduleInfo.Description;
                lines.Add(new HelpTableLine
                {
                    Names = new HelpCellContent(name),
                    Description = new HelpCellContent(description != null ? new string[] { description } : Array.Empty<string>()),
                });
            }
            renderedLines.AddRange(
                HelpTableLine.RenderTable(
                    lines.ToArray(),
                    Indent,
                    Indent,
                    terminalWidth
                )
            );

            return renderedLines.ToArray();
        }
    }
}