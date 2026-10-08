const { argumentMap, commandName, commandSetAssertion, shellTokens } = require('./command-set');

function restReadAssertion(output, context) {
  let expected;
  try {
    expected = new URL(context.config?.expectedUri);
  } catch {
    return { pass: false, score: 0, reason: 'A valid expected resource-group URI is required.' };
  }
  if (expected.origin !== 'https://management.azure.com' ||
      !/^\/subscriptions\/[0-9a-f-]+\/resourcegroups$/i.test(expected.pathname) ||
      expected.username || expected.password || expected.hash) {
    return { pass: false, score: 0, reason: 'The REST baseline requires the ARM resource-group collection URI.' };
  }
  const required = commandSetAssertion(output, {
    ...context,
    config: { expectedCommandSet: [{ command: 'az rest', match: 'all', requiredArguments: ['--uri'] }] },
  });
  if (!required.pass) return required;
  const failures = [];
  const collectionPath = uri => uri.pathname.replace(/\/resourcegroups$/i, '/resourcegroups');
  for (const [index, step] of context.metadata.plan.steps.entries()) {
    const tokens = shellTokens(step.cmd);
    if (commandName(tokens) !== 'az rest') continue;
    const value = argumentMap(tokens).get('--uri');
    let actual;
    try {
      actual = new URL(value);
    } catch {
      failures.push(`step ${index + 1}: --uri must be an absolute URI`);
      continue;
    }
    if (actual.origin !== expected.origin || actual.username || actual.password || actual.hash ||
        collectionPath(actual) !== collectionPath(expected) || actual.search !== expected.search) {
      failures.push(`step ${index + 1}: --uri must preserve the ARM origin, subscription, collection and API version`);
    }
  }
  return {
    pass: failures.length === 0,
    score: failures.length === 0 ? 1 : 0,
    reason: failures.length ? failures.join('; ') :
      'The requested ARM resource-group URI is preserved; resourcegroups/resourceGroups are equivalent collection spellings.',
  };
}

module.exports = { restReadAssertion };
