const assert = require('node:assert/strict');
const test = require('node:test');
const path = require('node:path');
const { conditionalAdviceAssertion, judgeInput } = require('./conditional-advice');

const context = {
  vars: { command: 'config get --key agent.nonexistent', signed_in: true, subscription_id: 'test-subscription' },
  metadata: {
    originalError: { message: "Configuration key 'agent.nonexistent' was not found." },
    plan: {
      steps: [
        { cmd: 'az config list --all', label: 'List available configuration keys' },
        { cmd: 'az config set --key agent.nonexistent --value myvalue --force', label: 'If you really want this custom key, create it first' },
      ],
    },
    toolTraceAvailable: true,
    toolCalls: [{ name: 'get_command', arguments: { name: 'config set' }, result: { cliName: 'config set' } }],
  },
};

test('semantic grading sees the recorded failure, conditional labels, full response, and tool evidence', () => {
  const response = 'Use config list. If you intended a custom key, you can create it with config set.';
  const before = structuredClone(context);
  const input = JSON.parse(judgeInput(response, context));
  assert.equal(input.submittedCommand, context.vars.command);
  assert.deepEqual(input.originalError, context.metadata.originalError);
  assert.equal(input.response, response);
  assert.deepEqual(input.plan, context.metadata.plan);
  assert.deepEqual(input.context.toolCalls, context.metadata.toolCalls);
  assert.equal(input.context.subscriptionId, 'test-subscription');
  assert.deepEqual(context, before);
});

test('a missing error, response, plan or failed analysis cannot silently receive semantic grading', () => {
  assert.throws(() => judgeInput('', context), /requires the recorded/);
  assert.throws(() => judgeInput('answer', { ...context, vars: {} }), /requires the recorded/);
  assert.throws(() => judgeInput('answer', { ...context, metadata: { plan: context.metadata.plan } }), /requires the recorded/);
  assert.throws(() => judgeInput('answer', { ...context, metadata: { ...context.metadata, plan: { steps: [] } } }), /requires the recorded/);
  assert.throws(() => judgeInput('answer', {
    ...context,
    metadata: { ...context.metadata, processFailure: { reason: 'timeout' } },
  }), /incomplete analysis/);
});

test('rubrics distinguish optional relevant advice from mandatory unrelated changes, not write-command keywords', () => {
  for (const kind of ['configuration', 'extension']) {
    const assertion = conditionalAdviceAssertion(kind);
    assert.equal(assertion.type, 'llm-rubric');
    assert.equal(assertion.config.checkName, 'Contextual recovery advice');
    assert.match(assertion.value, /conditional alternative is not a mandatory step/);
    assert.match(assertion.value, /FAIL.*unrelated\/destructive changes mandatory/);
    assert.match(assertion.value, /untrusted evidence/);
    assert.doesNotMatch(assertion.value, /must be read-only|always reject.*set/i);
  }
  assert.match(conditionalAdviceAssertion('configuration').value, /optional instruction.*config set.*legitimate/);
  assert.match(conditionalAdviceAssertion('extension').value, /optional instruction.*extension add.*legitimate/);
  assert.throws(() => conditionalAdviceAssertion('unsupported'), /Unknown conditional-advice rubric/);
});

test('the pinned Promptfoo runner calls the judge with evidence and preserves its pass/fail decision and rubric', async (t) => {
  const keys = ['PROMPTFOO_DISABLE_TELEMETRY', 'PROMPTFOO_DISABLE_UPDATE'];
  const before = Object.fromEntries(keys.map((key) => [key, process.env[key]]));
  for (const key of keys) process.env[key] = 'true';
  t.after(() => {
    for (const [key, value] of Object.entries(before)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  });
  const { evaluate } = await import('promptfoo');
  const prompts = [];
  const mockGrades = [
    { pass: true, score: 1, reason: 'Mock: conditional relevant advice.' },
    { pass: false, score: 0, reason: 'Mock: mandatory unrelated change.' },
  ];
  const judge = {
    id: () => 'offline-mock-advice-judge',
    callApi: async (prompt) => {
      prompts.push(prompt);
      return {
        output: JSON.stringify(mockGrades[prompts.length - 1]),
        tokenUsage: { prompt: 12, completion: 4, total: 16, numRequests: 1 },
      };
    },
  };
  const response = 'If this custom key is intended, creating it is an optional alternative.';
  const provider = {
    id: () => 'offline-mock-recorded-response',
    callApi: async () => ({ output: response, metadata: structuredClone(context.metadata) }),
  };
  const assertion = conditionalAdviceAssertion('configuration');
  assertion.transform = `file://${path.join(__dirname, 'conditional-advice.js')}:judgeInput`;
  const run = await evaluate({
    prompts: ['Offline integration test'],
    providers: [provider],
    writeLatestResults: false,
    tests: mockGrades.map((_grade, index) => ({
      description: `Mock judge result ${index + 1}`,
      vars: context.vars,
      options: { provider: judge },
      assert: [assertion],
    })),
  }, { cache: false, maxConcurrency: 1, showProgressBar: false });
  const summary = await run.toEvaluateSummary();
  assert.equal(prompts.length, 2);
  for (const prompt of prompts) {
    assert.ok(prompt.includes(context.metadata.originalError.message));
    assert.ok(prompt.includes('If you really want this custom key, create it first'));
    assert.ok(prompt.includes('get_command'));
  }
  for (const [index, result] of summary.results.entries()) {
    assert.equal(result.response.output, response);
    assert.equal(result.gradingResult.pass, mockGrades[index].pass);
    const graded = result.gradingResult.componentResults[0];
    assert.equal(graded.reason, mockGrades[index].reason);
    assert.equal(graded.assertion.type, 'llm-rubric');
    assert.equal(graded.assertion.value, assertion.value);
    assert.equal(graded.tokensUsed.total, 16);
  }
});
