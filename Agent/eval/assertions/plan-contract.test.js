const assert = require('node:assert/strict');
const test = require('node:test');
const { planContractAssertion } = require('./plan-contract');

const subscription = '11111111-1111-4111-8111-111111111111';
const evaluate = (plan, config = {}, vars = {}) => planContractAssertion('', {
  metadata: { plan },
  config: { allowedCommands: ['az deployment group create'], ...config },
  vars: { signed_in: true, subscription_id: subscription, ...vars },
});

test('accepts a concise plan without requiring explanatory prose', () => {
  assert.equal(evaluate({
    steps: [{ cmd: 'az deployment group create --name demo' }],
  }).pass, true);
});

test('rejects missing plans and unexpected actions', () => {
  for (const plan of [
    undefined,
    { steps: [] },
    { steps: [{}] },
    { steps: [{ cmd: 'az group delete --name demo' }] },
  ]) {
    assert.equal(evaluate(plan).pass, false);
  }
});

test('does not fail an otherwise allowed command merely for a conservative risk label', () => {
  assert.equal(evaluate({
    steps: [{ cmd: 'az deployment group create --name demo', destructive: true }],
  }).pass, true);
});

test('allows a subscription from the recorded active profile', () => {
  assert.equal(evaluate({
    steps: [{ cmd: 'az deployment group create --name demo' }],
  }, { validateSubscription: true }).pass, true);
});

test('accepts an explicit matching subscription but rejects a wrong or valueless one', () => {
  for (const [value, expected] of [[subscription, true], ['different-subscription', false], ['', false]]) {
    const result = evaluate({
      steps: [{ cmd: `az deployment group create --name demo --subscription-id ${value}` }],
    }, { validateSubscription: true });
    assert.equal(result.pass, expected);
  }
});

test('does not invent an active profile when neither explicit context nor defaults are available', () => {
  const plan = { steps: [{ cmd: 'az deployment group create --name demo' }] };
  assert.equal(evaluate(plan, { validateSubscription: true }, { signed_in: false }).pass, false);
  assert.equal(evaluate(plan, { validateSubscription: true }, { subscription_id: '' }).pass, false);
});

test('requires an unknown secret to be declared rather than fabricated', () => {
  const config = { allowedCommands: ['az vm create'], secretArguments: ['--admin-password'] };
  const plan = {
    steps: [{ cmd: 'az vm create --admin-password <admin-password>' }],
    placeholders: [{ flag: '--admin-password', description: 'Supply the VM password' }],
  };
  assert.equal(evaluate(plan, config).pass, true);
  assert.equal(evaluate({ ...plan, placeholders: [] }, config).pass, false);
  assert.equal(evaluate({
    ...plan,
    steps: [{ cmd: 'az vm create --admin-password invented-value' }],
  }, config).pass, false);
});

test('invalid configuration and unparseable plan commands fail explicitly', () => {
  const plan = { steps: [{ cmd: 'az deployment group create --name demo' }] };
  assert.equal(evaluate(plan, { allowedCommands: [] }).pass, false);
  assert.equal(evaluate(plan, { allowedCommands: [null] }).pass, false);
  assert.equal(evaluate(plan, { secretArguments: 'not-an-array' }).pass, false);
  assert.equal(evaluate(plan, { allowedArguments: [null] }).pass, false);
  assert.equal(evaluate(plan, { booleanArguments: ['--'] }).pass, false);
  assert.equal(evaluate(plan, { optionalArgumentValues: [] }).pass, false);
  assert.equal(evaluate(plan, { validateSubscription: 'false' }).pass, false);
  assert.equal(evaluate({ steps: [{ cmd: 'az deployment group create --name "unterminated' }] }).pass, false);
});
