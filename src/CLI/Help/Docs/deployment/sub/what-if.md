---
summary: Preview the changes a subscription scoped deployment would make, without applying them.
status: preview
schema: 1.0.0
online version:
---

Description: Run a subscription scoped what-if operation, mirroring `az deployment sub what-if`. What-if is read-only: it predicts the set of changes (Create, Modify, Delete, Deploy, NoChange, Ignore) an ARM template would make without creating or modifying any resources. The template is supplied with one of `--template-uri` (a remote ARM template URL) or `--template-spec-id` (the resource id of a Template Spec version). `--name` (required) is the deployment name; `--location` (required) is the region where the deployment metadata is stored; `--mode` (required) is the deployment mode, `Incremental` or `Complete`; `--subscription-id` (optional) selects the subscription. Template parameters can be supplied with `--parameters-uri` (a remote parameters file URL). `--query-string` is a SAS token or other query string sent on the template link with `--template-uri`; it is not added to `--parameters-uri`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. `--result-format` selects how much detail the predicted changes carry, `FullResourcePayloads` (default, full before/after resource snapshots) or `ResourceIdOnly` (resource ids only). The command waits for the what-if long-running operation to finish and returns the predicted change list.

### Preview a remote ARM template with a remote parameters file

```bash
az deployment sub what-if --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Preview a Template Spec returning resource ids only

```bash
az deployment sub what-if --location eastus --name myDeployment --mode Incremental --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --result-format ResourceIdOnly
```
