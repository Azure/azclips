const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const profile = require('./cases/profile-show');
const vm = require('./cases/vm-create-short-flags');
const deployment = require('./cases/deploy-web-app');
const group = require('./cases/group-create-missing-location');
const vnet = require('./cases/vnet-create-missing-location');
const identity = require('./cases/identity-create-missing-rg');
const application = require('./cases/msgraph-app-create-missing-display-name');
const baseline = [
  group,
  vnet,
  identity,
  application,
  require('./cases/config-get-unknown-key'),
  require('./cases/extension-show-not-found'),
  require('./cases/rest-invalid-method'),
];
const { commandSetAssertion } = require('./assertions/command-set');
const { planContractAssertion } = require('./assertions/plan-contract');

const subscription = '11111111-1111-4111-8111-111111111111';
const groupFor = (scenario) => scenario.assert[0].config.expectedCommandSet[0].argumentValues['--resource-group'];
const vmCommand = `az vm create --name test-vm --resource-group ${groupFor(vm)} --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password <admin-password> --network-interface-name test-vm-nic --virtual-network-name test-vm-vnet --network-security-group-name test-vm-nsg`;
const deploymentCommand = `az deployment group create --resource-group ${groupFor(deployment)} --name azclips-eval-web --template-file fixtures/web-app.bicep`;
const groupCommand = 'az group create --name azclips-eval-rg --location eastus';
const vnetCommand = `az network vnet create --name azclips-eval-vnet --resource-group ${groupFor(vnet)} --address-prefixes 10.42.0.0/16 --dns-servers 10.42.0.4 --location eastus`;
const identityCommand = 'az identity create --name azclips-eval-id --location eastus --resource-group identity-eval-rg';
const applicationCommand = 'az msgraph app create --sign-in-audience AzureADMyOrg --display-name azclips-eval-app';
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

test('the reference suite registers the reference and baseline cases and no default LLM judge', (context) => {
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
  assert.deepEqual(config.tests, [profile, vm, deployment, ...baseline]);
  assert.equal(config.defaultTest.options?.provider, undefined);
  assert.equal(config.defaultTest.vars.subscription_id, subscription);
});

test('baseline cases have unique ids, deterministic assertions, and no destructive operations', () => {
  assert.equal(new Set([profile, vm, deployment, ...baseline].map((s) => s.vars.case_id)).size, 3 + baseline.length);
  for (const scenario of baseline) {
    assert.ok(scenario.assert.every((a) => a.type === 'javascript'));
    const allowed = scenario.assert[1].config.allowedCommands;
    assert.ok(allowed.every((c) => !/ (delete|remove|set|add)$/.test(c)));
  }
});

test('the suite prefers create cases where available and removes redundant discovery cases', () => {
  const scenarios = [profile, vm, deployment, ...baseline];
  assert.deepEqual(scenarios.map((scenario) => scenario.vars.case_id), [
    'profile-show-active',
    'vm-create-long-flags',
    'deployment-template-recovery',
    'group-create-missing-location',
    'vnet-create-missing-location',
    'identity-create-missing-rg',
    'msgraph-app-create-missing-display-name',
    'config-get-unknown-key',
    'extension-show-not-found',
    'rest-invalid-method',
  ]);
  for (const [scenario, command] of [
    [vm, 'az vm create'],
    [deployment, 'az deployment group create'],
    [group, 'az group create'],
    [vnet, 'az network vnet create'],
    [identity, 'az identity create'],
    [application, 'az msgraph app create'],
  ]) {
    assert.equal(scenario.assert[0].config.expectedCommandSet[0].command, command);
  }
});

test('missing-parameter create cases reject their unchanged failing inputs', () => {
  for (const scenario of [group, vnet, identity, application]) {
    assert.ok(grade(scenario, [`az ${scenario.vars.command}`]).some((result) => !result.pass),
      scenario.vars.case_id);
  }
});

test('group create accepts different supplied locations while preserving the name', () => {
  for (const command of [groupCommand, groupCommand.replace('eastus', 'westeurope')]) {
    assert.ok(grade(group, [command]).every((result) => result.pass), command);
  }
  for (const command of [
    groupCommand.replace('--location eastus', '--location'),
    groupCommand.replace('azclips-eval-rg', 'another-rg'),
  ]) {
    assert.ok(grade(group, [command]).some((result) => !result.pass), command);
  }
});

test('VNet create accepts a supplied location, preserved network settings, and active subscription', () => {
  for (const command of [
    vnetCommand,
    vnetCommand.replace('--location eastus', '--location westeurope'),
    `${vnetCommand} --subscription-id ${subscription}`,
  ]) {
    assert.ok(grade(vnet, [command]).every((result) => result.pass), command);
  }
});

test('VNet create rejects missing values, altered network settings, and a different scope', () => {
  for (const command of [
    vnetCommand.replace('--location eastus', ''),
    vnetCommand.replace('--location eastus', '--location'),
    vnetCommand.replace('--location eastus', '--location ""'),
    vnetCommand.replace('--name azclips-eval-vnet', '--name another-vnet'),
    vnetCommand.replace(`--resource-group ${groupFor(vnet)}`, '--resource-group another-rg'),
    vnetCommand.replace('--address-prefixes 10.42.0.0/16', '--address-prefixes 10.43.0.0/16'),
    vnetCommand.replace('--address-prefixes 10.42.0.0/16', ''),
    vnetCommand.replace('--dns-servers 10.42.0.4', '--dns-servers 10.42.0.5'),
    vnetCommand.replace('--dns-servers 10.42.0.4', ''),
    `${vnetCommand} --subscription-id another-subscription`,
    `${vnetCommand} --unknown-setting value`,
  ]) {
    assert.ok(grade(vnet, [command]).some((result) => !result.pass), command);
  }
  assert.ok(grade(vnet, [vnetCommand], {
    vars: { signed_in: false, subscription_id: '' },
  }).some((result) => !result.pass));
});

test('VNet recovery cannot replace creation with discovery or add unrelated changes', () => {
  for (const commands of [
    [`az network vnet list --resource-group ${groupFor(vnet)}`],
    [vnetCommand, groupCommand],
    [vnetCommand, `az network vnet delete --name azclips-eval-vnet --resource-group ${groupFor(vnet)}`],
  ]) {
    assert.ok(grade(vnet, commands).some((result) => !result.pass), commands.join('; '));
  }
});

test('identity create supplies a group without changing the given name or required location', () => {
  for (const command of [
    identityCommand,
    identityCommand.replace('identity-eval-rg', 'another-existing-rg'),
  ]) {
    assert.ok(grade(identity, [command]).every((result) => result.pass), command);
  }
  for (const command of [
    identityCommand.replace('--resource-group identity-eval-rg', '--resource-group'),
    identityCommand.replace('--resource-group identity-eval-rg', '--resource-group ""'),
    identityCommand.replace('--location eastus', ''),
    identityCommand.replace('--location eastus', '--location'),
    identityCommand.replace('--location eastus', '--location westus'),
    identityCommand.replace('azclips-eval-id', 'another-identity'),
  ]) {
    assert.ok(grade(identity, [command]).some((result) => !result.pass), command);
  }
});

test('application create accepts different display names without changing single-tenant intent', () => {
  for (const command of [
    applicationCommand,
    applicationCommand.replace('azclips-eval-app', '"Evaluation application"'),
    applicationCommand.replace('azclips-eval-app', '<app-display-name>'),
  ]) {
    assert.ok(grade(application, [command]).every((result) => result.pass), command);
  }
});

test('application create rejects empty names, changed audience, unsupported flags, and unrelated operations', () => {
  for (const commands of [
    [applicationCommand.replace('--display-name azclips-eval-app', '--display-name')],
    [applicationCommand.replace('--display-name azclips-eval-app', '--display-name ""')],
    [applicationCommand.replace('--sign-in-audience AzureADMyOrg', '')],
    [applicationCommand.replace('AzureADMyOrg', 'AzureADMultipleOrgs')],
    [`${applicationCommand} --subscription-id ${subscription}`],
    ['az msgraph app list'],
    [applicationCommand, 'az msgraph app delete --id another-app'],
    [applicationCommand, groupCommand],
  ]) {
    assert.ok(grade(application, commands).some((result) => !result.pass), commands.join('; '));
  }
});

test('every create step must satisfy its contract, not only one matching step', () => {
  for (const [scenario, command] of [
    [group, groupCommand],
    [vnet, vnetCommand],
    [identity, identityCommand],
    [application, applicationCommand],
  ]) {
    assert.ok(grade(scenario, [command, `az ${scenario.vars.command}`]).some((result) => !result.pass),
      scenario.vars.case_id);
  }
});
