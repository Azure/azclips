---
summary: Delete a Microsoft Entra application registration.
status: preview
schema: 1.0.0
online version:
---

Description: Delete an application registration from Microsoft Graph by its object ID via `DELETE /applications/{id}`. Graph performs a soft delete; the application is recoverable for 30 days. Mirrors `az ad app delete`.

### Delete an application

```bash
az msgraph app delete --id 00000000-0000-0000-0000-000000000000
```
