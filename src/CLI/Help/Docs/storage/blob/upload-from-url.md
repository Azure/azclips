---
summary: Create a block blob from the content of a source URL.
status: preview
schema: 1.0.0
online version:
---

Description: Create a block blob whose content is read synchronously from a source URL. The source must be publicly readable or carry its own SAS token. Use `blob copy start` for an asynchronous, server-side copy instead.

### Create a blob from a public URL

```bash
az storage blob upload-from-url --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name readme.md --source-uri https://raw.githubusercontent.com/Azure/azure-rest-api-specs/refs/heads/main/specification/storage/data-plane/BlobStorage/readme.md
```
