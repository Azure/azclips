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
};

module.exports = {
  description: 'azclips agent plan eval (PoC)',
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
    options: {
      provider: {
        id: 'file://judge/judge-azure.js',
        config: {
          endpoint: 'https://cli-copilot.openai.azure.com',
          deployment: 'gpt-5.4-mini',
          apiVersion: '2024-12-01-preview',
        },
      },
    },
  },
  tests: [profileShow, vmCreateShortFlags, deployWebApp],
};