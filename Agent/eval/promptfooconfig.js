const groupCreateMissingLocation = require('./cases/group-create-missing-location');
const vnetCreateMissingLocation = require('./cases/vnet-create-missing-location');
const identityCreateMissingRg = require('./cases/identity-create-missing-rg');
const msgraphAppCreateMissingDisplayName = require('./cases/msgraph-app-create-missing-display-name');
const configGetUnknownKey = require('./cases/config-get-unknown-key');
const extensionShowNotFound = require('./cases/extension-show-not-found');
const restInvalidMethod = require('./cases/rest-invalid-method');
const deployWebApp = require('./cases/deploy-web-app');
const profileShow = require('./cases/profile-show');
const vmCreateShortFlags = require('./cases/vm-create-short-flags');
const { readRuntimePrompt } = require('./targets/azclips');
const { readJudgeConfiguration } = require('./judge/judge-azure');

const runtimePrompt = readRuntimePrompt();

const runContext = {
  commit: process.env.EVAL_COMMIT || 'local',
  branch: process.env.EVAL_BRANCH || 'local',
  run_at: process.env.EVAL_RUN_AT || 'local',
  flavor: process.env.AZURECLI_FLAVOR || 'azure cli',
  signed_in: process.env.AZCLIPS_EVALUATION_SIGNED_IN === 'true',
  subscription_id: process.env.AZCLIPS_EVALUATION_SUBSCRIPTION_ID || '',
};

if (!runContext.signed_in || !runContext.subscription_id) {
  throw new Error('Run the reference suite through run-evaluation.ps1 with a signed-in active profile.');
}

const judgeConfiguration = readJudgeConfiguration();
const tests = [
  profileShow,
  vmCreateShortFlags,
  deployWebApp,
  groupCreateMissingLocation,
  vnetCreateMissingLocation,
  identityCreateMissingRg,
  msgraphAppCreateMissingDisplayName,
  configGetUnknownKey,
  extensionShowNotFound,
  restInvalidMethod,
];

module.exports = {
  description: 'AZCLIPS Agent plans: deterministic contracts and contextual advice grading',
  prompts: [
    {
      raw: runtimePrompt.content,
      label: `runtime oneshot ${runtimePrompt.sha256.slice(0, 12)}`,
    },
  ],
  providers: [
    {
      id: 'file://targets/azclips.js',
      label: 'azclips-dev',
      config: {
        timeoutMs: 300000,
        recoveryMode: process.env.AZCLIPS_EVALUATION_RECOVERY_MODE || 'automatic',
      },
    },
  ],
  defaultTest: {
    vars: runContext,
  },
  evaluateOptions: {
    maxConcurrency: 1,
  },
  tests: tests.map((scenario) => scenario.assert.some((assertion) => assertion.type === 'llm-rubric')
    ? {
      ...scenario,
      options: {
        ...scenario.options,
        provider: { id: 'file://judge/judge-azure.js', config: judgeConfiguration },
      },
    }
    : scenario),
};