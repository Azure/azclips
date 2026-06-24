# Resource group scope deployment what-if - CLI (azv2 deployment group what-if)
# Equivalent of: az deployment group what-if
#
# What-if is READ-ONLY: it predicts the changes a template would make and never
# creates or modifies resources. The same name can be reused safely.
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped what-if inherits the resource group's region,
# so no location flag is required here.

# Run unattended: on a command ERROR azv2 normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang. (The old CIRRUS_AGENT_DISABLED env var was removed; the agent now
# reads config.agent.enabled and also auto-skips when stdin isn't a TTY.)
azv2 config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

# Happy path (template-uri): preview a public quickstart template in Incremental mode.
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-1 \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --mode Incremental \
    --debug

# Bicep happy path (template-file): compile a local .bicep template via the Bicep CLI and
# preview it. Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-bicep \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --mode Incremental \
    --debug

# Inline-parameter precedence: the .bicepparam supplies storageAccountName=cirrusbicep0001
# (the base); the repeatable --parameter overrides it, proving inline overlays the file.
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-inline \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --parameter "storageAccountName=cirruswhatifinline" \
    --mode Incremental \
    --debug

# --result-format ResourceIdOnly: the predicted changes carry resource ids only
# (no full before/after payloads).
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-idonly \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=cirruswhatifidonly" \
    --result-format ResourceIdOnly \
    --mode Incremental \
    --debug

# Expected-failure case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No what-if is run.
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-bicep-negative \
    --template-file "$BICEP_DIR/malformed.bicep" \
    --mode Incremental \
    --debug

# Negative case: invalid --result-format value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message listing the
# allowed values (FullResourcePayloads, ResourceIdOnly) and does NOT run a what-if.
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-rf-negative \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --result-format Foo \
    --mode Incremental \
    --debug

# Expected-failure case (LIVE what-if API error): whatif-invalid-template.json parses fine but
# references the undefined parameter 'undefinedNameParam', so ARM completes the what-if
# long-running operation with a terminal 'Failed' state and an InvalidTemplate error in
# result.error. This exercises the orchestrator's on-the-wire error handling (NOT a local
# pre-flight check): the command MUST exit non-zero with a flattened error and MUST NOT report
# a successful no-change result.
azv2 deployment group what-if \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group cirrus-test-group-1 \
    --name cirrus-test-whatif-apierror \
    --template-file "$BICEP_DIR/whatif-invalid-template.json" \
    --mode Incremental \
    --debug
