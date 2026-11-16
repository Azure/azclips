---
summary: Create a federated identity credential on a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Create a federated identity credential that trusts an external OIDC issuer, so a workload presenting a matching token can exchange it for a Microsoft Entra access token on behalf of the managed identity. Specify either --subject or the claims-matching-expression pair, but not both.

### Create a federated credential using a subject

```bash
az identity federated-credential create --identity-name myIdentity --resource-group myResourceGroup --name myFederatedCredential --issuer https://kubernetes-oauth.azure.com --subject system:serviceaccount:ns:svcaccount --audiences api://AzureADTokenExchange
```

### Create a federated credential for GitHub Actions using a claims matching expression

Trust GitHub Actions tokens for the `main` branch of `contoso/my-repo`.

```bash
az identity federated-credential create --identity-name myIdentity --resource-group myResourceGroup --name myFederatedCredential --issuer https://token.actions.githubusercontent.com --claims-matching-expression-version 1 --claims-matching-expression-value "claims['sub'] eq 'repo:contoso/my-repo:ref:refs/heads/main'" --audiences api://AzureADTokenExchange
```
