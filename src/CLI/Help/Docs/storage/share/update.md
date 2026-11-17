---
summary: Update the properties of a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Update the quota, access tier, or root squash setting of a file share.

### Set the quota of a file share

```bash
az storage share update --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare --quota 200
```
