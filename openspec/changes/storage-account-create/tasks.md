## 1. Storage Create Surface

- [x] 1.1 Add a Storage account create customization under `src/Extensions/Storage/Customization` and route `storage account create` / `New-AzStorageAccount` through it from `src/Extensions/Storage/ExtensionIndex.cs`.
- [x] 1.2 Add CLI command metadata for the selected shared create fields, preserving existing generated parameters and aliases.
- [x] 1.3 Add PowerShell `New-AzStorageAccount` wrapper parameters for the selected shared create fields, preserving existing wrapper behavior.

## 2. Request Construction

- [x] 2.1 Implement customization-owned request body construction without editing generated Storage models or atomic processors.
- [x] 2.2 Build encryption, customer-managed key, encryption identity, managed identity, service encryption, infrastructure encryption, and queue/table key type request fields.
- [x] 2.3 Build Azure Files identity-based authentication request fields for directory service options, Active Directory properties, SMB OAuth settings, and default share permission.
- [x] 2.4 Build network rule set, virtual network/subnet rule, IP rule, resource access rule, routing preference, edge-zone, IPv6 endpoint preference, geo-priority replication, placement, and allowed-copy-scope request fields.
- [x] 2.5 Build security and policy request fields for TLS normalization, blob public access, shared key access, key policy, SAS policy with default `Log` action, cross-tenant replication, account immutability, and large file share state.

## 3. Validation

- [x] 3.1 Build the Storage extension project with `dotnet build src/Extensions/Storage/Extensions.Storage.csproj --no-restore`.
- [x] 3.2 Verify both CLI and PowerShell Storage account create metadata bind the selected shared fields into the customization processor.