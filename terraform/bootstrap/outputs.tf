output "client_id" {
  description = "Client (application) ID of the CI identity. Published to GitHub as the AZURE_CLIENT_ID environment variable."
  value       = azuread_application.ci.client_id
}

output "service_principal_object_id" {
  description = "Object (principal) ID of the service principal that holds the RBAC role assignment."
  value       = azuread_service_principal.ci.object_id
}

output "app_display_name" {
  description = "Display name of the app registration, as shown in the Entra portal."
  value       = local.app_display_name
}

output "federated_subject" {
  description = "OIDC subject the federated credential trusts. The workflow job's `environment:` must match this."
  value       = local.federated_subject
}

output "alias" {
  description = "Developer alias. Published to GitHub as the ALIAS environment variable so CI resolves the same resources and state as local runs."
  value       = var.alias
}

output "tenant_id" {
  description = "Entra tenant ID. Published to GitHub as AZURE_TENANT_ID."
  value       = var.tenant_id
}

output "subscription_id" {
  description = "Azure subscription ID. Published to GitHub as AZURE_SUBSCRIPTION_ID."
  value       = var.subscription_id
}

output "github_environment" {
  description = "GitHub Actions environment the credential is scoped to."
  value       = var.github_environment
}
