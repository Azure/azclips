module.exports = {
  description: 'msgraph app create without required --display-name -> add a name without changing the audience',
  vars: {
    case_id: 'msgraph-app-create-missing-display-name',
    command_group: 'msgraph',
    goal: 'Register a single-tenant application; supply the missing display name without broadening the sign-in audience.',
    command: 'msgraph app create --sign-in-audience AzureADMyOrg',
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
            command: 'az msgraph app create',
            match: 'all',
            requiredArguments: ['--display-name', '--sign-in-audience'],
            argumentValues: { '--sign-in-audience': 'AzureADMyOrg' },
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
        allowedCommands: ['az msgraph app create'],
        allowedArguments: ['--display-name', '--sign-in-audience'],
      },
    },
  ],
};
