---
summary: Download one Blob to disk or binary standard output.
status: preview
schema: 1.0.0
online version:
---

Description: Provide `--file` for a disk download with a properties result. Without it, standard output contains only exact content bytes: no JSON, newline or text conversion. Query projection is not supported for binary output. Disk overwrite defaults to true. Snapshot/version and supported inclusive range selectors are preserved.

### Download to a local file

```bash
az storage blob download --account-name myaccount --container-name reports --name report.csv --file report.csv
```
