---
summary: Wait until a tenant deployment reaches a condition.
status: preview
schema: 1.0.0
online version:
---

Description: Gets the tenant deployment every `--interval` seconds (default 30) until the requested condition holds, then returns without output. Supply at least one condition. `--created` and `--updated` wait for the provisioning state `Succeeded`; a missing deployment keeps `--created` waiting but fails `--updated`. `--deleted` waits until the deployment no longer exists. `--exists` waits until the deployment can be read, whatever its provisioning state. `--custom` waits until a JMESPath expression evaluated against the deployment's ARM JSON is true, for example `properties.provisioningState=='Canceled'`. Except with `--exists`, the command fails as soon as the deployment's provisioning state is `Failed`. If the condition does not hold within `--timeout` seconds (default 3600), the command fails.

### Wait for a deployment to succeed

```bash
az deployment tenant wait --name myDeployment --created
```

### Wait for a canceled deployment, polling every 10 seconds

```bash
az deployment tenant wait --name myDeployment --custom "properties.provisioningState=='Canceled'" --interval 10
```

### Wait for a deployment to be deleted

```bash
az deployment tenant wait --name myDeployment --deleted --timeout 600
```
