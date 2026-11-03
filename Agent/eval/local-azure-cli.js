const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

function environmentValue(env, name, platform) {
  if (env[name] !== undefined) return env[name];
  if (platform !== 'win32') return undefined;
  const key = Object.keys(env).find((candidate) => candidate.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : env[key];
}

function resolveAzureCli(env = process.env, platform = process.platform) {
  const configuredCli = environmentValue(env, 'AZCLIPS_EVALUATION_AZURE_CLI', platform)?.trim();
  if (configuredCli) {
    return { command: configuredCli, prefixArgs: [] };
  }

  if (platform === 'win32') {
    const candidates = [
      environmentValue(env, 'ProgramFiles', platform),
      environmentValue(env, 'ProgramFiles(x86)', platform),
    ].filter(Boolean).map((root) =>
      path.win32.join(root, 'Microsoft SDKs', 'Azure', 'CLI2', 'python.exe'));
    const command = candidates.find((candidate) => fs.existsSync(candidate));
    if (command) return { command, prefixArgs: ['-IBm', 'azure.cli'] };
  }
  throw new Error(
    'Classic Azure CLI was not found. Install it in its standard Windows location or set AZCLIPS_EVALUATION_AZURE_CLI to its executable. Refusing to fall back to az on PATH, which may be AZCLIPS.',
  );
}

function getAzureCliToken(
  scope,
  env = process.env,
  spawnSync = childProcess.spawnSync,
  platform = process.platform,
) {
  const tenantId = environmentValue(env, 'AZURE_TENANT_ID', platform)?.trim();
  const args = [
    'account',
    'get-access-token',
    '--scope',
    scope,
    '--output',
    'json',
  ];
  if (tenantId) args.push('--tenant', tenantId);

  const resolved = resolveAzureCli(env, platform);
  const result = spawnSync(resolved.command, [...resolved.prefixArgs, ...args], {
    encoding: 'utf8',
    env,
    shell: false,
    windowsHide: true,
    timeout: 60_000,
  });
  if (result.error?.code === 'ETIMEDOUT') {
    throw new Error(
      'Classic Azure CLI token acquisition timed out after 60 seconds. Complete the interactive Azure CLI sign-in separately before retrying.',
      { cause: result.error },
    );
  }
  if (result.error) throw result.error;
  if (result.status !== 0) {
    const detail = (result.stderr || '').trim();
    throw new Error(
      detail || `Azure CLI token acquisition failed with exit code ${result.status}`,
    );
  }

  const response = JSON.parse(result.stdout);
  if (typeof response.accessToken !== 'string' || !response.accessToken.trim()) {
    throw new Error('Azure CLI returned no access token');
  }
  const expiresOnTimestamp = response.expires_on
    ? Number(response.expires_on) * 1000
    : Date.parse(response.expiresOn);
  if (!Number.isFinite(expiresOnTimestamp) || expiresOnTimestamp <= 0) {
    throw new Error('Azure CLI returned an invalid token expiration');
  }
  return {
    token: response.accessToken,
    expiresOnTimestamp,
  };
}

class LocalAzureCliCredential {
  constructor(env = process.env) {
    this.env = env;
  }

  async getToken(scope) {
    return getAzureCliToken(scope, this.env);
  }
}

module.exports = {
  getAzureCliToken,
  LocalAzureCliCredential,
  resolveAzureCli,
};