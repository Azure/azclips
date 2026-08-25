---
summary: Delete a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a blob, snapshot, or version using Microsoft Entra authentication.

### Delete a blob

```bash
az storage blob delete --account-name mystorage --container-name files --name reports/report.csv
```

### Delete a blob and its snapshots

```bash
az storage blob delete --account-name mystorage --container-name files --name reports/report.csv --delete-snapshots include
```
