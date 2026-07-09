variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant ID used for the managed identity E2E environment."
  default     = "4f00b3b6-2940-4f2c-b037-94637c180d30"
}

variable "subscription_id" {
  type        = string
  description = "Azure subscription ID used for the managed identity E2E environment."
  default     = "f758ac53-3e63-4317-a956-0997793808d7"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the EXISTING resource group to deploy into. This template only ever deploys into this group."
  nullable    = false
}

variable "vm_name" {
  type        = string
  description = "Name of the Linux virtual machine (also used as a prefix for its network resources)."
  default     = "cirrus-mi-e2e-vm"
}

variable "user_assigned_identity_name" {
  type        = string
  description = "Name of the user-assigned managed identity to create and attach to the VM."
  default     = "cirrus-mi-e2e-uami"
}

variable "uami_role" {
  type        = string
  description = "RBAC role granted to the user-assigned identity at the resource-group scope."
  default     = "Reader"
}

variable "system_assigned_identity_role" {
  type        = string
  description = "RBAC role granted to the VM's system-assigned identity at the resource-group scope."
  default     = "Reader"
}

variable "resource_lock_name" {
  type        = string
  description = "Name of the CanNotDelete management lock applied to the resource group."
  default     = "test-retension"
}

variable "vm_size" {
  type        = string
  description = "Azure VM size/SKU."
  default     = "Standard_D2s_v4"
}

variable "admin_username" {
  type        = string
  description = "Admin username for the Linux VM."
  default     = "azureuser"
}

variable "os_disk_type" {
  type        = string
  description = "Managed OS disk storage account type."
  default     = "Standard_LRS"
}

variable "ssh_public_key" {
  type        = string
  description = "Optional SSH public key (OpenSSH format) for admin login. When set, it replaces the generated throwaway keypair so you can SSH in with the matching private key. Leave empty to keep the generated key (CI drives the VM via `az vm run-command`, no SSH needed)."
  default     = ""
  nullable    = false

  validation {
    condition     = var.ssh_public_key == "" || can(regex("^ssh-(rsa|ed25519|ecdsa) ", var.ssh_public_key))
    error_message = "ssh_public_key must be empty or an OpenSSH public key starting with 'ssh-rsa', 'ssh-ed25519', or 'ssh-ecdsa'."
  }
}

variable "allowed_ssh_source_cidr" {
  type        = string
  description = "Source CIDR allowed to reach SSH (port 22). Leave empty to keep port 22 closed (the default). Set to your IP (e.g. 1.2.3.4/32) to lock inbound SSH down, or '*' to allow any."
  default     = ""
  nullable    = false
}

variable "image" {
  type = object({
    publisher = string
    offer     = string
    sku       = string
    version   = string
  })
  description = "Marketplace image reference for the Linux VM (defaults to Ubuntu 22.04 LTS Gen2)."
  default = {
    publisher = "Canonical"
    offer     = "0001-com-ubuntu-server-jammy"
    sku       = "22_04-lts-gen2"
    version   = "latest"
  }
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to all resources created by this template."
  default     = {}
}
