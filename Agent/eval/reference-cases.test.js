const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const profile = require('./cases/profile-show');
const vm = require('./cases/vm-create-short-flags');
const deployment = require('./cases/deploy-web-app');
const { commandSetAssertion } = require('./assertions/command-set');
const { planContractAssertion } = require('./assertions/plan-contract');

const subscription = '11111111-1111-4111-8111-111111111111';
const groupFor = (scenario) => scenario.assert[0].config.expectedCommandSet[0].argumentValues['--resource-group'];
const vmCommand = `az vm create --name test-vm --resource-group ${groupFor(vm)} --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password <admin-password> --network-interface-name test-vm-nic --virtual-network-name test-vm-vnet --network-security-group-name test-vm-nsg`;
const deploymentCommand = `az deployment group create --resource-group ${groupFor(deployment)} --name azclips-eval-web --template-file fixtures/web-app.bicep`;
const vmNeeds = [{ flag: '--admin-password', description: 'Supply the VM password' }];

function grade(scenario, commands, { placeholders = [], vars = {} } = {}) {
  const plan = { steps: commands.map((cmd, index) => ({ id: index + 1, label: 'Next step', cmd })), placeholders };
  return scenario.assert.map((assertion) => {
    assert.equal(assertion.type, 'javascript');
    const run = assertion.metric === 'command-set' ? commandSetAssertion : planContractAssertion;
    return run('', {
      config: assertion.config,
      metadata: { plan },
      vars: { ...scenario.vars, signed_in: true, subscription_id: subscription, ...vars },
    });
  });
}

test('the three reference cases use only deterministic assertions and stable identifiers', () => {
  assert.equal(new Set([profile, vm, deployment].map((scenario) => scenario.vars.case_id)).size, 3);
  for (const scenario of [profile, vm, deployment]) {
    assert.ok(scenario.vars.goal);
    assert.ok(scenario.vars.command_group);
    assert.ok(scenario.assert.every((assertion) => assertion.type === 'javascript'));
  }
  assert.match(profile.vars.command, /^profile show --actve$/);
  assert.match(deployment.vars.command, /^deploment group create --resource-group /);
});

test('profile reference accepts current CLI default-active and explicit selection without prose', () => {
  for (const command of [
    'az profile show',
    'az profile show --active',
    'az profile show --active true',
    'az profile show --active false',
  ]) {
    assert.ok(grade(profile, [command]).every((result) => result.pass));
  }
});

test('profile reference rejects invalid flags, named selection, and unrelated changes', () => {
  for (const commands of [
    ['az profile show --actve'],
    ['az profile show --help'],
    ['az profile show --active invalid'],
    ['az profile show --active --name another-profile'],
    ['az profile show --active', 'az profile remove --name current'],
  ]) {
    assert.ok(grade(profile, commands).some((result) => !result.pass));
  }
});

test('VM reference accepts complete long flags and a declared secret placeholder', () => {
  assert.ok(grade(vm, [vmCommand], { placeholders: vmNeeds }).every((result) => result.pass));
});

test('VM reference rejects missing values, changed user input, short flags, and invented secrets', () => {
  for (const command of [
    vmCommand.replace('--vm-size Standard_D2s_v5', '--vm-size'),
    vmCommand.replace('--name test-vm', '--name another-vm'),
    vmCommand.replace('--name test-vm', '-n test-vm'),
    vmCommand.replace('<admin-password>', 'invented-value'),
  ]) {
    assert.ok(grade(vm, [command], { placeholders: vmNeeds }).some((result) => !result.pass));
  }
  assert.ok(grade(vm, [vmCommand]).some((result) => !result.pass));
});

test('VM reference does not require the optional subscription flag when the profile supplies it', () => {
  assert.ok(!vm.assert[0].config.expectedCommandSet[0].requiredArguments.includes('--subscription-id'));
  assert.ok(grade(vm, [`${vmCommand} --subscription-id ${subscription}`], {
    placeholders: vmNeeds,
  }).every((result) => result.pass));
  assert.ok(grade(vm, [`${vmCommand} --subscription-id another-subscription`], {
    placeholders: vmNeeds,
  }).some((result) => !result.pass));
});

test('deployment reference accepts the existing profile subscription and equivalent path separators', () => {
  for (const command of [
    deploymentCommand,
    deploymentCommand.replace('fixtures/web-app.bicep', '.\\fixtures\\web-app.bicep'),
    `${deploymentCommand} --subscription-id ${subscription}`,
    `${deploymentCommand} --mode Incremental`,
  ]) {
    assert.ok(grade(deployment, [command]).every((result) => result.pass));
  }
});

test('deployment reference rejects wrong scope, changed template, missing parameters, and extra resource changes', () => {
  for (const commands of [
    [`${deploymentCommand} --subscription-id another-subscription`],
    [deploymentCommand.replace('fixtures/web-app.bicep', 'fixtures/other.bicep')],
    [deploymentCommand.replace('--name azclips-eval-web', '')],
    [deploymentCommand, `az group create --name ${groupFor(deployment)} --location eastus`],
    [`${deploymentCommand} --mode Complete`],
  ]) {
    assert.ok(grade(deployment, commands).some((result) => !result.pass));
  }
  assert.ok(grade(deployment, [deploymentCommand], {
    vars: { signed_in: false, subscription_id: '' },
  }).some((result) => !result.pass));
});

test('the reference suite registers exactly three cases and no default LLM judge', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-reference-config-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const executable = path.join(directory, 'az.exe');
  const prompts = path.join(directory, 'Extensions', 'Microsoft.Azure.Azclips.Extensions.Agent', 'prompts');
  fs.mkdirSync(prompts, { recursive: true });
  fs.writeFileSync(executable, '');
  fs.writeFileSync(path.join(prompts, 'oneshot.md'), 'Reference prompt for {{flavor}}');
  const environment = {
    AZCLIPS_EXE: executable,
    AZCLIPS_EVALUATION_SIGNED_IN: 'true',
    AZCLIPS_EVALUATION_SUBSCRIPTION_ID: subscription,
  };
  const before = Object.fromEntries(Object.keys(environment).map((key) => [key, process.env[key]]));
  context.after(() => {
    for (const [key, value] of Object.entries(before)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
    delete require.cache[require.resolve('./promptfooconfig')];
  });
  Object.assign(process.env, environment);
  const config = require('./promptfooconfig');
  assert.deepEqual(config.tests, [profile, vm, deployment]);
  assert.equal(config.defaultTest.options?.provider, undefined);
  assert.equal(config.defaultTest.vars.subscription_id, subscription);
});
