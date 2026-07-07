output "vm_id" {
  description = "Resource ID of the Linux VM."
  value       = azurerm_linux_virtual_machine.vm.id
}

output "vm_name" {
  description = "Name of the Linux VM."
  value       = azurerm_linux_virtual_machine.vm.name
}

output "resource_group" {
  description = "Resource group the VM was deployed into."
  value       = local.resource_group_name
}

output "location" {
  description = "Azure region of the deployment."
  value       = local.location
}

output "public_ip_address" {
  description = "Public IP address of the VM."
  value       = azurerm_public_ip.pip.ip_address
}

output "ssh_command" {
  description = "Ready-to-use SSH command to connect to the VM."
  value       = "ssh ${var.admin_username}@${azurerm_public_ip.pip.ip_address}"
}

output "user_assigned_identity_client_id" {
  description = "Client ID of the user-assigned managed identity (use with `az login --identity --client-id`)."
  value       = azurerm_user_assigned_identity.uami.client_id
}

output "user_assigned_identity_resource_id" {
  description = "Resource ID of the user-assigned managed identity (use with `az login --identity --resource-id`)."
  value       = azurerm_user_assigned_identity.uami.id
}

output "user_assigned_identity_principal_id" {
  description = "Principal (object) ID of the user-assigned managed identity (use with `az login --identity --object-id`)."
  value       = azurerm_user_assigned_identity.uami.principal_id
}
