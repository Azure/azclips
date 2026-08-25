---
summary: Upload a file to a block, page, or append blob.
status: preview
schema: 1.0.0
online version:
---

Description: Upload a local file using Microsoft Entra authentication. Supports block, page, and append blobs, metadata, tags, properties, access tiers, and encryption scopes. Existing blobs are protected unless `--overwrite` is specified.

### Upload a file

```bash
az storage blob upload --account-name mystorage --container-name files --name reports/report.bin --file ./report.bin
```

### Replace an existing blob

```bash
az storage blob upload --account-name mystorage --container-name files --name report.bin --file ./report.bin --overwrite
```

### Upload a page blob with metadata

```bash
az storage blob upload --account-name mystorage --container-name disks --name disk.vhd --file ./disk.vhd --blob-type Page --metadata owner=team --premium-tier P10
```
