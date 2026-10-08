const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: "vm create with short flags -n/-g → plan must use long flags",
  vars: {
    case_id: 'vm-create-long-flags',
    command_group: 'vm',
    goal: 'Create the requested VM in an existing group; request the password rather than inventing it.',
    command: `vm create -n test-vm -g ${resourceGroup}`,
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
            command: 'az vm create',
            match: 'all',
            requiredArguments: [
              '--name',
              '--location',
              '--resource-group',
              '--vm-size',
              '--admin-username',
              '--admin-password',
              '--network-interface-name',
              '--virtual-network-name',
              '--network-security-group-name',
            ],
            forbiddenArguments: ['-n', '-g', '--size'],
            argumentValues: {
              '--name': 'test-vm',
              '--resource-group': resourceGroup,
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
        allowedCommands: ['az vm create'],
        validateSubscription: true,
        secretArguments: ['--admin-password'],
      },
    },
  ],
};