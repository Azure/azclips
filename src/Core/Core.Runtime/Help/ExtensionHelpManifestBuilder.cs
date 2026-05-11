using System.Reflection;
using Core.Abstractions;

namespace Core.Runtime
{
    public static class ExtensionHelpManifestBuilder
    {
        public static HelpManifest BuildManifest(
            IExtensionIndex extensionIndex,
            string extensionName,
            string extensionVersion)
        {
            var cliTable = GetCommandTable(extensionIndex, "s_cliTable");
            var psTable = GetCommandTable(extensionIndex, "s_psTable");

            var processorTypes = cliTable.Values
                .Concat(psTable.Values)
                .Select(metadata => metadata.ProcessorType)
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            var commands = new List<HelpCommand>();
            foreach (var processorType in processorTypes)
            {
                var cliEntry = cliTable.FirstOrDefault(kvp => kvp.Value.ProcessorType == processorType);
                var psEntry = psTable.FirstOrDefault(kvp => kvp.Value.ProcessorType == processorType);

                var cliMetadata = cliEntry.Value;
                var psMetadata = psEntry.Value;
                if (cliMetadata == null && psMetadata == null)
                {
                    continue;
                }

                var metadataForDescription = cliMetadata ?? psMetadata!;
                var processorInfo = processorType.GetCustomAttribute<ProcessorInfoAttribute>();

                var cliName = NormalizeCliName(extensionName, cliEntry.Key);
                var psName = psEntry.Key ?? string.Empty;
                var markdown = MarkdownCommandHelpProvider.Get(extensionName, cliEntry.Key);
                var summary = GetSummary(markdown, processorInfo, metadataForDescription);
                var description = GetDescription(markdown, processorInfo, metadataForDescription);

                var parameters = BuildParameters(cliMetadata, psMetadata, processorType)
                    .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                    .ToList();
                var examples = BuildExamples(markdown, processorInfo);
                var tags = processorInfo?.Tags ?? [];

                commands.Add(new HelpCommand(
                    Name: metadataForDescription.Name,
                    CliName: cliName,
                    PsName: psName,
                    Summary: summary,
                    Description: description,
                    Parameters: parameters,
                    Examples: examples,
                    Tags: tags));
            }

            var sortedCommands = commands
                .OrderBy(command => command.CliName, StringComparer.Ordinal)
                .ThenBy(command => command.PsName, StringComparer.Ordinal)
                .ToList();

            return new HelpManifest(extensionName, extensionVersion, sortedCommands);
        }

        public static HelpIndex BuildIndex(IEnumerable<HelpManifest> manifests)
        {
            var groups = new List<HelpGroupIndexEntry>();
            var commands = new List<HelpCommandIndexEntry>();

            foreach (var manifest in manifests)
            {
                var extensionGroup = manifest.Extension.ToLowerInvariant();
                var extensionSummary = $"Manage {manifest.Extension} commands.";

                groups.Add(new HelpGroupIndexEntry(
                    Name: extensionGroup,
                    Summary: extensionSummary,
                    Extension: manifest.Extension,
                    Prefix: extensionGroup));

                foreach (var command in manifest.Commands)
                {
                    commands.Add(new HelpCommandIndexEntry(
                        Name: command.Name,
                        CliName: command.CliName,
                        PsName: command.PsName,
                        Extension: manifest.Extension,
                        Group: extensionGroup,
                        Summary: command.Summary));
                }
            }

            var sortedGroups = groups
                .OrderBy(group => group.Name, StringComparer.Ordinal)
                .ThenBy(group => group.Extension, StringComparer.Ordinal)
                .ToList();

            var sortedCommands = commands
                .OrderBy(command => command.Group, StringComparer.Ordinal)
                .ThenBy(command => command.CliName, StringComparer.Ordinal)
                .ThenBy(command => command.PsName, StringComparer.Ordinal)
                .ToList();

            return new HelpIndex(sortedGroups, sortedCommands);
        }

        private static List<HelpParameter> BuildParameters(CommandMetadata? cliMetadata, CommandMetadata? psMetadata, Type processorType)
        {
            var parameters = new List<HelpParameter>();
            var psByLogicalName = psMetadata?.Parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                                 ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cliParameter in cliMetadata?.Parameters ?? new Dictionary<string, ParameterMetadata>())
            {
                var cliName = cliParameter.Key;
                var parameter = cliParameter.Value;

                var psName = psByLogicalName.TryGetValue(parameter.Name, out var mappedPsName)
                    ? mappedPsName
                    : parameter.Name;

                var attribute = FindArgumentAttribute(processorType, parameter.Name);
                parameters.Add(new HelpParameter(
                    Name: cliName,
                    PsName: psName,
                    Type: NormalizeTypeName(parameter.ParameterType),
                    Required: parameter.IsRequired,
                    Description: parameter.Description,
                    Summary: attribute?.Summary ?? string.Empty,
                    Aliases: parameter.Aliases,
                    AllowedValues: parameter.AllowedValues,
                    ParameterSetName: attribute?.ParameterSetName ?? "(All)",
                    SupportsPipeline: attribute?.SupportsPipeline ?? false,
                    SupportsWildcards: attribute?.SupportsWildcards ?? false));
            }

            return parameters;
        }

        private static ArgumentInfoAttribute? FindArgumentAttribute(Type processorType, string parameterName)
        {
            var property = processorType.GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null)
            {
                return property.GetCustomAttribute<ArgumentInfoAttribute>();
            }

            return processorType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(propertyInfo => propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>())
                .FirstOrDefault(attribute => string.Equals(attribute?.SerializedName, parameterName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetSummary(MarkdownCommandHelp markdown, ProcessorInfoAttribute? processorInfo, CommandMetadata metadata)
        {
            if (!string.IsNullOrWhiteSpace(markdown.Summary))
            {
                return markdown.Summary;
            }

            if (!string.IsNullOrWhiteSpace(processorInfo?.Summary))
            {
                return processorInfo.Summary.Trim();
            }

            if (!string.IsNullOrWhiteSpace(processorInfo?.Description))
            {
                return processorInfo.Description.Trim();
            }

            return HumanizeCommandName(metadata.Name);
        }

        private static string GetDescription(MarkdownCommandHelp markdown, ProcessorInfoAttribute? processorInfo, CommandMetadata metadata)
        {
            if (!string.IsNullOrWhiteSpace(markdown.Description))
            {
                return markdown.Description;
            }

            if (!string.IsNullOrWhiteSpace(processorInfo?.Description))
            {
                return processorInfo.Description.Trim();
            }

            return HumanizeCommandName(metadata.Name);
        }

        private static HelpExampleSet BuildExamples(MarkdownCommandHelp markdown, ProcessorInfoAttribute? processorInfo)
        {
            if (markdown.Examples.Count > 0)
            {
                return new HelpExampleSet(
                    Cli: markdown.Examples.Where(static e => !e.IsPowerShell).Select(static e => new HelpExample(e.Title, e.Command)).ToList(),
                    PowerShell: markdown.Examples.Where(static e => e.IsPowerShell).Select(static e => new HelpExample(e.Title, e.Command)).ToList());
            }

            if (processorInfo?.Examples == null || processorInfo.Examples.Length == 0)
            {
                return new HelpExampleSet(Cli: [], PowerShell: []);
            }

            var cliExamples = new List<HelpExample>();
            for (var i = 0; i < processorInfo.Examples.Length; i++)
            {
                var raw = processorInfo.Examples[i]?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var delimiter = raw.IndexOf('|');
                if (delimiter <= 0 || delimiter >= raw.Length - 1)
                {
                    cliExamples.Add(new HelpExample($"Example {i + 1}", raw));
                    continue;
                }

                var title = raw[..delimiter].Trim();
                var command = raw[(delimiter + 1)..].Trim();
                cliExamples.Add(new HelpExample(
                    string.IsNullOrWhiteSpace(title) ? $"Example {i + 1}" : title,
                    command));
            }

            return new HelpExampleSet(Cli: cliExamples, PowerShell: []);
        }

        private static string HumanizeCommandName(string name)
        {
            return name.EndsWith("Command", StringComparison.Ordinal)
                ? name[..^"Command".Length]
                : name;
        }

        private static string NormalizeCliName(string extensionName, string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return extensionName.ToLowerInvariant();
            }

            return $"{extensionName.ToLowerInvariant()} {rawKey}";
        }

        private static string NormalizeTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";

            return type.Name;
        }

        private static Dictionary<string, CommandMetadata> GetCommandTable(IExtensionIndex extensionIndex, string fieldName)
        {
            var field = extensionIndex.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field?.GetValue(null) is Dictionary<string, CommandMetadata> table)
            {
                return table;
            }

            throw new InvalidOperationException(
                $"Extension index type '{extensionIndex.GetType().FullName}' does not expose expected static field '{fieldName}'.");
        }
    }
}
