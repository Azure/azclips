---
summary: Validate connectivity and authentication to an Azure Container Registry.
status: ga
schema: 1.0.0
online version:
---

Description: Verify that the current AAD identity can authenticate to the specified container registry. The command exchanges an AAD token for an ACR access token and confirms the login server is reachable.

### Connect to a registry

```bash
az acr connect --name myregistry
```

### Connect using a fully-qualified login server

```bash
az acr connect --name myregistry.azurecr.io
```
