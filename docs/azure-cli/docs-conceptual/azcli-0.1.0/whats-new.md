---
title: What's new in AzCLIPS (Private preview)
description: Discover the latest AzCLIPS features in private preview, including shell-native Tab completion for Azure CLI commands, parameters, and valid values.
---

# What's new in AzCLIPS (Private preview)

This article highlights new features and capabilities available in the AzCLIPS private preview.

## Tab completion

Tab completion is a built-in feature in AzCLIPS that helps you construct Azure CLI commands more
quickly and reduce errors during command entry.

When you partially type a command and press **Tab**, AzCLIPS provides contextual suggestions based
on the current command. Suggestions can include commands, subcommands, parameter names, and valid
parameter values.

### Examples

|             Scenario              |             Example             |                                          Result                                          |
| --------------------------------- | ------------------------------- | ---------------------------------------------------------------------------------------- |
| Complete commands and subcommands | `az vm <Tab>`                   | Displays available operations such as `create`, `list`, and `delete`.                    |
| Complete parameter names          | `az vm create --<Tab>`          | Suggests supported parameters such as `--name`, `--resource-group`, and `--location`.    |
| Complete parameter values         | `az vm create --location <Tab>` | Suggests valid region values based on Azure CLI metadata, such as `eastus` and `westus`. |

### How it works

Tab completion integrates with your existing terminal environment and uses the native completion
features of your shell.

The feature:

- Uses the native completion mechanism provided by PowerShell, Bash, and Zsh
- Is triggered by pressing the <kbd>Tab</kbd> key
- Completes commands using local Azure CLI metadata
- Works offline and doesn't require authentication for supported completion types

AzCLIPS doesn't introduce a new interface or change existing shell behavior. Existing shell
features, such as history-based suggestions, continue to work as expected.

### Private preview behavior

During the private preview:

- Tab completion isn't enabled by default
- Enable the feature by following the post-installation setup instructions
- AzCLIPS doesn't update your shell profile automatically
- The feature is available only in interactive terminal sessions
- Scripts, pipelines, and other non-interactive scenarios aren't affected
