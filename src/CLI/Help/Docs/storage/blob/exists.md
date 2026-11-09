---
summary: Test whether the selected Blob resource exists.
status: preview
schema: 1.0.0
online version:
---

Description: Return an object with an `exists` boolean. Only the selected resource's not-found response produces false; permission, network and service failures remain errors.

### Check an exact blob

```bash
az storage blob exists --account-name myaccount --container-name reports --name report.csv
```
