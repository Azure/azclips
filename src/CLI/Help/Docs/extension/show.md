---
summary: Show discovery-backed details of an installed extension.
status: ga
schema: 1.0.0
online version:
---

Description: Display metadata for a single installed extension package as discovered on disk, including its package id, installed version, pin state, and contributed CLI verbs. The extension is identified by its package id (full or short suffix); verb and namespace tokens are not accepted as selectors.

### Show by short name

```bash
az extension show --name Compute
```

### Show by package id

```bash
az extension show --name Microsoft.Azure.Azclips.Extensions.Compute
```
