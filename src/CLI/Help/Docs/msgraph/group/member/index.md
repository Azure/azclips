---
summary: Manage members of a Microsoft Entra group.
status: preview
schema: 1.0.0
online version:
---

Description: List a group's members and check whether a directory object is a member of a group.

### List members of a group

```bash
az msgraph group member list --group 11111111-2222-3333-4444-555555555555
```

### Check whether a user is a member of a group

```bash
az msgraph group member check --group 11111111-2222-3333-4444-555555555555 --member-id aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```
