import "@typespec/rest";
import "@typespec/http";
import "@azure-tools/typespec-azure-resource-manager";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Rest;
using TypeSpec.Http;
using Azure.ResourceManager;
using Azure.ResourceManager.Foundations;

namespace Microsoft.Resources;

model ResourceListResult is Azure.Core.Page<GenericResourceExpanded>;

union AliasPathTokenType {
  string,
  NotSpecified: "NotSpecified",
  Any: "Any",
  String: "String",
  Object: "Object",
  Array: "Array",
  Integer: "Integer",
  Number: "Number",
  Boolean: "Boolean",
}

union AliasPathAttributes {
  string,
  None: "None",
  Modifiable: "Modifiable",
}

union ProviderAuthorizationConsentState {
  string,
  NotSpecified: "NotSpecified",
  Required: "Required",
  NotRequired: "NotRequired",
  Consented: "Consented",
}

union ExtendedLocationType {
  string,
  EdgeZone: "EdgeZone",
}

union ExportTemplateOutputFormat {
  string,
  Json: "Json",
  Bicep: "Bicep",
}

union TagsPatchOperation {
  string,
  Replace: "Replace",
  Merge: "Merge",
  Delete: "Delete",
}

#suppress "@azure-tools/typespec-azure-core/no-enum" "For backward compatibility"
enum AliasPatternType {
  NotSpecified,
  Extract,
}

#suppress "@azure-tools/typespec-azure-core/no-enum" "For backward compatibility"
enum AliasType {
  NotSpecified,
  PlainText,
  Mask,
}

#suppress "@azure-tools/typespec-azure-core/no-enum" "For backward compatibility"
enum ResourceIdentityType {
  SystemAssigned,
  UserAssigned,
  `SystemAssigned, UserAssigned`,
  None,
}

model Operation {
  name?: string;
  display?: OperationDisplay;
}

model OperationDisplay {
  provider?: string;
  resource?: string;
  operation?: string;
  description?: string;
}

#suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "For backward compatibility"
@error
@Legacy.armExternalType
model CloudError {
  error?: ErrorResponse;
}

#suppress "@azure-tools/typespec-client-generator-core/csharp-model-suffix" "Name kept for backward compatibility"
model ErrorResponse {
  @visibility(Lifecycle.Read)
  code?: string;

  @visibility(Lifecycle.Read)
  message?: string;

  @visibility(Lifecycle.Read)
  target?: string;

  @visibility(Lifecycle.Read)
  @identifiers(#["message", "target"])
  details?: ErrorResponse[];

  @visibility(Lifecycle.Read)
  @identifiers(#[])
  additionalInfo?: ErrorAdditionalInfo[];
}

model Provider {
  @visibility(Lifecycle.Read)
  id?: string;

  `namespace`?: string;

  @visibility(Lifecycle.Read)
  registrationState?: string;

  @visibility(Lifecycle.Read)
  registrationPolicy?: string;

  @visibility(Lifecycle.Read)
  @identifiers(#["resourceType"])
  resourceTypes?: ProviderResourceType[];

  providerAuthorizationConsentState?: ProviderAuthorizationConsentState;
}

model ProviderResourceType {
  resourceType?: string;
  locations?: string[];

  @identifiers(#["location", "type"])
  locationMappings?: ProviderExtendedLocation[];

  @identifiers(#["name"])
  aliases?: Alias[];

  apiVersions?: string[];

  @visibility(Lifecycle.Read)
  defaultApiVersion?: string;

  #suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
  @identifiers(#["location"])
  zoneMappings?: ZoneMapping[];

  @visibility(Lifecycle.Read)
  @identifiers(#["apiVersion", "profileVersion"])
  apiProfiles?: ApiProfile[];

  capabilities?: string;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  properties?: Record<string>;
}

model ProviderExtendedLocation {
  location?: string;
  type?: string;
  extendedLocations?: string[];
}

model Alias {
  name?: string;

  @identifiers(#[])
  paths?: AliasPath[];

  type?: AliasType;
  defaultPath?: string;
  defaultPattern?: AliasPattern;

  @visibility(Lifecycle.Read)
  defaultMetadata?: AliasPathMetadata;
}

model AliasPath {
  path?: string;
  apiVersions?: string[];
  pattern?: AliasPattern;

  @visibility(Lifecycle.Read)
  metadata?: AliasPathMetadata;
}

model AliasPattern {
  phrase?: string;
  variable?: string;
  type?: AliasPatternType;
}

#suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
model AliasPathMetadata {
  @visibility(Lifecycle.Read)
  type?: AliasPathTokenType;

  @visibility(Lifecycle.Read)
  attributes?: AliasPathAttributes;
}

#suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
model ZoneMapping {
  location?: string;

  #suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
  zones?: string[];
}

#suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
model ApiProfile {
  @visibility(Lifecycle.Read)
  profileVersion?: string;

  @visibility(Lifecycle.Read)
  apiVersion?: string;
}

model ProviderPermissionListResult is Azure.Core.Page<ProviderPermission>;

model ProviderPermission {
  applicationId?: string;
  roleDefinition?: RoleDefinition;
  managedByRoleDefinition?: RoleDefinition;
  providerAuthorizationConsentState?: ProviderAuthorizationConsentState;
}

model RoleDefinition {
  id?: string;
  name?: string;
  isServiceRole?: boolean;

  @identifiers(#[])
  permissions?: Permission[];

  scopes?: string[];
}

model Permission {
  actions?: string[];
  notActions?: string[];
  dataActions?: string[];
  notDataActions?: string[];
}

#suppress "@azure-tools/typespec-client-generator-core/csharp-model-suffix" "Name kept for backward compatibility"
model ProviderRegistrationRequest {
  thirdPartyProviderConsent?: ProviderConsentDefinition;
}

model ProviderConsentDefinition {
  consentToAuthorization?: boolean;
}

model ProviderListResult is Azure.Core.Page<Provider>;

model ProviderResourceTypeListResult is Azure.Core.Page<ProviderResourceType>;

#suppress "@azure-tools/typespec-azure-core/composition-over-inheritance" "For backward compatibility"
model GenericResourceExpanded extends GenericResource {
  @visibility(Lifecycle.Read)
  createdTime?: utcDateTime;

  @visibility(Lifecycle.Read)
  changedTime?: utcDateTime;

  @visibility(Lifecycle.Read)
  provisioningState?: string;
}

model Plan {
  name?: string;
  publisher?: string;
  product?: string;
  promotionCode?: string;
  version?: string;
}

model Sku {
  name?: string;
  tier?: string;
  size?: string;
  family?: string;
  `model`?: string;
  capacity?: int32;
}

model Identity {
  @visibility(Lifecycle.Read)
  principalId?: string;

  @visibility(Lifecycle.Read)
  tenantId?: string;

  type?: ResourceIdentityType;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  userAssignedIdentities?: Record<IdentityUserAssignedIdentitiesValue>;
}

#suppress "@azure-tools/typespec-azure-core/documentation-required" "For backward compatibility"
model IdentityUserAssignedIdentitiesValue {
  @visibility(Lifecycle.Read)
  principalId?: string;

  @visibility(Lifecycle.Read)
  clientId?: string;
}

model Resource {
  @visibility(Lifecycle.Read)
  id?: string;

  @visibility(Lifecycle.Read)
  name?: string;

  @visibility(Lifecycle.Read)
  type?: string;

  location?: string;

  @visibility(Lifecycle.Read, Lifecycle.Create)
  extendedLocation?: ExtendedLocation;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  tags?: Record<string>;
}

model ExtendedLocation {
  type?: ExtendedLocationType;
  name?: string;
}

model ResourceGroupProperties {
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-provisioning-state" "For backward compatibility"
  @visibility(Lifecycle.Read)
  provisioningState?: string;
}

model ResourceGroupPatchable {
  name?: string;
  properties?: ResourceGroupProperties;
  managedBy?: string;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  tags?: Record<string>;
}

#suppress "@azure-tools/typespec-client-generator-core/csharp-model-suffix" "Name kept for backward compatibility"
model ExportTemplateRequest {
  resources?: string[];
  options?: string;
  outputFormat?: ExportTemplateOutputFormat;
}

model ResourceGroupExportResult {
  #suppress "@azure-tools/typespec-azure-core/no-unknown" "For backward compatibility"
  template?: unknown;

  output?: string;
  error?: Azure.ResourceManager.CommonTypes.ErrorDetail;
}

model ResourcesMoveInfo {
  resources?: string[];
  targetResourceGroup?: string;
}

model TagValue {
  @visibility(Lifecycle.Read)
  id?: string;

  #suppress "@azure-tools/typespec-client-generator-core/property-name-conflict" "For backward compatibility"
  tagValue?: string;

  count?: TagCount;
}

model TagCount {
  type?: string;
  value?: int32;
}

model TagDetails {
  @visibility(Lifecycle.Read)
  id?: string;

  tagName?: string;
  count?: TagCount;
  values?: TagValue[];
}

model TagsListResult is Azure.Core.Page<TagDetails>;

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-provisioning-state" "For backward compatibility"
model Tags {
  #suppress "@azure-tools/typespec-client-generator-core/property-name-conflict" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  tags?: Record<string>;
}

model TagsPatchResource {
  operation?: TagsPatchOperation;
  properties?: Tags;
}

model GenericResourceFilter {
  resourceType?: string;
  tagname?: string;
  tagvalue?: string;
}

model ResourceGroupFilter {
  tagName?: string;
  tagValue?: string;
}

model ResourceProviderOperationDisplayProperties {
  publisher?: string;
  provider?: string;
  resource?: string;
  operation?: string;
  description?: string;
}

model SubResource {
  id?: string;
}
