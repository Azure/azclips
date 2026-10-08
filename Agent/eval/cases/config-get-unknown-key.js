const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: 'config get with an unknown key -> list config',
  vars: {
    case_id: 'config-get-unknown-key',
    command_group: 'config',
    goal: 'The configuration key does not exist; list available settings read-only, do not change config.',
    command: `config get --key agent.nonexistent`,
  },
  assert: [
    {
      type: 'javascript',
      value: 'file://assertions/command-set.js:commandSetAssertion',
      metric: 'command-set',
      config: {
        checkName: 'Required discovery command',
        source: 'metadata',
        path: 'plan.steps',
        expectedCommandSet: [
          {
            command: 'az config list',
            match: 'any',
            requiredArguments: [],
            forbiddenArguments: ['-n', '-g'],
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        checkName: 'Read-only configuration discovery',
        allowedCommands: ['az config list', 'az config get'],
        validateSubscription: true,
      },
    },
  ],
};
