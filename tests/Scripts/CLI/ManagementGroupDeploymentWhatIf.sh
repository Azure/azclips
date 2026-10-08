# Management group scope deployment what-if - CLI (az deployment mg what-if)
# Equivalent of: az deployment mg what-if
#
# What-if is READ-ONLY: it predicts the changes a template would make and never creates or
# modifies resources.
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to preview against
#   AZCLIPS_SUBSCRIPTION_ID     - nested subscription targeted by the create-preview cases

az config set --key agent.enabled --value false

# No `set -e`: the negative case below is EXPECTED to exit non-zero.

FAILURES=0
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { [[ "$3" == "$2" ]] && echo "OK: $1 => '$3'" || fail "$1 expected '$2' but got '$3'"; }

MANAGEMENT_GROUP_ID="${AZCLIPS_MANAGEMENT_GROUP_ID:?Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.}"
SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID to the target nested subscription.}"

TEMPLATE_FILE="$(mktemp -t azclips-mg-template-XXXXXX.json)"
cat > "$TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
JSON

echo "== 1. empty template (expect no predicted changes) =="
status=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$TEMPLATE_FILE" \
    --query status --output tsv --debug)
assert_eq "empty template status" "Succeeded" "$status"
changeCount=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$TEMPLATE_FILE" \
    --query "length(properties.changes)" --output tsv --debug)
assert_eq "empty template change count" "0" "$changeCount"

# Nested resource-group-create template so the what-if preview reports real "Create" changes.
NESTED_TEMPLATE_FILE="$(mktemp -t azclips-mg-nested-template-XXXXXX.json)"
cat > "$NESTED_TEMPLATE_FILE" <<'JSON'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "targetSubscriptionId": { "type": "string" },
    "rgName": { "type": "string" },
    "rgLocation": { "type": "string" }
  },
  "resources": [
    {
      "type": "Microsoft.Resources/deployments",
      "apiVersion": "2021-04-01",
      "name": "nestedRGDeployment",
      "location": "[parameters('rgLocation')]",
      "subscriptionId": "[parameters('targetSubscriptionId')]",
      "properties": {
        "mode": "Incremental",
        "template": {
          "$schema": "https://schema.management.azure.com/schemas/2019-08-01/subscriptionDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "resources": [
            {
              "type": "Microsoft.Resources/resourceGroups",
              "apiVersion": "2021-04-01",
              "name": "[parameters('rgName')]",
              "location": "[parameters('rgLocation')]"
            }
          ]
        }
      }
    }
  ]
}
JSON

echo "== 2. --result-format ResourceIdOnly (expect at least one predicted Create change) =="
RGNAME2="azclips-mg-nested-$(date +%y%m%d%H%M%S)"
status=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$NESTED_TEMPLATE_FILE" \
    --parameters targetSubscriptionId="$SUBSCRIPTION_ID" rgName="$RGNAME2" rgLocation=WestUS \
    --result-format ResourceIdOnly \
    --query status --output tsv --debug)
assert_eq "result-format status" "Succeeded" "$status"
changeCount=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$NESTED_TEMPLATE_FILE" \
    --parameters targetSubscriptionId="$SUBSCRIPTION_ID" rgName="$RGNAME2" rgLocation=WestUS \
    --result-format ResourceIdOnly \
    --query "length(properties.changes)" --output tsv --debug)
(( changeCount > 0 )) && hasChanges=true || hasChanges=false
assert_eq "result-format reported at least one change" "true" "$hasChanges"

echo "== 3. --exclude-change-types Create (expect the Create change filtered out) =="
RGNAME3="azclips-mg-nested-$(date +%y%m%d%H%M%S)"
status=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$NESTED_TEMPLATE_FILE" \
    --parameters targetSubscriptionId="$SUBSCRIPTION_ID" rgName="$RGNAME3" rgLocation=WestUS \
    --exclude-change-types Create \
    --query status --output tsv --debug)
assert_eq "exclude-change-types status" "Succeeded" "$status"
changeCount=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$NESTED_TEMPLATE_FILE" \
    --parameters targetSubscriptionId="$SUBSCRIPTION_ID" rgName="$RGNAME3" rgLocation=WestUS \
    --exclude-change-types Create \
    --query "length(properties.changes)" --output tsv --debug)
assert_eq "exclude-change-types change count" "0" "$changeCount"

echo "== 4. --validation-level ProviderNoRbac (preview without an RBAC permission check) =="
status=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location WestUS \
    --template-file "$NESTED_TEMPLATE_FILE" \
    --parameters targetSubscriptionId="$SUBSCRIPTION_ID" rgName="azclips-mg-nested-$(date +%y%m%d%H%M%S)" rgLocation=WestUS \
    --validation-level ProviderNoRbac \
    --query status --output tsv --debug)
assert_eq "validation-level ProviderNoRbac status" "Succeeded" "$status"

echo "== 5. negative: invalid --result-format Foo (must be rejected) =="
if output=$(az deployment mg what-if \
    --management-group-id "$MANAGEMENT_GROUP_ID" \
    --location eastus \
    --template-file "$TEMPLATE_FILE" \
    --result-format Foo \
    --debug 2>&1)
then
    echo "$output"
    didFail=false
else
    grep -qF "Parameter 'ResultFormat' value 'Foo' is not allowed. Allowed values: FullResourcePayloads, ResourceIdOnly" <<< "$output" && didFail=true || didFail=false
    [[ "$didFail" == "false" ]] && echo "$output"
fi
assert_eq "invalid --result-format Foo failed with the expected message" "true" "$didFail"

if (( FAILURES > 0 )); then
    echo "== deployment mg what-if smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== deployment mg what-if smoke test finished =="
