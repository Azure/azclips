---
summary: Delete the selected Blob resource.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a base blob, snapshot or version using the selected conditions. Base blobs with snapshots require an explicit `--delete-snapshots include` or `only` policy. Successful deletion produces no normal output; failures remain visible.

### Delete a blob without snapshots

```bash
az storage blob delete --account-name myaccount --container-name reports --name obsolete.csv
```
