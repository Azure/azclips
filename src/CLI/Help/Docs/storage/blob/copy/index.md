---
summary: Manage asynchronous blob copy operations.
status: preview
schema: 1.0.0
online version:
---

Description: Start a server-side copy into a blob from a source URL. The copy runs asynchronously; check its progress with `az storage blob show`.

### Start a blob copy

```bash
az storage blob copy start --account-name mystorageaccount --account-key $ACCOUNT_KEY --destination-container mycontainer --destination-blob copied.md --source-uri https://raw.githubusercontent.com/Azure/azure-rest-api-specs/refs/heads/main/specification/storage/data-plane/BlobStorage/readme.md
```
