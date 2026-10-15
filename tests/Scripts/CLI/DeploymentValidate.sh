#!/usr/bin/env bash
# Resource group deployment validate smoke test for the azclips CLI surface.
# Exercises `az deployment group validate`. Validate is READ-ONLY: it asks ARM whether a template
# would be accepted and never creates or modifies resources, so there is nothing to clean up.
# The resource-group-scoped validate inherits the resource group's region, so no location is needed.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. Template-file cases compile Bicep, so the Bicep CLI must be on PATH or at ~/.azure/bin/bicep.
#   4. A resource group must already exist. If you don't have one, run:
#        ./tests/Scripts/CLI/CreateResourceGroup.sh <resource-group> -l <location> -s <subscription-id>
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription holding the test resource group
#
# Optional:
#   AZCLIPS_TENANT_ID      - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP - existing resource group to validate against; defaults to azclips-test-group-1
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/DeploymentValidate.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
# az.exe can't open Git Bash paths (/c/...); `pwd -W` gives the Windows form.
BICEP_DIR="$(cd "$REPO_ROOT/tests/Scripts/Bicep" && { pwd -W 2>/dev/null || pwd; })"

AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Stop Git Bash from rewriting ARM IDs like /subscriptions/... into Windows paths.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-test-group-1}"
TEMPLATE_URI="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json"

expect_succeeded() {
  if [[ "$2" != "Succeeded" ]]; then
    echo "$1 returned provisioningState '$2', expected 'Succeeded'" >&2
    exit 1
  fi
  echo "provisioningState: $2"
}

# Disable the interactive agent so errors surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo -e "\n=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo -e "\n=== [deployment group validate] template uri ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-1 \
  --template-uri "$TEMPLATE_URI" \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "template-uri validate" "$STATE"

echo -e "\n=== [deployment group validate] bicep template + bicepparam ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-bicep \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "bicep validate" "$STATE"

echo -e "\n=== [deployment group validate] inline parameter overrides bicepparam ==="
# The .bicepparam sets storageAccountName=azclipsbicep0001; --parameter overlays it.
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-inline \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameters-file "$BICEP_DIR/sample-storage.bicepparam" \
  --parameter "storageAccountName=azclipsvalidateinline" \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "inline-override validate" "$STATE"

echo -e "\n=== [deployment group validate] --validation-level Template ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-level \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameter "storageAccountName=azclipsvalidatelevel" \
  --validation-level Template \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "template-level validate" "$STATE"

echo -e "\n=== [deployment group validate] --rollback-on-error ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-rollback \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameter "storageAccountName=azclipsvalidaterollback" \
  --rollback-on-error \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "rollback-on-error validate" "$STATE"

echo -e "\n=== [deployment group validate] --validation-level Provider (202 long-running) ==="
# ARM answers 202 + Location and the command polls it; the output must be the final result.
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-lro \
  --template-file "$BICEP_DIR/sample-storage.bicep" \
  --parameter "storageAccountName=azclipsvalidatelro" \
  --validation-level Provider \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "long-running validate" "$STATE"

echo -e "\n=== [deployment group validate] nullable template parameter may be omitted ==="
STATE="$("$AZ" deployment group validate \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name azclips-test-validate-nullable \
  --template-file "$BICEP_DIR/nullable-parameter.json" \
  --mode Incremental \
  --query properties.provisioningState -o tsv)"
expect_succeeded "nullable-parameter validate" "$STATE"

# Negative cases run inside `if` so `set -e` doesn't abort on the expected failure.
echo -e "\n=== [deployment group validate] malformed bicep must fail to compile ==="
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-bicep-negative \
      --template-file "$BICEP_DIR/malformed.bicep" \
      --mode Incremental 2>&1)"; then
  echo "validate of a malformed bicep file succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *BCP* ]]; then
  echo "malformed bicep failed without a bicep diagnostic: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: bicep diagnostic"

echo -e "\n=== [deployment group validate] --validation-level Foo is rejected ==="
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-vl-negative \
      --template-uri "$TEMPLATE_URI" \
      --validation-level Foo \
      --mode Incremental 2>&1)"; then
  echo "validate with --validation-level Foo succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *ProviderNoRbac* ]]; then
  echo "invalid validation level failed without listing the allowed values: $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: invalid --validation-level"

echo -e "\n=== [deployment group validate] rollback options are mutually exclusive ==="
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-rollback-negative \
      --template-file "$BICEP_DIR/sample-storage.bicep" \
      --rollback-on-error \
      --rollback-to-deployment-name azclips-test-validate-1 \
      --mode Incremental 2>&1)"; then
  echo "validate with both rollback options succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *"mutually exclusive"* ]]; then
  echo "both rollback options failed without 'mutually exclusive': $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: rollback options are mutually exclusive"

echo -e "\n=== [deployment group validate] ARM rejects an invalid template ==="
# whatif-invalid-template.json references an undefined parameter; ARM answers 400 with a
# DeploymentValidateResult, which the command must flatten into a readable error.
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-apierror \
      --template-file "$BICEP_DIR/whatif-invalid-template.json" \
      --mode Incremental 2>&1)"; then
  echo "validate of an invalid template succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *"Deployment validation failed"* ]]; then
  echo "invalid template failed without 'Deployment validation failed': $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: Deployment validation failed"

echo -e "\n=== [deployment group validate] nonexistent template file is rejected locally ==="
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-missing-file \
      --template-file "$BICEP_DIR/does-not-exist.json" \
      --mode Incremental 2>&1)"; then
  echo "validate of a nonexistent template file succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *"File not found"* ]]; then
  echo "nonexistent template file failed without 'File not found': $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: File not found"

echo -e "\n=== [deployment group validate] malformed JSON template is rejected locally ==="
BROKEN_TEMPLATE="$(mktemp --suffix=.json)"
trap 'rm -f "$BROKEN_TEMPLATE"' EXIT
echo '{ not json' > "$BROKEN_TEMPLATE"
BROKEN_TEMPLATE_ARG="$BROKEN_TEMPLATE"
command -v cygpath > /dev/null && BROKEN_TEMPLATE_ARG="$(cygpath -w "$BROKEN_TEMPLATE")"
if OUTPUT="$("$AZ" deployment group validate \
      --subscription-id "$SUBSCRIPTION_ID" \
      --resource-group "$RESOURCE_GROUP" \
      --name azclips-test-validate-malformed-json \
      --template-file "$BROKEN_TEMPLATE_ARG" \
      --mode Incremental 2>&1)"; then
  echo "validate of a malformed JSON template succeeded" >&2
  exit 1
fi
if [[ "$OUTPUT" != *"Failed to parse file"* ]]; then
  echo "malformed JSON template failed without 'Failed to parse file': $OUTPUT" >&2
  exit 1
fi
echo "Expected failure: Failed to parse file"

echo -e "\nAll deployment validate smoke checks completed."
