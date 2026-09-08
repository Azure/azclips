---
summary: Create a Microsoft Entra application registration.
status: preview
schema: 1.0.0
online version:
---

Description: Register a new Microsoft Entra application. Only `--display-name` is required; the created application, including its object ID and app ID, is returned.

### Create an application registration

```bash
az msgraph app create --display-name my-app
```

### Create an application for a single-tenant audience

```bash
az msgraph app create --display-name my-app --sign-in-audience AzureADMyOrg
```
