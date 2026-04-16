namespace Tests.Common.Utilities;

/// <summary>
/// Canned ARM JSON responses for isolated benchmarks.
/// Property names match the Azure Resource Manager API response shape (camelCase).
/// </summary>
public static class MockResponses
{
    public const string ResourceGroupList = """
        {
            "value": [
                {
                    "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/benchmark-rg-1",
                    "name": "benchmark-rg-1",
                    "type": "Microsoft.Resources/resourceGroups",
                    "location": "eastus",
                    "properties": {
                        "provisioningState": "Succeeded"
                    },
                    "managedBy": "",
                    "tags": {}
                },
                {
                    "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/benchmark-rg-2",
                    "name": "benchmark-rg-2",
                    "type": "Microsoft.Resources/resourceGroups",
                    "location": "westus2",
                    "properties": {
                        "provisioningState": "Succeeded"
                    },
                    "managedBy": "",
                    "tags": {}
                }
            ]
        }
        """;

    public const string ResourceGroupGet = """
        {
            "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/{name}",
            "name": "{name}",
            "type": "Microsoft.Resources/resourceGroups",
            "location": "eastus",
            "properties": {
                "provisioningState": "Succeeded"
            },
            "managedBy": "",
            "tags": {}
        }
        """;

    public const string ResourceGroupCreate = """
        {
            "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/{name}",
            "name": "{name}",
            "type": "Microsoft.Resources/resourceGroups",
            "location": "{location}",
            "properties": {
                "provisioningState": "Succeeded"
            },
            "managedBy": "",
            "tags": {}
        }
        """;
}
