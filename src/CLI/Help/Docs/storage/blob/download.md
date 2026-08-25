---
summary: Download a blob to a file.
status: preview
schema: 1.0.0
online version:
---

Description: Download blob content using Microsoft Entra authentication. The destination may be a file or directory; when omitted, the blob name is used in the current directory. Snapshot, version, Azure Blob URL, tag-condition, and MD5-check options are supported.

### Download a blob

```bash
az storage blob download --account-name mystorage --container-name files --name reports/report.bin --file ./report.bin
```

### Replace an existing local file

```bash
az storage blob download --account-name mystorage --container-name files --name report.bin --file ./report.bin --overwrite
```

### Download a snapshot and verify its checksum

```bash
az storage blob download --account-name mystorage --container-name files --name report.bin --snapshot 2026-08-24T00:00:00Z --check-md5
```
