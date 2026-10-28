---
title: Troubleshoot Azure CLI issues (Private preview)
description: Learn what to do if you encounter issues while using Azure CLI during the private preview and how to report problems to the development team.
ms.topic: troubleshooting
---

# Troubleshoot Azure CLI issues (Private preview)

This article provides general guidance for troubleshooting issues encountered while using Azure CLI
during the private preview.

Because Azure CLI is under active development, you may encounter unexpected behavior, incomplete
functionality, or other issues.

## Report an issue

If you encounter a problem that you can't resolve, report it in [the AzCLIPS GitHub repository][01].

When creating an issue, include as much information as possible, such as:

- A description of the issue
- The command you ran
- Any error messages or output
- Your operating system and shell
- The Azure CLI version
- Steps to reproduce the issue, if known

Providing detailed information helps the development team investigate and resolve issues more
quickly.

## Understand command errors

The `--output` option formats successful command results on stdout. It doesn't change the format
of errors on stderr. Errors remain human-readable text even when you use `--output json`.

For automation, keep stdout and stderr separate and use the process exit code to determine whether
the command succeeded. A failed command leaves stdout empty unless the command explicitly documents
partial-result behavior.

### Missing option values

When supplied, an option that accepts multiple values must have at least one value after each
occurrence. A bare option, including an empty repeated occurrence, produces a usage error before
the command runs. Omit an optional option if you don't want to provide values.

An explicitly quoted empty string (`""`) is still a value; whether it is valid depends on the
command. Boolean switches don't require a value.

## Inspect command timing

Add `--debug` to any command to include a `[CommandTiming]` summary on stderr.
For example:

```azurecli
az config list --all --debug
az vm create --help --debug
```

The summary shows startup, command-tree construction, extension loading, dispatch
preparation, command invocation, and finalization when those phases occur. Help and
completion report discovery and generation instead of processor invocation. To inspect
completion, keep its selector first and put the diagnostic flag last:

```azurecli
az --completions vm --debug
az --completion-candidates vm --debug
```

Timings use a shared monotonic clock and work even when telemetry is disabled.
Normal command results remain on stdout. A phase's duration includes its nested rows:
for example, dispatch preparation includes selected-extension loading, and command
invocation includes output written by the command. Do not add nested rows or the existing
routing and DLL-load tables to their enclosing durations.

The total covers the observed managed lifecycle, including awaited recovery and host
disposal. It excludes native/.NET startup before managed entry, writing the summary,
later runtime teardown, and completion of the detached telemetry uploader. Compare it
with externally measured process wall time rather than expecting exact equality.
Uninstrumented intervals are reported separately; they do not prove a specific bottleneck.
Failures before the debug logging policy can be resolved might not produce a summary.
An exit code or error still determines success; a closed timing row does not imply success.

## Known issues

Known issues and workarounds will be added to this article as they become available.

### Browser launch issues in WSL (Linux)

If Azure CLI can't open links in your browser when running inside WSL, verify your `wslu` setup:

1. Check your installed `wslu` version:

   ```bash
   apt-cache policy wslu
   ```

2. Add the official `wslu` PPA:

   ```bash
   sudo add-apt-repository ppa:wslutilities/wslu
   ```

3. Update packages and install `wslu`:

   ```bash
   sudo apt update && sudo apt install wslu
   ```

4. Verify `wslview`:

   ```bash
   wslview --version
   wslview https://wslu.wedotstud.io/wslu/install.html#ubuntu
   ```

5. Ensure the `BROWSER` environment variable points to `wslview`:

   ```bash
   export BROWSER=/usr/bin/wslview
   ```

   Add this line to your `~/.bashrc` to persist it.

6. Confirm WSL interop is enabled in `/etc/wsl.conf`:

   ```bash
   cat /etc/wsl.conf
   ```

   The `[interop]` section should have `enabled=true`:

   ```text
   [interop]
   enabled=true
   ```

   If interop is disabled, enable it and restart WSL.

For more details, see:

- <https://wslu.wedotstud.io/wslu/install.html>
- <https://github.com/wslutilities/wslu>
- <https://launchpad.net/~wslutilities/+archive/ubuntu/wslu>
- <https://learn.microsoft.com/windows/wsl/>

## Next steps

If you continue to experience problems, submit an issue in [the AzCLIPS GitHub
repository][01] so the development team can investigate.

<!-- link references -->

[01]: https://github.com/Azure/azclips/issues
