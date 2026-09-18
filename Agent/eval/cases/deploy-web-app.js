module.exports = {
  description: 'invalid deploy command with a known web app template → plan uses deployment group create',
  vars: {
    command: 'deploy web app --resource-group azclips-eval-rg --name azclips-eval-web --template-file fixtures/web-app.bicep',
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
            command: 'az deployment group create',
            match: 'all',
            requiredArguments: [
              '--subscription-id',
              '--resource-group',
              '--name',
              '--template-file',
            ],
            pathArguments: ['--template-file'],
            argumentValues: {
              '--resource-group': 'azclips-eval-rg',
              '--name': 'azclips-eval-web',
              '--template-file': 'fixtures/web-app.bicep',
            },
          },
        ],
      },
    },
    {
      type: 'llm-rubric',
      value: [
        'Grade the error-recovery PLAN below against these anchors.',
        'PASS only if ALL hold:',
        "(1) It recognizes `deploy` isn't a real command group.",
        '(2) It explains that the supplied template should be deployed with `deployment group create`.',
        '(3) Agent-selected or derived values are surfaced under `⚠ Values to review before running:` instead of being silently injected.',
        'Command and argument completeness are checked separately by a deterministic assertion. Do not grade them here.',
        'Reply PASS or FAIL + one sentence why.',
      ].join('\n'),
    },
  ],
};