---
summary: Delete a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a file share and all its contents. If the share has snapshots, pass `--delete-snapshots include` to delete them too, or use `--snapshot` to delete a single snapshot.

### Delete a file share

```bash
az storage share delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare
```
