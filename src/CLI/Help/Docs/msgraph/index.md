---
summary: Query Microsoft Entra directory objects through Microsoft Graph.
status: preview
schema: 1.0.0
online version:
---

Description: Query Microsoft Entra directory objects — such as users, groups, and application registrations — through Microsoft Graph. Use subcommands to look up, list, check, or manage resources in the signed-in tenant.

### Show the signed-in user

```bash
az msgraph user signed-in
```

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

### Show a group by object ID

```bash
az msgraph group show --group 11111111-2222-3333-4444-555555555555
```

### List groups whose display name starts with a prefix

```bash
az msgraph group list --display-name Eng
```

### List members of a group

```bash
az msgraph group member list --group 11111111-2222-3333-4444-555555555555
```

### Check whether a user is a member of a group

```bash
az msgraph group member check --group 11111111-2222-3333-4444-555555555555 --member-id aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

### Create an application registration

```bash
az msgraph app create --display-name my-app
```

### List application registrations

```bash
az msgraph app list
```

### Show an application registration by object ID

```bash
az msgraph app show --id 00000000-0000-0000-0000-000000000000
```

### Delete an application registration

```bash
az msgraph app delete --id 00000000-0000-0000-0000-000000000000
```
