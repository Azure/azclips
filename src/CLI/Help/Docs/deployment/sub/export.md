---
summary: Export the template used for a subscription deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Export the Azure Resource Manager template that a subscription scoped deployment was created from, and print it to stdout as JSON. The command is read-only: it never creates or modifies resources. Azure Resource Manager returns the template exactly as it was submitted, with parameters and variables left unresolved, so the output can be fed straight back into `az deployment sub create --template-file`. Exporting a deployment that does not exist fails with `DeploymentNotFound`. The subscription defaults to the active profile's subscription unless `--subscription-id` is given. `-n` is accepted for `--name`.

### Export a deployment's template

```bash
az deployment sub export --name myDeployment
```

### Save the exported template to a file

```bash
az deployment sub export --name myDeployment > myDeployment.json
```

### Export a deployment's template from a specific subscription

```bash
az deployment sub export --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
