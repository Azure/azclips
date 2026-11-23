---
summary: Delete a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Delete a blob. If the blob has snapshots, pass `--delete-snapshots include` to delete the blob with its snapshots, or `--delete-snapshots only` to delete only the snapshots.

### Delete a blob

```bash
az storage blob delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt
```

### Delete a blob and its snapshots

```bash
az storage blob delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt --delete-snapshots include
```
