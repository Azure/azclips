---
summary: Download Azure Files content through bounded ranges.
status: preview
schema: 1.0.0
online version:
---

Description: Requires explicit Files backup intent. Destination can be a file or existing directory; omitted destination uses the source filename. Default `--open-mode wb` replaces content. Append mode `ab` requires one worker. Inclusive range selectors and content validation are supported.

### Download a file

```bash
az storage file download --account-name myaccount --share-name reports --path monthly/report.csv --dest report.csv --enable-file-backup-request-intent
```
