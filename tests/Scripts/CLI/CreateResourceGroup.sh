#!/usr/bin/env bash
# Creates a resource group through the built CLI, for use as a setup step by other
# test scripts. Idempotent: `group create` on an existing group is a no-op update,
# so re-running is safe.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Usage: ./tests/Scripts/CLI/CreateResourceGroup.sh <resource-group> [options]
#   -g, --resource-group   required (or pass it as the first positional argument)
#   -l, --location         defaults to eastus
#   -s, --subscription-id  defaults to the sample test subscription
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"
if [[ ! -x "$AZ" ]]; then
    echo "Built CLI not found at $REPO_ROOT/artifacts/Debug/Core.Cli/az[.exe]. Run dotnet build first." >&2
    exit 1
fi

RESOURCE_GROUP=""
LOCATION=eastus
SUBSCRIPTION_ID=f758ac53-3e63-4317-a956-0997793808d7

usage() {
    echo "Usage: $0 <resource-group> [-l location] [-s subscription-id]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -g|--resource-group)  RESOURCE_GROUP="${2:-}"; shift 2 ;;
        -l|--location)        LOCATION="${2:-}"; shift 2 ;;
        -s|--subscription-id) SUBSCRIPTION_ID="${2:-}"; shift 2 ;;
        -h|--help)            usage; exit 0 ;;
        -*)                   echo "Unknown option '$1'." >&2; usage; exit 1 ;;
        *)
            if [[ -n "$RESOURCE_GROUP" ]]; then
                echo "Unexpected argument '$1'. Only the resource group is positional; everything else takes a flag." >&2
                usage
                exit 1
            fi
            RESOURCE_GROUP="$1"; shift ;;
    esac
done

if [[ -z "$RESOURCE_GROUP" ]]; then
    usage
    exit 1
fi

echo "--- Create resource group '$RESOURCE_GROUP' in '$LOCATION' ---"
"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"
