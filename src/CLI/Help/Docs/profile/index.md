---
summary: Manage saved Azure authentication profiles.
status: ga
schema: 1.0.0
online version:
---

Description: Manage named authentication profiles captured at login. A profile stores the tenant, subscription, cloud, and authentication context so you can switch between accounts without re-authenticating. Use list to view all profiles, show to inspect one, switch to change the active profile, and remove to delete one.

### List all saved profiles

```bash
azv2 profile list
```

### Show the active profile

```bash
azv2 profile show --active
```

### Switch the active profile

```bash
azv2 profile switch myprofile
```
