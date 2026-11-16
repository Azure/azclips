---
summary: Get the details of a Microsoft Entra group.
status: preview
schema: 1.0.0
online version:
---

Description: Retrieve full details for a group. Accepts either the group's object ID or its display name (prefix works if there is a unique match).

### Show a group by object ID

```bash
az msgraph group show --group 11111111-2222-3333-4444-555555555555
```

### Show a group by display name

```bash
az msgraph group show --group Sales
```

### Show a group with selected fields only

```bash
az msgraph group show --group Sales --select id displayName mailNickname
```

### Show a group with selected fields plus default fields

```bash
az msgraph group show --group Sales --select id displayName mailNickname --append-selected
```
