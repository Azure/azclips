const { isDeepStrictEqual } = require('node:util');

function getPath(value, path) {
  if (!path) return value;
  return path.split('.').reduce((current, segment) => current?.[segment], value);
}

function shellTokens(command) {
  const tokens = [];
  const pattern = /(\s+)|"((?:\\.|[^"\\])*)"|'((?:''|[^'])*)'|([^\s"']+)/gy;
  let token;
  while (pattern.lastIndex < command.length) {
    const match = pattern.exec(command);
    if (!match) throw new SyntaxError('Unterminated quote');
    if (match[1]) {
      if (token !== undefined) tokens.push(token);
      token = undefined;
    } else {
      token = (token ?? '') + (match[2] ?? match[3]?.replaceAll("''", "'") ?? match[4]);
    }
  }
  if (token !== undefined) tokens.push(token);
  return tokens;
}

function commandName(tokens) {
  const names = [];
  for (const token of tokens) {
    if (token.startsWith('-')) break;
    names.push(token.toLowerCase());
  }
  return names.join(' ');
}

function argumentMap(tokens) {
  const result = new Map();
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index];
    if (!token.startsWith('-')) continue;
    const equals = token.indexOf('=');
    if (equals > 0) {
      result.set(token.slice(0, equals), token.slice(equals + 1));
      continue;
    }
    const next = tokens[index + 1];
    if (next !== undefined && (!next.startsWith('-') || /^-(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i.test(next))) {
      result.set(token, next);
      index += 1;
    } else {
      result.set(token, true);
    }
  }
  return result;
}

function normalizeActualCommand(value, config) {
  if (typeof value === 'string') return value;
  if (value && typeof value === 'object') {
    const commandField = config.commandField || 'cmd';
    return value[commandField];
  }
  return undefined;
}

function normalizePathArgument(value) {
  return value
    .replaceAll('\\', '/')
    .replace(/^\.\//, '');
}

function configurationError(config) {
  const isRecord = (value) => value !== null && typeof value === 'object'
    && [Object.prototype, null].includes(Object.getPrototypeOf(value));
  const isArgument = (value) => typeof value === 'string' && /^--?[^=\s"'-][^=\s"']*$/.test(value);
  if (!isRecord(config)) return 'config must be an object';
  if (config.source !== undefined && !['metadata', 'json'].includes(config.source)) {
    return 'source must be metadata or json';
  }
  for (const field of ['path', 'commandField']) {
    if (config[field] !== undefined && typeof config[field] !== 'string') return `${field} must be a string`;
  }
  if (!Array.isArray(config.expectedCommandSet) || config.expectedCommandSet.length === 0) {
    return 'expectedCommandSet must be a nonempty array';
  }
  for (const [index, expected] of config.expectedCommandSet.entries()) {
    const label = `expectedCommandSet[${index}]`;
    if (!isRecord(expected)) return `${label} must be an object`;
    if (typeof expected.command !== 'string' || !expected.command.trim()) {
      return `${label}.command must be a nonempty string`;
    }
    let tokens;
    try {
      tokens = shellTokens(expected.command);
    } catch {
      return `${label}.command must be parseable`;
    }
    if (tokens.some((token) => !token.trim() || token.startsWith('-'))) {
      return `${label}.command must contain only the command name`;
    }
    if (expected.match !== undefined && !['any', 'all'].includes(expected.match)) {
      return `${label}.match must be any or all`;
    }
    for (const field of ['requiredArguments', 'forbiddenArguments', 'booleanArguments', 'pathArguments']) {
      if (expected[field] === undefined) continue;
      if (!Array.isArray(expected[field]) || [...expected[field]].some((argument) => !isArgument(argument))) {
        return `${label}.${field} must be an array of argument names`;
      }
    }
    if (expected.argumentValues !== undefined) {
      if (!isRecord(expected.argumentValues)) return `${label}.argumentValues must be an object`;
      for (const [argument, value] of Object.entries(expected.argumentValues)) {
        if (!isArgument(argument)
          || !(typeof value === 'string' || typeof value === 'boolean'
            || (typeof value === 'number' && Number.isFinite(value)))) {
          return `${label}.argumentValues must map argument names to strings, booleans, or finite numbers`;
        }
      }
    }
  }
  return undefined;
}

function checkCommand(command, expected) {
  const tokens = shellTokens(command);
  const args = argumentMap(tokens);
  const failures = [];
  const pathArguments = new Set(expected.pathArguments || []);
  const booleanArguments = new Set(expected.booleanArguments || []);

  for (const argument of expected.requiredArguments || []) {
    if (!args.has(argument)) failures.push(`missing ${argument}`);
    else {
      const value = args.get(argument);
      // Only explicitly declared boolean switches may be bare.
      const isBooleanSwitch = booleanArguments.has(argument) && value === true;
      if (!isBooleanSwitch && (typeof value !== 'string' || !value.trim())) {
        failures.push(`missing value for ${argument}`);
      }
    }
  }
  for (const argument of expected.forbiddenArguments || []) {
    if (args.has(argument)) failures.push(`contains forbidden ${argument}`);
  }
  for (const [argument, value] of Object.entries(expected.argumentValues || {})) {
    if (!args.has(argument)) failures.push(`missing ${argument}`);
    else {
      const actualValue = String(args.get(argument));
      const expectedValue = String(value);
      const normalizedActual = pathArguments.has(argument) ? normalizePathArgument(actualValue) : actualValue;
      const normalizedExpected = pathArguments.has(argument) ? normalizePathArgument(expectedValue) : expectedValue;
      if (isDeepStrictEqual(normalizedActual, normalizedExpected)) continue;
      failures.push(`${argument} expected ${JSON.stringify(value)}, got ${JSON.stringify(args.get(argument))}`);
    }
  }
  return failures;
}

function commandSetAssertion(output, context) {
  const config = context?.config;
  const error = configurationError(config);
  if (error) return { pass: false, score: 0, reason: `Invalid assertion configuration: ${error}` };
  let source = context.metadata;
  if (config.source === 'json') {
    try {
      source = JSON.parse(output);
    } catch {
      return { pass: false, score: 0, reason: 'Invalid JSON output' };
    }
  }
  const actualSet = getPath(source, config.path || 'plan.steps');
  if (!Array.isArray(actualSet)) {
    return {
      pass: false,
      score: 0,
      reason: `Expected an array at ${config.source || 'metadata'}:${config.path || 'plan.steps'}`,
    };
  }

  const actual = [];
  for (const [index, item] of actualSet.entries()) {
    const command = normalizeActualCommand(item, config);
    try {
      if (typeof command !== 'string' || !command.trim()) throw new Error('expected a nonempty command string');
      const name = commandName(shellTokens(command));
      if (!name.trim()) throw new Error('expected a command name');
      actual.push({ index, command, name });
    } catch (error) {
      return { pass: false, score: 0, reason: `Unparseable command at step ${index + 1}: ${error.message}` };
    }
  }
  const componentResults = [];

  for (const expected of config.expectedCommandSet) {
    const expectedName = expected.command.toLowerCase();
    const matches = actual.filter((item) => item.name === expectedName);
    if (matches.length === 0) {
      componentResults.push({
        pass: false,
        score: 0,
        reason: `No command matched ${expected.command}`,
      });
      continue;
    }

    const results = matches.map((item) => ({
      ...item,
      failures: checkCommand(item.command, expected),
    }));
    const mode = expected.match || 'all';
    const passing = results.filter((result) => result.failures.length === 0);
    const pass = mode === 'any' ? passing.length > 0 : passing.length === results.length;
    const details = results
      .filter((result) => result.failures.length > 0)
      .map((result) => `step ${result.index + 1}: ${result.failures.join(', ')}`)
      .join('; ');
    componentResults.push({
      pass,
      score: pass ? 1 : 0,
      reason: pass
        ? `${expected.command}: ${mode === 'all' ? 'all' : 'at least one'} matching command satisfied the contract`
        : `${expected.command}: ${details}`,
    });
  }

  const pass = componentResults.length > 0 && componentResults.every((result) => result.pass);
  return {
    pass,
    score: componentResults.length
      ? componentResults.filter((result) => result.pass).length / componentResults.length
      : 0,
    reason: pass ? 'Command set contract passed' : 'Command set contract failed',
    componentResults,
  };
}

module.exports = {
  commandSetAssertion,
  argumentMap,
  commandName,
  shellTokens,
};
