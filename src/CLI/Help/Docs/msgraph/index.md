---
summary: Query Microsoft Entra directory objects through Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Query Microsoft Entra directory objects—such as users and application registrations—through Microsoft Graph. Use subcommands to look up, list, and manage resources in the signed-in tenant.

### List users in the signed-in tenant

```bash
az msgraph user list
```

### Show a user by user principal name

```bash
az msgraph user show --id user@contoso.com
```

### Show a user by object ID

```bash
az msgraph user show --id 00000000-0000-0000-0000-000000000000
```

### Create an application registration

```bash
az msgraph app create --display-name my-app
```
