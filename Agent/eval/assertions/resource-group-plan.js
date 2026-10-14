const { argumentMap, commandName, commandSetAssertion, shellTokens } = require('./command-set');
const { planContractAssertion } = require('./plan-contract');

function resourceGroupPlanAssertion(output, context) {
  const resourceCommand = context.config?.resourceCommand;
  if (!['az identity create', 'az vm create'].includes(resourceCommand)) {
    return { pass: false, score: 0, reason: 'Resource-group prerequisites require a supported resourceCommand' };
  }
  const resource = resourceCommand === 'az identity create' ? 'identity' : 'VM';
  const allowedCommands = Array.isArray(context.config?.allowedCommands)
    ? context.config.allowedCommands.filter(command => typeof command === 'string').join(', ')
    : '(not configured)';
  const expectation = [
    `Allowed operations: ${allowedCommands}`,
    `Any created resource group must have a nonempty name and location and be used by a later ${resource} creation step.`,
    `The resource group may use a different location from the ${resource}.`,
    ...(context.config?.validateSubscription ? ['Use the recorded signed-in subscription; explicit subscription arguments must match it.'] : []),
  ].join('\n');
  const describe = (result) => ({ ...result, expectation });
  const contract = planContractAssertion(output, context);
  if (!contract.pass) return describe(contract);

  const steps = context.metadata.plan.steps.map((step) => {
    const tokens = shellTokens(step.cmd);
    return { name: commandName(tokens), args: argumentMap(tokens) };
  });
  if (steps.some((step) => step.name === 'az group create')) {
    const groups = commandSetAssertion(output, {
      ...context,
      config: {
        source: 'metadata',
        path: 'plan.steps',
        expectedCommandSet: [{
          command: 'az group create',
          match: 'all',
          requiredArguments: ['--name', '--location'],
        }],
      },
    });
    if (!groups.pass) return describe(groups);
  }

  const failures = [];
  for (const [index, step] of steps.entries()) {
    if (step.name === 'az group create') {
      const group = step.args.get('--name');
      const usedLater = steps.slice(index + 1).some((candidate) =>
        candidate.name === resourceCommand &&
        typeof candidate.args.get('--resource-group') === 'string' &&
        candidate.args.get('--resource-group').toLowerCase() === group.toLowerCase());
      if (!usedLater) {
        failures.push(`Plan step ${index + 1}: the created resource group must be used by a later ${resource} creation step.`);
      }
    }
    if (context.config?.validateSubscription && step.name === 'az identity create' &&
        step.args.has('--subscription')) {
      const subscription = step.args.get('--subscription');
      if (typeof subscription !== 'string' ||
          subscription.toLowerCase() !== context.vars.subscription_id.toLowerCase()) {
        failures.push(`Plan step ${index + 1}: the identity subscription must match the recorded active subscription.`);
      }
    }
  }
  return {
    pass: failures.length === 0,
    score: failures.length === 0 ? 1 : 0,
    reason: failures.length ? failures.join('; ') :
      `The plan stays within ${resource} creation and its required resource group; creating that group first is allowed.`,
    expectation,
  };
}

module.exports = { resourceGroupPlanAssertion };
