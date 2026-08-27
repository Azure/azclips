---
summary: Manage Microsoft Entra application registrations through Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Microsoft Entra application registrations through Microsoft Graph. Use `az msgraph app create` to register an application, `az msgraph app show` to retrieve one by object ID, and `az msgraph app delete` to remove one.

### Create an application registration

```bash
az msgraph app create --display-name my-app
```

### Show an application by object ID

```bash
az msgraph app show --id 00000000-0000-0000-0000-000000000000
```

### Delete an application

```bash
az msgraph app delete --id 00000000-0000-0000-0000-000000000000
```
