---
summary: List saved command failures locally.
status: preview
schema: 1.0.0
online version:
---

Description: List saved failures without AI, Azure authentication, or network access. Output has id, command, and errorDetail fields, sorted by numeric PID ascending, then time and counter descending. Error detail is single-line and limited to 200 characters; use az troubleshoot show to read the complete error. It does not change or clean up history.

### List saved failures

```bash
az troubleshoot list --output table
```
