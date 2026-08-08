#!/usr/bin/env bash
#
# mi-live-test.sh — Full managed-identity live test against real Azure (no SSH).
#
# Drives an already-provisioned Linux VM entirely through `az vm run-command`
# (the Azure control plane + the VM guest agent) — there is no SSH, no open
# port 22, and no long-lived SSH key. All Azure resources it touches live in
# your resource group.
#
# Flow (Stage 1, terraform/linux-vm/provision-infrastructure.sh, must have provisioned the VM):
#
#   1. publish-vm-mi.sh --tar        build the self-contained az bundle (+ .tar.gz)
#   2. upload the tarball to a blob in a storage account IN YOUR RESOURCE GROUP,
#      then mint a short-lived, read-only SAS URL for it
#   3. az vm run-command invoke a small script on the VM that:
#        - curls the bundle from that SAS URL, extracts it, and
#        - runs setup-vm-mi.sh --test-mi (login --identity + vm show), verifying
#          the system-assigned and any user-assigned identities.
#      The script prints a PASS/FAIL sentinel that this orchestrator checks —
#      run-command reports invocation success, not script exit code, so the
#      sentinel is how we turn a failed smoke test into a non-zero exit here.
#
# VM/identity/location details come from the Terraform outputs of
# terraform/linux-vm. Azure auth is your `az login` (interactive locally, OIDC in
# CI) — the same identity that provisions the VM.
#
# Usage:
#   scripts/mi-live-test.sh [options]
#
#   --rid RID              Runtime id for the bundle (default: linux-x64)
#   --config CONFIG        Debug|Release (default: Release)
#   --vm-name NAME         VM name to verify (default: terraform output vm_name)
#   --resource-group RG    Resource group for ALL operations (default: terraform
#                          output resource_group, which derives from your alias)
#   --storage-account NAME Storage account for the bundle blob (default: derived
#                          deterministically from subscription+RG; created if absent)
#   --container NAME       Blob container name (default: milive-bundles)
#   --location LOC         Region for a newly created storage account (default:
#                          terraform output location, else the RG's location)
#   --provision            Run terraform/linux-vm/provision-infrastructure.sh first (provision/update
#                          the VM). Fast no-op when nothing changed. Default: off.
#   --skip-publish         Reuse the existing tarball instead of rebuilding
#   --keep-blob            Do not delete the uploaded bundle blob afterwards
#
# Examples:
#   scripts/mi-live-test.sh
#   scripts/mi-live-test.sh --provision
#   scripts/mi-live-test.sh --skip-publish
#
set -euo pipefail

RID="linux-x64"
CONFIG="Release"
VM_NAME=""
RESOURCE_GROUP=""
STORAGE_ACCOUNT=""
CONTAINER="milive-bundles"
LOCATION=""
PROVISION=0
SKIP_PUBLISH=0
KEEP_BLOB=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)             RID="$2"; shift 2 ;;
    --config)          CONFIG="$2"; shift 2 ;;
    --vm-name)         VM_NAME="$2"; shift 2 ;;
    --resource-group)  RESOURCE_GROUP="$2"; shift 2 ;;
    --storage-account) STORAGE_ACCOUNT="$2"; shift 2 ;;
    --container)       CONTAINER="$2"; shift 2 ;;
    --location)        LOCATION="$2"; shift 2 ;;
    --provision)       PROVISION=1; shift ;;
    --skip-publish)    SKIP_PUBLISH=1; shift ;;
    --keep-blob)       KEEP_BLOB=1; shift ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -n 48
      exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPTS_DIR="$REPO_ROOT/scripts"
TF_DIR="$REPO_ROOT/terraform/linux-vm"

# Shared helpers, including the pinned tenant/subscription and require_az_context.
# shellcheck source=../terraform/tf-common.sh
source "$REPO_ROOT/terraform/tf-common.sh"

# shellcheck source=lib/timing.sh
source "$SCRIPTS_DIR/lib/timing.sh"

# Fail fast, before anything slow or billable: building the bundle takes minutes,
# and every later step assumes the right subscription. This also covers az being
# missing or signed out.
require_az_context

# Preflight: fail fast with a clear message if required tooling/config is missing.
preflight() {
  local missing=()

  # terraform is needed to auto-discover the VM/identity/location outputs (and to provision).
  command -v terraform >/dev/null 2>&1 || missing+=("terraform")

  # dotnet is needed to build the bundle, unless reusing a tarball.
  if [[ "$SKIP_PUBLISH" -eq 0 ]]; then
    command -v dotnet >/dev/null 2>&1 || missing+=("dotnet (.NET SDK)")
  fi

  # provision-infrastructure.sh needs `alias` — from either tfvars file or the
  # environment. It names every resource and selects the Terraform state blob.
  if [[ "$PROVISION" -eq 1 \
    && ! -f "$REPO_ROOT/terraform/terraform.tfvars" \
    && ! -f "$TF_DIR/terraform.tfvars" \
    && -z "${TF_VAR_alias:-}" ]]; then
    missing+=("$REPO_ROOT/terraform/terraform.tfvars (set alias; copy from terraform.tfvars.example) or TF_VAR_alias")
  fi

  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "!! Preflight failed — missing prerequisites:" >&2
    for m in "${missing[@]}"; do
      echo "     - $m" >&2
    done
    echo "   Install/provide the above, then re-run. See --help." >&2
    exit 1
  fi
}

preflight

# 0) Optionally provision/update the VM first (Stage 1). The script runs a plan
#    and only applies when there are pending changes — a fast no-op otherwise.
if [[ "$PROVISION" -eq 1 ]]; then
  timing_start "terraform provision (plan + apply-if-changed)"
  "$TF_DIR/provision-infrastructure.sh"
  timing_end
  echo
else
  echo "==> Skipping provision (pass --provision to plan/apply the VM first)"
fi

# `terraform output` reads REMOTE state, which needs the backend initialised and
# ARM_ACCESS_KEY exported in THIS shell. Unconditional: the --provision wrapper
# runs as a child process, so the key it exports never reaches us.
timing_start "terraform init backend + read outputs"
init_module_state "$TF_DIR" "linux-vm" || {
  echo "!! Could not open the Terraform state for this alias." >&2
  exit 1
}

# Resolve a Terraform output value, or empty string if unavailable.
tf_output() {
  terraform -chdir="$TF_DIR" output -raw "$1" 2>/dev/null | tr -d '\r' || true
}

# Run `az ... -o tsv` and strip carriage returns. The Azure CLI on Windows (used
# via WSL) emits CRLF, and a stray trailing \r corrupts SAS URLs, keys, and args.
az_tsv() {
  az "$@" -o tsv | tr -d '\r'
}

# Fill target details from Terraform outputs when not given explicitly.
[[ -z "$VM_NAME"        ]] && VM_NAME="$(tf_output vm_name)"
[[ -z "$RESOURCE_GROUP" ]] && RESOURCE_GROUP="$(tf_output resource_group)"
[[ -z "$LOCATION"       ]] && LOCATION="$(tf_output location)"

# User-assigned managed identity selectors (source of truth: Terraform outputs).
UAMI_CLIENT_ID="$(tf_output user_assigned_identity_client_id)"
UAMI_RESOURCE_ID="$(tf_output user_assigned_identity_resource_id)"
UAMI_OBJECT_ID="$(tf_output user_assigned_identity_principal_id)"
timing_end

# No name fallbacks: resource names are per-developer (alias-derived), so a
# guessed default would point at somebody else's VM or at nothing at all. Fail
# with an actionable message instead.
if [[ -z "$VM_NAME" || -z "$RESOURCE_GROUP" ]]; then
  echo "!! Could not determine VM name / resource group from the Terraform outputs." >&2
  echo "   Provision first (creates them and populates the outputs):" >&2
  echo "     scripts/mi-live-test.sh --provision" >&2
  echo "   or pass them explicitly with --vm-name and --resource-group." >&2
  echo "   If you have provisioned already, check that terraform/linux-vm has the" >&2
  echo "   same alias configured — each alias has its own state and resources." >&2
  exit 1
fi

SUBSCRIPTION_ID="$(az_tsv account show --query id 2>/dev/null || true)"
if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "!! Not logged in to Azure (az account show failed). Run 'az login'." >&2
  exit 1
fi

# Region for a newly created storage account: TF output, else the RG's location.
if [[ -z "$LOCATION" ]]; then
  LOCATION="$(az_tsv group show -n "$RESOURCE_GROUP" --query location 2>/dev/null || true)"
fi

# Deterministic, globally-unique-ish storage account name (3-24 lowercase alnum):
# stable per subscription+RG so re-runs reuse the same account. Overridable.
if [[ -z "$STORAGE_ACCOUNT" ]]; then
  hash="$(printf '%s' "${SUBSCRIPTION_ID}-${RESOURCE_GROUP}" | sha256sum | cut -c1-16)"
  STORAGE_ACCOUNT="milive${hash}"
fi

BUNDLE_NAME="${CONFIG}-${RID}"
TARBALL="$REPO_ROOT/publish/vm/azclips-${BUNDLE_NAME}.tar.gz"

echo "==> Target VM:        ${VM_NAME}"
echo "==> Resource group:   ${RESOURCE_GROUP}"
echo "==> Verifying:        vm show --name ${VM_NAME} --resource-group ${RESOURCE_GROUP}"
echo "==> Bundle:           ${BUNDLE_NAME}"
echo "==> Storage account:  ${STORAGE_ACCOUNT} (container ${CONTAINER})"
echo

# 1) Build the bundle unless reusing an existing tarball.
if [[ "$SKIP_PUBLISH" -eq 1 ]]; then
  echo "==> --skip-publish: reusing $TARBALL"
  if [[ ! -f "$TARBALL" ]]; then
    echo "!! Tarball not found: $TARBALL (drop --skip-publish to build it)." >&2
    exit 1
  fi
else
  timing_start "build bundle (publish-vm-mi.sh)"
  "$SCRIPTS_DIR/publish-vm-mi.sh" --rid "$RID" --config "$CONFIG" --tar
  timing_end
fi

# 2) Ensure a storage account + container exist IN THE RESOURCE GROUP, then upload
#    the tarball as a blob. A shared-key SAS is used so the VM can download with a
#    plain HTTPS GET (no identity needed for the download itself). Listing keys is
#    covered by the same Contributor rights used to provision the VM.
echo
timing_start "ensure storage account + container"
if ! az storage account show -n "$STORAGE_ACCOUNT" -g "$RESOURCE_GROUP" -o none 2>/dev/null; then
  if [[ -z "$LOCATION" ]]; then
    echo "!! Could not determine a location to create storage account '$STORAGE_ACCOUNT'." >&2
    echo "   Pass --location, or ensure the resource group exists." >&2
    exit 1
  fi
  echo "==> Creating storage account ${STORAGE_ACCOUNT} in ${RESOURCE_GROUP} (${LOCATION})"
  az storage account create \
    -n "$STORAGE_ACCOUNT" -g "$RESOURCE_GROUP" -l "$LOCATION" \
    --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 \
    --allow-blob-public-access false --allow-shared-key-access true -o none
else
  echo "==> Reusing storage account ${STORAGE_ACCOUNT}"
fi

STORAGE_KEY="$(az_tsv storage account keys list -n "$STORAGE_ACCOUNT" -g "$RESOURCE_GROUP" --query '[0].value')"
if [[ -z "$STORAGE_KEY" ]]; then
  echo "!! Could not read a storage account key for ${STORAGE_ACCOUNT}." >&2
  exit 1
fi

az storage container create \
  --name "$CONTAINER" --account-name "$STORAGE_ACCOUNT" --account-key "$STORAGE_KEY" -o none
timing_end

BLOB_NAME="azclips-${BUNDLE_NAME}-$(date -u +%Y%m%d%H%M%S).tar.gz"
echo "==> Uploading $(basename "$TARBALL") ($(du -h "$TARBALL" | cut -f1)) -> ${STORAGE_ACCOUNT}/${CONTAINER}/${BLOB_NAME}"
timing_start "upload bundle blob"
az storage blob upload \
  --account-name "$STORAGE_ACCOUNT" --account-key "$STORAGE_KEY" \
  --container-name "$CONTAINER" --name "$BLOB_NAME" --file "$TARBALL" --overwrite -o none
timing_end

# Short-lived, read-only SAS URL (2h) so the VM can pull the bundle over HTTPS.
timing_start "generate SAS url"
SAS_EXPIRY="$(date -u -d '+2 hours' '+%Y-%m-%dT%H:%MZ')"
SAS_TOKEN="$(az_tsv storage blob generate-sas \
  --account-name "$STORAGE_ACCOUNT" --account-key "$STORAGE_KEY" \
  --container-name "$CONTAINER" --name "$BLOB_NAME" \
  --permissions r --expiry "$SAS_EXPIRY" --https-only)"
BLOB_URL="https://${STORAGE_ACCOUNT}.blob.core.windows.net/${CONTAINER}/${BLOB_NAME}?${SAS_TOKEN}"
timing_end

# Clean up local temp files (and the blob unless --keep-blob) on exit. The RG's
# CanNotDelete lock is management-plane only, so deleting the blob (data-plane) is fine.
REMOTE_SCRIPT="$(mktemp)"
cleanup() {
  rm -f "$REMOTE_SCRIPT"
  if [[ "$KEEP_BLOB" -eq 0 ]]; then
    az storage blob delete \
      --account-name "$STORAGE_ACCOUNT" --account-key "$STORAGE_KEY" \
      --container-name "$CONTAINER" --name "$BLOB_NAME" -o none 2>/dev/null || true
  fi
}
trap cleanup EXIT

# User-assigned identity selectors, forwarded to the VM-side test so it verifies
# `login --identity` via --client-id/--resource-id/--object-id too.
UAMI_ARGS=""
[[ -n "$UAMI_CLIENT_ID"   ]] && UAMI_ARGS+=" --uami-client-id '${UAMI_CLIENT_ID}'"
[[ -n "$UAMI_RESOURCE_ID" ]] && UAMI_ARGS+=" --uami-resource-id '${UAMI_RESOURCE_ID}'"
[[ -n "$UAMI_OBJECT_ID"   ]] && UAMI_ARGS+=" --uami-object-id '${UAMI_OBJECT_ID}'"

# 3) The script that runs ON the VM via run-command. It downloads + extracts the
#    bundle, then runs the existing setup-vm-mi.sh smoke test. Backslash-escaped
#    $ are evaluated on the VM; unescaped ${...} are expanded here.
cat > "$REMOTE_SCRIPT" <<REMOTE
# run-command executes this via /bin/sh (dash), so keep it POSIX — no pipefail.
# The heavy lifting (setup-vm-mi.sh) runs under its own bash shebang.
set -eu
export HOME="\${HOME:-/root}"
WORK="\$(mktemp -d)"
cd "\$WORK"

# The bundle carries timing.sh, but it is not on disk yet — so time these two
# steps by hand, emitting the same markers the orchestrator parses.
export TIMING_EMIT_MARKERS=1
mark() { echo "MI_LIVE_TIMING:\$1|\$(( ( \$(date +%s%N) - \$2 ) / 1000000 ))|0"; }

echo "==> Downloading bundle from Azure Storage"
t0=\$(date +%s%N)
if command -v curl >/dev/null 2>&1; then
  curl -fsSL '${BLOB_URL}' -o bundle.tar.gz
elif command -v wget >/dev/null 2>&1; then
  wget -q -O bundle.tar.gz '${BLOB_URL}'
else
  echo "!! neither curl nor wget available on the VM" >&2
  echo "MI_LIVE_RESULT:FAIL"
  exit 1
fi
mark "download bundle" "\$t0"

echo "==> Extracting bundle"
t0=\$(date +%s%N)
tar -xzf bundle.tar.gz
mark "extract bundle" "\$t0"
BUNDLE="${BUNDLE_NAME}"
chmod +x "\$BUNDLE/setup-vm-mi.sh" 2>/dev/null || true

if "\$BUNDLE/setup-vm-mi.sh" --bundle "\$BUNDLE" --test-mi \
     --vm-name '${VM_NAME}' --resource-group '${RESOURCE_GROUP}' \
     --subscription-id '${SUBSCRIPTION_ID}'${UAMI_ARGS}; then
  echo "MI_LIVE_RESULT:PASS"
else
  echo "MI_LIVE_RESULT:FAIL"
  exit 1
fi
REMOTE

echo
timing_start "az vm run-command (download + install + tests)"
RUN_OUTPUT="$(az_tsv vm run-command invoke \
  --resource-group "$RESOURCE_GROUP" --name "$VM_NAME" \
  --command-id RunShellScript --scripts "@$REMOTE_SCRIPT" \
  --query 'value[0].message')"
timing_end
RUN_COMMAND_MS="${_TIMING_MS[-1]}"

echo "----- run-command output -----"
echo "$RUN_OUTPUT"
echo "------------------------------"

# The orchestrator's own table first, then the VM's, rebuilt from the markers
# the guest printed. Two tables rather than one because the run-command row
# already contains every VM row.
timing_summary "Stage 2 — orchestrator (mi-live-test.sh)"

timing_reset
while IFS='|' read -r label ms detail; do
  [[ -z "$label" || -z "$ms" ]] && continue
  timing_record "$label" "$ms" "${detail:-0}"
done < <(grep -o 'MI_LIVE_TIMING:[^|]*|[0-9]*|[0-9]*' <<<"$RUN_OUTPUT" | sed 's/^MI_LIVE_TIMING://' || true)
TIMING_EMIT_MARKERS="" timing_summary "Stage 2 — on the VM (via run-command)" "$RUN_COMMAND_MS"

# run-command only returns the tail of stdout, so a very chatty guest can lose
# the earliest markers. Say so rather than showing a silently short table.
if ! grep -q 'MI_LIVE_TIMING:download bundle' <<<"$RUN_OUTPUT"; then
  echo "    (note: run-command truncated its reply — earliest VM steps are missing above)"
  echo
fi

if grep -q 'MI_LIVE_RESULT:PASS' <<<"$RUN_OUTPUT"; then
  echo "==> End-to-end MI test PASSED."
else
  echo "!! End-to-end MI test FAILED (no PASS sentinel in run-command output)." >&2
  exit 1
fi
