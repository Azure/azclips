module.exports = {
  description: 'identity create without required --resource-group -> plan adds one',
  vars: {
    case_id: 'identity-create-missing-rg',
    command_group: 'identity',
    goal: 'Create the managed identity in eastus; supply the missing resource group, creating it first when needed, without changing the identity name or location.',
    command: 'identity create --name azclips-eval-id --location eastus',
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
            command: 'az identity create',
            match: 'all',
            requiredArguments: ['--name', '--resource-group', '--location'],
            forbiddenArguments: ['-n', '-g'],
            argumentValues: {
              '--name': 'azclips-eval-id',
              '--location': 'eastus',
            },
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/identity-plan.js:identityPlanAssertion',
      metric: 'plan-contract',
      config: {
        checkName: 'Identity prerequisites',
        allowedCommands: ['az identity create', 'az group list', 'az group create'],
        validateSubscription: true,
      },
    },
  ],
};
