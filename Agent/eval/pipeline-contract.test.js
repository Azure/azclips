const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const { parse } = require('yaml');

const pipelineRoot = path.resolve(__dirname, '..', '..', '.azure-pipelines');
const readYaml = (relative) => parse(fs.readFileSync(path.join(pipelineRoot, relative), 'utf8'));

test('alpha evaluation checks use the shared named pool and no service connection', () => {
  const pipeline = readYaml('azclips-alpha-build.yml');
  const template = pipeline.stages.find((stage) =>
    stage.template === 'templates/dist/stages/agent-evaluation.yml');

  assert.deepEqual(template.parameters, {
    windows_pool: '${{ variables.windows_pool }}',
  });
  const variables = readYaml('templates/variables.yml');
  assert.ok(variables.variables.windows_pool.startsWith('pool-'));
});

test('evaluation CI runs offline tests, not Azure login or model evaluation', () => {
  const template = readYaml('templates/dist/stages/agent-evaluation.yml');
  assert.deepEqual(template.parameters.map((parameter) => parameter.name), ['windows_pool']);
  assert.equal(template.stages.length, 1);
  const stage = template.stages[0];
  assert.equal(stage.stage, 'AgentEvaluationChecks');
  assert.deepEqual(stage.dependsOn, []);
  assert.equal(stage.jobs.length, 1);
  const job = stage.jobs[0];
  assert.deepEqual(job.pool, { name: '${{ parameters.windows_pool }}' });
  assert.deepEqual(job.steps.map((step) => step.task || step.checkout || step.pwsh.trim()), [
    'self',
    'NodeTool@0',
    'npm ci --legacy-peer-deps\nexit $LASTEXITCODE',
    'npm test\nexit $LASTEXITCODE',
  ]);
  for (const step of job.steps.filter((step) => step.pwsh)) {
    assert.equal(step.workingDirectory, 'Agent/eval');
  }
});
