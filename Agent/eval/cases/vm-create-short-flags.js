module.exports = {
  description: "vm create with short flags -n/-g → plan must use long flags",
  vars: {
    command: 'vm create -n test-vm -g my-rg',
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
            command: 'az vm create',
            match: 'all',
            requiredArguments: [
              '--subscription-id',
              '--name',
              '--location',
              '--resource-group',
              '--vm-size',
              '--admin-username',
              '--admin-password',
              '--network-interface-name',
              '--virtual-network-name',
              '--network-security-group-name',
            ],
            forbiddenArguments: ['-n', '-g'],
            argumentValues: {
              '--name': 'test-vm',
              '--resource-group': 'my-rg',
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
        "(1) It points out -n/-g short flags aren't supported; use --name/--resource-group.",
        '(2) It clearly explains why the added values need user review.',
        '(3) Agent-selected or derived values are surfaced under `⚠ Values to review before running:` instead of being silently injected.',
        'Command and argument completeness are checked separately by a deterministic assertion. Do not grade them here.',
        'Reply PASS or FAIL + one sentence why.',
      ].join('\n'),
    },
  ],
};