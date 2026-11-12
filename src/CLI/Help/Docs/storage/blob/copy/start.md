---
summary: Start an asynchronous copy of a source URL into a blob.
status: preview
schema: 1.0.0
online version:
---

Description: Start a server-side copy from a source URL into a destination blob. The source can be another blob or any publicly readable URL. The command returns once the copy is scheduled; check its status with `az storage blob show`.

### Copy a public URL into a blob

```bash
az storage blob copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --destination-container mycontainer --destination-blob copied.md --source-uri https://raw.githubusercontent.com/Azure/azure-rest-api-specs/refs/heads/main/specification/storage/data-plane/BlobStorage/readme.md
```

### Copy a blob within the account

```bash
az storage blob copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --destination-container backup --destination-blob myblob.txt --source-uri https://mystorageaccount.blob.core.windows.net/mycontainer/myblob.txt
```
