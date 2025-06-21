<!-- region Generated -->
# Az.Databricks
This directory contains the PowerShell module for the Databricks service.

---
## Info
- Modifiable: yes
- Generated: all
- Committed: yes
- Packaged: yes

---
## Detail
This module was primarily generated via [AutoRest](https://github.com/Azure/autorest) using the [PowerShell](https://github.com/Azure/autorest.powershell) extension.

## Module Requirements
- [Az.Accounts module](https://www.powershellgallery.com/packages/Az.Accounts/), version 2.7.5 or greater

## Authentication
AutoRest does not generate authentication code for the module. Authentication is handled via Az.Accounts by altering the HTTP payload before it is sent.

## Development
For information on how to develop for `Az.Databricks`, see [how-to.md](how-to.md).
<!-- endregion -->

---
### AutoRest Configuration
> see https://aka.ms/autorest

``` yaml
commit: 8dc708fdac9cb97b346ddb38106ac16e668f64cd
tag: package-2024-05-01
require:
  - $(this-folder)/../readme.azure.noprofile.md
  - $(repo)/specification/databricks/resource-manager/readme.md
try-require:
  - $(repo)/specification/databricks/resource-manager/readme.powershell.md

module-version: 1.2.0
title: Databricks
subject-prefix: $(service-name)

inlining-threshold: 100
disable-transform-identity-type: true
flatten-userassignedidentity: false

# resourcegroup-append: true
# identity-correction-for-post: true
# nested-object-to-string: true

# # For new modules, please avoid setting 3.x using the use-extension method and instead, use 4.x as the default option
# use-extension:
#   "@autorest/powershell": "3.x"

directive:
  - from: swagger-document
    where: $.definitions.EncryptionV2
    transform: delete $.required

  - from: swagger-document
    where: $.definitions.ManagedDiskEncryption
    transform: delete $.required

# Worked around this issue: https://github.com/Azure/autorest.powershell/issues/1258
  # - from: EncryptionEntitiesDefinition.json.cs
  #   where: $
  #   transform: $ = $.replace('ManagedService;', '_managedService;')

  # - from: EncryptionEntitiesDefinition.json.cs
  #   where: $
  #   transform: $ = $.replace('ManagedDisk;', '_managedDisk;')

# Remove cmdlet, Private link related resource should be ignored. 
  - where:
     subject: PrivateEndpointConnection|PrivateLinkResource
    remove: true
  - where:
      variant: ^(Create|Update)(?!.*?(Expanded|JsonFilePath|JsonString))
    remove: true
  - where:
      variant: ^CreateViaIdentity$|^CreateViaIdentityExpanded$
    remove: true
  # Follow directive is v3 specific. If you are using v3, uncomment following directive and comments out two directives above
  #- where:
  #    variant: ^Create$|^CreateViaIdentity$|^CreateViaIdentityExpanded$|^Update$|^UpdateViaIdentity$
  #  remove: true

  # Remove the set-* cmdlet
  - where:
      verb: Set
    remove: true
```
