---
summary: Show the signed-in Microsoft Entra user.
status: preview
schema: 1.0.0
online version:
---

Description: Get profile details for the currently signed-in user (`/me`).

### Show the signed-in user

```bash
az msgraph user signed-in
```

### Show selected properties and keep default fields

```bash
az msgraph user signed-in --select id displayName mail --append-selected
```
