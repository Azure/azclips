---
summary: Manage Azure Container Registries.
status: ga
schema: 1.0.0
online version:
---

Description: Connect to, inspect, and manage repositories, manifests, and tags in Azure Container Registries via the ACR data-plane API. Authentication uses AAD token exchange to obtain scoped ACR access tokens automatically.

### Discover acr commands

```bash
az acr --help
```

### Verify connectivity to a registry

```bash
az acr connect --name myregistry
```

### List all repositories

```bash
az acr repository list --registry myregistry
```
