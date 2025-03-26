# Azure PowerShell

This library is the adapter for the commands of services run in powershell framework. It should combined with a main powershell module named (Azure.Core)

## TODO list

- [] expose commands to install/uninstall/upgrade a service. Those commands are relied by (Azure.Core)
- [] generate .Net code of a wrapper powershell module for a service. And the (Azure.Core) will compile and register that module in powershell framework.
- [] support wrapper modules to call related services.
