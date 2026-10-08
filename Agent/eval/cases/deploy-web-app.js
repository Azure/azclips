const { recoveryQualityAssertion } = require('../judge/recovery-quality');
const resourceGroup = process.env.AZCLIPS_EVALUATION_RESOURCE_GROUP || 'azclips-test-rg';

module.exports = {
  description: 'deployment command-group typo with a known template -> plan uses deployment group create',
  vars: {
    case_id: 'deployment-template-recovery',
    command_group: 'deployment',
    goal: 'Preserve the supplied deployment operation name and template when correcting the command group.',
    command: `deploment group create --resource-group ${resourceGroup} --name azclips-eval-web --template-file fixtures/web-app.bicep`,
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
            command: 'az deployment group create',
            match: 'all',
            requiredArguments: [
              '--resource-group',
              '--name',
              '--template-file',
            ],
            pathArguments: ['--template-file'],
            argumentValues: {
              '--resource-group': resourceGroup,
              '--name': 'azclips-eval-web',
              '--template-file': 'fixtures/web-app.bicep',
            },
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
        allowedCommands: ['az deployment group create'],
        validateSubscription: true,
        optionalArgumentValues: { '--mode': 'Incremental' },
      },
    },
    recoveryQualityAssertion('deployment'),
  ],
};