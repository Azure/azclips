---
name: "integrate-compare"
description: "Compare Azure CLI and Azure PowerShell custom code for an Azclips command and stop with a feature decision table."
agent: "integrate-custom-code"
tools: [read, search, edit, web]
argument-hint: "Service module, resource, action, and optional remote main source hints"
---

Compare upstream Azure CLI and Azure PowerShell custom behavior for one Azclips command. Do not create or edit files.

**Input**: The argument should identify the Swagger/TSP service module, resource, and action. Optional hints may include known Azclips command names, Azure CLI commands, Azure PowerShell cmdlets, repo-relative upstream paths on `main`, or exclusions.

Read upstream Azure CLI, Azure CLI Extensions, and Azure PowerShell evidence from the remote GitHub `main` branch only. Do not inspect local clones, local checkouts, forks, feature branches, or PR branches as upstream evidence. If remote `main` cannot be reached or does not contain the needed evidence, stop with `insufficient-evidence` instead of falling back to local upstream files.

**Steps**

1. Resolve the Azclips CLI command and PowerShell cmdlet mapping for the target API.
2. Search targeted remote `main` upstream paths first:
   - Azure CLI: `Azure/azure-cli@main` command module custom files and live tests.
   - Azure CLI extensions: `Azure/azure-cli-extensions@main` extension custom files and live tests.
   - Azure PowerShell: `Azure/azure-powershell@main` handwritten command files, Autorest custom files, and live tests.
3. Read `.github/skills/azclips-custom-code-classifier/references/blacklist.md` and apply it before recommending any feature.
4. Classify each candidate feature as `shared-customization`, `cli-only-surface`, `powershell-only-surface`, or `drop`.
5. Compare upstream behavior against current Azclips generated processors, existing `Customization`, routing, and wrappers.

## Output Format

Return a concise summary plus a feature comparison table. The table is mandatory for `/integrate-compare` and must use exactly these columns:

| command ID | Feature | Azure CLI source + code block | Azure PowerShell source + code block | Current Azclips evidence block | Action |
|---|---|---|---|---|---|

Column requirements:
- `command ID`: stable feature ID, such as `F1`, `F2`, or `F3`.
- `Feature`: concise behavior name and intent.
- `Azure CLI source + code block`: link to the remote upstream Azure CLI or Azure CLI Extensions `main` source file, preferably with line anchors when available, plus a short quoted or summarized code block showing the behavior. Use remote GitHub links only, not local clone paths.
- `Azure PowerShell source + code block`: link to the remote upstream Azure PowerShell `main` source file, preferably with line anchors when available, plus a short quoted or summarized code block showing the behavior. Use remote GitHub links only, not local clone paths.
- `Current Azclips evidence block`: local Azclips evidence such as `ExtensionIndex.cs`, `Customization`, `Processors`, or PowerShell wrapper links. Local workspace file links are allowed here.
- `Action`: one of exactly:
  - `implement: shared-customization`
  - `needs-user-direction: cli-only-surface`
  - `needs-user-direction: powershell-only-surface`
  - `drop: <blacklist or reason>`

End with `blocked_on_user_direction` asking the user which feature IDs to implement or drop. Do not generate OpenSpec artifacts, product code, docs, or tests.