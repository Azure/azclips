---
title: az storage account | Microsoft Learn
uid: az_storage_account
ms.topic: generated-reference
ms.devlang: azurecli
---

# az storage account

Manage account resources.

## Commands

| Name | Description |
| --- | --- |
| [az storage account create](#az_storage_account_create) | Create a new account. |
| [az storage account delete](#az_storage_account_delete) | Delete a account. |
| [az storage account list](#az_storage_account_list) | List account resources by resource group. |
| [az storage account list-by-subscription](#az_storage_account_list_by_subscription) | List account resources across the subscription. |
| [az storage account show](#az_storage_account_show) | Get details of a specific account. |
| [az storage account update](#az_storage_account_update) | Update an existing account. |
| [az storage account wait](#az_storage_account_wait) | Invoke wait on a account. |
<a name="az_storage_account_create"></a>

### az storage account create

Create a new account. Required Azure API long-running-operation (LRO): use `--no-wait` to return before completion.

#### Syntax

```azurecli
az storage account create [--access-tier <string>]
                            [--allow-blob-public-access <bool>]
                            [--allow-cross-tenant-replication <bool>]
                            [--allow-shared-key-access <bool>]
                            [--allow-shared-key-access-for-services-blob-enabled <bool>]
                            [--allow-shared-key-access-for-services-file-enabled <bool>]
                            [--allow-shared-key-access-for-services-queue-enabled <bool>]
                            [--allow-shared-key-access-for-services-table-enabled <bool>]
                            [--allowed-copy-scope <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-account-type <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-guid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-sid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-forest-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-sam-account-name <string>]
                            [--azure-files-identity-based-authentication-default-share-permission <string>]
                            [--azure-files-identity-based-authentication-directory-service-options <string>]
                            [--azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled <bool>]
                            [--body-json-patch <string>]
                            [--custom-domain-use-sub-domain-name <bool>]
                            [--data-collaboration-policy-properties-allow-cross-tenant-data-sharing <bool>]
                            [--data-collaboration-policy-properties-allow-storage-connectors <bool>]
                            [--data-collaboration-policy-properties-allow-storage-data-shares <bool>]
                            [--default-to-o-auth-authentication <bool>]
                            [--dns-endpoint-type <string>]
                            [--dual-stack-endpoint-preference-publish-ipv6-endpoint <bool>]
                            [--enable-extended-groups <bool>]
                            [--enable-https-traffic-only <bool>]
                            [--enable-nfs-v3 <bool>]
                            [--encryption-encryption-identity-encryption-federated-identity-client-id <string>]
                            [--encryption-encryption-identity-encryption-user-assigned-identity <string>]
                            [--encryption-key-source <string>]
                            [--encryption-key-vault-properties-key-name <string>]
                            [--encryption-key-vault-properties-key-vault-uri <string>]
                            [--encryption-key-vault-properties-key-version <string>]
                            [--encryption-require-infrastructure-encryption <bool>]
                            [--encryption-services-blob-enabled <bool>]
                            [--encryption-services-blob-key-type <string>]
                            [--encryption-services-file-enabled <bool>]
                            [--encryption-services-file-key-type <string>]
                            [--encryption-services-queue-enabled <bool>]
                            [--encryption-services-queue-key-type <string>]
                            [--encryption-services-table-enabled <bool>]
                            [--encryption-services-table-key-type <string>]
                            [--geo-priority-replication-status-is-blob-enabled <bool>]
                            [--immutable-storage-with-versioning-enabled <bool>]
                            [--immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes <bool>]
                            [--immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days <int>]
                            [--immutable-storage-with-versioning-immutability-policy-state <string>]
                            [--is-hns-enabled <bool>]
                            [--is-local-user-enabled <bool>]
                            [--is-sftp-enabled <bool>]
                            [--key-policy-key-expiration-period-in-days <int>]
                            --kind <string>
                            [--large-file-shares-state <string>]
                            --location <string>
                            [--minimum-tls-version <string>]
                            --name <string>
                            [--network-rule-set-bypass <string>]
                            [--network-rule-set-default-action <string>]
                            [--no-wait <bool>]
                            [--placement-zone-placement-policy <string>]
                            [--public-network-access <string>]
                            --resource-group <string>
                            [--routing-preference-publish-internet-endpoints <bool>]
                            [--routing-preference-publish-microsoft-endpoints <bool>]
                            [--routing-preference-routing-choice <string>]
                            [--sas-policy-expiration-action <string>]
                            [--sas-policy-sas-expiration-period <string>]
                            --sku-name <string>
                            --subscription-id <string>
                            [--tags <object>]
                            [--zones <list<string>>]
```

#### Example

```azurecli
az storage account create --resource-group MyResourceGroup --name my-account
```

#### Required Parameters

**`--kind`**  (string)
> The `kind` parameter.

**`--location`**  (string)
> The `location` parameter.

**`--name`**  (string)
> The accountName..

**`--resource-group`**  (string)
> The resource group name.

**`--sku-name`**  (string)
> The `sku-name` parameter.

**`--subscription-id`**  (string)
> Azure Subscription Id.

#### Optional Parameters

**`--access-tier`**  (string)
> The `access-tier` parameter.

**`--allow-blob-public-access`**  (bool)
> The `allow-blob-public-access` parameter.

**`--allow-cross-tenant-replication`**  (bool)
> The `allow-cross-tenant-replication` parameter.

**`--allow-shared-key-access`**  (bool)
> The `allow-shared-key-access` parameter.

**`--allow-shared-key-access-for-services-blob-enabled`**  (bool)
> The `allow-shared-key-access-for-services-blob-enabled` parameter.

**`--allow-shared-key-access-for-services-file-enabled`**  (bool)
> The `allow-shared-key-access-for-services-file-enabled` parameter.

**`--allow-shared-key-access-for-services-queue-enabled`**  (bool)
> The `allow-shared-key-access-for-services-queue-enabled` parameter.

**`--allow-shared-key-access-for-services-table-enabled`**  (bool)
> The `allow-shared-key-access-for-services-table-enabled` parameter.

**`--allowed-copy-scope`**  (string)
> The `allowed-copy-scope` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-account-type`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-account-type` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-guid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-guid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-sid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-sid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-forest-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-forest-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-sam-account-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-sam-account-name` parameter.

**`--azure-files-identity-based-authentication-default-share-permission`**  (string)
> The `azure-files-identity-based-authentication-default-share-permission` parameter.

**`--azure-files-identity-based-authentication-directory-service-options`**  (string)
> The `azure-files-identity-based-authentication-directory-service-options` parameter.

**`--azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled`**  (bool)
> The `azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled` parameter.

**`--body-json-patch`**  (string)
> Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).

**`--custom-domain-use-sub-domain-name`**  (bool)
> The `custom-domain-use-sub-domain-name` parameter.

**`--data-collaboration-policy-properties-allow-cross-tenant-data-sharing`**  (bool)
> The `data-collaboration-policy-properties-allow-cross-tenant-data-sharing` parameter.

**`--data-collaboration-policy-properties-allow-storage-connectors`**  (bool)
> The `data-collaboration-policy-properties-allow-storage-connectors` parameter.

**`--data-collaboration-policy-properties-allow-storage-data-shares`**  (bool)
> The `data-collaboration-policy-properties-allow-storage-data-shares` parameter.

**`--default-to-o-auth-authentication`**  (bool)
> The `default-to-o-auth-authentication` parameter.

**`--dns-endpoint-type`**  (string)
> The `dns-endpoint-type` parameter.

**`--dual-stack-endpoint-preference-publish-ipv6-endpoint`**  (bool)
> The `dual-stack-endpoint-preference-publish-ipv6-endpoint` parameter.

**`--enable-extended-groups`**  (bool)
> The `enable-extended-groups` parameter.

**`--enable-https-traffic-only`**  (bool)
> The `enable-https-traffic-only` parameter.

**`--enable-nfs-v3`**  (bool)
> The `enable-nfs-v3` parameter.

**`--encryption-encryption-identity-encryption-federated-identity-client-id`**  (string)
> The `encryption-encryption-identity-encryption-federated-identity-client-id` parameter.

**`--encryption-encryption-identity-encryption-user-assigned-identity`**  (string)
> The `encryption-encryption-identity-encryption-user-assigned-identity` parameter.

**`--encryption-key-source`**  (string)
> The `encryption-key-source` parameter.

**`--encryption-key-vault-properties-key-name`**  (string)
> The `encryption-key-vault-properties-key-name` parameter.

**`--encryption-key-vault-properties-key-vault-uri`**  (string)
> The `encryption-key-vault-properties-key-vault-uri` parameter.

**`--encryption-key-vault-properties-key-version`**  (string)
> The `encryption-key-vault-properties-key-version` parameter.

**`--encryption-require-infrastructure-encryption`**  (bool)
> The `encryption-require-infrastructure-encryption` parameter.

**`--encryption-services-blob-enabled`**  (bool)
> The `encryption-services-blob-enabled` parameter.

**`--encryption-services-blob-key-type`**  (string)
> The `encryption-services-blob-key-type` parameter.

**`--encryption-services-file-enabled`**  (bool)
> The `encryption-services-file-enabled` parameter.

**`--encryption-services-file-key-type`**  (string)
> The `encryption-services-file-key-type` parameter.

**`--encryption-services-queue-enabled`**  (bool)
> The `encryption-services-queue-enabled` parameter.

**`--encryption-services-queue-key-type`**  (string)
> The `encryption-services-queue-key-type` parameter.

**`--encryption-services-table-enabled`**  (bool)
> The `encryption-services-table-enabled` parameter.

**`--encryption-services-table-key-type`**  (string)
> The `encryption-services-table-key-type` parameter.

**`--geo-priority-replication-status-is-blob-enabled`**  (bool)
> The `geo-priority-replication-status-is-blob-enabled` parameter.

**`--immutable-storage-with-versioning-enabled`**  (bool)
> The `immutable-storage-with-versioning-enabled` parameter.

**`--immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes`**  (bool)
> The `immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes` parameter.

**`--immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days`**  (int)
> The `immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days` parameter.

**`--immutable-storage-with-versioning-immutability-policy-state`**  (string)
> The `immutable-storage-with-versioning-immutability-policy-state` parameter.

**`--is-hns-enabled`**  (bool)
> The `is-hns-enabled` parameter.

**`--is-local-user-enabled`**  (bool)
> The `is-local-user-enabled` parameter.

**`--is-sftp-enabled`**  (bool)
> The `is-sftp-enabled` parameter.

**`--key-policy-key-expiration-period-in-days`**  (int)
> The `key-policy-key-expiration-period-in-days` parameter.

**`--large-file-shares-state`**  (string)
> The `large-file-shares-state` parameter.

**`--minimum-tls-version`**  (string)
> The `minimum-tls-version` parameter.

**`--network-rule-set-bypass`**  (string)
> The `network-rule-set-bypass` parameter.

**`--network-rule-set-default-action`**  (string)
> The `network-rule-set-default-action` parameter.

**`--no-wait`**  (bool)
> Do not wait for the long-running operation to complete; return the initial response immediately.

**`--placement-zone-placement-policy`**  (string)
> The `placement-zone-placement-policy` parameter.

**`--public-network-access`**  (string)
> The `public-network-access` parameter.

**`--routing-preference-publish-internet-endpoints`**  (bool)
> The `routing-preference-publish-internet-endpoints` parameter.

**`--routing-preference-publish-microsoft-endpoints`**  (bool)
> The `routing-preference-publish-microsoft-endpoints` parameter.

**`--routing-preference-routing-choice`**  (string)
> The `routing-preference-routing-choice` parameter.

**`--sas-policy-expiration-action`**  (string)
> The `sas-policy-expiration-action` parameter.

**`--sas-policy-sas-expiration-period`**  (string)
> The `sas-policy-sas-expiration-period` parameter.

**`--tags`**  (object)
> Resource tags.

**`--zones`**  (List<string>)
> The `zones` parameter.

<a name="az_storage_account_delete"></a>

### az storage account delete

Delete a account. Long-running-operation.

#### Syntax

```azurecli
az storage account delete --name <string>
                            --resource-group <string>
                            --subscription-id <string>
```

#### Example

```azurecli
az storage account delete --resource-group MyResourceGroup --name my-account
```

#### Required Parameters

**`--name`**  (string)
> The accountName..

**`--resource-group`**  (string)
> The resource group name.

**`--subscription-id`**  (string)
> Azure Subscription Id.

<a name="az_storage_account_list"></a>

### az storage account list

Enumerate all account resources in the given resource group.

#### Syntax

```azurecli
az storage account list --resource-group <string>
                            --subscription-id <string>
```

#### Example

```azurecli
az storage account list --resource-group MyResourceGroup
```

#### Required Parameters

**`--resource-group`**  (string)
> The resource group name.

**`--subscription-id`**  (string)
> Azure Subscription Id.

<a name="az_storage_account_list_by_subscription"></a>

### az storage account list-by-subscription

Enumerate all account resources in the subscription.

#### Syntax

```azurecli
az storage account list-by-subscription --subscription-id <string>
```

#### Example

```azurecli
az storage account list-by-subscription --resource-group MyResourceGroup
```

#### Required Parameters

**`--subscription-id`**  (string)
> Azure Subscription Id.

<a name="az_storage_account_show"></a>

### az storage account show

Retrieve details of a account by name.

#### Syntax

```azurecli
az storage account show [--body-json-patch <string>]
                            [--expand <string>]
                            --name <string>
                            --resource-group <string>
                            --subscription-id <string>
```

#### Example

```azurecli
az storage account show --resource-group MyResourceGroup --name my-account
```

#### Required Parameters

**`--name`**  (string)
> The accountName..

**`--resource-group`**  (string)
> The resource group name.

**`--subscription-id`**  (string)
> Azure Subscription Id.

#### Optional Parameters

**`--body-json-patch`**  (string)
> Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).

**`--expand`**  (string)
> The `expand` parameter.

<a name="az_storage_account_update"></a>

### az storage account update

Patch fields on an existing account.

#### Syntax

```azurecli
az storage account update [--access-tier <string>]
                            [--allow-blob-public-access <bool>]
                            [--allow-cross-tenant-replication <bool>]
                            [--allow-shared-key-access <bool>]
                            [--allow-shared-key-access-for-services-blob-enabled <bool>]
                            [--allow-shared-key-access-for-services-file-enabled <bool>]
                            [--allow-shared-key-access-for-services-queue-enabled <bool>]
                            [--allow-shared-key-access-for-services-table-enabled <bool>]
                            [--allowed-copy-scope <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-account-type <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-guid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-domain-sid <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-forest-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name <string>]
                            [--azure-files-identity-based-authentication-active-directory-properties-sam-account-name <string>]
                            [--azure-files-identity-based-authentication-default-share-permission <string>]
                            [--azure-files-identity-based-authentication-directory-service-options <string>]
                            [--azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled <bool>]
                            [--body-json-patch <string>]
                            [--custom-domain-use-sub-domain-name <bool>]
                            [--data-collaboration-policy-properties-allow-cross-tenant-data-sharing <bool>]
                            [--data-collaboration-policy-properties-allow-storage-connectors <bool>]
                            [--data-collaboration-policy-properties-allow-storage-data-shares <bool>]
                            [--default-to-o-auth-authentication <bool>]
                            [--dns-endpoint-type <string>]
                            [--dual-stack-endpoint-preference-publish-ipv6-endpoint <bool>]
                            [--enable-extended-groups <bool>]
                            [--enable-https-traffic-only <bool>]
                            [--encryption-encryption-identity-encryption-federated-identity-client-id <string>]
                            [--encryption-encryption-identity-encryption-user-assigned-identity <string>]
                            [--encryption-key-source <string>]
                            [--encryption-key-vault-properties-key-name <string>]
                            [--encryption-key-vault-properties-key-vault-uri <string>]
                            [--encryption-key-vault-properties-key-version <string>]
                            [--encryption-require-infrastructure-encryption <bool>]
                            [--encryption-services-blob-enabled <bool>]
                            [--encryption-services-blob-key-type <string>]
                            [--encryption-services-file-enabled <bool>]
                            [--encryption-services-file-key-type <string>]
                            [--encryption-services-queue-enabled <bool>]
                            [--encryption-services-queue-key-type <string>]
                            [--encryption-services-table-enabled <bool>]
                            [--encryption-services-table-key-type <string>]
                            [--geo-priority-replication-status-is-blob-enabled <bool>]
                            [--immutable-storage-with-versioning-enabled <bool>]
                            [--immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes <bool>]
                            [--immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days <int>]
                            [--immutable-storage-with-versioning-immutability-policy-state <string>]
                            [--is-local-user-enabled <bool>]
                            [--is-sftp-enabled <bool>]
                            [--key-policy-key-expiration-period-in-days <int>]
                            [--kind <string>]
                            [--large-file-shares-state <string>]
                            [--minimum-tls-version <string>]
                            --name <string>
                            [--network-rule-set-bypass <string>]
                            [--network-rule-set-default-action <string>]
                            [--placement-zone-placement-policy <string>]
                            [--public-network-access <string>]
                            --resource-group <string>
                            [--routing-preference-publish-internet-endpoints <bool>]
                            [--routing-preference-publish-microsoft-endpoints <bool>]
                            [--routing-preference-routing-choice <string>]
                            [--sas-policy-expiration-action <string>]
                            [--sas-policy-sas-expiration-period <string>]
                            [--sku-name <string>]
                            --subscription-id <string>
                            [--tags <object>]
                            [--zones <list<string>>]
```

#### Example

```azurecli
az storage account update --resource-group MyResourceGroup --name my-account
```

#### Required Parameters

**`--name`**  (string)
> The accountName..

**`--resource-group`**  (string)
> The resource group name.

**`--subscription-id`**  (string)
> Azure Subscription Id.

#### Optional Parameters

**`--access-tier`**  (string)
> The `access-tier` parameter.

**`--allow-blob-public-access`**  (bool)
> The `allow-blob-public-access` parameter.

**`--allow-cross-tenant-replication`**  (bool)
> The `allow-cross-tenant-replication` parameter.

**`--allow-shared-key-access`**  (bool)
> The `allow-shared-key-access` parameter.

**`--allow-shared-key-access-for-services-blob-enabled`**  (bool)
> The `allow-shared-key-access-for-services-blob-enabled` parameter.

**`--allow-shared-key-access-for-services-file-enabled`**  (bool)
> The `allow-shared-key-access-for-services-file-enabled` parameter.

**`--allow-shared-key-access-for-services-queue-enabled`**  (bool)
> The `allow-shared-key-access-for-services-queue-enabled` parameter.

**`--allow-shared-key-access-for-services-table-enabled`**  (bool)
> The `allow-shared-key-access-for-services-table-enabled` parameter.

**`--allowed-copy-scope`**  (string)
> The `allowed-copy-scope` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-account-type`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-account-type` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-azure-storage-sid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-guid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-guid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-domain-sid`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-domain-sid` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-forest-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-forest-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-net-bios-domain-name` parameter.

**`--azure-files-identity-based-authentication-active-directory-properties-sam-account-name`**  (string)
> The `azure-files-identity-based-authentication-active-directory-properties-sam-account-name` parameter.

**`--azure-files-identity-based-authentication-default-share-permission`**  (string)
> The `azure-files-identity-based-authentication-default-share-permission` parameter.

**`--azure-files-identity-based-authentication-directory-service-options`**  (string)
> The `azure-files-identity-based-authentication-directory-service-options` parameter.

**`--azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled`**  (bool)
> The `azure-files-identity-based-authentication-smb-o-auth-settings-is-smb-o-auth-enabled` parameter.

**`--body-json-patch`**  (string)
> Raw JSON deep-merged into the request body after flat parameters. Escape hatch for schema shapes the emitter cannot flatten (e.g. arrays of models).

**`--custom-domain-use-sub-domain-name`**  (bool)
> The `custom-domain-use-sub-domain-name` parameter.

**`--data-collaboration-policy-properties-allow-cross-tenant-data-sharing`**  (bool)
> The `data-collaboration-policy-properties-allow-cross-tenant-data-sharing` parameter.

**`--data-collaboration-policy-properties-allow-storage-connectors`**  (bool)
> The `data-collaboration-policy-properties-allow-storage-connectors` parameter.

**`--data-collaboration-policy-properties-allow-storage-data-shares`**  (bool)
> The `data-collaboration-policy-properties-allow-storage-data-shares` parameter.

**`--default-to-o-auth-authentication`**  (bool)
> The `default-to-o-auth-authentication` parameter.

**`--dns-endpoint-type`**  (string)
> The `dns-endpoint-type` parameter.

**`--dual-stack-endpoint-preference-publish-ipv6-endpoint`**  (bool)
> The `dual-stack-endpoint-preference-publish-ipv6-endpoint` parameter.

**`--enable-extended-groups`**  (bool)
> The `enable-extended-groups` parameter.

**`--enable-https-traffic-only`**  (bool)
> The `enable-https-traffic-only` parameter.

**`--encryption-encryption-identity-encryption-federated-identity-client-id`**  (string)
> The `encryption-encryption-identity-encryption-federated-identity-client-id` parameter.

**`--encryption-encryption-identity-encryption-user-assigned-identity`**  (string)
> The `encryption-encryption-identity-encryption-user-assigned-identity` parameter.

**`--encryption-key-source`**  (string)
> The `encryption-key-source` parameter.

**`--encryption-key-vault-properties-key-name`**  (string)
> The `encryption-key-vault-properties-key-name` parameter.

**`--encryption-key-vault-properties-key-vault-uri`**  (string)
> The `encryption-key-vault-properties-key-vault-uri` parameter.

**`--encryption-key-vault-properties-key-version`**  (string)
> The `encryption-key-vault-properties-key-version` parameter.

**`--encryption-require-infrastructure-encryption`**  (bool)
> The `encryption-require-infrastructure-encryption` parameter.

**`--encryption-services-blob-enabled`**  (bool)
> The `encryption-services-blob-enabled` parameter.

**`--encryption-services-blob-key-type`**  (string)
> The `encryption-services-blob-key-type` parameter.

**`--encryption-services-file-enabled`**  (bool)
> The `encryption-services-file-enabled` parameter.

**`--encryption-services-file-key-type`**  (string)
> The `encryption-services-file-key-type` parameter.

**`--encryption-services-queue-enabled`**  (bool)
> The `encryption-services-queue-enabled` parameter.

**`--encryption-services-queue-key-type`**  (string)
> The `encryption-services-queue-key-type` parameter.

**`--encryption-services-table-enabled`**  (bool)
> The `encryption-services-table-enabled` parameter.

**`--encryption-services-table-key-type`**  (string)
> The `encryption-services-table-key-type` parameter.

**`--geo-priority-replication-status-is-blob-enabled`**  (bool)
> The `geo-priority-replication-status-is-blob-enabled` parameter.

**`--immutable-storage-with-versioning-enabled`**  (bool)
> The `immutable-storage-with-versioning-enabled` parameter.

**`--immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes`**  (bool)
> The `immutable-storage-with-versioning-immutability-policy-allow-protected-append-writes` parameter.

**`--immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days`**  (int)
> The `immutable-storage-with-versioning-immutability-policy-immutability-period-since-creation-in-days` parameter.

**`--immutable-storage-with-versioning-immutability-policy-state`**  (string)
> The `immutable-storage-with-versioning-immutability-policy-state` parameter.

**`--is-local-user-enabled`**  (bool)
> The `is-local-user-enabled` parameter.

**`--is-sftp-enabled`**  (bool)
> The `is-sftp-enabled` parameter.

**`--key-policy-key-expiration-period-in-days`**  (int)
> The `key-policy-key-expiration-period-in-days` parameter.

**`--kind`**  (string)
> The `kind` parameter.

**`--large-file-shares-state`**  (string)
> The `large-file-shares-state` parameter.

**`--minimum-tls-version`**  (string)
> The `minimum-tls-version` parameter.

**`--network-rule-set-bypass`**  (string)
> The `network-rule-set-bypass` parameter.

**`--network-rule-set-default-action`**  (string)
> The `network-rule-set-default-action` parameter.

**`--placement-zone-placement-policy`**  (string)
> The `placement-zone-placement-policy` parameter.

**`--public-network-access`**  (string)
> The `public-network-access` parameter.

**`--routing-preference-publish-internet-endpoints`**  (bool)
> The `routing-preference-publish-internet-endpoints` parameter.

**`--routing-preference-publish-microsoft-endpoints`**  (bool)
> The `routing-preference-publish-microsoft-endpoints` parameter.

**`--routing-preference-routing-choice`**  (string)
> The `routing-preference-routing-choice` parameter.

**`--sas-policy-expiration-action`**  (string)
> The `sas-policy-expiration-action` parameter.

**`--sas-policy-sas-expiration-period`**  (string)
> The `sas-policy-sas-expiration-period` parameter.

**`--sku-name`**  (string)
> The `sku-name` parameter.

**`--tags`**  (object)
> Resource tags.

**`--zones`**  (List<string>)
> The `zones` parameter.

<a name="az_storage_account_wait"></a>

### az storage account wait

Perform the `wait` operation on a account.

#### Syntax

```azurecli
az storage account wait --name <string>
                            --resource-group <string>
                            --subscription-id <string>
```

#### Example

```azurecli
az storage account wait --resource-group MyResourceGroup --name my-account
```

#### Required Parameters

**`--name`**  (string)
> The accountName..

**`--resource-group`**  (string)
> The resource group name.

**`--subscription-id`**  (string)
> Azure Subscription Id.

