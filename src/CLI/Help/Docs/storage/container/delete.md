---
summary: Delete a blob container.
status: preview
schema: 1.0.0
online version:
---

Description: Mark a container and all blobs in it for deletion. The container is removed later by garbage collection, and a container with the same name can't be created until then.

### Delete a container

```bash
az storage container delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --name mycontainer
```
