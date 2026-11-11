---
summary: Update a federated identity credential on a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Update the issuer, audiences, subject, or claims-matching-expression of a federated identity credential, or update generic properties with --set/--add/--remove. Setting --subject clears any existing claims-matching-expression, and vice versa, since the two are mutually exclusive.

### Update a federated credential's audiences

```bash
az identity federated-credential update --identity-name myIdentity --resource-group myResourceGroup --name myFederatedCredential --audiences api://AzureADTokenExchange
```

### Switch a federated credential to use a claims matching expression

```bash
az identity federated-credential update --identity-name myIdentity --resource-group myResourceGroup --name myFederatedCredential --claims-matching-expression-version 1 --claims-matching-expression-value "claims['sub'] eq 'repo:contoso/my-repo:ref:refs/heads/main'"
```
