> TODO BEFORE MERGE: Delete this temporary planning file.

# TEMP PR Plan: Extension Resolution and Redundancy

## Scope

- Define deterministic extension conflict resolution across install locations.
- Remove redundancy in extension metadata and management flow.
- Verify assembly-loading config paths are respected, configurable, and observable.
- Reduce manual maintenance for built-in command mapping where feasible.

## Minimal Checklist

### 1) Lock decisions

- [ ] Decide precedence for same extension identity across builtin and versioned directories.
- [ ] Decide duplicate-command policy: hard-fail or deterministic winner.
- [ ] Decide built-ins strategy: keep explicit command-map maintenance or move to discovery/generation.
- [ ] Decide whether `ExtensionVerb` and `GetVerbs()` stay dual-source or are unified.
- [ ] Lock canonical naming: `verb` vs `leaf` vs `command group`.

### 2) Implement

- [ ] Centralize extension resolution into one code path.
- [ ] Enforce configured assembly-loading directories during discovery/loading.
- [ ] Implement conflict diagnostics showing selected source and reason.
- [ ] Reduce manual built-in command-map edits where feasible.

### 3) Verify and package

- [ ] Add or update tests for all scenarios in matrix.
- [ ] Build: `dotnet build azclips.slnx` and run targeted tests.
- [ ] Manually validate default and overridden directory behavior plus conflict paths.
- [ ] Prepare PR summary with behavior changes, test evidence, and deferred items.

## Scenario Matrix

| Scenario                                                   | Expected behavior | Test added | Notes                                    |
| ---------------------------------------------------------- | ----------------- | ---------- | ---------------------------------------- |
| Same extension in builtin and versioned dirs               | TBD               | [ ]        | Validate precedence + diagnostics        |
| Same command contributed by extensions in both dirs        | TBD               | [ ]        | Verify hard-fail vs deterministic winner |
| Custom `assemblyLoading.builtinInstallationDir` override   | TBD               | [ ]        | Verify effective path used               |
| Custom `assemblyLoading.versionedInstallationDir` override | TBD               | [ ]        | Verify effective path used               |
| `ExtensionVerb` duplicates `GetVerbs()` data               | TBD               | [ ]        | Validate purpose vs redundancy           |
| Built-ins `login` and `config` command map maintenance     | TBD               | [ ]        | Assess manual-edit requirement           |

## Open Questions

- If command contributors conflict, preserve hard error behavior or switch to deterministic selection?
- Should built-ins continue explicit command-map maintenance or shift to discovery/generation?
- Are user-facing docs/help updates required for any policy changes?

## PR Link

- Active PR for this branch: https://github.com/Azure/azclips/pull/370
