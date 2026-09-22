## ADDED Requirements

### Requirement: Storage account create encryption and identity
The system SHALL allow Storage account create to send encryption and identity settings in the create request, including service encryption settings, customer-managed key settings, key vault properties, encryption identity, infrastructure encryption, queue/table key type settings, system-assigned identity, and user-assigned identity references.

#### Scenario: Create account with customer-managed encryption
- **WHEN** a user invokes Storage account create with customer-managed encryption settings
- **THEN** the request body SHALL include the requested encryption key source, key vault properties, and encryption identity
- **AND** service encryption settings for the requested storage services SHALL be represented in the same create request

#### Scenario: Create account with managed identity
- **WHEN** a user invokes Storage account create with system-assigned or user-assigned identity settings
- **THEN** the request body SHALL include the corresponding top-level identity configuration
- **AND** user-assigned identity resource IDs SHALL be preserved in the request body

#### Scenario: Create account with queue or table key type
- **WHEN** a user invokes Storage account create with queue or table encryption key type settings
- **THEN** the request body SHALL include the requested key type settings for those services

### Requirement: Storage account create Azure Files identity authentication
The system SHALL allow Storage account create to send Azure Files identity-based authentication settings in the create request, including directory service options, Active Directory properties, default share permission, and SMB OAuth settings.

#### Scenario: Create account with directory-backed Azure Files authentication
- **WHEN** a user invokes Storage account create with Azure Files AADDS, AD, or AADKERB authentication settings
- **THEN** the request body SHALL include Azure Files identity-based authentication with the requested directory service option
- **AND** any supplied Active Directory properties SHALL be included in the same authentication configuration

#### Scenario: Create account with SMB OAuth and default share permission
- **WHEN** a user invokes Storage account create with SMB OAuth settings or a default share permission
- **THEN** the request body SHALL include those Azure Files authentication settings

### Requirement: Storage account create network and topology
The system SHALL allow Storage account create to send network rules and endpoint/routing topology settings in the create request, including network rule sets, virtual network and subnet rules, IP rules, resource access rules, routing preference, edge-zone extended location, IPv6 endpoint preference, geo-priority replication, placement policy, and allowed copy scope.

#### Scenario: Create account with network access rules
- **WHEN** a user invokes Storage account create with network rule set, virtual network, subnet, IP, or resource access settings
- **THEN** the request body SHALL include the corresponding network access rules

#### Scenario: Create account with endpoint and routing topology
- **WHEN** a user invokes Storage account create with routing preference, edge-zone, IPv6 endpoint, placement, geo-priority replication, or allowed copy scope settings
- **THEN** the request body SHALL include those topology settings in the create request

### Requirement: Storage account create security and policy
The system SHALL allow Storage account create to send security, policy, and compatibility settings in the create request, including TLS version normalization, blob public access, shared key access, key expiration policy, SAS expiration policy, cross-tenant replication, account-level immutability, and large file share state.

#### Scenario: Create account with normalized TLS settings
- **WHEN** a user invokes Storage account create with a TLS version value that maps to the Storage resource provider TLS 1.2 value
- **THEN** the request body SHALL send the normalized TLS 1.2 value expected by the service

#### Scenario: Create account with access and replication policy settings
- **WHEN** a user invokes Storage account create with blob public access, shared key access, key expiration policy, SAS expiration policy, or cross-tenant replication settings
- **THEN** the request body SHALL include the requested policy settings
- **AND** when a SAS expiration period is provided without an explicit expiration action, the request body SHALL set the SAS expiration action to `Log`

#### Scenario: Create account with immutability or large file share settings
- **WHEN** a user invokes Storage account create with account-level immutability or large file share settings
- **THEN** the request body SHALL include those settings in the create request