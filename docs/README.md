---
title: End-user documentation
description: End-user documentation for the Azure CLI and Azure PowerShell private preview, including conceptual and reference documentation.
---

# Azure documentation

This folder contains the end-user documentation for the Azure CLI and Azure PowerShell private
preview.

The documentation is organized by product and content type.

## Documentation structure

|                                Folder                                 |                                                      Description                                                      |
| --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| [azure-cli/docs-conceptual](azure-cli/docs-conceptual/)               | Conceptual documentation for Azure CLI, including getting started, installation, tutorials, and how-to guides.        |
| [azure-cli/docs-reference](azure-cli/docs-reference/)                 | Reference documentation for Azure CLI commands.                                                                       |
| [azure-powershell/docs-conceptual](azure-powershell/docs-conceptual/) | Conceptual documentation for Azure PowerShell, including getting started, installation, tutorials, and how-to guides. |
| [azure-powershell/docs-reference](azure-powershell/docs-reference/)   | Reference documentation for Azure PowerShell cmdlets.                                                                 |

> [!NOTE]
> Engineering documentation for contributors and maintainers is located in the **eng-docs** folder
> at the root of the repository.

## Survey reminder

On the first command execution, AzClips creates `~/.azclips/azclips-survey.json`
alongside its configuration file.
The file tracks Azure CLI and Azure PowerShell executions independently, along with
their respective client versions:

```json
{"commandcount":{"cli":1,"powershell":0},"version":{"cli":"0.1.5","powershell":null}}
```

If a client's version differs from the stored version, only that client's count
resets to zero before the current command increments it to one. Older survey files
without a version field are upgraded on the next command for each client, resetting
that client's count at that time. An unexecuted client has a `null` version.
Each command that reaches processor execution increments its client's counter, even if
the processor subsequently fails. Help, discovery, parsing failures, and module imports
do not count. On the sixth command for each client (the command after its counter reaches
five), AzClips displays a client-specific reminder once through the warning stream:

- Azure CLI: `[Survey] Tell us what you think of Azure CLI. This survey should take about 2 minutes. Run 'az survey' to open in browser.`
- Azure PowerShell: `[Survey] Tell us what you think of Azure PowerShell. This survey should take about 2 minutes. Run 'Open-AzSurvey' to open in browser.`

The message does not become part of command results or PowerShell pipeline objects, and normal warning
suppression settings apply. Counters continue increasing after the reminder.

The reminder is enabled by default. Disable it with `az config set --key survey.enabled --value false`
or `Set-AzConfig -Key survey.enabled -Value false`. Set the value to `true` to re-enable it.
Disabling the reminder does not pause counting; if command six occurs while disabled,
the reminder is not shown retroactively on re-enabling. A new client version resets that
client's count and allows a new reminder on its sixth command.

When `AZCLIPS_HOME` is set, the survey file is stored in that directory instead.
If the state cannot be read or written, AzClips reports a warning and still runs the command;
invalid state is not reset automatically.
