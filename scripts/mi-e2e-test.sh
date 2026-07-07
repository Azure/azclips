#!/usr/bin/env bash
#
# mi-e2e-test.sh — Full managed-identity end-to-end test (run on your LOCAL machine).
#
# Orchestrates the whole flow against an already-provisioned VM (Stage 1,
# terraform/linux-vm/change.sh, must have run first):
#
#   1. publish-vm-mi.sh --tar         build the self-contained az bundle (+ .tar.gz)
#   2. scp the tarball to the VM
#   3. ssh to the VM: extract + setup-vm-mi.sh --bundle ... --test-mi
#        (installs az, then `login --identity` + `vm show` to verify MI auth)
#
# VM connection details come from the Terraform outputs of terraform/linux-vm
# (public_ip_address, vm_name, resource_group). SSH uses your default key/agent.
#
# Usage:
#   scripts/mi-e2e-test.sh [options]
#
#   --rid RID             Runtime id for the bundle (default: linux-x64)
#   --config CONFIG       Debug|Release (default: Release)
#   --user USER           SSH user (default: azureuser)
#   --vm-name NAME        VM name to verify (default: terraform output vm_name)
#   --resource-group RG   Resource group to verify (default: terraform output resource_group)
#   --provision           Run terraform/linux-vm/change.sh first (provision/update
#                         the VM). Fast no-op when nothing changed. Default: off.
#   --skip-publish        Reuse the existing tarball instead of rebuilding
#
# Examples:
#   scripts/mi-e2e-test.sh
#   scripts/mi-e2e-test.sh --provision
#   scripts/mi-e2e-test.sh --skip-publish
#
set -euo pipefail

RID="linux-x64"
CONFIG="Release"
USER_NAME="azureuser"
VM_NAME=""
RESOURCE_GROUP=""
PROVISION=0
SKIP_PUBLISH=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)             RID="$2"; shift 2 ;;
    --config)          CONFIG="$2"; shift 2 ;;
    --user)            USER_NAME="$2"; shift 2 ;;
    --vm-name)         VM_NAME="$2"; shift 2 ;;
    --resource-group)  RESOURCE_GROUP="$2"; shift 2 ;;
    --provision)       PROVISION=1; shift ;;
    --skip-publish)    SKIP_PUBLISH=1; shift ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -n 33
      exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPTS_DIR="$REPO_ROOT/scripts"
TF_DIR="$REPO_ROOT/terraform/linux-vm"

# Preflight: fail fast with a clear message if required tooling/config is missing,
# rather than erroring cryptically partway through. Required tools depend on which
# stages will actually run (dotnet only when publishing).
preflight() {
  local missing=()

  # ssh/scp are always needed (Stage 3 ships to and runs on the VM).
  command -v ssh >/dev/null 2>&1 || missing+=("ssh (OpenSSH client)")
  command -v scp >/dev/null 2>&1 || missing+=("scp (OpenSSH client)")

  # terraform is always needed to auto-discover the VM host (and to provision).
  command -v terraform >/dev/null 2>&1 || missing+=("terraform")

  # dotnet is needed to build the bundle (Stage 2), unless reusing a tarball.
  if [[ "$SKIP_PUBLISH" -eq 0 ]]; then
    command -v dotnet >/dev/null 2>&1 || missing+=("dotnet (.NET SDK)")
  fi

  # change.sh requires terraform.tfvars (ssh_public_key has no default).
  if [[ "$PROVISION" -eq 1 && ! -f "$TF_DIR/terraform.tfvars" ]]; then
    missing+=("$TF_DIR/terraform.tfvars (set ssh_public_key; copy from terraform.tfvars.example)")
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

# 0) Optionally provision/update the VM first (Stage 1). change.sh runs a
#    plan and only applies when there are pending changes, so this is a fast
#    no-op when the infrastructure already matches.
if [[ "$PROVISION" -eq 1 ]]; then
  echo "==> --provision: running terraform/linux-vm/change.sh"
  "$TF_DIR/change.sh"
  echo
fi

# Resolve a Terraform output value, or empty string if unavailable.
tf_output() {
  terraform -chdir="$TF_DIR" output -raw "$1" 2>/dev/null || true
}

# Fill connection/target details from Terraform outputs when not given explicitly.
HOST="$(tf_output public_ip_address)"
[[ -z "$VM_NAME"        ]] && VM_NAME="$(tf_output vm_name)"
[[ -z "$RESOURCE_GROUP" ]] && RESOURCE_GROUP="$(tf_output resource_group)"

if [[ -z "$HOST" ]]; then
  echo "!! Could not determine VM host from terraform output. Run with --provision, or" >&2
  echo "   run terraform apply in $TF_DIR first." >&2
  exit 1
fi

# Sensible fallbacks if Terraform outputs were unavailable.
[[ -z "$VM_NAME"        ]] && VM_NAME="test-vm-mi"
[[ -z "$RESOURCE_GROUP" ]] && RESOURCE_GROUP="ming-test"

# User-assigned managed identity selectors, sourced from Terraform outputs (the
# source of truth — created by the module). Passed to the VM-side test so it can
# verify `login --identity` + vm show via --client-id, --resource-id and
# --object-id. Empty when the module exposes no UAMI outputs (test skips them).
UAMI_CLIENT_ID="$(tf_output user_assigned_identity_client_id)"
UAMI_RESOURCE_ID="$(tf_output user_assigned_identity_resource_id)"
UAMI_OBJECT_ID="$(tf_output user_assigned_identity_principal_id)"

SSH_OPTS=(-o StrictHostKeyChecking=accept-new)

BUNDLE_NAME="${CONFIG}-${RID}"
TARBALL="$REPO_ROOT/publish/vm/azclips-${BUNDLE_NAME}.tar.gz"

echo "==> Target VM:       ${USER_NAME}@${HOST}"
echo "==> Verifying:       vm show --name ${VM_NAME} --resource-group ${RESOURCE_GROUP}"
echo "==> Bundle:          ${BUNDLE_NAME}"
echo

# 1) Build the bundle (Stage 2) unless reusing an existing tarball.
if [[ "$SKIP_PUBLISH" -eq 1 ]]; then
  echo "==> --skip-publish: reusing $TARBALL"
  if [[ ! -f "$TARBALL" ]]; then
    echo "!! Tarball not found: $TARBALL (drop --skip-publish to build it)." >&2
    exit 1
  fi
else
  echo "==> Building bundle via publish-vm-mi.sh ..."
  "$SCRIPTS_DIR/publish-vm-mi.sh" --rid "$RID" --config "$CONFIG" --tar
fi

# 2) Ship the tarball to the VM.
echo
echo "==> Copying $(basename "$TARBALL") to ${USER_NAME}@${HOST}:~/"
scp "${SSH_OPTS[@]}" "$TARBALL" "${USER_NAME}@${HOST}:~/"

# 3) On the VM: extract, then install + MI smoke test (Stage 3). The UAMI selectors
#    (from Terraform output) are passed through so the VM-side test verifies the
#    user-assigned identity via --client-id/--resource-id/--object-id in addition
#    to the system-assigned one.
UAMI_ARGS=""
[[ -n "$UAMI_CLIENT_ID"   ]] && UAMI_ARGS+=" --uami-client-id '${UAMI_CLIENT_ID}'"
[[ -n "$UAMI_RESOURCE_ID" ]] && UAMI_ARGS+=" --uami-resource-id '${UAMI_RESOURCE_ID}'"
[[ -n "$UAMI_OBJECT_ID"   ]] && UAMI_ARGS+=" --uami-object-id '${UAMI_OBJECT_ID}'"

echo
echo "==> Running setup-vm-mi.sh --test-mi on the VM ..."
ssh "${SSH_OPTS[@]}" "${USER_NAME}@${HOST}" \
  "set -euo pipefail; \
   tar -xzf ~/azclips-${BUNDLE_NAME}.tar.gz -C ~/; \
   ~/${BUNDLE_NAME}/setup-vm-mi.sh --bundle ~/${BUNDLE_NAME} --test-mi \
     --vm-name '${VM_NAME}' --resource-group '${RESOURCE_GROUP}'${UAMI_ARGS}"

echo
echo "==> End-to-end MI test PASSED."
