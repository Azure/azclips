const { recoveryQualityAssertion } = require('../judge/recovery-quality');

module.exports = {
  description: 'config get with an unknown key -> list config',
  vars: {
    case_id: 'config-get-unknown-key',
    command_group: 'config',
    goal: 'Explain the missing key and discover available settings; optionally explain how to create the requested custom setting if that is what the user wants.',
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
        checkName: 'Configuration recovery scope',
        allowedCommands: ['az config list', 'az config get', 'az config set'],
        validateSubscription: true,
      },
    },
    recoveryQualityAssertion('configuration'),
  ],
};
