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
const rest = require('./cases/rest-invalid-method');
const config = require('./cases/config-get-unknown-key');
const extension = require('./cases/extension-show-not-found');
const baseline = [
  group,
  vnet,
  identity,
  application,
  config,
  extension,
  rest,
];
const { commandSetAssertion } = require('./assertions/command-set');
const { planContractAssertion } = require('./assertions/plan-contract');
const { identityPlanAssertion } = require('./assertions/identity-plan');
const assertionRunners = {
  'file://assertions/command-set.js:commandSetAssertion': commandSetAssertion,
  'file://assertions/plan-contract.js:planContractAssertion': planContractAssertion,
  'file://assertions/identity-plan.js:identityPlanAssertion': identityPlanAssertion,
};

const subscription = '11111111-1111-4111-8111-111111111111';
const groupFor = (scenario) => scenario.assert[0].config.expectedCommandSet[0].argumentValues['--resource-group'];
const vmCommand = `az vm create --name test-vm --resource-group ${groupFor(vm)} --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password <admin-password> --network-interface-name test-vm-nic --virtual-network-name test-vm-vnet --network-security-group-name test-vm-nsg`;
const deploymentCommand = `az deployment group create --resource-group ${groupFor(deployment)} --name azclips-eval-web --template-file fixtures/web-app.bicep`;
const groupCommand = 'az group create --name azclips-eval-rg --location eastus';
const vnetCommand = `az network vnet create --name azclips-eval-vnet --resource-group ${groupFor(vnet)} --address-prefixes 10.42.0.0/16 --dns-servers 10.42.0.4 --location eastus`;
const identityCommand = 'az identity create --name azclips-eval-id --location eastus --resource-group identity-eval-rg';
const applicationCommand = 'az msgraph app create --sign-in-audience AzureADMyOrg --display-name azclips-eval-app';
const restCommand = `az rest --method GET --uri https://management.azure.com/subscriptions/${process.env.AZCLIPS_EVALUATION_SUBSCRIPTION_ID || subscription}/resourcegroups?api-version=2021-04-01`;
const vmNeeds = [{ flag: '--admin-password', description: 'Supply the VM password' }];

function grade(scenario, commands, { placeholders = [], vars = {} } = {}) {
  const plan = { steps: commands.map((cmd, index) => ({ id: index + 1, label: 'Next step', cmd })), placeholders };
  return scenario.assert.filter((assertion) => assertion.type === 'javascript').map((assertion) => {
    assert.equal(assertion.type, 'javascript');
    const run = assertionRunners[assertion.value];
    assert.equal(typeof run, 'function', `Unsupported assertion: ${assertion.value}`);
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
    vmCommand.replace('--vm-size Standard_D2s_v5', '--size Standard_D2s_v5'),
    `${vmCommand} --size Standard_D2s_v5`,
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

test('the suite registers an explicit semantic judge only for the two conditional-advice cases', (context) => {
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
    AZURE_OPENAI_ENDPOINT: 'https://test-judge.openai.azure.com',
    AZURE_OPENAI_DEPLOYMENT: 'test-judge',
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
  assert.deepEqual(config.tests.map((scenario) => scenario.vars), [profile, vm, deployment, ...baseline].map((scenario) => scenario.vars));
  assert.deepEqual(config.tests.filter((scenario) => scenario.options?.provider).map((scenario) => scenario.vars.case_id),
    ['config-get-unknown-key', 'extension-show-not-found']);
  for (const scenario of config.tests.filter((candidate) => candidate.options?.provider)) {
    assert.equal(scenario.options.provider.id, 'file://judge/judge-azure.js');
    assert.equal(scenario.options.provider.config.endpoint, 'https://test-judge.openai.azure.com');
    assert.equal(scenario.options.provider.config.deployment, 'test-judge');
    assert.equal(scenario.assert.filter((assertion) => assertion.type === 'llm-rubric').length, 1);
  }
  assert.equal(config.defaultTest.options?.provider, undefined);
  assert.equal(config.defaultTest.vars.subscription_id, subscription);
  assert.equal(config.evaluateOptions.maxConcurrency, 1);
  assert.equal(config.providers[0].config.timeoutMs, 300000);
});

test('baseline cases keep deterministic checks and reject removal without rejecting optional set or add advice', () => {
  assert.equal(new Set([profile, vm, deployment, ...baseline].map((s) => s.vars.case_id)).size, 3 + baseline.length);
  for (const scenario of baseline) {
    assert.equal(scenario.assert.filter((a) => a.type === 'javascript').length, 2);
    const allowed = scenario.assert[1].config.allowedCommands;
    assert.ok(allowed.every((c) => !/ (delete|remove)$/.test(c)));
  }
});

test('structural checks accept the optional configuration and extension advice from the recorded responses', () => {
  for (const [scenario, commands] of [
    [config, [
      'az config list --all',
      'az config get --key output.format',
      'az config set --key agent.nonexistent --value myvalue --force',
      'az config get --key agent.nonexistent',
    ]],
    [extension, [
      'az extension list',
      'az extension show --name <installed-extension-name>',
      'az extension add --name <extension-name>',
      'az extension show --name <extension-name>',
    ]],
  ]) {
    assert.ok(grade(scenario, commands).every((result) => result.pass));
    assert.equal(scenario.assert.at(-1).type, 'llm-rubric', 'Semantic correctness is a separate judge decision.');
  }
});

test('conditional-advice cases still require discovery and reject unrelated operations', () => {
  for (const [scenario, commands] of [
    [config, ['az config set --key agent.nonexistent --value myvalue --force']],
    [config, ['az config list', 'az config delete --key agent.enabled']],
    [extension, ['az extension add --name guessed-package']],
    [extension, ['az extension list', 'az extension remove --name Accounts']],
  ]) {
    assert.ok(grade(scenario, commands).some((result) => !result.pass));
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

test('identity recovery accepts a matching resource-group prerequisite, including a different group location', () => {
  for (const location of ['eastus', 'westeurope']) {
    const commands = [
      `az group create --name identity-eval-rg --location ${location} --subscription-id ${subscription}`,
      `${identityCommand} --subscription ${subscription}`,
    ];
    assert.ok(grade(identity, commands).every((result) => result.pass), commands.join('; '));
  }
  assert.ok(grade(identity, [
    'az group list',
    identityCommand,
  ]).every((result) => result.pass));
});

test('identity prerequisites must be complete, used by the identity, and in the intended subscription', () => {
  const prerequisite = 'az group create --name identity-eval-rg --location eastus';
  for (const commands of [
    [prerequisite.replace('--location eastus', ''), identityCommand],
    [prerequisite.replace('--location eastus', '--location'), identityCommand],
    [prerequisite.replace('--name identity-eval-rg', ''), identityCommand],
    [prerequisite.replace('identity-eval-rg', 'unrelated-rg'), identityCommand],
    [identityCommand, prerequisite],
    [`${prerequisite} --subscription-id another-subscription`, identityCommand],
    [prerequisite, `${identityCommand} --subscription another-subscription`],
    [prerequisite, `${identityCommand} --subscription`],
    [prerequisite, identityCommand.replace('--name azclips-eval-id', '--name another-id')],
    [prerequisite, identityCommand.replace('--location eastus', '--location westus')],
    [prerequisite, identityCommand, 'az group delete --name another-rg'],
  ]) {
    assert.ok(grade(identity, commands).some((result) => !result.pass), commands.join('; '));
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

test('REST accepts explicit case-insensitive GET or the supported GET default while preserving the URI', () => {
  assert.match(rest.vars.command, /^rest --method fetch --uri https:\/\/management\.azure\.com\/subscriptions\//);
  for (const command of [
    restCommand,
    restCommand.replace('--method GET', '--method get'),
    restCommand.replace('--method GET', '--method Get'),
    restCommand.replace('--method GET ', ''),
  ]) {
    assert.ok(grade(rest, [command]).every((result) => result.pass), command);
  }
});

test('REST rejects the failing stimulus, changed URI, legacy URL flag, and non-GET operations', () => {
  for (const commands of [
    [`az ${rest.vars.command}`],
    [restCommand.replace('--uri ', '--url ')],
    [restCommand.replace('api-version=2021-04-01', 'api-version=2022-09-01')],
    [restCommand.replace('/resourcegroups?', '/providers?')],
    [restCommand.replace('--method GET', '--method')],
    [restCommand.replace('--method GET', '--method DELETE')],
    [restCommand, restCommand.replace('--method GET', '--method PUT')],
    [restCommand, groupCommand],
  ]) {
    assert.ok(grade(rest, commands).some((result) => !result.pass), commands.join('; '));
  }
});
