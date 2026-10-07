const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: 'rest with an invalid HTTP method -> corrected to get',
  vars: {
    case_id: 'rest-invalid-method',
    command_group: 'rest',
    goal: 'fetch is not a valid HTTP method; use the read-only GET method and keep the URL.',
    command: `rest --method fetch --url https://management.azure.com/subscriptions/{sub}/resourcegroups?api-version=2021-04-01`,
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
            command: 'az rest',
            match: 'any',
            requiredArguments: ['--method', '--url'],
            forbiddenArguments: ['-n', '-g'],
            argumentValues: { '--method': 'get' },
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        allowedCommands: ['az rest'],
        validateSubscription: true,
      },
    },
  ],
};
