---
summary: Download a blob to a file.
status: preview
schema: 1.0.0
online version:
---

Description: Stream Azure Blob Storage content to a local file using Microsoft Entra authentication. Existing files are protected unless `--overwrite` is specified.

### Download a blob

```bash
az storage blob download --account-name mystorage --container-name files --name reports/report.bin --file ./report.bin
```

### Replace an existing local file

```bash
az storage blob download --account-name mystorage --container-name files --name report.bin --file ./report.bin --overwrite
```
