---
summary: Pin an installed extension so it won't be updated automatically.
status: ga
schema: 1.0.0
online version:
---

Description: Mark an extension as pinned. Pinned extensions are skipped by `azv2 extension update` unless `--force` is specified. Use this to hold a known-good version while other extensions update.

### Pin by verb

```bash
azv2 extension pin --name compute
```
