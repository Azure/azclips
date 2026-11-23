---
summary: List the members of a Microsoft Entra group.
status: preview
schema: 1.0.0
online version:
---

Description: List every user, group, service principal, or device that is a direct member of the specified group. The group can be identified by object ID, display name prefix (single match required), or exact display name.

### List members of a group by object ID

```bash
az msgraph group member list --group 11111111-2222-3333-4444-555555555555
```

### List members of a group by display name

```bash
az msgraph group member list --group Sales
```

### List members of a group by exact display name

```bash
az msgraph group member list --display-name "Sales Team"
```

### List members with a custom filter and projection

```bash
az msgraph group member list --group Sales --filter "startswith(displayName,'A')" --select id displayName
```

### Search members in a group

```bash
az msgraph group member list --group Sales --search "\"displayName:ada\"" --consistency-level eventual
```
