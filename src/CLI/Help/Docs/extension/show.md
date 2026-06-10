---
summary: Show details of an installed Cirrus extension.
status: ga
schema: 1.0.0
online version:
---

Description: Display metadata for a single installed extension, including its package id, installed version, source feed, pin status, and the CLI verbs it contributes. The extension is identified by its package id (full or short suffix); verb and namespace tokens are not accepted as selectors.

### Show by short name

```bash
az extension show --name Compute
```

### Show by package id

```bash
az extension show --name Microsoft.Azure.Cirrus.Extensions.Compute
```
