## Why

Azclips Storage account create currently sends a narrow generated request body, so shared create behavior cannot express several common Storage account configuration areas. This change defines the shared create contract for encryption, Azure Files authentication, network topology, and security policy request shaping.

## What Changes

- Add shared Storage account create request construction for encryption, customer-managed key, encryption identity, managed identity, service encryption, and queue/table key type fields.
- Add shared Azure Files identity-based authentication request construction, including directory service options, Active Directory properties, SMB OAuth settings, and default share permission.
- Add shared network, endpoint, routing, edge-zone, placement, geo-priority replication, and allowed-copy-scope request construction.
- Add shared security and policy request construction for TLS normalization, blob public access, shared key access, SAS/key policies, cross-tenant replication, account immutability, and large file share state.
- Preserve already-covered generated create fields such as SKU, kind, location, tags, zones, access tier, HTTPS-only, HNS, SFTP, local user, NFS v3, DNS endpoint type, public network access, blob public access, and minimum TLS as direct request fields.
- Drop surface-only and blacklisted behavior: CLI/PowerShell name-availability preflight differences, CLI defaults/aliases/local-context behavior, PowerShell wrapper-only behavior, and pure parameter validation or invalid-combination prechecks.

## Capabilities

### New Capabilities

- `storage-account-create`: Shared Storage account create request construction for advanced encryption, file authentication, network topology, and policy fields.

### Modified Capabilities

_(none - no existing Storage account create spec is being modified)_

## Impact

- **Storage extension**: Add create customization under `src/Extensions/Storage/Customization` and route create commands through it from `src/Extensions/Storage/ExtensionIndex.cs`.
- **Storage command surfaces**: Add shared create parameters to CLI metadata and the PowerShell `New-AzStorageAccount` wrapper only where needed to bind the selected shared fields.
- **Generated code**: Do not edit generated Storage models or generated atomic processors; any additional request shape lives in customization-owned code.
- **Tests/scripts**: No xUnit tests or live-test scripts are created by this change artifact; live-test scripts are handled later by `/integrate-live-test`.