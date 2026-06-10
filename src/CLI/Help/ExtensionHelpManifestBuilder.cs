using System.Reflection;
using Core.Abstractions;

namespace Core.Cli.Help
{
    /// <summary>
    /// Build-time projection of an extension's <see cref="IExtensionIndex"/> + its markdown
    /// help corpus into the publishable <see cref="HelpManifest"/> and
    /// <see cref="HelpIndex"/> shapes consumed by <c>HelpManifest.json</c>,
    /// <c>HelpIndex.json</c>, and the PowerShell MAML XML emitter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ExtensionIndexHelpAdapter"/> (CLI-only surface), this builder
    /// emits both surfaces in parallel: each <see cref="HelpCommand"/> carries a
    /// <c>CliName</c>, a <c>PsName</c>, and a <see cref="HelpExampleSet"/> split into
    /// <see cref="HelpExampleSet.Cli"/> and <see cref="HelpExampleSet.PowerShell"/> arrays.
    /// The split is driven by the example's fence language tag
    /// (<see cref="MarkdownExample.IsPowerShell"/>).
    /// </para>
    /// <para>
    /// Reflects over the extension index's <c>s_cliTable</c> / <c>s_psTable</c> static
    /// fields to enumerate commands. Processor types are discovered once and joined across
    /// both tables so a single command emits one <see cref="HelpCommand"/> with both
    /// surface names populated.
    /// </para>
    /// </remarks>
    public static class ExtensionHelpManifestBuilder
    {
        /// <summary>
        /// Builds a <see cref="HelpManifest"/> for a single extension. Commands are sorted
        /// by <c>CliName</c> then <c>PsName</c>; parameters within each command are sorted
        /// by name. The <see cref="HelpManifest.RootSummary"/> is sourced from the
        /// extension's <c>index.md</c> frontmatter (<c>summary:</c>) so <see cref="BuildIndex"/>
        /// can surface it as the group-level summary in the cross-extension index.
        /// </summary>
        /// <param name="extensionIndex">The extension's index (CLI + PS tables).</param>
        /// <param name="extensionName">Extension name used for help-folder lookup and CLI name normalization.</param>
        /// <param name="extensionVersion">Extension version string embedded in the manifest.</param>
        /// <param name="cliGroupName">
        /// Optional routed CLI group prefix (e.g. <c>"vm"</c> for the <c>compute</c> extension,
        /// <c>"group"</c> for the <c>resources</c> extension). When supplied, emitted
        /// <c>CliName</c>s use this prefix instead of <paramref name="extensionName"/>, so the
        /// manifest reflects the actual command users type. Defaults to <paramref name="extensionName"/>
        /// when omitted (preserves prior behavior for callers that do not yet know the routed prefix).
        /// </param>
        public static HelpManifest BuildManifest(
            IExtensionIndex extensionIndex,
            string extensionName,
            string extensionVersion,
            string? cliGroupName = null)
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

                var cliName = NormalizeCliName(cliGroupName ?? extensionName, cliEntry.Key);
                var psName = NormalizePsName(psEntry.Key);
                var markdown = MarkdownCommandHelpProvider.Get(extensionName, cliEntry.Key);
                var summary = GetSummary(markdown, processorInfo, metadataForDescription);
                var description = GetDescription(markdown, processorInfo, metadataForDescription);

                var parameters = BuildParameters(cliMetadata, psMetadata, processorType)
                    .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                    .ToList();
                var examples = BuildExamples(markdown, processorInfo);
                var tags = processorInfo?.Tags ?? [];

                commands.Add(new HelpCommand(
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

            return new HelpManifest(extensionName, extensionVersion, MarkdownCommandHelpProvider.Get(extensionName, string.Empty).Summary, sortedCommands);
        }

        /// <summary>
        /// Folds a sequence of per-extension <see cref="HelpManifest"/>s into a flat,
        /// cross-extension <see cref="HelpIndex"/>: every group and every command appears
        /// in a single sorted list keyed by extension + CLI/PS name. Drives CLI command
        /// discovery and search (<c>HelpIndex.json</c>).
        /// </summary>
        public static HelpIndex BuildIndex(IEnumerable<HelpManifest> manifests)
        {
            var groups = new List<HelpGroupIndexEntry>();
            var commands = new List<HelpCommandIndexEntry>();

            foreach (var manifest in manifests)
            {
                var prefix = manifest.Extension.ToLowerInvariant();

                groups.Add(new HelpGroupIndexEntry(
                    Extension: manifest.Extension,
                    Prefix: prefix,
                    Summary: manifest.RootSummary));

                foreach (var command in manifest.Commands)
                {
                    commands.Add(new HelpCommandIndexEntry(
                        CliName: command.CliName,
                        PsName: command.PsName,
                        Extension: manifest.Extension,
                        Summary: command.Summary));
                }
            }

            var sortedGroups = groups
                .OrderBy(group => group.Prefix, StringComparer.Ordinal)
                .ThenBy(group => group.Extension, StringComparer.Ordinal)
                .ToList();

            var sortedCommands = commands
                .OrderBy(command => command.Extension, StringComparer.Ordinal)
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

        /// <summary>
        /// Collapses an internal dispatch key like <c>"Get-VM_Get"</c> or <c>"Get-VM_List"</c>
        /// down to the user-facing cmdlet name <c>"Get-VM"</c>. Some extensions use a
        /// <c>Verb-Noun_ParameterSet</c> convention in <c>s_psTable</c> to dispatch by
        /// parameter set; the manifest/index surface should expose only the cmdlet name
        /// that PowerShell users actually type. Multiple parameter sets of the same cmdlet
        /// produce multiple <see cref="HelpCommand"/> entries with identical <c>PsName</c>
        /// but different <c>CliName</c>s — callers that want a single record per cmdlet
        /// must group on <c>PsName</c>.
        /// </summary>
        private static string NormalizePsName(string? rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return string.Empty;
            }

            var underscore = rawKey.IndexOf('_');
            return underscore > 0 ? rawKey[..underscore] : rawKey;
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
