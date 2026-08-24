// Orchestrate Scriban rendering. Mirror of emitter.py.
using Scriban;
using Scriban.Runtime;
using AzclipsCodegen.Ir;

namespace AzclipsCodegen;

public static class Emitter
{
    public static List<string> Emit(RpModel model, string outRoot, string templatesDir)
    {
        var written = new List<string>();
        var ops = model.Resources.SelectMany(r => r.Operations).ToList();

        var extDir = System.IO.Path.Combine(outRoot, "src", "Extensions", model.Name);
        var psDir = System.IO.Path.Combine(outRoot, "src", "PS", model.Name);
        var testsDir = System.IO.Path.Combine(outRoot, "tests", $"Extensions.{model.Name}.Tests");
        var cmdletDocsDir = System.IO.Path.Combine(outRoot, "docs", "cmdlets", model.Name);
        var cliDocsDir = System.IO.Path.Combine(outRoot, "docs", "cli", model.Name);

        string Render(string tmplName, Action<ScriptObject>? extra = null)
        {
            var path = System.IO.Path.Combine(templatesDir, tmplName);
            var source = File.ReadAllText(path);
            var tmpl = Template.Parse(source, path);
            if (tmpl.HasErrors)
                throw new InvalidOperationException($"Template parse errors in {tmplName}:\n{string.Join('\n', tmpl.Messages)}");

            var so = new ScriptObject();
            RegisterFilters(so);
            so["model"] = model;
            so["ops"] = ops;
            extra?.Invoke(so);

            var ctx = new TemplateContext();
            // Raise loop iteration limit — Network TSP has ~600 models and the
            // model.scriban does linear scans (`has_msi = false; for m in model.models`).
            // Default 1000 is fine for most RPs but Network/Compute exceed it.
            ctx.LoopLimit = 100000;
            ctx.MemberRenamer = m => m.Name;  // preserve C# names, our IR exposes snake_case too
            ctx.PushGlobal(so);
            return tmpl.Render(ctx);
        }

        void Write(string p, string content)
        {
            var dir = System.IO.Path.GetDirectoryName(p)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(p, content);
            written.Add(p);
        }

        // ── Extension files ──
        Write(System.IO.Path.Combine(extDir, $"Extensions.{model.Name}.csproj"), Render("csproj.scriban"));
        Write(System.IO.Path.Combine(extDir, "Compatibility.cs"), Render("compatibility.scriban"));
        Write(System.IO.Path.Combine(extDir, "Processors", $"{model.Name}ProcessorBase.cs"), Render("processor_base.scriban"));
        Write(System.IO.Path.Combine(extDir, "Models", $"{model.Name}Models.cs"), Render("model.scriban"));

        // Compute the set of Processor filenames we will write, so we can clean up
        // any stale files left over from earlier runs (e.g. when ProcessorClass names
        // were renamed by ps_class_rename or cli_verb_overrides updates). Without this
        // sweep, old files persist and reference now-removed properties → CS0103 at build.
        var plannedProcessors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{model.Name}ProcessorBase.cs",
        };
        var seenProcessorClassesForPlan = new HashSet<string>();
        foreach (var op in ops)
        {
            if (string.IsNullOrEmpty(op.ProcessorClass)) continue;
            if (!seenProcessorClassesForPlan.Add(op.ProcessorClass)) continue;
            plannedProcessors.Add($"{op.ProcessorClass}.cs");
        }
        var processorsDir = System.IO.Path.Combine(extDir, "Processors");
        if (Directory.Exists(processorsDir))
        {
            foreach (var stale in Directory.EnumerateFiles(processorsDir, "*Processor.cs"))
            {
                var fn = System.IO.Path.GetFileName(stale);
                if (!plannedProcessors.Contains(fn))
                {
                    File.Delete(stale);
                }
            }
        }

        // Dedupe by ProcessorClass so synthetic ops (e.g. Wait) that reuse an existing
        // Get processor don't re-emit and clobber it. Keep the FIRST occurrence — the
        // primary CRUD op — since it carries the full parameter set + correct template
        // context (terminal-kind, has_name, is_simple_parent_op).
        var seenProcessorClasses = new HashSet<string>();
        foreach (var op in ops)
        {
            if (string.IsNullOrEmpty(op.ProcessorClass)) continue;
            if (!seenProcessorClasses.Add(op.ProcessorClass)) continue;
            Write(System.IO.Path.Combine(extDir, "Processors", $"{op.ProcessorClass}.cs"),
                  Render("processor.scriban", so => so["op"] = op));
        }
        Write(System.IO.Path.Combine(extDir, "ExtensionIndex.cs"), Render("extension_index.scriban"));

        // ── PS files ──
        Write(System.IO.Path.Combine(psDir, $"Cmdlets.{model.Name}.csproj"), Render("ps_csproj.scriban"));
        Write(System.IO.Path.Combine(psDir, $"{model.Name}.psd1"), Render("psd1.scriban"));
        Write(System.IO.Path.Combine(psDir, $"{model.Name}.psm1"), RenderPowerShellAliases(model));
        Write(System.IO.Path.Combine(psDir, "ChangeLog.md"), Render("changelog.scriban"));

        var grouped = ops.GroupBy(o => o.CmdletClass);
        // Compute planned cmdlet filenames + sweep stale ones (renamed cmdlets from
        // earlier runs). Same rationale as Processors sweep above.
        var plannedCmdlets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in grouped)
        {
            if (!string.IsNullOrEmpty(g.Key)) plannedCmdlets.Add($"{g.Key}.cs");
        }
        var cmdletsDir = System.IO.Path.Combine(psDir, "Cmdlets");
        if (Directory.Exists(cmdletsDir))
        {
            foreach (var stale in Directory.EnumerateFiles(cmdletsDir, "*Cmdlet.cs"))
            {
                var fn = System.IO.Path.GetFileName(stale);
                if (!plannedCmdlets.Contains(fn)) File.Delete(stale);
            }
        }
        foreach (var g in grouped)
        {
            var group = g.ToList();
            Write(System.IO.Path.Combine(psDir, "Cmdlets", $"{g.Key}.cs"),
                  Render("cmdlet.scriban", so => so["group"] = group));
        }

        // ── Test project (A: smoke tests generated per codegen run) ──
        // One csproj + one SmokeTests.g.cs per RP. Regenerated on every run, so any
        // hand-written scenario tests must live in sibling files (e.g. Scenario.cs)
        // to survive. See tests_csproj.scriban / smoke_tests.scriban.
        Write(System.IO.Path.Combine(testsDir, $"Extensions.{model.Name}.Tests.csproj"),
              Render("tests_csproj.scriban"));
        Write(System.IO.Path.Combine(testsDir, "SmokeTests.g.cs"),
              Render("smoke_tests.scriban"));

        // ── PS cmdlet documentation (B: Platyps-format markdown per InvocationName) ──
        // One .md per unique PsPublicName. Docs land in docs/cmdlets/<Rp>/ and can be
        // consumed by Platyps to publish external help XML for the Az.* module.
        var docSeenPs = new HashSet<string>();
        foreach (var op in ops)
        {
            if (string.IsNullOrEmpty(op.PsPublicName)) continue;
            if (!docSeenPs.Add(op.PsPublicName)) continue;
            // Strip Windows-invalid filename chars from PS public name too (rare but
            // possible when TSP APIs contain special chars).
            var psDocName = System.Text.RegularExpressions.Regex.Replace(
                op.PsPublicName, @"[?*:""<>|/\\=$&@{}(),'']+", "_");
            Write(System.IO.Path.Combine(cmdletDocsDir, $"{psDocName}.md"),
                  Render("cmdlet_doc.scriban", so => so["op"] = op));
        }

        // ── CLI help documentation (C: markdown per CLI command) ──
        // One .md per unique CliInvocationName. Docs land in docs/cli/<Rp>/ and mirror
        // the Sphinx/reference-docs shape used by learn.microsoft.com/cli/azure/…
        var docSeenCli = new HashSet<string>();
        foreach (var op in ops)
        {
            if (string.IsNullOrEmpty(op.CliInvocationName)) continue;
            if (!docSeenCli.Add(op.CliInvocationName)) continue;
            // File name = command with spaces → underscores + strip Windows-invalid
            // filename characters (?, *, :, ", <, >, |, /, \, =, $, {, }, &, @, etc.)
            // so paths like `az_vm_list-versions?$expand=properties.md` become valid.
            var docName = "az_" + op.CliInvocationName.Replace(' ', '_');
            docName = System.Text.RegularExpressions.Regex.Replace(docName, @"[?*:""<>|/\\=$&@{}(),'']+", "_");
            Write(System.IO.Path.Combine(cliDocsDir, $"{docName}.md"),
                  Render("cli_help.scriban", so => so["op"] = op));
        }

        return written;
    }

    private static string RenderPowerShellAliases(RpModel model)
    {
        var lines = new List<string>
        {
            "# Generated compatibility aliases. Do not edit.",
        };
        if (model.Config.TryGetValue("ps_compat_aliases", out var raw)
            && raw is Dictionary<string, string> aliases
            && aliases.Count > 0)
        {
            foreach (var (alias, target) in aliases.OrderBy(pair => pair.Key))
            {
                var escapedAlias = alias.Replace("'", "''");
                var escapedTarget = target.Replace("'", "''");
                lines.Add($"Set-Alias -Name '{escapedAlias}' -Value '{escapedTarget}' -Scope Local");
            }
            var exports = string.Join(", ", aliases.Keys.OrderBy(key => key)
                .Select(key => $"'{key.Replace("'", "''")}'"));
            lines.Add($"Export-ModuleMember -Cmdlet * -Alias {exports}");
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void RegisterFilters(ScriptObject so)
    {
        // pascalize — convert kebab-case / snake_case → PascalCase.
        // Old behavior was "capitalize first letter only", which left kebab
        // hyphens in place and produced C# property names like
        // `Preferred-data-archive-auth-method` (won't compile).
        // Now: split on -/_ and capitalize each token.
        so.Import("pascalize", (string? s) =>
        {
            if (string.IsNullOrEmpty(s)) return s;
            var parts = s!.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1) sb.Append(part.Substring(1));
            }
            return sb.ToString();
        });
        so.Import("kebab", (string? s) => s == null ? "" : SwaggerParser.Kebab(s));
        // Convert an arbitrary string into a valid C# identifier suffix by replacing
        // any non-alphanumeric char with `_`. Used to build unique variable names
        // from cli_invocation_name (e.g. `list-versions?$expand=properties`).
        so.Import("sanitize_ident", (string? s) => s == null ? "" : System.Text.RegularExpressions.Regex.Replace(s, @"[^A-Za-z0-9]", "_"));
        so.Import("ljust", (object? s, int width) => (s?.ToString() ?? "").PadRight(width));
        so.Import("rjust", (object? s, int width) => (s?.ToString() ?? "").PadLeft(width));
        so.Import("xmldoc", (string? s) => string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s!, @"\s+", " ").Trim());
        so.Import("escape_quotes", (string? s) =>
        {
            var v = s ?? "";
            // Collapse embedded newlines and quotes so the string sits safely
            // inside a single C# string literal.
            return v.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\\\"");
        });
        so.Import("path_contains", (string? path, string? needle) =>
            !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(needle) && path!.Contains(needle!));
        so.Import("split_last", (string? s, string sep) =>
        {
            if (string.IsNullOrEmpty(s)) return "";
            var parts = s!.Split(sep);
            return parts[parts.Length - 1];
        });
        so.Import("split_at", (string? s, string sep, int i) =>
        {
            if (string.IsNullOrEmpty(s)) return "";
            var parts = s!.Split(sep);
            return i >= 0 && i < parts.Length ? parts[i] : "";
        });
        so.Import("dedupe_names", (System.Collections.IEnumerable ops) =>
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var o in ops)
            {
                var name = (o as Operation)?.PsPublicName ?? "";
                if (!string.IsNullOrEmpty(name) && seen.Add(name)) result.Add(name);
            }
            return result;
        });
        so.Import("cmdlet_classes_unique", (System.Collections.IEnumerable ops) =>
        {
            var seen = new HashSet<string>();
            foreach (var o in ops) { var c = (o as Operation)?.CmdletClass; if (!string.IsNullOrEmpty(c)) seen.Add(c); }
            return seen.ToList();
        });
        so.Import("cli_verbs_unique", (System.Collections.IEnumerable ops) =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in ops)
            {
                if (item is not Operation op || string.IsNullOrWhiteSpace(op.CliInvocationName))
                    continue;
                var firstSpace = op.CliInvocationName.IndexOf(' ');
                var verb = firstSpace < 0
                    ? op.CliInvocationName
                    : op.CliInvocationName.Substring(0, firstSpace);
                if (!string.IsNullOrWhiteSpace(verb)) seen.Add(verb);
            }
            return seen.OrderBy(value => value).ToList();
        });
        so.Import("filter_rg", (System.Collections.IEnumerable ops) =>
        {
            var result = new List<Operation>();
            foreach (var o in ops) if (o is Operation op && op.Path.Contains("resourceGroups")) result.Add(op);
            return result;
        });
        so.Import("filter_has_slots", (System.Collections.IEnumerable ops) =>
        {
            var result = new List<Operation>();
            foreach (var o in ops) if (o is Operation op && op.PathSlots.Count > 0) result.Add(op);
            return result;
        });
        so.Import("has_list_by_rg", (System.Collections.IEnumerable ops) =>
        {
            foreach (var o in ops) if (o is Operation op && op.Kind == "list_by_rg") return true;
            return false;
        });
        so.Import("envelope_params", (System.Collections.IEnumerable ps) =>
        {
            var result = new List<Param>();
            foreach (var o in ps) if (o is Param p && p.BodySlot.StartsWith("envelope.")) result.Add(p);
            return result;
        });
        so.Import("property_params", (System.Collections.IEnumerable ps) =>
        {
            var result = new List<Param>();
            foreach (var o in ps) if (o is Param p && p.BodySlot.StartsWith("properties.")) result.Add(p);
            return result;
        });
        so.Import("nested_params", (System.Collections.IEnumerable ps) =>
        {
            var result = new List<Param>();
            foreach (var o in ps) if (o is Param p && p.BodySlot.StartsWith("nested.")) result.Add(p);
            return result;
        });
        so.Import("nested_parents_of", (System.Collections.IEnumerable ps) =>
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var o in ps)
                if (o is Param p && p.BodySlot.StartsWith("nested."))
                {
                    var parts = p.BodySlot.Split('.');
                    if (parts.Length >= 2 && seen.Add(parts[1])) result.Add(parts[1]);
                }
            return result;
        });
        so.Import("params_of_parent", (System.Collections.IEnumerable ps, string parent) =>
        {
            var result = new List<Param>();
            foreach (var o in ps)
                if (o is Param p && p.BodySlot.StartsWith("nested."))
                {
                    var parts = p.BodySlot.Split('.');
                    if (parts.Length >= 2 && parts[1] == parent) result.Add(p);
                }
            return result;
        });
        so.Import("slot_field", (string bodySlot, int idx) =>
        {
            var parts = bodySlot.Split('.');
            return idx >= 0 && idx < parts.Length ? parts[idx] : "";
        });
        so.Import("required_of", (System.Collections.IEnumerable ps) =>
        {
            var result = new List<Param>();
            foreach (var o in ps) if (o is Param p && p.Required) result.Add(p);
            return result;
        });
        so.Import("optional_of", (System.Collections.IEnumerable ps) =>
        {
            var result = new List<Param>();
            foreach (var o in ps) if (o is Param p && !p.Required) result.Add(p);
            return result;
        });
        so.Import("resolve_props_class", (RpModel model, string requestClass) =>
        {
            var candidates = new List<string> { requestClass + "Properties" };
            if (requestClass.EndsWith("Resource")) candidates.Add(requestClass.Substring(0, requestClass.Length - 8) + "Properties");
            foreach (var c in candidates)
                foreach (var m in model.Models)
                    if (m.Name == c) return c;
            return "";
        });
        so.Import("psetname_of", (string invocationName) =>
        {
            // Only Get-* cmdlets carry a "_Get" / "_List" / "_ListBySubscription"
            // suffix; return the suffix as a bare C# identifier. Other verbs
            // (New-/Remove-/Update-/Invoke-) don't have parameter sets and
            // should return an empty string so callers can decide what to do.
            var i = invocationName.LastIndexOf('_');
            if (i < 0) return "Default";
            var tail = invocationName.Substring(i + 1);
            // Belt-and-suspenders: never emit hyphens or dashes in a C# identifier.
            return System.Text.RegularExpressions.Regex.Replace(tail, "[^A-Za-z0-9_]", "");
        });
        so.Import("verb_expr", (string verb) =>
        {
            return verb switch
            {
                "Get" => "VerbsCommon.Get",
                "New" => "VerbsCommon.New",
                "Remove" => "VerbsCommon.Remove",
                "Update" => "VerbsData.Update",
                "Set" => "VerbsCommon.Set",
                "Invoke" => "VerbsLifecycle.Invoke",
                "Start" => "VerbsLifecycle.Start",
                "Stop" => "VerbsLifecycle.Stop",
                "Test" => "VerbsDiagnostic.Test",
                "Enable" => "VerbsLifecycle.Enable",
                "Disable" => "VerbsLifecycle.Disable",
                _ => "\"" + verb + "\""
            };
        });
        so.Import("slot_desc_or", (PathSlot slot, string fallback) =>
        {
            var s = string.IsNullOrEmpty(slot.Description) ? fallback : slot.Description;
            // Same safety as escape_quotes: keep the value on one line and
            // escape any embedded double quotes so it sits inside "..." safely.
            return s.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\\\"");
        });
        so.Import("first_or", (string? v, string fallback) => string.IsNullOrEmpty(v) ? fallback : v);
        // Short git SHA for provenance headers (e.g. RpModel.SourceCommit) —
        // avoids depending on Scriban's builtin string-slicing across versions.
        so.Import("short_sha", (string? sha) => string.IsNullOrEmpty(sha) ? "" : (sha.Length > 7 ? sha.Substring(0, 7) : sha));
        so.Import("terminal_kind", (string kind) =>
        {
            return kind == "get" || kind == "create_or_update" || kind == "update"
                || kind == "delete" || kind == "resource_action" || kind == "resource_get_action";
        });
        so.Import("cs_default_for", (string csType) =>
        {
            if (csType.EndsWith("?")) return "";
            if (csType.StartsWith("List<")) return "[]";
            if (csType.StartsWith("Dictionary<")) return "new()";
            if (csType == "int" || csType == "long" || csType == "double" || csType == "float" || csType == "bool" || csType == "DateTimeOffset")
                return "default";
            return "null!";
        });
        so.Import("cfg_get", (Dictionary<string, object?> cfg, string key) =>
        {
            return cfg.TryGetValue(key, out var v) ? v : null;
        });
        so.Import("cfg_str", (Dictionary<string, object?> cfg, string key, string fallback) =>
        {
            if (cfg.TryGetValue(key, out var v) && v is string s) return s;
            return fallback;
        });
        so.Import("cfg_map_str", (
            Dictionary<string, object?> cfg,
            string key,
            string itemKey,
            string fallback) =>
        {
            if (cfg.TryGetValue(key, out var raw)
                && raw is Dictionary<string, string> map
                && map.TryGetValue(itemKey, out var value))
            {
                return value;
            }
            return fallback;
        });
        so.Import("cfg_bool", (Dictionary<string, object?> cfg, string key) =>
        {
            return cfg.TryGetValue(key, out var v) && v is bool b && b;
        });
        so.Import("cfg_list", (Dictionary<string, object?> cfg, string key) =>
        {
            if (cfg.TryGetValue(key, out var v))
            {
                if (v is System.Collections.IEnumerable e && v is not string) return e;
            }
            return new List<object>();
        });
    }
}
