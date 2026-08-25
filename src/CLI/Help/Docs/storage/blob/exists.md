---
summary: Check whether a blob exists.
status: preview
schema: 1.0.0
online version:
---

Description: Return `true` when a blob exists or `false` when it does not, using Microsoft Entra authentication.

### Check for a blob

```bash
az storage blob exists --account-name mystorage --container-name files --name reports/report.csv
```
