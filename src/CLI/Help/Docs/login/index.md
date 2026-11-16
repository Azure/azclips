---
summary: Authenticate to Azure.
status: ga
schema: 1.0.0
online version:
---

Description: Authenticate to Azure and persist account context for subsequent commands.

### Login with subscription id

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a named profile and make it active

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000 --tenant-id 00000000-0000-0000-0000-000000000000 --out-profile dev
```

### Switch to a saved profile for subsequent commands

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000 --tenant-id 00000000-0000-0000-0000-000000000000 --out-profile dev
az profile set --name prod
az group list
```

### Use a saved profile for one command without changing the active profile

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000 --tenant-id 00000000-0000-0000-0000-000000000000 --out-profile dev
az group list --profile prod
```

The `prod` profile must already exist and can use a different subscription and tenant. Switching makes it active; a one-command override leaves `dev` active.

### Login with device code

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

### Login with a managed identity

```bash
az login --identity --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with a user-assigned managed identity

```bash
az login --identity --client-id 00000000-0000-0000-0000-000000000000 --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with a managed identity using short flags

```bash
az login -i --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with a service principal using a secret

```bash
az login --service-principal --client-id 00000000-0000-0000-0000-000000000000 --client-secret MySecret --tenant 00000000-0000-0000-0000-000000000000 --subscription-id 00000000-0000-0000-0000-000000000000
```
