---
summary: Generate a user delegation SAS for a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Generate a blob SAS using Microsoft Entra authentication. The signed-in identity must be allowed to request a user delegation key.

### Generate a read-only SAS

```bash
az storage blob generate-sas --account-name mystorage --container-name files --name report.csv --permissions r --expiry 2026-08-29T00:00:00Z --https-only
```

### Return the complete blob URI

```bash
az storage blob generate-sas --account-name mystorage --container-name files --name report.csv --permissions rw --expiry 2026-08-29T00:00:00Z --full-uri
```
