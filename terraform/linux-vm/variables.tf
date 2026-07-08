variable "subscription_id" {
  type        = string
  description = "Azure subscription ID to deploy into. Defaults to a fixed subscription; override to target another."
  default     = "0c302431-1ad0-4187-87c9-d4cebcf7edc2"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the EXISTING resource group to deploy into. This template only ever deploys into this group."
  nullable    = false
}

variable "vm_name" {
  type        = string
  description = "Name of the Linux virtual machine (also used as a prefix for its network resources)."
  default     = "ming-test-vm"
}

variable "user_assigned_identity_name" {
  type        = string
  description = "Name of the user-assigned managed identity to create and attach to the VM."
  default     = "user-assigned-mi"
}

variable "uami_role" {
  type        = string
  description = "RBAC role granted to the user-assigned identity at the resource-group scope."
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
  default     = "Standard_D2als_v6"
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
