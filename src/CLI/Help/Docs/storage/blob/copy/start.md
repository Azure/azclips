---
summary: Start copying a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Start a server-side copy from an HTTP or HTTPS URL to a blob using Microsoft Entra authentication.

### Copy a blob

```bash
az storage blob copy start --account-name mystorage --destination-container files --destination-blob archive/report.csv --source-uri https://source.blob.core.windows.net/files/report.csv
```
