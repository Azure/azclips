module.exports = {
  description: "profile show → plan must add --active (regression from PR #556)",
  vars: {
    command: 'profile show',
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
            requiredArguments: ['--active'],
            booleanArguments: ['--active'],
          },
        ],
      },
    },
    {
      type: 'llm-rubric',
      value: [
        'Grade the error-recovery PLAN below against these anchors.',
        'PASS only if ALL hold:',
        '(1) It explains `profile show` needs a profile name or `--active`.',
        'Command and argument correctness are checked separately by a deterministic assertion. Do not grade them here.',
        'Reply PASS or FAIL + one sentence why.',
      ].join('\n'),
    },
  ],
};