---
summary: Authenticate to Azure.
status: ga
schema: 1.0.0
online version:
---

Description: Authenticate to Azure and persist account context for subsequent commands.

### Login with subscription id

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with device code

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```
