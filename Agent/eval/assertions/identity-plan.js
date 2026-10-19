const { resourceGroupPlanAssertion } = require('./resource-group-plan');

function identityPlanAssertion(output, context) {
  return resourceGroupPlanAssertion(output, {
    ...context,
    config: { ...context.config, resourceCommand: 'az identity create' },
  });
}

module.exports = { identityPlanAssertion };
