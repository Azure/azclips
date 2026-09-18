const deployWebApp = require('./cases/deploy-web-app');
const profileShow = require('./cases/profile-show');
const vmCreateShortFlags = require('./cases/vm-create-short-flags');
const { readRuntimePrompt } = require('./targets/azclips');

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

module.exports = {
  description: 'AZCLIPS Agent reference plans: deterministic contracts',
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
    },
  ],
  defaultTest: {
    vars: runContext,
  },
  tests: [profileShow, vmCreateShortFlags, deployWebApp],
};