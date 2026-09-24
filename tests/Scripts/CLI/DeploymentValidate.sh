# Resource group scope deployment validate - CLI (az deployment group validate)
# Equivalent of: az deployment group validate
#
# Validate is READ-ONLY: it asks ARM whether a template would be accepted and never
# creates or modifies resources. The same name can be reused safely.
#
# Shared test context:
#   subscription: 282bb33f-067d-4179-a7a4-b833c13af735
#   resource group: azclips-test-group-1 (region: australiaeast)
# The resource-group-scoped validate inherits the resource group's region,
# so no location flag is required here.

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang. (The old AZCLIPS_AGENT_DISABLED env var was removed; the agent now
# reads config.agent.enabled and also auto-skips when stdin isn't a TTY.)
az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.

# Resolve the sibling Bicep folder relative to THIS script so it works regardless of the caller's CWD.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../Bicep"

# Happy path (template-uri): validate a public quickstart template in Incremental mode.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-1 \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --mode Incremental \
    --debug

# Bicep happy path (template-file): compile a local .bicep template via the Bicep CLI and
# validate it. Requires the Bicep CLI on PATH or the az-bundled bicep (~/.azure/bin/bicep).
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-bicep \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --mode Incremental \
    --debug

# Inline-parameter precedence: the .bicepparam supplies storageAccountName=azclipsbicep0001
# (the base); the repeatable --parameter overrides it, proving inline overlays the file.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-inline \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
    --parameter "storageAccountName=azclipsvalidateinline" \
    --mode Incremental \
    --debug

# --validation-level Template: validate the template only, without calling resource providers.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-level \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=azclipsvalidatelevel" \
    --validation-level Template \
    --mode Incremental \
    --debug

# Rollback option: record that the group should roll back to its last successful deployment
# on failure. Accepted by validate and carried in the request body.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-rollback \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --parameter "storageAccountName=azclipsvalidaterollback" \
    --rollback-on-error \
    --mode Incremental \
    --debug

# Expected-failure case: a malformed .bicep must fail compilation and surface bicep's
# diagnostics (non-zero exit -> InvalidOperationException). No validate call is made.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-bicep-negative \
    --template-file "$BICEP_DIR/malformed.bicep" \
    --mode Incremental \
    --debug

# Negative case: invalid --validation-level value (Foo) must be rejected.
# Expected result: the command fails fast with a clear validation message listing the
# allowed values (Template, Provider, ProviderNoRbac) and does NOT call the validate API.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-vl-negative \
    --template-uri https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json \
    --validation-level Foo \
    --mode Incremental \
    --debug

# Negative case: the two rollback options are mutually exclusive and must be rejected
# locally before any request is sent.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-rollback-negative \
    --template-file "$BICEP_DIR/sample-storage.bicep" \
    --rollback-on-error \
    --rollback-to-deployment-name azclips-test-validate-1 \
    --mode Incremental \
    --debug

# Expected-failure case (LIVE validate API error): whatif-invalid-template.json parses fine but
# references the undefined parameter 'undefinedNameParam', so ARM rejects it. Validate reports
# this as HTTP 400 carrying a DeploymentValidateResult whose error describes the failure. This
# exercises the processor's 400 handling (NOT a local pre-flight check): the command MUST exit
# non-zero with a flattened, readable error rather than raw ARM JSON, and MUST NOT report success.
az deployment group validate \
    --subscription-id 282bb33f-067d-4179-a7a4-b833c13af735 \
    --resource-group azclips-test-group-1 \
    --name azclips-test-validate-apierror \
    --template-file "$BICEP_DIR/whatif-invalid-template.json" \
    --mode Incremental \
    --debug
