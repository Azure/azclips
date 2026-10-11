// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Port of the legacy what-if pretty printers, run on the legacy flattened result (<c>changes</c>,
/// <c>potentialChanges</c>, <c>diagnostics</c>). <c>cli: true</c> follows az CLI <c>_formatters.py</c>;
/// <c>cli: false</c> follows Azure PowerShell <c>WhatIfOperationResultFormatter</c>.
/// </summary>
public static class DeploymentWhatIfFormatter
{
    private const string Orange = "\u001b[38;5;208m", Green = "\u001b[38;5;77m", Purple = "\u001b[38;5;141m", Blue = "\u001b[38;5;39m",
        Gray = "\u001b[38;5;246m", Red = "\u001b[38;5;203m", DarkYellow = "\u001b[38;5;136m", Reset = "\u001b[0m";

    private static readonly Dictionary<string, (string Symbol, string Color)> s_styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Create"] = ("+", Green),
        ["Delete"] = ("-", Orange),
        ["Modify"] = ("~", Purple),
        ["Array"] = ("~", Purple),
        ["Deploy"] = ("!", Blue),
        ["NoChange"] = ("=", Reset),
        ["Ignore"] = ("*", Gray),
        ["Unsupported"] = ("x", Gray),
        ["NoEffect"] = ("x", Gray),
    };

    private static readonly string[] s_cliOrder = ["Delete", "Create", "Deploy", "Modify", "NoChange", "Unsupported", "Ignore", "NoEffect"];
    private static readonly string[] s_psOrder = ["Delete", "Create", "Deploy", "Modify", "Unsupported", "NoEffect", "NoChange", "Ignore"];
    private static readonly string[] s_propertyOrder = ["Delete", "Create", "Modify", "NoEffect"];

    private static readonly Regex s_managementGroup = new(@"^\/?providers\/Microsoft.Management\/managementGroups\/(?<id>[\w\d_\.\(\)-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_cliSubscription = new(@"^\/?subscriptions\/(?<id>[a-f0-9-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_psSubscription = new(@"^\/?subscriptions\/(?<id>[\w\d-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_resourceGroup = new(@"^\/resourceGroups\/(?<id>[-\w\._\(\)]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_relative = new(@"^\/providers/(?<id>.+$)", RegexOptions.IgnoreCase);

    public static string Format(JsonObject result, bool cli, bool color) => new Writer(cli, color).Format(result);

    /// <summary>
    /// Legacy az CLI colors only on a terminal. ponytail: mirrors Core.Runtime ColorPolicy's env/redirect rules
    /// (not referenced by extensions), without the <c>core.output.color</c> config.
    /// </summary>
    public static bool CliColor() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
        && (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORCE_COLOR"))
            || (!string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase) && !Console.IsOutputRedirected));

    private sealed class Writer(bool cli, bool color)
    {
        private readonly StringBuilder _text = new();
        private readonly Stack<string> _colors = new();
        private readonly string _newLine = cli ? "\n" : Environment.NewLine;
        private readonly StringComparer _sort = cli ? StringComparer.Ordinal : StringComparer.CurrentCulture;

        public string Format(JsonObject result)
        {
            List<JsonObject> changes = Items(result["changes"]), potential = Items(result["potentialChanges"]);
            AppendLine(cli
                ? "Note: The result may contain false positive predictions (noise).\nYou can help us improve the accuracy of the result by opening an issue here: https://aka.ms/WhatIfIssues\nNOTICE! - Want to get What-if without noise? Move to Deployment Stacks What-If https://aka.ms/stackswhatifGA"
                : $"Note: The result may contain false positive predictions (noise).{_newLine}You can help us improve the accuracy of the result by opening an issue here: https://aka.ms/WhatIfIssues.");
            AppendLine();
            FormatLegend(cli ? changes : [.. changes, .. potential]);
            FormatResourceChanges(changes, definite: true);
            FormatStats(changes, definite: true);
            FormatResourceChanges(potential, definite: false);
            FormatStats(potential, definite: false);
            FormatDiagnostics([.. changes, .. potential], Items(result["diagnostics"]));
            return _text.ToString();
        }

        private void FormatLegend(List<JsonObject> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }
            HashSet<string> types = new(StringComparer.OrdinalIgnoreCase);
            void AddPropertyTypes(JsonNode? propertyChanges)
            {
                foreach (JsonObject change in Items(propertyChanges))
                {
                    string type = Type(change, "propertyChangeType");
                    types.Add(type.Equals("Array", StringComparison.OrdinalIgnoreCase) ? "Modify" : type);
                    AddPropertyTypes(change["children"]);
                }
            }
            foreach (JsonObject change in changes)
            {
                types.Add(Type(change));
                AddPropertyTypes(change["delta"]);
            }
            Append("Resource and property changes are indicated with ").AppendLine(types.Count == 1 ? "this symbol:" : "these symbols:");
            foreach (string type in types.OrderBy(Weight))
            {
                // Legacy CLI prints str.title() of the enum value, e.g. "Nochange".
                string name = cli && type.Length > 0 ? char.ToUpperInvariant(type[0]) + type[1..].ToLowerInvariant() : type;
                Append(Indent(1)).Append(Style(type).Symbol, Style(type).Color).Append(" ").Append(name).AppendLine();
            }
        }

        private void FormatResourceChanges(List<JsonObject> changes, bool definite)
        {
            if (changes.Count == 0)
            {
                return;
            }
            var split = changes.Select(c => (Change: c, Id: SplitResourceId(c))).ToList();
            int scopeCount = split.Select(x => x.Id.Scope.ToUpperInvariant()).Distinct().Count();
            AppendLine();
            if (!definite && !cli)
            {
                AppendLine().AppendLine();
            }
            Append(definite ? "The deployment will update the following " : "The following change MAY OR MAY NOT be deployed to the following ")
                .AppendLine(scopeCount == 1 ? "scope:" : "scopes:");
            foreach (var scope in split.OrderBy(x => x.Id.Scope.ToUpperInvariant(), _sort).GroupBy(x => x.Id.Scope.ToUpperInvariant()))
            {
                AppendLine().AppendLine($"Scope: {scope.First().Id.Scope}");
                var sorted = scope.OrderBy(x => Weight(Type(x.Change))).ThenBy(x => x.Id.Relative ?? string.Empty, _sort).ToList();
                foreach (var group in sorted.GroupBy(x => Type(x.Change), StringComparer.OrdinalIgnoreCase))
                {
                    Push(Style(group.Key).Color);
                    foreach (var x in group)
                    {
                        FormatResourceChange(x.Change, x.Id.Relative, ReferenceEquals(x.Change, sorted[^1].Change));
                    }
                    Pop();
                }
            }
        }

        private void FormatResourceChange(JsonObject change, string? relativeResourceId, bool isLast)
        {
            if (relativeResourceId is null)
            {
                return;
            }
            string type = Type(change);
            JsonNode? before = change["before"], after = change["after"];
            AppendLine();
            FormatPath(relativeResourceId, 0, 1, () => Append(Style(type).Symbol).Append(" "), () =>
            {
                if (!string.IsNullOrEmpty(ApiVersion(before, after)))
                {
                    Push(Reset);
                    Append($" [{ApiVersion(before, after)}]");
                    Pop();
                }
            });
            if (Is(type, "Create") && Present(after))
            {
                FormatJson(after, indent: 2);
            }
            else if (Is(type, "Delete") && Present(before))
            {
                FormatJson(before, indent: 2);
            }
            else if (change["delta"] is JsonArray { Count: > 0 } delta)
            {
                Push(Reset);
                AppendLine();
                FormatPropertyChanges(SortProperties(delta));
                Pop();
            }
            else if (isLast)
            {
                AppendLine();
            }
        }

        private void FormatStats(List<JsonObject> changes, bool definite)
        {
            if (!definite && changes.Count == 0)
            {
                return;
            }
            AppendLine().Append(definite ? "Resource changes: " : "Potential changes: ");
            Append(changes.Count == 0 ? "no change" : string.Join(", ", changes
                .GroupBy(Type, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => Weight(g.Key))
                .Select(g => g.Key.ToLowerInvariant() switch
                {
                    "nochange" => $"{g.Count()} no change",
                    "unsupported" => $"{g.Count()} unsupported",
                    string t => $"{g.Count()} to {t}",
                })));
            Append(".");
        }

        private void FormatDiagnostics(List<JsonObject> changes, List<JsonObject> diagnostics)
        {
            var unsupported = changes.Where(c => Is(Type(c), "Unsupported"))
                .Select(c => (Level: (string?)"Warning", Code: (string?)"Unsupported", Message: Str(c["unsupportedReason"]), Target: Str(c["resourceId"])));
            var api = diagnostics.Select(d => (Level: Str(d["level"]), Code: Str(d["code"]), Message: Str(d["message"]), Target: Str(d["target"])));
            var all = (cli ? unsupported.Concat(api) : api.Concat(unsupported)).ToList();
            if (all.Count == 0)
            {
                return;
            }
            AppendLine().AppendLine().Append($"Diagnostics ({all.Count}): ").AppendLine();
            foreach (var d in all)
            {
                Push(d.Level?.ToLowerInvariant() switch { "error" => Red, "warning" => DarkYellow, "info" => Reset, _ => Gray });
                // Legacy CLI: "{target} ({code}) {message}" (Python prints None); PS: "({target}) {message} ({code})".
                Append(cli ? $"{d.Target ?? "None"} ({d.Code ?? "None"}) {d.Message ?? "None"}" : $"({d.Target}) {d.Message} ({d.Code})").AppendLine();
                Pop();
            }
        }

        private void FormatPropertyChanges(List<JsonObject> changes, int indent = 2)
        {
            int maxPathLength = changes.Where(ShouldConsiderPathLength).Select(c => Path(c).Length).DefaultIfEmpty().Max();
            foreach (JsonObject change in changes)
            {
                FormatPropertyChange(change, maxPathLength, indent);
                AppendLine();
            }
        }

        private bool ShouldConsiderPathLength(JsonObject change) => Type(change, "propertyChangeType").ToLowerInvariant() switch
        {
            "create" => IsLeaf(change["after"]),
            "noeffect" when !cli => IsLeaf(change["after"]),
            "delete" or "modify" => IsLeaf(change["before"]),
            _ => change["children"] is not JsonArray { Count: > 0 },
        };

        private void FormatPropertyChange(JsonObject change, int maxPathLength, int indent)
        {
            string type = Type(change, "propertyChangeType");
            JsonNode? before = change["before"], after = change["after"];
            JsonArray? children = change["children"] as JsonArray;
            switch (type.ToLowerInvariant())
            {
                case "create":
                    FormatPropertyChangePath(change, after, maxPathLength, indent);
                    FormatJsonColored(after, indent + 1, Green);
                    break;
                case "delete":
                    FormatPropertyChangePath(change, before, maxPathLength, indent);
                    FormatJsonColored(before, indent + 1, Orange);
                    break;
                case "modify":
                    FormatPropertyChangePath(change, before, maxPathLength, indent);
                    if (cli ? children is { Count: > 0 } : children is not null)
                    {
                        AppendLine().AppendLine();
                        FormatPropertyChanges(SortProperties(children), indent + 1);
                    }
                    else
                    {
                        FormatJsonColored(before, indent + 1, Orange);
                        if (IsNonEmptyObject(before))
                        {
                            AppendLine().Append(Indent(indent + 1));
                        }
                        else
                        {
                            Append(" ");
                        }
                        Append("=>");
                        if (!IsNonEmptyObject(after))
                        {
                            Append(" ");
                        }
                        FormatJsonColored(after, indent + 1, Green);
                        if (!IsLeaf(before) && IsLeaf(after))
                        {
                            AppendLine();
                        }
                    }
                    break;
                case "array":
                    FormatPropertyChangePath(change, null, maxPathLength, indent);
                    int arrayIndent = indent + 1;
                    if (string.IsNullOrEmpty(Path(change)))
                    {
                        arrayIndent--;
                        Append(Indent(arrayIndent));
                    }
                    if (children is not { Count: > 0 })
                    {
                        AppendLine("[]");
                        break;
                    }
                    Append("[").AppendLine();
                    FormatPropertyChanges(Items(children)
                        .OrderBy(c => int.Parse(Path(c)))
                        .ThenBy(c => PropertyWeight(Type(c, "propertyChangeType")))
                        .ToList(), arrayIndent);
                    Append(Indent(arrayIndent)).Append("]");
                    break;
                case "noeffect":
                    FormatPropertyChangePath(change, after, maxPathLength, indent);
                    FormatJsonColored(after, indent + 1, Gray);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change), $"Unknown property change type: {type}.");
            }
        }

        private void FormatPropertyChangePath(JsonObject change, JsonNode? value, int maxPathLength, int indent)
        {
            string path = Path(change), type = Type(change, "propertyChangeType");
            if (path.Length == 0)
            {
                return;
            }
            bool hasChildren = change["children"] is JsonArray { Count: > 0 };
            int padding = maxPathLength - path.Length + 1;
            if (value is JsonArray { Count: > 0 } || (Is(type, "Array") && hasChildren))
            {
                padding = 1;
            }
            if (IsNonEmptyObject(value) || (Is(type, "Modify") && hasChildren))
            {
                padding = 0;
            }
            FormatPath(path, padding, indent, () => Append(Style(type).Symbol, Style(type).Color).Append(" "), FormatColon);
        }

        private void FormatJsonColored(JsonNode? value, int indent, string jsonColor)
        {
            Push(jsonColor);
            FormatJson(value, indent: indent);
            Pop();
        }

        // Legacy WhatIfJsonFormatter / _format_json.
        private void FormatJson(JsonNode? value, string path = "", int maxPathLength = 0, int indent = 0)
        {
            if (IsLeaf(value))
            {
                FormatPath(path, maxPathLength - path.Length + 1, indent, tail: FormatColon);
                Append(value?.GetValueKind() switch
                {
                    null or JsonValueKind.Null => "null",
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.String => $"\"{value.GetValue<string>()}\"",
                    _ => value.ToJsonString(),
                });
            }
            else if (value is JsonArray array)
            {
                FormatPath(path, 1, indent, tail: FormatColon);
                Append("[", Reset).AppendLine();
                int lastLeaf = array.Select((v, i) => IsLeaf(v) ? i : 0).Max();
                for (int i = 0; i < array.Count; i++)
                {
                    if (IsNonEmptyObject(array[i]))
                    {
                        FormatPath(i.ToString(), 0, indent + 1, tail: FormatColon);
                        FormatJson(array[i], indent: indent + 1);
                    }
                    else
                    {
                        FormatJson(array[i], i.ToString(), lastLeaf.ToString().Length, indent + 1);
                    }
                    AppendLine();
                }
                Append(Indent(indent)).Append("]", Reset);
            }
            else if (value is JsonObject obj)
            {
                bool isRoot = path.Length == 0;
                if (isRoot)
                {
                    AppendLine().AppendLine();
                    maxPathLength = MaxPathLength(obj);
                    indent++;
                }
                foreach ((string key, JsonNode? child) in obj)
                {
                    FormatJson(child, isRoot ? key : $"{path}.{key}", maxPathLength, indent);
                    if (!IsNonEmptyObject(child))
                    {
                        AppendLine();
                    }
                }
            }
        }

        private static int MaxPathLength(JsonObject obj) => obj
            .Where(p => p.Value is not JsonArray { Count: > 0 })
            .Select(p => p.Value is JsonObject { Count: > 0 } child ? p.Key.Length + 1 + MaxPathLength(child) : p.Key.Length)
            .DefaultIfEmpty()
            .Max();

        private void FormatPath(string path, int padding, int indent, Action? head = null, Action? tail = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            Append(Indent(indent));
            head?.Invoke();
            Append(path);
            tail?.Invoke();
            Append(new string(' ', Math.Max(0, padding)));
        }

        private void FormatColon() => Append(":", Reset);

        private (string Scope, string? Relative) SplitResourceId(JsonObject change)
        {
            string? id = Str(change["resourceId"]);
            if (!cli)
            {
                // Legacy PS runs Regex.Match on the null id, so extensible resources fail the what-if (user decision).
                ArgumentNullException.ThrowIfNull(id, "input");
            }
            else if (string.IsNullOrEmpty(id))
            {
                // Legacy CLI: extensible resources are identified by extension and identifiers.
                string scope = change["extension"] is JsonObject extension ? $"{Str(extension["name"])}@{Str(extension["version"])}" : "Unknown";
                string? relative = change["identifiers"] is JsonObject { Count: > 0 } identifiers
                    ? string.Join(", ", identifiers.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value?.ToJsonString() ?? "null"}"))
                    : null;
                return (scope, relative);
            }
            Match managementGroup = s_managementGroup.Match(id);
            string remaining = id[managementGroup.Length..];
            Match subscription = (cli ? s_cliSubscription : s_psSubscription).Match(remaining);
            remaining = remaining[subscription.Length..];
            Match resourceGroup = s_resourceGroup.Match(remaining);
            remaining = remaining[resourceGroup.Length..];
            Match relativeMatch = s_relative.Match(remaining);
            string relativeId = relativeMatch.Groups["id"].Value;
            if (managementGroup.Success)
            {
                string managementGroupId = $"Microsoft.Management/ManagementGroups/{managementGroup.Groups["id"].Value}";
                return relativeMatch.Success ? ($"/providers/{managementGroupId}", relativeId) : ("/", managementGroupId);
            }
            if (subscription.Success)
            {
                string subscriptionScope = $"/subscriptions/{subscription.Groups["id"].Value.ToLowerInvariant()}";
                if (resourceGroup.Success)
                {
                    string resourceGroupId = $"resourceGroups/{resourceGroup.Groups["id"].Value}";
                    return relativeMatch.Success ? ($"{subscriptionScope}/{resourceGroupId}", relativeId) : (subscriptionScope, resourceGroupId);
                }
                return (subscriptionScope, relativeId);
            }
            return ("/", relativeId);
        }

        // CLI reads only the first non-empty snapshot; PS falls back from before to after.
        private string? ApiVersion(JsonNode? before, JsonNode? after) => cli
            ? Str((before is JsonObject { Count: > 0 } ? before : after)?["apiVersion"])
            : Str(before?["apiVersion"]) ?? Str(after?["apiVersion"]);

        // CLI tests Python truthiness (an empty object is false); PS tests for null.
        private bool Present(JsonNode? value) => value is not null && !(cli && value is JsonObject { Count: 0 });

        private int Weight(string type) => Array.FindIndex(cli ? s_cliOrder : s_psOrder, t => Is(t, type));

        private List<JsonObject> SortProperties(JsonArray changes) => Items(changes)
            .OrderBy(c => PropertyWeight(Type(c, "propertyChangeType")))
            .ThenBy(Path, _sort)
            .ToList();

        private static int PropertyWeight(string type) =>
            Array.FindIndex(s_propertyOrder, t => Is(t, Is(type, "Array") ? "Modify" : type));

        private static (string Symbol, string Color) Style(string type) => s_styles.GetValueOrDefault(type, (" ", Reset));

        private static bool Is(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

        private static string Type(JsonObject change) => Type(change, "changeType");

        private static string Type(JsonObject change, string property) => Str(change[property]) ?? string.Empty;

        private static string Path(JsonObject change) => Str(change["path"]) ?? string.Empty;

        private static string? Str(JsonNode? node) => node is null ? null : node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : node.ToJsonString();

        private static List<JsonObject> Items(JsonNode? node) => node is JsonArray array ? [.. array.OfType<JsonObject>()] : [];

        private static bool IsLeaf(JsonNode? value) => value is not (JsonArray { Count: > 0 } or JsonObject { Count: > 0 });

        private static bool IsNonEmptyObject(JsonNode? value) => value is JsonObject { Count: > 0 };

        private static string Indent(int level) => new(' ', 2 * Math.Max(0, level));

        private Writer Append(string? value, string? valueColor = null)
        {
            if (valueColor is not null)
            {
                Push(valueColor);
            }
            _text.Append(value);
            if (valueColor is not null)
            {
                Pop();
            }
            return this;
        }

        private Writer AppendLine(string? value = null) => Append(value).Append(_newLine);

        private void Push(string value)
        {
            if (color)
            {
                _colors.Push(value);
                _text.Append(value);
            }
        }

        private void Pop()
        {
            if (color)
            {
                _colors.Pop();
                _text.Append(_colors.TryPeek(out string? previous) ? previous : Reset);
            }
        }
    }
}
