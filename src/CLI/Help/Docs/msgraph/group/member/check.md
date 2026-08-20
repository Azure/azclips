---
summary: Check whether a directory object is a member of a Microsoft Entra group.
status: preview
schema: 1.0.0
online version:
---

Description: Report whether the specified member (user, group, service principal, or device) is a transitive member of the given group. The group can be identified by object ID or display name prefix (single match required). Returns `true` if the member is in the group, `false` otherwise.

### Check whether a user is a member of a group

```bash
az msgraph group member check --group 11111111-2222-3333-4444-555555555555 --member-id aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

### Check by group display name

```bash
az msgraph group member check --group Sales --member-id aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```
