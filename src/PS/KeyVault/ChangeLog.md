## Upcoming Release

* Accept classic CLI and PowerShell policy inputs alongside REST policy JSON.
* Align certificate results with classic CLI fields and PowerShell properties. Add explicit Wait and PassThru behavior for certificate cmdlets.

* Add secret set, retrieval, listing, version listing, attribute update and soft-delete commands.
* Add Undo-AzKeyVaultSecretRemoval to recover a soft-deleted secret and its versions.
* Return secret metadata by default, with explicit plaintext retrieval through AsPlainText.
* Add certificate creation, import, retrieval, listing, version listing, attribute and policy updates, public download and soft-delete commands.
* Add Undo-AzKeyVaultCertificateRemoval to recover a soft-deleted certificate and its versions.
* Support encrypted PFX and PEM import, public-only DER and PEM export, and bounded certificate creation polling.
* Purge, backup and restore are not exposed in this preview.