---
summary: Show the properties of a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Get the properties and metadata of a file share, such as its quota, access tier, and last-modified time. Use `--snapshot` to read a share snapshot.

### Show a file share

```bash
az storage share show --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare
```
