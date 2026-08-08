# STAGE 0 — the GitHub Actions CI identity.
#
# Entra objects (app registration, service principal, federated credential) live
# here; the Azure resources they are granted access to are created by stage 1
# (terraform/linux-vm). This module is applied ONLY from a developer machine by a
# human with Owner on the subscription plus rights to create Entra applications.
# It must never run in CI, because it creates the very identity CI logs in as.

locals {
  app_display_name = "${var.alias}-azclips-live-test-mi"

  # GitHub's OIDC token subject for an environment-scoped job. Must match the
  # workflow's `environment:` value exactly.
  federated_subject = "repo:${var.github_repo}:environment:${var.github_environment}"
}

resource "azuread_application" "ci" {
  display_name = local.app_display_name

  # Display names are not unique in Entra, and the object is identified by
  # client_id/object_id, so the alias prefix exists purely to keep a shared
  # tenant's app list readable.
}

resource "azuread_service_principal" "ci" {
  client_id = azuread_application.ci.client_id
}

# The trust between GitHub and Entra: GitHub mints an OIDC id token for the
# workflow, Entra exchanges it for an Azure access token when issuer + subject +
# audience all match. No client secret is ever created.
resource "azuread_application_federated_identity_credential" "github" {
  application_id = azuread_application.ci.id
  display_name   = "github-${var.github_environment}"
  description    = "GitHub Actions OIDC for ${var.github_repo} (${var.github_environment} environment)"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = local.federated_subject
}

# Subscription scope (not resource-group scope) because stage 1 creates its own
# per-developer resource group, which cannot be granted from inside itself.
resource "azurerm_role_assignment" "ci_subscription" {
  scope                = "/subscriptions/${var.subscription_id}"
  role_definition_name = var.subscription_role
  principal_id         = azuread_service_principal.ci.object_id

  # A newly created service principal is not immediately visible to ARM, which
  # would otherwise fail this assignment with PrincipalNotFound. This skips the
  # pre-flight directory lookup instead of sleeping for replication.
  skip_service_principal_aad_check = true
}
