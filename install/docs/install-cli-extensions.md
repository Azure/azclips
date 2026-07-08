# Install azcli Extensions

> **Most users should start with the [install guide](../README.md).** This page covers add-on extensions, which you only need on **lean** installs or to pin a specific extension version.

Add-on extensions ship as NuGet packages named `azcli.extensions.<name>.<version>.nupkg`. You need a working azcli before you can add extensions — follow the [install guide](../README.md) to install the **lean** or **full** package first, then use the steps below.

> The **full** azcli package already bundles the add-on extensions — you only need this guide if you installed the **lean** package or want a specific extension version from a package file.

> **First, make sure `az` points at azcli in this shell.** Because azcli installs in isolation (PATH is not modified by default), add it to `PATH` for the session before running the `az extension` commands below — otherwise `az` may resolve to the classic azure-cli:
>
> ```bash
> export PATH="$HOME/.local/share/azcli:$PATH"     # Linux / macOS — current shell only
> ```
>
> ```powershell
> $env:Path = "$env:LOCALAPPDATA\Programs\azcli;$env:Path"   # Windows — current session only
> ```
>
> Or skip this and call azcli by its full path (e.g. `~/.local/share/azcli/az extension add …`).

## Install from the extensions bundle

Each release includes a single `azcli-extensions-<version>.tar.gz` containing every extension package plus `NOTICE`/`LICENSE`. This bundle is how add-on extensions are distributed.

Get the bundle from the latest release. The quickest way is your authenticated GitHub CLI (`gh`) — the same session the installer uses, which works on the private repo:

```bash
gh release download --repo Azure/azclips --pattern 'azcli-extensions-*.tar.gz' --dir . --clobber
```

Alternatively, download `azcli-extensions-<version>.tar.gz` manually from the repo's [GitHub Releases](https://github.com/Azure/azclips/releases) page (the same release you installed azcli from).

Then extract and install from it. The bundle unpacks as loose `.nupkg` files, so create a folder to extract into. (Extension package versions are independent of the release version, so the globs below avoid hardcoding a version.)

```bash
mkdir -p azcli-extensions
tar -xzf ./azcli-extensions-*.tar.gz -C azcli-extensions

# Install one extension (e.g. compute)
az extension add --path ./azcli-extensions/azcli.extensions.compute.*.nupkg

# Or install every bundled extension
for pkg in ./azcli-extensions/azcli.extensions.*.nupkg; do
  az extension add --path "$pkg"
done

az extension list
```

On Windows, `tar` ships in-box and handles `.tar.gz` directly — use the same `mkdir`/`tar -xzf` commands.

## Verify

```bash
az extension list
```

The extension you added should appear in the list. If the command it provides now runs, the extension is installed.

## Manage extensions

| Task | Command |
| --- | --- |
| List installed extensions | `az extension list` |

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `az` is not found | Install azcli first — see the [install guide](../README.md). |
| A command says an extension is missing | Install the add-on from the extensions bundle with `az extension add --path <pkg>` (see above). |
| `--path` package not found | Confirm the file name and version match your download, e.g. `azcli.extensions.compute.0.1.0.nupkg`. |
| Extension already installed | Use `az extension update --name <name>` or remove then re-add it. |
