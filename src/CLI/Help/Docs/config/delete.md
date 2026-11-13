---
summary: Delete a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Remove a configuration setting by key. Once deleted, the setting reverts to its default value (if one exists). Unknown keys are rejected unless --force is specified.

### Delete a configuration setting

```bash
az config delete --key clients.cli.output.format
```

### Force delete without validation

```bash
az config delete --key custom.mykey --force
```
