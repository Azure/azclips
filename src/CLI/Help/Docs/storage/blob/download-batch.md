---
summary: Download matching Blob names into an existing local directory.
status: preview
schema: 1.0.0
online version:
---

Description: Patterns follow Python fnmatch semantics rather than PowerShell wildcard rules. Targets and collisions are checked before writing. Overwrite defaults to false; `--dryrun` reports selections without writing. Failures can leave completed files; completed/skipped/failed progress remains visible.

### Preview matching downloads

```bash
az storage blob download-batch --account-name myaccount --source reports --destination downloads --pattern "*.csv" --dryrun
```
