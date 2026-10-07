const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: 'extension show with an unknown name -> list extensions',
  vars: {
    case_id: 'extension-show-not-found',
    command_group: 'extension',
    goal: 'The extension is not installed; list installed extensions read-only, do not add or remove.',
    command: `extension show --name no-such-extension`,
  },
  assert: [
    {
      type: 'javascript',
      value: 'file://assertions/command-set.js:commandSetAssertion',
      metric: 'command-set',
      config: {
        source: 'metadata',
        path: 'plan.steps',
        expectedCommandSet: [
          {
            command: 'az extension list',
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
        allowedCommands: ['az extension list', 'az extension show'],
        validateSubscription: true,
      },
    },
  ],
};
