---
summary: List blobs in a container.
status: preview
schema: 1.0.0
online version:
---

Description: List blobs in a container using Microsoft Entra authentication. Results can be filtered by prefix and include optional metadata, tags, versions, snapshots, or deleted blobs.

When `--num-results` stops before the end, the last blob contains `continuationToken`; pass it to `--marker` to continue.

### List blobs

```bash
az storage blob list --account-name mystorage --container-name files
```

### List report blobs with metadata

```bash
az storage blob list --account-name mystorage --container-name files --prefix reports/ --include metadata --num-results 100
```
