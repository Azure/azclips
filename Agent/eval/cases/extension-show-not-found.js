const { recoveryQualityAssertion } = require('../judge/recovery-quality');

module.exports = {
  description: 'extension show with an unknown name -> list extensions',
  vars: {
    case_id: 'extension-show-not-found',
    command_group: 'extension',
    goal: 'Discover installed extensions and explain the missing name; optionally offer installation if the intended extension is not installed.',
    command: `extension show --name no-such-extension`,
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
        checkName: 'Extension recovery scope',
        allowedCommands: ['az extension list', 'az extension show', 'az extension add'],
        validateSubscription: true,
      },
    },
    recoveryQualityAssertion('extension'),
  ],
};
