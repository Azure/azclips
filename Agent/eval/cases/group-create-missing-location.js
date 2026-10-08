module.exports = {
  description: 'group create without required --location -> plan adds a location',
  vars: {
    case_id: 'group-create-missing-location',
    command_group: 'group',
    goal: 'Create the resource group; the required location is missing, so supply a valid one.',
    command: `group create --name azclips-eval-rg`,
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
            command: 'az group create',
            match: 'all',
            requiredArguments: ['--name', '--location'],
            forbiddenArguments: ['-n', '-g'],
            argumentValues: { '--name': 'azclips-eval-rg' },
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
        allowedCommands: ['az group create'],
        validateSubscription: true,
      },
    },
  ],
};
