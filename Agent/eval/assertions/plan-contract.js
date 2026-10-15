const { argumentMap, commandName, shellTokens } = require('./command-set');

function planContractAssertion(_output, context) {
  const config = context.config || {};
  const plan = context.metadata?.plan;
  const failures = [];
  if (!Array.isArray(config.allowedCommands) || config.allowedCommands.length === 0 ||
      config.allowedCommands.some((command) => typeof command !== 'string' || !command.trim())) {
    return { pass: false, score: 0, reason: 'Plan contract requires a nonempty allowedCommands list' };
  }
  for (const key of ['secretArguments', 'allowedArguments', 'booleanArguments']) {
    if (config[key] !== undefined &&
        (!Array.isArray(config[key]) ||
         config[key].some((flag) => typeof flag !== 'string' || !/^--[\w-]+$/.test(flag)))) {
      return { pass: false, score: 0, reason: `${key} must be an array of parameter names` };
    }
  }
  if (config.optionalArgumentValues !== undefined &&
      (!config.optionalArgumentValues || Array.isArray(config.optionalArgumentValues) ||
       typeof config.optionalArgumentValues !== 'object' ||
       Object.values(config.optionalArgumentValues).some((value) => typeof value !== 'string' || !value.trim()))) {
    return { pass: false, score: 0, reason: 'optionalArgumentValues must map parameters to nonempty strings' };
  }
  if (config.validateSubscription !== undefined && typeof config.validateSubscription !== 'boolean') {
    return { pass: false, score: 0, reason: 'validateSubscription must be a boolean' };
  }
  if (!plan || !Array.isArray(plan.steps) || plan.steps.length === 0) {
    return { pass: false, score: 0, reason: 'A structured, nonempty plan is required' };
  }
  const allowed = new Set(config.allowedCommands.map((command) => command.toLowerCase()));
  const subscription = context.vars?.subscription_id;
  const hasProfile = context.vars?.signed_in === true &&
    typeof subscription === 'string' && subscription.trim().length > 0;

  for (const [index, step] of plan.steps.entries()) {
    if (!step || typeof step.cmd !== 'string' || !step.cmd.trim()) {
      failures.push(`step ${index + 1}: missing command`);
      continue;
    }
    let tokens;
    try {
      tokens = shellTokens(step.cmd);
    } catch (error) {
      failures.push(`step ${index + 1}: ${error.message}`);
      continue;
    }
    const name = commandName(tokens);
    if (!allowed.has(name)) {
      failures.push(`step ${index + 1}: unexpected operation ${name}`);
    }
    const args = argumentMap(tokens);
    if (config.allowedArguments) {
      for (const flag of args.keys()) {
        if (!config.allowedArguments.includes(flag)) {
          failures.push(`step ${index + 1}: unexpected argument ${flag}`);
        }
      }
    }
    for (const flag of config.booleanArguments || []) {
      if (!args.has(flag)) continue;
      const value = args.get(flag);
      if (value !== true && value !== false &&
          (typeof value !== 'string' || !/^(true|false)$/i.test(value))) {
        failures.push(`step ${index + 1}: ${flag} must be a boolean switch`);
      }
    }
    for (const [flag, expected] of Object.entries(config.optionalArgumentValues || {})) {
      if (args.has(flag) &&
          (typeof args.get(flag) !== 'string' || args.get(flag).toLowerCase() !== expected.toLowerCase())) {
        failures.push(`step ${index + 1}: ${flag} must preserve ${expected} behavior`);
      }
    }
    if (config.validateSubscription) {
      if (!hasProfile) {
        failures.push('The reference case requires a signed-in active subscription');
      } else if (args.has('--subscription-id')) {
        const value = args.get('--subscription-id');
        if (typeof value !== 'string' || value.toLowerCase() !== subscription.toLowerCase()) {
          failures.push(`step ${index + 1}: subscription must match the recorded active profile`);
        }
      }
    }
    for (const flag of config.secretArguments || []) {
      if (!args.has(flag)) continue;
      const value = args.get(flag);
      const declared = Array.isArray(plan.placeholders) && plan.placeholders.some((need) =>
        need?.flag === flag && typeof need.description === 'string' && need.description.trim());
      if (typeof value !== 'string' || !/^<[^<>\s]+>$/.test(value) || !declared) {
        failures.push(`step ${index + 1}: ${flag} must be a declared user-supplied placeholder`);
      }
    }
  }
  return {
    pass: failures.length === 0,
    score: failures.length === 0 ? 1 : 0,
    reason: failures.length ? failures.join('; ') : 'Plan contract passed',
  };
}

module.exports = { planContractAssertion };
