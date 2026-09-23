---
summary: Preview changes from a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Run ARM what-if for a tenant-scope deployment without changing resources. Use `--result-format` to control payload detail and `--exclude-change-types` to suppress selected change types.

### Preview tenant deployment changes

```bash
az deployment tenant what-if --location eastus --name tenantPreview --template-file ./tenant.bicep --result-format ResourceIdOnly
```
