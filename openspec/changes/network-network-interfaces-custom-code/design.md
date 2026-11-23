# Design

## Context

The Network extension is being built in the codegen output folder before it moves to `src/Extensions/Network` (migration skill step 12). The generated atomic processors for NetworkInterfaces are `NewNetworkInterfaceProcessor`, `GetNetworkInterfaceProcessor`, `ListNetworkInterfaceProcessor`, `ListAllNetworkInterfaceProcessor`, and `DeleteNetworkInterfaceProcessor`. They are read-only. `NewNetworkInterfaceProcessor` exposes `IpConfigurations` (a list of `NetworkInterfaceIPConfiguration`, whose flattened `SubnetId`, `PrivateIPAddress`, `PrivateIPAllocationMethod`, `PrivateIPAddressVersion`, and `PublicIPAddress` are settable), `NetworkSecurityGroupId`, `ExtendedLocationName`, `ExtendedLocationType`, plus `SubscriptionId` and `ResourceGroupName`. There is no `ExtensionIndex.cs` and no PowerShell module yet. No behavior in this change needs a non-Network REST operation. See proposal.md for motivation and the spec for required behavior.

## Goals / Non-Goals

**Goals:**
- Build the single IP configuration from flat inputs (F1, F8, F9), expand CLI names into IDs (F3), and map the edge zone (F4) as handwritten customization on top of the create processor.
- Add list routing (F17) on top of the list processors.
- Add PowerShell remove confirmation and `-PassThru` (F22) in the PowerShell wrapper only.

**Non-Goals:**
- No change to generated models or atomic processors, no new endpoints, no api-version or URL in customization.
- No `$expand`, long-running-operation, or paging logic: the generated processors and processor base own them.
- No subnet, public IP, NSG, or resource-group requests: names are expanded locally.
- No `primary` flag and no default `privateIPAddressVersion` on the IP configuration.
- No compile in this change while the code lives in the output folder (the migration skill compiles at step 13).

## Decisions

- **Create customization inherits the create processor and only shapes the body.** A `Customization` class for NetworkInterfaces create inherits `NewNetworkInterfaceProcessor` (its interface covers every parent input) and adds flat inputs: subnet ID, public IP address ID, private IP address, private IP address version, IP configuration name, edge zone, and the CLI virtual network name. Before the inherited PUT it builds `IpConfigurations` with exactly one `NetworkInterfaceIPConfiguration`, using the smallest request-construction entry the private-preview guide allows. Alternative: a full `ProcessAsync()` override; rejected because one PUT is enough.
- **IP configuration name has one input with a default.** The IP configuration name input defaults to `ipconfig1` (F1). Only the PowerShell surface binds `-IpConfigurationName` to it at step 14 (F9); the CLI does not expose it, so CLI requests always use `ipconfig1`.
- **Allocation method follows the private IP address.** A non-empty private IP address sets `PrivateIPAddress` and `PrivateIPAllocationMethod = Static`; otherwise `PrivateIPAllocationMethod = Dynamic` and no `PrivateIPAddress` is set (F1). `PublicIPAddress` is set to an object carrying only the ID when a public IP address ID is given, and left unset otherwise.
- **Private IP address version is sent only when given.** The version input sets `PrivateIPAddressVersion` on the IP configuration when it has a value (F8); only the CLI binds `--private-ip-address-version` at step 14.
- **CLI name-or-ID expansion is a customization helper driven by CLI-only inputs.** A private helper builds `Microsoft.Network` resource IDs from `SubscriptionId` and `ResourceGroupName`: a subnet value starting with `/` is kept; a subnet name with the virtual network name becomes `/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{name}`; a public IP address or NSG name becomes `.../publicIPAddresses/{name}` or `.../networkSecurityGroups/{name}` (F3). The virtual network name input is bound only by the CLI. PowerShell passes IDs (`-SubnetId`, `-PublicIpAddressId`, `-NetworkSecurityGroupId`), which start with `/` and so pass through unchanged. No GET is sent for any reference.
- **Edge zone is a customization input.** The edge-zone input sets `ExtendedLocationName` and `ExtendedLocationType = EdgeZone`; with no edge zone neither is set, so no `extendedLocation` is sent (F4).
- **List customization routes between both list processors.** A `Customization` list class inherits `ListNetworkInterfaceProcessor` (the resource group becomes optional), keeps the inherited route when a resource group is given, and composes `ListAllNetworkInterfaceProcessor` with `CreateExtendedProcessor<T>` when it is not, following `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs`. Paging stays in the processor base (F17).
- **F22 lives in the PowerShell wrapper.** Confirmation (`ShouldProcess`/`-Force`) and `-PassThru` are PowerShell-only, so they go into the `Remove-AzNetworkInterface` wrapper under `src/PS/Network/Cmdlets`, following existing wrappers such as `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. Putting them in the shared customization would leak PowerShell behavior into the CLI.

## Risks / Trade-offs

- [The PowerShell module does not exist until migration step 14] → the F9 binding and the F22 task are carried out when that module is created; they are marked so in tasks.md.
- [CLI `--subnet` name without `--vnet-name`] → this change states no behavior for it; the helper must not invent an ID for it. A dedicated error needs a separate decision.
- [No compile before step 13] → each task is verified by review against the spec scenarios; compile errors surface at step 13.
