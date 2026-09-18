module.exports = {
  description: 'profile active-flag typo -> show the active profile',
  vars: {
    case_id: 'profile-show-active',
    command_group: 'profile',
    goal: 'Show the active profile without changing authentication or profiles.',
    command: 'profile show --actve',
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
            command: 'az profile show',
            match: 'all',
            booleanArguments: ['--active'],
            forbiddenArguments: ['--name', '--actve'],
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        allowedCommands: ['az profile show'],
        allowedArguments: ['--active'],
        booleanArguments: ['--active'],
      },
    },
  ],
};