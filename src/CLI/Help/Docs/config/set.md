---
summary: Set or update a configuration value.
status: ga
schema: 1.0.0
online version:
---

Description: Set a configuration value using dot notation keys. If the key already exists, it will be updated. Unknown keys are rejected unless --force is specified.

### Set output format to JSON

```bash
az config set --key clients.cli.output.format --value json
```

### Set a custom configuration key with force flag

```bash
az config set --key custom.mykey --value myvalue --force
```
