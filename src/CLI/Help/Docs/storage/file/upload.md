---
summary: Upload a local file through bounded Azure Files ranges.
status: preview
schema: 1.0.0
online version:
---

Description: Use a remote path or default to the local filename. Requires explicit Files backup intent. Content settings, metadata and optional range MD5 validation are supported. Workers default to two and are limited to four; each range is at most 4 MiB. A partially initialized file is not reported as a completed upload.

Compressed sources are uploaded unchanged. For example, `report.json.gz` defaults to `application/gzip` without an inferred content encoding. Explicit content type and encoding override inference; when specifying an encoding, also supply the intended decoded content type.

### Upload and create missing parent directories

```bash
az storage file upload --account-name myaccount --share-name reports --source report.csv --path monthly/report.csv --enable-file-backup-request-intent
```
