---
summary: Upload files from a directory to a blob container.
status: preview
schema: 1.0.0
online version:
---

Description: Recursively upload files from a local directory using Microsoft Entra authentication.

### Upload matching files

```bash
az storage blob upload-batch --account-name mystorage --destination files --source ./reports --pattern "*.csv"
```
