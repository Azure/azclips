---
summary: Manage Microsoft Entra groups.
status: preview
schema: 1.0.0
online version:
---

Description: Look up, list, and inspect the membership of Microsoft Entra groups.

### Show a group by object ID

```bash
az msgraph group show --group 11111111-2222-3333-4444-555555555555
```

### Show a group by display name

```bash
az msgraph group show --group Sales
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
