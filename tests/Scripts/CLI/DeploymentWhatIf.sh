# Resource group scope deployment what-if - CLI (azv2 deployment group what-if)
# Equivalent of: az deployment group what-if
#
# What-if is read-only: it predicts the changes a deployment would make WITHOUT
# creating or modifying any resources.
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped what-if inherits the resource group's region,
# so no location flag is required here.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

# Happy path: predict the changes for a local .bicep storage template.
# Expected: the operation polls the Location LRO to completion and returns a
# WhatIfOperationResult with status Succeeded and a "Create" change for the storage account.
# No real storage account is created.
STAMP=$(date +%y%m%d%H%M%S)
deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-whatif-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirruswhatif$STAMP" \
    --mode Incremental \
    --debug

# ResourceIdOnly result-format variant: a leaner change list (no full payloads).
STAMP=$(date +%y%m%d%H%M%S)
deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name "cirrus-test-whatif-idonly-$STAMP" \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirruswhid$STAMP" \
    --result-format ResourceIdOnly \
    --mode Incremental \
    --debug

# Negative case: invalid --result-format value (Foo) must be rejected.
# Expected: the command fails fast with a clear validation message listing the allowed
# values (ResourceIdOnly, FullResourcePayloads) and does NOT issue a what-if.
deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-rf-negative \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --result-format Foo \
    --mode Incremental \
    --debug

# Negative case (local Bicep compilation error): a malformed .bicep fails compilation locally
# (non-zero exit -> InvalidOperationException) before any request is made. No what-if is run.
deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-bicep-negative \
    --template-file "$BICEP_DIR/malformed.bicep" \
    --mode Incremental \
    --debug
