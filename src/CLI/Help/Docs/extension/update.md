---
summary: Update an installed Cirrus extension to a newer version.
status: ga
schema: 1.0.0
online version:
---

Description: Replace the installed version of an extension with a newer one from the configured feed. Pinned extensions are skipped unless `--force` is supplied.

### Update to the latest version

```bash
azv2 extension update --name compute
```

### Update to a specific version

```bash
azv2 extension update --name compute --version 2.1.0
```

### Force update over a pin

```bash
azv2 extension update --name compute --force
```
