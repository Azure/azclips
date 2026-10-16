const assert = require('node:assert/strict');
const test = require('node:test');
const { argumentMap, commandName, commandSetAssertion, shellTokens } = require('./command-set');
const profileConfig = require('../cases/profile-show').assert[0].config;

const expected = [{
  command: 'az vm create',
  requiredArguments: ['--name', '--resource-group'],
  forbiddenArguments: ['-n', '-g'],
  argumentValues: { '--resource-group': 'my-rg' },
}];

function run(metadata, config = {}) {
  return commandSetAssertion('', {
    metadata,
    config: {
      source: 'metadata',
      path: 'plan.steps',
      expectedCommandSet: expected,
      ...config,
    },
  });
}

test('passes when the matching command satisfies its own argument contract', () => {
  const result = run({
    plan: { steps: [{ cmd: 'az vm create --name vm1 --resource-group my-rg' }] },
  });
  assert.equal(result.pass, true);
});

test('fails when one of two matching commands misses a required argument', () => {
  const result = run({
    plan: {
      steps: [
        { cmd: 'az vm create --name vm1 --resource-group my-rg' },
        { cmd: 'az vm create --resource-group my-rg' },
      ],
    },
  });
  assert.equal(result.pass, false);
  assert.match(result.componentResults[0].reason, /step 2: missing --name/);
});

test('supports match any when only one matching command must satisfy the contract', () => {
  const result = run(
    {
      plan: {
        steps: [
          { cmd: 'az vm create --resource-group my-rg' },
          { cmd: 'az vm create --name vm1 --resource-group my-rg' },
        ],
      },
    },
    { expectedCommandSet: [{ ...expected[0], match: 'any' }] },
  );
  assert.equal(result.pass, true);
});

test('fails forbidden arguments and exact argument value mismatches', () => {
  const result = run({
    plan: { steps: [{ cmd: 'az vm create -n vm1 --name vm1 --resource-group other-rg' }] },
  });
  assert.equal(result.pass, false);
  assert.match(result.componentResults[0].reason, /contains forbidden -n/);
  assert.match(result.componentResults[0].reason, /--resource-group expected "my-rg"/);
});

test('normalizes separators only for explicitly declared path arguments', () => {
  const result = run(
    {
      plan: {
        steps: [{
          cmd: 'az vm create --name vm1 --resource-group my-rg --template-file Agent\\eval\\fixture.bicep',
        }],
      },
    },
    {
      expectedCommandSet: [{
        ...expected[0],
        pathArguments: ['--template-file'],
        argumentValues: {
          '--resource-group': 'my-rg',
          '--template-file': 'Agent/eval/fixture.bicep',
        },
      }],
    },
  );
  assert.equal(result.pass, true);
});

test('treats explicit relative path prefixes as equivalent', () => {
  const result = commandSetAssertion('', {
    metadata: {
      plan: {
        steps: [{ cmd: 'az deployment group create --template-file .\\fixtures\\web-app.bicep' }],
      },
    },
    config: {
      source: 'metadata',
      path: 'plan.steps',
      expectedCommandSet: [{
        command: 'az deployment group create',
        pathArguments: ['--template-file'],
        argumentValues: { '--template-file': 'fixtures/web-app.bicep' },
      }],
    },
  });

  assert.equal(result.pass, true);
});

test('keeps non-path argument value matching strict', () => {
  const result = run({
    plan: { steps: [{ cmd: 'az vm create --name vm1 --resource-group my\\rg' }] },
  });
  assert.equal(result.pass, false);
  assert.match(result.componentResults[0].reason, /--resource-group expected "my-rg"/);
});

test('fails when the expected command does not exist', () => {
  const result = run({ plan: { steps: [{ cmd: 'az group create --name rg1' }] } });
  assert.equal(result.pass, false);
  assert.match(result.componentResults[0].reason, /No command matched az vm create/);
});

test('supports JSON output and a configurable command field', () => {
  const result = commandSetAssertion(
    JSON.stringify({ commands: [{ command: 'az vm create --name vm1 --resource-group my-rg' }] }),
    {
      config: {
        source: 'json',
        path: 'commands',
        commandField: 'command',
        expectedCommandSet: expected,
      },
    },
  );
  assert.equal(result.pass, true);
});

const missingValues = [
  ['bare flag', ''],
  ['followed by another flag', ' --other present'],
  ['empty equals value', '='],
  ['empty double-quoted value', ' ""'],
  ['empty single-quoted value', " ''"],
  ['empty double-quoted equals value', '=""'],
  ['empty single-quoted equals value', "=''"],
  ['whitespace-only value', ' " \t "'],
  ['whitespace-only equals value', "=' \t '"],
];

for (const argument of ['--location', '--vm-size', '--subscription-id']) {
  for (const [description, suffix] of missingValues) {
    test(`rejects ${argument} with ${description}`, () => {
      const result = run(
        { plan: { steps: [{ cmd: `az vm create ${argument}${suffix}` }] } },
        { expectedCommandSet: [{ command: 'az vm create', requiredArguments: [argument] }] },
      );
      assert.equal(result.pass, false);
      assert.equal(result.score, 0);
      assert.match(result.componentResults[0].reason, /missing value for/);
    });
  }
}

for (const [argument, value] of [
  ['--location westus', 'westus'],
  ['--location=westus', 'westus'],
  ['--location\twestus', 'westus'],
  ['--location "west us"', 'west us'],
  ["--location 'west us'", 'west us'],
  ['--location="west us"', 'west us'],
  ["--location='west us'", 'west us'],
  ['--location=" westus "', ' westus '],
]) {
  test(`accepts a nonempty value in ${JSON.stringify(argument)}`, () => {
    const result = run(
      { plan: { steps: [{ cmd: `az vm create ${argument}` }] } },
      {
        expectedCommandSet: [{
          command: 'az vm create',
          requiredArguments: ['--location'],
          argumentValues: { '--location': value },
        }],
      },
    );
    assert.equal(result.pass, true);
  });
}

test('keeps the profile case compatible with default-active and explicit boolean selection', () => {
  assert.deepEqual(profileConfig.expectedCommandSet[0].booleanArguments, ['--active']);
  assert.equal(run({ plan: { steps: [{ cmd: 'az profile show --active' }] } }, profileConfig).pass, true);
  assert.equal(run({ plan: { steps: [{ cmd: 'az profile show' }] } }, profileConfig).pass, true);
});

test('does not infer a required boolean switch from an expected true value', () => {
  const result = run(
    { plan: { steps: [{ cmd: 'az profile show --active' }] } },
    {
      expectedCommandSet: [{
        command: 'az profile show',
        requiredArguments: ['--active'],
        argumentValues: { '--active': true },
      }],
    },
  );
  assert.equal(result.pass, false);
});

for (const [value, argument, pass] of [
  [true, '--active', true],
  [true, '--active true', true],
  [true, '--active=true', true],
  [true, '--active false', false],
  [true, '--active=false', false],
  [false, '--active', false],
  [false, '--active false', true],
  [false, '--active=false', true],
  [false, '--active true', false],
  [false, '--active=true', false],
  [true, '--active=', false],
  [false, '--active=""', false],
]) {
  test(`enforces the boolean value ${value} for ${argument}`, () => {
    const result = run(
      { plan: { steps: [{ cmd: `az profile show ${argument}` }] } },
      {
        expectedCommandSet: [{
          command: 'az profile show',
          requiredArguments: ['--active'],
          booleanArguments: ['--active'],
          argumentValues: { '--active': value },
        }],
      },
    );
    assert.equal(result.pass, pass);
  });
}

test('accepts negative numeric values without treating them as additional flags', () => {
  for (const value of ['-1', '-0.5', '-.5', '-1e3', '-1.5e-3']) {
    for (const separator of [' ', '=']) {
      const command = `az vm create --count${separator}${value} --name vm1`;
      const result = run(
        { plan: { steps: [{ cmd: command }] } },
        {
          expectedCommandSet: [{
            command: 'az vm create',
            requiredArguments: ['--count', '--name'],
            argumentValues: { '--count': value, '--name': 'vm1' },
          }],
        },
      );
      assert.equal(result.pass, true, command);
      assert.equal(argumentMap(shellTokens(command)).has(value), false, command);
    }
  }
});

test('preserves token and argument exports, quoting, and last-value-wins behavior', () => {
  const tokens = shellTokens(
    'AZ VM CREATE --name "first vm" --name=\'Sam\'\'s VM\' --path="C:\\my files\\vm.bicep" --active',
  );
  assert.deepEqual(tokens, [
    'AZ', 'VM', 'CREATE', '--name', 'first vm', "--name=Sam's VM",
    '--path=C:\\my files\\vm.bicep', '--active',
  ]);
  assert.equal(commandName(tokens), 'az vm create');
  assert.deepEqual([...argumentMap(tokens)], [
    ['--name', "Sam's VM"],
    ['--path', 'C:\\my files\\vm.bicep'],
    ['--active', true],
  ]);
});

test('preserves quoted path normalization and arbitrary option order', () => {
  for (const separator of [' ', '=']) {
    const result = run(
      {
        plan: {
          steps: [{
            cmd: `az vm create --template-file${separator}".\\Agent\\eval\\my template.bicep" --resource-group=my-rg --name vm1`,
          }],
        },
      },
      {
        expectedCommandSet: [{
          ...expected[0],
          requiredArguments: [...expected[0].requiredArguments, '--template-file'],
          pathArguments: ['--template-file'],
          argumentValues: {
            ...expected[0].argumentValues,
            '--template-file': 'Agent/eval/my template.bicep',
          },
        }],
      },
    );
    assert.equal(result.pass, true, separator);
  }
});

test('keeps quoted non-path values strict rather than normalizing them', () => {
  const result = run(
    { plan: { steps: [{ cmd: 'az vm create --name vm1 --resource-group="my\\rg"' }] } },
    { expectedCommandSet: [{ ...expected[0], argumentValues: { '--resource-group': 'my/rg' } }] },
  );
  assert.equal(result.pass, false);
});

test('preserves any/all behavior when one matching command has a valueless required flag', () => {
  const metadata = {
    plan: {
      steps: [
        { cmd: 'az vm create --name --resource-group my-rg' },
        { cmd: 'az vm create --name vm1 --resource-group my-rg' },
      ],
    },
  };
  for (const match of ['any', 'all']) {
    const result = run(metadata, { expectedCommandSet: [{ ...expected[0], match }] });
    assert.equal(result.pass, match === 'any');
    if (match === 'all') assert.match(result.componentResults[0].reason, /step 1: missing value for --name/);
  }
});

test('preserves exact case-insensitive command matching and aggregate scores', () => {
  const metadata = { plan: { steps: [{ cmd: 'AZ VM CREATE --name vm1 --resource-group my-rg' }] } };
  assert.equal(run(metadata).pass, true);
  const result = run(metadata, {
    expectedCommandSet: [...expected, { command: 'az vm create-other' }],
  });
  assert.equal(result.pass, false);
  assert.equal(result.score, 0.5);
  assert.deepEqual(result.componentResults.map((component) => component.pass), [true, false]);
});

const invalidConfigurations = [
  undefined,
  null,
  [],
  'invalid',
  {},
  { expectedCommandSet: [] },
  { expectedCommandSet: null },
  { expectedCommandSet: {} },
  { expectedCommandSet: 'az vm create' },
  ...[
    null,
    [],
    {},
    { command: '' },
    { command: '   ' },
    { command: 42 },
    { command: '--name vm1' },
    { command: 'az vm create --name' },
    { command: 'az vm "create' },
    { ...expected[0], match: 'every' },
    { ...expected[0], match: '' },
    { ...expected[0], requiredArguments: '--name' },
    { ...expected[0], requiredArguments: [null] },
    { ...expected[0], forbiddenArguments: {} },
    { ...expected[0], booleanArguments: '--name' },
    { ...expected[0], booleanArguments: [''] },
    { ...expected[0], pathArguments: null },
    { ...expected[0], argumentValues: [] },
    { ...expected[0], argumentValues: null },
    { ...expected[0], argumentValues: 'invalid' },
    { ...expected[0], argumentValues: { '--name': {} } },
    { ...expected[0], argumentValues: { '--name': undefined } },
    { ...expected[0], argumentValues: { '--name': NaN } },
    { ...expected[0], argumentValues: { '': 'vm1' } },
  ].map((entry) => ({ expectedCommandSet: [entry] })),
  { expectedCommandSet: expected, source: 'typo' },
  { expectedCommandSet: expected, path: [] },
  { expectedCommandSet: expected, commandField: {} },
];

test('fails explicitly for invalid assertion configurations instead of passing or throwing', () => {
  for (const config of invalidConfigurations) {
    const result = commandSetAssertion('', {
      metadata: { plan: { steps: [{ cmd: 'az vm create --name vm1 --resource-group my-rg' }] } },
      config,
    });
    assert.equal(result.pass, false, JSON.stringify(config));
    assert.equal(result.score, 0, JSON.stringify(config));
    assert.match(result.reason, /^Invalid assertion configuration:/, JSON.stringify(config));
  }
});

test('rejects invalid command entries and unterminated quotes even when another command matches', () => {
  for (const command of [
    undefined, null, 42, '', ' \t ', '""', '--name vm1',
    'az vm create --name "vm1 --resource-group my-rg',
    "az vm create --name vm1 --resource-group my-rg --note='unterminated",
    'az vm create --name vm1 --resource-group my-rg --note="unterminated',
  ]) {
    for (const match of ['any', 'all']) {
      const result = run(
        {
          plan: {
            steps: [
              { cmd: 'az vm create --name vm1 --resource-group my-rg' },
              { cmd: command },
            ],
          },
        },
        { expectedCommandSet: [{ ...expected[0], match }] },
      );
      assert.equal(result.pass, false, JSON.stringify(command));
      assert.equal(result.score, 0);
      assert.match(result.reason, /Unparseable command at step 2/);
    }
  }
});

test('fails explicitly for malformed JSON output and non-array or empty command sets', () => {
  const invalidJson = commandSetAssertion('{', {
    config: { source: 'json', expectedCommandSet: expected },
  });
  assert.equal(invalidJson.pass, false);
  assert.equal(invalidJson.score, 0);
  assert.match(invalidJson.reason, /Invalid JSON output/);

  for (const steps of [undefined, null, {}, []]) {
    const result = run({ plan: { steps } });
    assert.equal(result.pass, false);
    assert.equal(result.score, 0);
  }
});
