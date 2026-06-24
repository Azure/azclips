---
summary: Pin an installed extension so it won't be updated automatically.
status: ga
schema: 1.0.0
online version:
---

Description: Mark an extension as pinned. Pinned extensions are skipped by `az extension update` unless `--force` is specified. Use this to hold a known-good version while other extensions update. The extension is identified by its package id (full or short suffix); verb and namespace tokens are not accepted as selectors.

### Pin by short name

```bash
az extension pin --name Compute
```
