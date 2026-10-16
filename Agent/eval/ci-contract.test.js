const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const { parse } = require('yaml');

const workflowPath = path.resolve(__dirname, '..', '..', '.github', 'workflows', 'build-and-test.yml');
const workflow = parse(fs.readFileSync(workflowPath, 'utf8'));
const job = workflow.jobs['eval-harness-tests'];

test('eval harness tests run as a Windows job without secrets or Azure sign-in', () => {
  assert.ok(job, 'build-and-test.yml must define the eval-harness-tests job');
  assert.equal(job['runs-on'], 'windows-latest');
  assert.equal(job.permissions, undefined);
  assert.equal(job.env, undefined);
  assert.doesNotMatch(JSON.stringify(job), /secrets\.|azure\/login|az login|id-token|AZURE_/i);
});

test('eval harness tests install locked dependencies and run only the offline suite', () => {
  assert.deepEqual(job.steps.map((step) => step.uses || step.run.trim()), [
    'actions/checkout@v4',
    'actions/setup-node@v4',
    'npm ci --legacy-peer-deps',
    'npm test',
  ]);
  assert.equal(job.steps[0].with['persist-credentials'], false);
  assert.equal(job.steps[1].with['node-version'], '24.x');
  for (const step of job.steps.filter((step) => step.run)) {
    assert.equal(step['working-directory'], 'Agent/eval');
  }
});