const { argumentMap, commandName, commandSetAssertion, shellTokens } = require('./command-set');
const { planContractAssertion } = require('./plan-contract');

const contracts = {
  'az config info': { requiredArguments: [], allowedArguments: ['--keys-only'] },
  'az config list': { requiredArguments: [], allowedArguments: ['--all', '--json'] },
  'az config get': { requiredArguments: ['--key'], allowedArguments: ['--key', '--json'] },
  'az config set': { requiredArguments: ['--key', '--value'], allowedArguments: ['--key', '--value', '--force'] },
};

function configurationReadAssertion(output, context) {
  const scope = planContractAssertion(output, {
    ...context,
    config: { allowedCommands: Object.keys(contracts) },
  });
  if (!scope.pass) return scope;
  let missingKey;
  try {
    missingKey = argumentMap(shellTokens(context.vars?.command || '')).get('--key');
  } catch (error) {
    return { pass: false, score: 0, reason: `The original config command is invalid: ${error.message}` };
  }
  if (typeof missingKey !== 'string' || !missingKey.trim()) {
    return { pass: false, score: 0, reason: 'The recorded missing configuration key is required.' };
  }
  const failures = [];
  let createsMissingKey = false;
  let hasReadPath = false;
  for (const [index, step] of context.metadata.plan.steps.entries()) {
    const tokens = shellTokens(step.cmd);
    const name = commandName(tokens);
    const args = argumentMap(tokens);
    const contract = contracts[name];
    const stepContext = { ...context, metadata: { plan: { steps: [step] } } };
    const parameters = commandSetAssertion(output, {
      ...stepContext,
      config: { expectedCommandSet: [{ command: name, match: 'all', requiredArguments: contract.requiredArguments }] },
    });
    const flags = planContractAssertion(output, {
      ...stepContext,
      config: {
        allowedCommands: [name],
        allowedArguments: contract.allowedArguments,
        booleanArguments: ['--all', '--json', '--force', '--keys-only'],
      },
    });
    if (!parameters.pass || !flags.pass) {
      failures.push(`step ${index + 1}: ${!parameters.pass ? parameters.reason : flags.reason}`);
      continue;
    }
    if (name === 'az config list' || name === 'az config info') hasReadPath = true;
    if (name === 'az config set' && args.get('--key') === missingKey) {
      const force = args.get('--force');
      if (force !== true && String(force).toLowerCase() !== 'true') {
        failures.push(`step ${index + 1}: creating the missing custom key requires --force`);
      } else {
        createsMissingKey = true;
      }
    }
    if (name === 'az config get') {
      if (args.get('--key') === missingKey && !createsMissingKey) {
        failures.push(`step ${index + 1}: do not retry the missing key before creating it`);
      } else {
        hasReadPath = true;
      }
    }
  }
  if (!hasReadPath) failures.push('Include configuration discovery or a usable configuration read, not only a write.');
  return {
    pass: failures.length === 0,
    score: failures.length === 0 ? 1 : 0,
    reason: failures.length ? failures.join('; ') :
      'Includes discovery or a structurally valid configuration read; the LLM checks that any suggested key or conditional creation is relevant.',
  };
}

module.exports = { configurationReadAssertion };
