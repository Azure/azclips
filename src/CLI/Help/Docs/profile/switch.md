---
summary: Set the active profile persistently.
status: ga
schema: 1.0.0
online version:
---

Description: Set the active profile persistently. The named profile must already exist. Switching performs no re-authentication; it only changes which profile subsequent commands use.

### Switch the active profile

```bash
azv2 profile switch myprofile
```
