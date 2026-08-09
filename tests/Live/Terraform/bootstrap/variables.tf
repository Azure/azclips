# Keep this validation in sync with:
#   - tests/Live/Terraform/linux-vm/variables.tf         (same variable)
#   - tests/Live/Terraform/bootstrap/setup-github-identity.sh        (bash copy of the regex)
#   - tests/Live/Terraform/linux-vm/provision-infrastructure.sh      (bash copy of the regex)
variable "alias" {
  type        = string
  nullable    = false
  description = <<-EOT
    Your short user alias. Prefixes every per-developer resource so multiple
    developers can share one tenant/subscription without colliding, and selects
    the per-developer Terraform state blob. Required — there is no default.
  EOT

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,12}[a-z0-9]$", var.alias))
    error_message = "alias must be 3-14 characters, lowercase, start with a letter, and contain only a-z, 0-9 and '-'. The 14-character limit comes from the state storage account name, which is capped at 24 characters."
  }
}

variable "github_repo" {
  type        = string
  nullable    = false
  description = <<-EOT
    The GitHub repository the workflow runs from, as "owner/repo". This must
    match EXACTLY, because it forms the federated credential subject that GitHub's
    OIDC token is checked against — a mismatch fails at login time with an opaque
    error. Required — there is no default.
  EOT

  validation {
    condition     = can(regex("^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9._-]{1,100}$", var.github_repo))
    error_message = "github_repo must be in \"owner/repo\" form, e.g. \"xuming-ms/azclips\"."
  }
}

variable "github_environment" {
  type        = string
  description = <<-EOT
    GitHub Actions environment used by the workflow. The federated credential is
    scoped to this environment rather than to a branch, so the workflow can be run
    from any branch without adding credentials. The workflow job MUST declare a
    matching `environment:` value or the OIDC subject will not match.
  EOT
  default     = "dev"
}

variable "subscription_role" {
  type        = string
  description = "RBAC role granted to the CI identity at subscription scope. Contributor is required because stage 1 creates its own resource group."
  default     = "Contributor"
}

variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant ID hosting the app registration. Supplied by the wrapper script from the active az context."
}

variable "subscription_id" {
  type        = string
  description = "Azure subscription ID the CI identity is granted access to. Supplied by the wrapper script from the active az context."
}
