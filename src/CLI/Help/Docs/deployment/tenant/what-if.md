---
summary: Preview changes from a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Run ARM what-if for a tenant-scope deployment without changing resources. Use `--result-format` to control payload detail and `--exclude-change-types` to suppress selected change types. Results are pretty-printed by default; use `--no-pretty-print` to return the What-If result in the selected output format.

### Preview tenant deployment changes

```bash
az deployment tenant what-if --location eastus --name tenantPreview --template-file ./tenant.bicep --result-format ResourceIdOnly
```

### Return the What-If result as JSON without unchanged resources

```bash
az deployment tenant what-if --location eastus --template-file ./tenant.json --no-pretty-print --exclude-change-types NoChange
```
