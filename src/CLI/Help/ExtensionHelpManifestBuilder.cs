// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.Cli.Help
{
    /// <summary>
    /// Build-time projection of an extension's <see cref="IExtensionIndex"/> + its markdown
    /// help corpus into the publishable <see cref="HelpManifest"/> and <see cref="HelpIndex"/>
    /// shapes consumed by <c>HelpManifest.json</c>, <c>HelpIndex.json</c>, and the agent's
    /// help lookup tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unit of help is the <b>verb</b>, not the extension package. An extension that
    /// declares multiple <c>[ExtensionVerb]</c> attributes (e.g. Compute → <c>vm</c>,
    /// <c>disk</c>) produces one manifest per verb. Each manifest covers exactly the
    /// commands that live under its verb's namespace.
    /// </para>
    /// <para>
    /// Unlike <see cref="ExtensionIndexHelpAdapter"/> (CLI-only surface), this builder
    /// emits both surfaces: each <see cref="HelpCommand"/> carries a <c>CliName</c>, a
    /// <c>PsName</c>, and a <see cref="HelpExampleSet"/> split into
    /// <see cref="HelpExampleSet.Cli"/> and <see cref="HelpExampleSet.PowerShell"/>.
    /// The split is driven by the example's fence language tag
    /// (<see cref="MarkdownExample.IsPowerShell"/>).
    /// </para>
    /// </remarks>
    public static class ExtensionHelpManifestBuilder
    {
        /// <summary>
        /// Builds one <see cref="HelpManifest"/> per verb declared by the extension. Verbs
        /// come from <see cref="ExtensionVerbAttribute"/> declarations; commands are partitioned across
        /// manifests by their <c>s_cliTable</c> key prefix.
        /// </summary>
        public static IReadOnlyList<HelpManifest> BuildManifests(
            IExtensionIndex extensionIndex,
            string extensionVersion)
        {
            ArgumentNullException.ThrowIfNull(extensionIndex);

            var verbs = ExtensionVerbDeclarations.GetDeclaredVerbs(extensionIndex);
            if (verbs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Extension '{extensionIndex.GetType().FullName}' declares no [ExtensionVerb] attributes.");
            }

            return verbs
                .Select(verb => BuildManifest(extensionIndex, extensionVersion, verb))
                .ToList();
        }

        /// <summary>
        /// Builds a single <see cref="HelpManifest"/> for one verb. Commands whose
        /// CLI-table key starts with <c>"{verb}"</c> or equals <c>"{verb}"</c> are
        /// included; everything else is scoped out.
        /// </summary>
        public static HelpManifest BuildManifest(
            IExtensionIndex extensionIndex,
            string extensionVersion,
            string verb)
        {
            ArgumentNullException.ThrowIfNull(extensionIndex);
            if (string.IsNullOrWhiteSpace(verb))
            {
                throw new ArgumentException("Verb cannot be null or empty.", nameof(verb));
            }

            var verbLower = verb.ToLowerInvariant();
            var cliTable = extensionIndex.GetCommandTable(ClientType.CLI);
            var psTable = extensionIndex.GetCommandTable(ClientType.PowerShell);

            // Scope the CLI table to this verb only; keys become relative paths
            // (e.g. "vm create" → "create", "vm" → "").
            var verbCliTable = ScopeToVerb(cliTable, verbLower);

            // The PS table is keyed by cmdlet name (no verb prefix), so we join on
            // ProcessorType to discover which PS entries belong to this verb.
            var verbProcessorTypes = new HashSet<Type>(verbCliTable.Values.Select(m => m.ProcessorType));
            var verbPsTable = psTable
                .Where(kvp => verbProcessorTypes.Contains(kvp.Value.ProcessorType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            var processorTypes = verbCliTable.Values
                .Concat(verbPsTable.Values)
                .Select(metadata => metadata.ProcessorType)
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            var commands = new List<HelpCommand>();
            foreach (var processorType in processorTypes)
            {
                var cliEntry = verbCliTable.FirstOrDefault(kvp => kvp.Value.ProcessorType == processorType);
                var psEntry = verbPsTable.FirstOrDefault(kvp => kvp.Value.ProcessorType == processorType);

                var cliMetadata = cliEntry.Value;
                var psMetadata = psEntry.Value;
                if (cliMetadata == null && psMetadata == null)
                {
                    continue;
                }

                var metadataForDescription = cliMetadata ?? psMetadata!;
                var processorInfo = processorType.GetCustomAttribute<ProcessorInfoAttribute>();

                var cliName = BuildCliName(verbLower, cliEntry.Key);
                var psName = NormalizePsName(psEntry.Key);
                var markdown = MarkdownCommandHelpProvider.Get(verbLower, cliEntry.Key ?? string.Empty);
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
                    Tags: tags,
                    PreviewMessage: NullIfEmpty(processorInfo?.PreviewMessage),
                    ExperimentalMessage: NullIfEmpty(processorInfo?.ExperimentalMessage),
                    DeprecatedMessage: NullIfEmpty(processorInfo?.DeprecatedMessage)));
            }

            var sortedCommands = commands
                .OrderBy(command => command.CliName, StringComparer.Ordinal)
                .ThenBy(command => command.PsName, StringComparer.Ordinal)
                .ToList();

            return new HelpManifest(
                Verb: verbLower,
                Version: extensionVersion,
                RootSummary: MarkdownCommandHelpProvider.Get(verbLower, string.Empty).Summary,
                Commands: sortedCommands);
        }

        /// <summary>
        /// Folds a sequence of per-verb <see cref="HelpManifest"/>s into a flat
        /// cross-verb <see cref="HelpIndex"/>: every verb-root and every command appears
        /// in a single sorted list. Drives CLI command discovery and search.
        /// </summary>
        public static HelpIndex BuildIndex(IEnumerable<HelpManifest> manifests)
        {
            var groups = new List<HelpGroupIndexEntry>();
            var commands = new List<HelpCommandIndexEntry>();

            foreach (var manifest in manifests)
            {
                groups.Add(new HelpGroupIndexEntry(
                    Verb: manifest.Verb,
                    Summary: manifest.RootSummary));

                foreach (var command in manifest.Commands)
                {
                    commands.Add(new HelpCommandIndexEntry(
                        CliName: command.CliName,
                        PsName: command.PsName,
                        Verb: manifest.Verb,
                        Summary: command.Summary));
                }
            }

            var sortedGroups = groups
                .OrderBy(group => group.Verb, StringComparer.Ordinal)
                .ToList();

            var sortedCommands = commands
                .OrderBy(command => command.Verb, StringComparer.Ordinal)
                .ThenBy(command => command.CliName, StringComparer.Ordinal)
                .ThenBy(command => command.PsName, StringComparer.Ordinal)
                .ToList();

            return new HelpIndex(sortedGroups, sortedCommands);
        }

        /// <summary>
        /// Filters <paramref name="fullTable"/> to entries belonging to <paramref name="verb"/>
        /// and strips the verb prefix from each key. Entries from other verbs are dropped.
        /// </summary>
        private static Dictionary<string, CommandMetadata> ScopeToVerb(
            IReadOnlyDictionary<string, CommandMetadata> fullTable,
            string verb)
        {
            var result = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);
            var prefix = verb + " ";

            foreach (var kvp in fullTable)
            {
                var key = kvp.Key ?? string.Empty;
                if (key.Equals(verb, StringComparison.OrdinalIgnoreCase))
                {
                    result[string.Empty] = kvp.Value;
                }
                else if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result[key[prefix.Length..]] = kvp.Value;
                }
            }

            return result;
        }

        private static string BuildCliName(string verb, string? relativeKey)
        {
            if (string.IsNullOrWhiteSpace(relativeKey))
            {
                return verb;
            }

            return $"{verb} {relativeKey}";
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

                var property = ProcessorParameterReflection.FindPropertyForParameter(processorType, parameter.Name);
                var attribute = property?.GetCustomAttribute<ArgumentInfoAttribute>();
                var groupName = ProcessorParameterReflection.GetGroupName(property);
                var valueSources = ProcessorParameterReflection.GetValueSources(property);

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
                    SupportsWildcards: attribute?.SupportsWildcards ?? false,
                    GroupName: groupName,
                    ValueSources: valueSources));
            }

            return parameters;
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

        /// <summary>
        /// Collapses an internal dispatch key like <c>"Get-VM_Get"</c> down to the
        /// user-facing cmdlet name <c>"Get-VM"</c>. Some extensions use a
        /// <c>Verb-Noun_ParameterSet</c> convention in <c>s_psTable</c> to dispatch by
        /// parameter set; the manifest/index surface exposes only the cmdlet name.
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

        // Normalise empty ProcessorInfoAttribute message strings to null so nullable HelpCommand
        // fields reflect "no message" and the WhenWritingNull JSON writer suppresses them.
        private static string? NullIfEmpty(string? value)
            => string.IsNullOrEmpty(value) ? null : value;

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
    }
}
