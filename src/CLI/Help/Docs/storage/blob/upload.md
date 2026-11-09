---
summary: Upload a local file as a Block, Page or Append Blob.
status: preview
schema: 1.0.0
online version:
---

Description: The remote name defaults to the local filename. Block is the default type; a case-sensitive `.vhd` suffix infers Page. Page input must be 512-byte aligned. Overwrite defaults to false; existing Append content is appended unless replacement is explicitly requested. Workers default to two and are limited to four; chunks are bounded to 8 MiB, or 4 MiB for Page.

Compressed sources are uploaded unchanged. For example, `report.json.gz` defaults to `application/gzip` without an inferred content encoding. Explicit content type and encoding override inference; when specifying an encoding, also supply the intended decoded content type.

The upload result's `versionId` identifies the version returned by the final content write when Storage provides one; otherwise it is null. Ordinary base-blob property reads do not select a version implicitly.

### Upload and validate content

```bash
az storage blob upload --account-name myaccount --container-name reports --file report.csv --validate-content
```
