const subscriptionId = process.env.AZCLIPS_EVALUATION_SUBSCRIPTION_ID || '11111111-1111-4111-8111-111111111111';
const resourceUri = `https://management.azure.com/subscriptions/${subscriptionId}/resourcegroups?api-version=2021-04-01`;

module.exports = {
  description: 'rest with an invalid HTTP method -> corrected to get',
  vars: {
    case_id: 'rest-invalid-method',
    command_group: 'rest',
    goal: 'fetch is not a supported HTTP method; use GET or its supported default and preserve the resource-group URI in the active subscription.',
    command: `rest --method fetch --uri ${resourceUri}`,
  },
  assert: [
    {
      type: 'javascript',
      value: 'file://assertions/command-set.js:commandSetAssertion',
      metric: 'command-set',
      config: {
        checkName: 'Preserve the requested URI',
        source: 'metadata',
        path: 'plan.steps',
        expectedCommandSet: [
          {
            command: 'az rest',
            match: 'all',
            requiredArguments: ['--uri'],
            argumentValues: { '--uri': resourceUri },
          },
        ],
      },
    },
    {
      type: 'javascript',
      value: 'file://assertions/plan-contract.js:planContractAssertion',
      metric: 'plan-contract',
      config: {
        checkName: 'Read-only HTTP request',
        allowedCommands: ['az rest'],
        allowedArguments: ['--method', '--uri'],
        optionalArgumentValues: { '--method': 'GET' },
        validateSubscription: true,
      },
    },
  ],
};
