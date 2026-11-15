---
summary: Upload a local file as a block blob.
status: preview
schema: 1.0.0
online version:
---

Description: Upload a local file to a container as a block blob. An existing blob with the same name is not overwritten. Set content headers, metadata, tags, and the access tier on the new blob.

### Upload a file

```bash
az storage blob upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt --file ./myfile.txt
```

### Upload a file with a content type and metadata

```bash
az storage blob upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name report.json --file ./report.json --content-type application/json --metadata team=storage env=test
```
