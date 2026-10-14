const { recoveryQualityAssertion } = require('../judge/recovery-quality');

module.exports = {
  description: 'config get with an unknown key -> provide a usable configuration read path',
  vars: {
    case_id: 'config-get-unknown-key',
    command_group: 'config',
    goal: 'Explain the missing key and show how to read a valid setting, discover available keys, or conditionally create and read the requested custom setting.',
    command: `config get --key agent.nonexistent`,
  },
  assert: [
    {
      type: 'javascript',
      value: 'file://assertions/configuration-read.js:configurationReadAssertion',
      metric: 'command-set',
      config: {
        checkName: 'Usable configuration read path',
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        checkName: 'Configuration recovery scope',
        allowedCommands: ['az config info', 'az config list', 'az config get', 'az config set'],
        validateSubscription: true,
      },
    },
    recoveryQualityAssertion('configuration'),
  ],
};
