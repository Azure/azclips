---
summary: Wait until a subscription deployment reaches a given state.
status: preview
schema: 1.0.0
online version:
---

Description: Poll a subscription scoped deployment until a condition is met, mirroring `az deployment sub wait`. Specify at least one condition: `--created` (the deployment exists with provisioning state `Succeeded`), `--updated` (the existing deployment has provisioning state `Succeeded`), `--deleted` (the deployment no longer exists), or `--exists` (the deployment exists in any state). When several conditions are given, the wait ends as soon as any one of them is met. The deployment is checked every `--interval` seconds (default 30) for at most `--timeout` seconds (default 3600). The command fails if the deployment reaches the `Failed` provisioning state (unless `--exists` is given), and prints nothing on success. When the timeout elapses, the command fails with the error `Wait operation timed-out after N seconds.` instead of returning successfully. The subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### Wait for a deployment to succeed

```bash
az deployment sub wait --name myDeployment --created
```

### Wait for a deployment to be deleted, polling every 10 seconds for up to 10 minutes

```bash
az deployment sub wait --name myDeployment --deleted --interval 10 --timeout 600
```

### Wait until a deployment succeeds or is deleted

```bash
az deployment sub wait --name myDeployment --created --deleted
```
