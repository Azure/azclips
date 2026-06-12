---
summary: Update an installed Cirrus extension to a newer version.
status: ga
schema: 1.0.0
online version:
---

Description: Replace the installed version of an extension with a newer one from the configured feed. Pinned extensions are skipped unless `--force` is supplied. The extension is identified by its package id (full or short suffix); verb and namespace tokens are not accepted as selectors.

### Update to the latest version

```bash
azv2 extension update --name Compute
```

### Update to a specific version

```bash
azv2 extension update --name Compute --version 2.1.0
```

### Force update over a pin

```bash
azv2 extension update --name Compute --force
```
