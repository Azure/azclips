---
summary: Upload a file to a block blob.
status: preview
schema: 1.0.0
online version:
---

Description: Stream a local file to Azure Blob Storage using Microsoft Entra authentication. Existing blobs are protected unless `--overwrite` is specified.

### Upload a file

```bash
az storage blob upload --account-name mystorage --container-name files --name reports/report.bin --file ./report.bin
```

### Replace an existing blob

```bash
az storage blob upload --account-name mystorage --container-name files --name report.bin --file ./report.bin --overwrite
```
