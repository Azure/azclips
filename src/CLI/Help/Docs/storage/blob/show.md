---
summary: Read one Blob's properties and tags.
status: preview
schema: 1.0.0
online version:
---

Description: Retrieve metadata without downloading content. Select a snapshot or version, not both. Missing and denied resources remain errors; use `exists` for an explicit existence result.

### Inspect an exact blob

```bash
az storage blob show --account-name myaccount --container-name reports --name report.csv
```
