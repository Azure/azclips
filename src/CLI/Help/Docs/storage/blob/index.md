---
summary: Manage blobs in Azure Blob Storage.
status: preview
schema: 1.0.0
online version:
---

Description: Upload, download, list, inspect, copy, and delete block blobs. Authenticate with `--account-name` and `--account-key`, or with `--sas-token`. Alternatively, pass the full blob or container URL with `--url`.

### Upload a blob

```bash
az storage blob upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt --file ./myfile.txt
```

### List blobs in a container

```bash
az storage blob list --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer
```
