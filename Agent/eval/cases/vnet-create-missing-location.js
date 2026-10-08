const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: 'network vnet create without required --location -> preserve network settings and add a location',
  vars: {
    case_id: 'vnet-create-missing-location',
    command_group: 'network',
    goal: 'Create the requested virtual network in the existing group; supply the missing location without changing its name, address space, or DNS server.',
    command: `network vnet create --name azclips-eval-vnet --resource-group ${resourceGroup} --address-prefixes 10.42.0.0/16 --dns-servers 10.42.0.4`,
  },
  assert: [
    {
      type: 'javascript',
      value: 'file://assertions/command-set.js:commandSetAssertion',
      metric: 'command-set',
      config: {
        checkName: 'Required commands and parameters',
        source: 'metadata',
        path: 'plan.steps',
        expectedCommandSet: [
          {
            command: 'az network vnet create',
            match: 'all',
            requiredArguments: [
              '--name',
              '--resource-group',
              '--location',
              '--address-prefixes',
              '--dns-servers',
            ],
            argumentValues: {
              '--name': 'azclips-eval-vnet',
              '--resource-group': resourceGroup,
              '--address-prefixes': '10.42.0.0/16',
              '--dns-servers': '10.42.0.4',
            },
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        checkName: 'Plan steps and scope',
        allowedCommands: ['az network vnet create'],
        allowedArguments: [
          '--name',
          '--resource-group',
          '--location',
          '--address-prefixes',
          '--dns-servers',
          '--subscription-id',
        ],
        validateSubscription: true,
      },
    },
  ],
};
