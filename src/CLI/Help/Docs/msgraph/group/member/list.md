---
summary: List the members of a Microsoft Entra group.
status: preview
schema: 1.0.0
online version:
---

Description: List every user, group, service principal, or device that is a direct member of the specified group. The group can be identified by object ID or display name prefix (single match required).

### List members of a group by object ID

```bash
az msgraph group member list --group 11111111-2222-3333-4444-555555555555
```

### List members of a group by display name

```bash
az msgraph group member list --group Sales
```
