const assert = require('node:assert/strict');
const fs = require('node:fs');
const test = require('node:test');
const { getAzureCliToken, resolveAzureCli } = require('./local-azure-cli');

test('resolves an explicit Azure CLI command first', () => {
  assert.deepEqual(
    resolveAzureCli({ AZCLIPS_EVALUATION_AZURE_CLI: 'C:\\tools\\az.exe' }, 'win32'),
    { command: 'C:\\tools\\az.exe', prefixArgs: [] },
  );
});

test('gets the requested scope through the classic Azure CLI', () => {
  const calls = [];
  const token = getAzureCliToken(
    'api://evaluation/access_as_user',
    {
      AZCLIPS_EVALUATION_AZURE_CLI: 'C:\\tools\\az.exe',
      AZURE_TENANT_ID: 'tenant-1',
    },
    (command, args, options) => {
      calls.push({ command, args, options });
      return {
        status: 0,
        stdout: JSON.stringify({
          accessToken: 'test-token',
          expires_on: '1786700000',
        }),
        stderr: '',
      };
    },
    'win32',
  );

  assert.equal(token.token, 'test-token');
  assert.equal(token.expiresOnTimestamp, 1786700000000);
  assert.equal(calls[0].command, 'C:\\tools\\az.exe');
  assert.deepEqual(calls[0].args, [
    'account',
    'get-access-token',
    '--scope',
    'api://evaluation/access_as_user',
    '--output',
    'json',
    '--tenant',
    'tenant-1',
  ]);
  assert.equal(calls[0].options.shell, false);
  assert.equal(calls[0].options.timeout, 60_000);
});

test('bounds token acquisition and reports a stalled CLI explicitly', () => {
  const error = Object.assign(new Error('spawn timed out'), { code: 'ETIMEDOUT' });
  assert.throws(
    () => getAzureCliToken(
      'api://evaluation/access_as_user',
      { AZCLIPS_EVALUATION_AZURE_CLI: 'classic-az' },
      () => ({ error, status: null, stdout: '', stderr: '' }),
      'win32',
    ),
    (failure) => {
      assert.match(failure.message, /timed out after 60 seconds/);
      assert.equal(failure.cause, error);
      return true;
    },
  );
});

test('preserves native startup errors and CLI authentication errors', () => {
  const missing = Object.assign(new Error('CLI not found'), { code: 'ENOENT' });
  const env = { AZCLIPS_EVALUATION_AZURE_CLI: 'classic-az' };
  assert.throws(
    () => getAzureCliToken('scope', env, () => ({ error: missing }), 'linux'),
    (failure) => failure === missing,
  );
  assert.throws(
    () => getAzureCliToken('scope', env, () => ({
      status: 1, stderr: 'Interactive sign-in is required', stdout: '',
    }), 'linux'),
    /Interactive sign-in is required/,
  );
});

test('uses the classic Azure CLI Python module on Windows', (context) => {
  context.mock.method(fs, 'existsSync', () => true);
  const resolved = resolveAzureCli(
    { ProgramFiles: 'C:\\Program Files' },
    'win32',
  );

  assert.equal(
    resolved.command,
    'C:\\Program Files\\Microsoft SDKs\\Azure\\CLI2\\python.exe',
  );
  assert.deepEqual(resolved.prefixArgs, ['-IBm', 'azure.cli']);
});

test('resolves copied Windows environment variables case-insensitively', (context) => {
  context.mock.method(fs, 'existsSync', () => true);
  for (const key of ['PROGRAMFILES', 'programfiles', 'ProgramFiles']) {
    assert.deepEqual(resolveAzureCli({ [key]: 'C:\\Program Files' }, 'win32'), {
      command: 'C:\\Program Files\\Microsoft SDKs\\Azure\\CLI2\\python.exe',
      prefixArgs: ['-IBm', 'azure.cli'],
    });
  }
  assert.equal(resolveAzureCli({
    azclips_evaluation_azure_cli: 'C:\\tools\\classic-az.exe',
  }, 'win32').command, 'C:\\tools\\classic-az.exe');
});

test('never falls back to the AZCLIPS command on PATH', (context) => {
  context.mock.method(fs, 'existsSync', () => false);
  assert.throws(
    () => resolveAzureCli({ PROGRAMFILES: 'C:\\Program Files' }, 'win32'),
    /Refusing to fall back to az on PATH/,
  );
  assert.throws(() => resolveAzureCli({}, 'linux'), /AZCLIPS_EVALUATION_AZURE_CLI/);
});

for (const response of [
  { accessToken: 'test-token' },
  { accessToken: 'test-token', expires_on: 'invalid' },
  { accessToken: 'test-token', expires_on: '0' },
]) {
  test(`rejects invalid token expiry ${JSON.stringify(response.expires_on)}`, () => {
    assert.throws(() => getAzureCliToken(
      'scope',
      { AZCLIPS_EVALUATION_AZURE_CLI: 'classic-az' },
      () => ({ status: 0, stdout: JSON.stringify(response) }),
      'linux',
    ), /invalid token expiration/);
  });
}

test('accepts the legacy textual expiry without inventing a token lifetime', () => {
  const expiresOn = '2030-01-01T00:00:00Z';
  const token = getAzureCliToken(
    'scope',
    { AZCLIPS_EVALUATION_AZURE_CLI: 'classic-az' },
    () => ({ status: 0, stdout: JSON.stringify({ accessToken: 'test-token', expiresOn }) }),
    'linux',
  );
  assert.equal(token.expiresOnTimestamp, Date.parse(expiresOn));
});