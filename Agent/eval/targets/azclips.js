// promptfoo custom provider — drives the installed azclips agent (one-shot AI
// error-recovery) and returns the rendered plan as the provider "output".
//
// Capture the rendered response for semantic grading; structured plans and
// usage are read separately from the diagnostic trace.
// Process failures are provider errors, not scoreable error-recovery plans.
//
// Config: set AZCLIPS_EXE to the az.exe under test (defaults to the dev build).

const childProcess = require('node:child_process');
const { createHash } = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const DEFAULT_EXE = path.join(
  process.env.LOCALAPPDATA || os.homedir(),
  'Programs', 'azcli-dev', 'az.exe',
);

// SpinnerService frames followed by ConsoleAgentRenderer's CR + padding + CR erase.
const SPINNER_FRAME = /( {2}[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏] [^\r\n]*)\r( +)\r/g;
const TRACE_PATH = /^\s*trace:\s*(.+?\.jsonl)\s*$/gim;
const DEFAULT_TRACE_DIR = path.join(os.tmpdir(), 'azclips-evaluation', 'traces');
const MAX_BUFFER_BYTES = 8 * 1024 * 1024;

function findRuntimePromptPath(exe = process.env.AZCLIPS_EXE || DEFAULT_EXE) {
  const baseDir = path.dirname(path.resolve(exe));
  const candidates = [
    path.join(baseDir, 'Extensions', 'Microsoft.Azure.Azclips.Extensions.Agent', 'prompts', 'oneshot.md'),
    path.join(baseDir, '..', 'Core.Agent', 'prompts', 'oneshot.md'),
    path.join(baseDir, 'prompts', 'oneshot.md'),
  ];
  const promptPath = candidates.find((candidate) => fs.existsSync(candidate));
  if (!promptPath) {
    throw new Error(`Unable to find the runtime oneshot prompt for ${exe}`);
  }
  return path.resolve(promptPath);
}

function readRuntimePrompt(exe) {
  const promptPath = findRuntimePromptPath(exe);
  const content = fs.readFileSync(promptPath, 'utf8');
  return {
    content,
    path: promptPath,
    sha256: createHash('sha256').update(content).digest('hex'),
  };
}

// Evaluations are often launched from an AI agent host. Its variables configure the same Copilot runtime
// that the target embeds (extra tools, MCP servers, credentials) and are absent from a user's terminal.
// AZCLIPS_IN_AGENT_MODE comes from an azclips Agent host and would turn off the recovery under test.
// AZURE_OPENAI_* can configure the separate Judge and are documented Agent inputs too.
const HOST_AGENT_VARIABLE = /^(COPILOT_|GITHUB_COPILOT_|CLAUDE_|ANTHROPIC_|OPENAI_|AZURE_OPENAI_|AZCLIPS_EVALUATION_|VSCODE_|ELECTRON_)/i;
const HOST_AGENT_VARIABLES = new Set(['AI_AGENT', 'RUBBER_DUCK_AGENT', 'NODE_OPTIONS', 'AZCLIPS_IN_AGENT_MODE']);

function isHostAgentVariable(name) {
  return HOST_AGENT_VARIABLE.test(name) || HOST_AGENT_VARIABLES.has(name.toUpperCase());
}

function removedHostVariables(baseEnvironment) {
  return Object.keys(baseEnvironment).filter(isHostAgentVariable).sort();
}

function buildEvaluationEnvironment(baseEnvironment, flavor = baseEnvironment.AZURECLI_FLAVOR || 'azure cli') {
  const environment = {};
  for (const [name, value] of Object.entries(baseEnvironment)) {
    if (!isHostAgentVariable(name)) environment[name] = value;
  }
  environment.AZURECLI_FLAVOR = flavor;
  return environment;
}

function execFileUnattended(file, args, options, callback) {
  const child = childProcess.execFile(file, args, options, callback);
  child.stdin?.end();
  return child;
}

// The Agent runs in a child process of az.exe; ending only az.exe leaves it running into later cases.
const PROCESS_TREE_GRACE_MS = 5000;

function descendantProcessIds(rootPid) {
  const childrenByParent = new Map();
  const table = childProcess.execFileSync('ps', ['-A', '-o', 'pid=,ppid='], { encoding: 'utf8' });
  for (const line of table.split('\n')) {
    const [pid, parentPid] = line.trim().split(/\s+/).map(Number);
    if (!Number.isInteger(pid) || !Number.isInteger(parentPid)) continue;
    if (!childrenByParent.has(parentPid)) childrenByParent.set(parentPid, []);
    childrenByParent.get(parentPid).push(pid);
  }
  const descendants = [];
  const seen = new Set([rootPid]);
  const pending = [rootPid];
  while (pending.length > 0) {
    for (const pid of childrenByParent.get(pending.pop()) || []) {
      if (seen.has(pid)) continue;
      seen.add(pid);
      descendants.push(pid);
      pending.push(pid);
    }
  }
  return descendants;
}

function terminateProcessTree(child, platform = process.platform) {
  if (!child?.pid) return;
  if (platform === 'win32') {
    childProcess.spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true });
    return;
  }
  // Linux and macOS have no tree kill; list the target's descendants before ending the target.
  let descendants = [];
  try {
    descendants = descendantProcessIds(child.pid);
  } catch {
    // Without ps, still end the target itself.
  }
  child.kill('SIGKILL');
  for (const pid of descendants) {
    try {
      process.kill(pid, 'SIGKILL');
    } catch {
      // The process has already ended.
    }
  }
}

function getProcessFailure(error, timeoutMs, failedToStart = false) {
  if (!error) return undefined;
  const spawnFailure = failedToStart ||
    ['ENOENT', 'EACCES', 'EPERM', 'ENOEXEC'].includes(error.code) ||
    (typeof error.syscall === 'string' && /^spawn(?:\s|$)/.test(error.syscall));
  if (Number.isInteger(error.code) && error.code !== 0 &&
    !error.killed && !error.signal && !spawnFailure) {
    return undefined;
  }

  // execFile error messages can contain the full command, arguments and stderr.
  const code = Number.isFinite(error.code) ||
    (typeof error.code === 'string' && /^[A-Z][A-Z0-9_]{0,63}$/.test(error.code))
    ? error.code : null;
  const signal = typeof error.signal === 'string' && /^SIG[A-Z0-9]+$/.test(error.signal)
    ? error.signal : null;
  const codeDetail = code === null ? '' : ` (${code})`;
  let reason = 'process';
  let message = `Azclips process failed${codeDetail}. Check the executable and process options before retrying.`;
  if (error.code === 'ERR_CHILD_PROCESS_STDIO_MAXBUFFER') {
    reason = 'max-buffer';
    message = 'Azclips process output exceeded the 8 MiB maxBuffer limit. Reduce command output before retrying.';
  } else if (error.killed || error.signal || error.code === 'ETIMEDOUT') {
    reason = 'terminated';
    const timeoutDetail = Number.isFinite(timeoutMs) ? ` (${timeoutMs} ms)` : '';
    message = `Azclips process was terminated${signal ? ` by ${signal}` : ''}. Check config.timeoutMs${timeoutDetail} and external termination before retrying.`;
  } else if (spawnFailure) {
    reason = 'spawn';
    message = `Azclips process could not be started${codeDetail}. Check AZCLIPS_EXE/config.exe, executable permissions and process options.`;
  }

  return {
    type: 'infrastructure',
    reason,
    code,
    signal,
    killed: Boolean(error.killed),
    ...(reason === 'terminated' && Number.isFinite(timeoutMs) && { timeoutMs }),
    ...(reason === 'max-buffer' && { maxBufferBytes: MAX_BUFFER_BYTES }),
    message,
  };
}

function stripNoise(raw) {
  const withoutSpinner = raw.replace(SPINNER_FRAME, (sequence, frame, padding, offset) => {
    const atLineStart = offset === 0 || raw[offset - 1] === '\r' || raw[offset - 1] === '\n';
    return atLineStart && padding.length === frame.length ? '' : sequence;
  });
  return withoutSpinner
    .split(/\r?\n/)
    .filter((line) => {
      const t = line.trim();
      return t.length > 0 &&
        !/^trace:\s*.+\.jsonl$/i.test(t);
    })
    .map((line) => line.replace(/\s+$/g, ''))
    .join('\n')
    .trim();
}

function splitAgentOutput(raw, exitCode = 0) {
  const cleaned = stripNoise(raw);
  if (!exitCode) return { output: cleaned };

  const lines = cleaned.split(/\r?\n/);
  const errorIndex = lines.findIndex((line) => /^\s*\[ERROR\]\s*/i.test(line));
  if (errorIndex < 0) return { output: cleaned };

  const aiHelpIndex = lines.findIndex((line) => line.trim() === 'AI help');
  const errorEnd = aiHelpIndex > errorIndex ? aiHelpIndex : errorIndex + 1;
  const errorLines = lines.slice(errorIndex, errorEnd);
  errorLines[0] = errorLines[0].replace(/^\s*\[ERROR\]\s*/i, '');

  return {
    output: aiHelpIndex > errorIndex
      ? lines.slice(aiHelpIndex).join('\n').trim()
      : cleaned,
    executionError: {
      source: 'azclips',
      exitCode,
      message: errorLines.join('\n').trim(),
    },
  };
}

function findTracePath(raw) {
  const matches = Array.from(raw.matchAll(TRACE_PATH));
  return matches.at(-1)?.[1]?.trim();
}

const FAILURE_ID_HINT = /\baz troubleshoot --id ([1-9]\d{0,9}-\d{4}-\d{4}-\d{4}-\d{4,20})(?=["'\s]|$)/g;
// Warnings that the target prints instead of a failure ID when it could not save the failure record.
const CAPTURE_WARNING = /(?:Secure failure history|The failure could not be saved|Failure history is busy|Recovery history policy)[^\r\n]*/g;

function findFailureId(raw) {
  const ids = [...new Set([...raw.matchAll(FAILURE_ID_HINT)].map((match) => match[1]).filter((id) => id.length <= 46))];
  if (ids.length === 1) return ids[0];
  const warnings = [...new Set((raw.match(CAPTURE_WARNING) || []).map((warning) => warning.trim().slice(0, 300)))]
    .slice(0, 3)
    .map((warning) => redactTraceValue(warning));
  const details = warnings.length ? `Capture warning: ${warnings.join(' | ')}` : 'No capture warning was printed.';
  throw Object.assign(
    new Error('Manual evaluation requires one exact failure ID from the failed command; refusing to troubleshoot the ' +
      `global latest failure. Found ${ids.length} failure IDs. ${details}`),
    { code: ids.length ? 'FAILURE_ID_AMBIGUOUS' : 'FAILURE_ID_MISSING', captureWarnings: warnings },
  );
}

function originalCliError(stdout, stderr, exitCode) {
  for (const text of [stderr || '', stdout || '']) {
    let start = -1;
    let depth = 0;
    let quoted = false;
    let escaped = false;
    for (let index = 0; index < text.length; index += 1) {
      const character = text[index];
      if (start < 0) {
        if (character !== '{') continue;
        start = index;
        depth = 1;
        continue;
      }
      if (quoted) {
        if (escaped) escaped = false;
        else if (character === '\\') escaped = true;
        else if (character === '"') quoted = false;
        continue;
      }
      if (character === '"') quoted = true;
      else if (character === '{') depth += 1;
      else if (character === '}') depth -= 1;
      if (depth !== 0) continue;
      let value;
      try {
        value = JSON.parse(text.slice(start, index + 1));
      } catch (error) {
        if (!(error instanceof SyntaxError)) throw error;
      }
      start = -1;
      if (typeof value?.error?.message === 'string' && value.error.message.trim()) {
        return redactTraceValue({
          source: 'azclips',
          exitCode,
          ...(typeof value.error.code === 'string' && { code: value.error.code }),
          message: value.error.message,
        });
      }
    }
  }
  const raw = [stderr, stdout].filter((text) => typeof text === 'string' && text.trim()).join('\n');
  const { executionError } = splitAgentOutput(raw, exitCode);
  const message = executionError?.message || stripNoise(raw)
    .split('\n')
    .filter((line) => !/\baz troubleshoot(?: --id\b| without arguments\b)/.test(line))
    .join('\n').trim();
  return redactTraceValue({
    source: 'azclips',
    exitCode,
    message: message || `The CLI exited with code ${exitCode} without an error message.`,
  });
}

function readStructuredPlan(tracePath) {
  if (!tracePath || !fs.existsSync(tracePath)) return undefined;
  const lines = fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean);
  let plan;
  for (const line of lines) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (
      entry.kind === 'agent_event' &&
      entry.event === 'ToolStarted' &&
      entry.payload?.Name === 'propose_plan' &&
      typeof entry.payload.ArgsJson === 'string'
    ) {
      plan = JSON.parse(entry.payload.ArgsJson);
    }
  }
  return plan;
}

const SECRET_KEY = /(authorization|api[-_]?key|token|password|secret|connection[-_]?string|sas)/i;
// Query names whose -, _ or . separated part names a credential, e.g. api-key, access_token, client_secret.
const SECRET_VALUE = /(Bearer\s+)[^\s"']+|([?&](?:[\w.-]*[-_.])?(?:api[-_]?key|apikey|key|token|secret|password|passwd|pwd|sig|signature|credential|code)(?:[-_.][\w.-]*)?=)[^&\s"']+/gi;

function redactTraceValue(value, key = '') {
  if (SECRET_KEY.test(key)) return '[REDACTED]';
  if (typeof value === 'string') {
    return value.replace(SECRET_VALUE, (_match, bearer, query) => `${bearer || query || ''}[REDACTED]`);
  }
  if (Array.isArray(value)) return value.map((item) => redactTraceValue(item));
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([childKey, item]) => [
      childKey,
      redactTraceValue(item, childKey),
    ]));
  }
  return value;
}

function elapsedMilliseconds(value) {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value !== 'string') return undefined;
  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(value);
  if (!match) return undefined;
  const days = Number(match[1] || 0);
  const hours = Number(match[2]);
  const minutes = Number(match[3]);
  const seconds = Number(match[4]);
  const fraction = Number(`0.${match[5] || 0}`);
  return Math.round((((days * 24 + hours) * 60 + minutes) * 60 + seconds + fraction) * 1000);
}

function parseJsonOrText(value) {
  if (typeof value !== 'string') return value;
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function readToolCalls(tracePath) {
  if (!tracePath || !fs.existsSync(tracePath)) return [];
  const calls = [];
  const byCallId = new Map();
  const internalTools = new Set(['propose_plan', 'submit_response']);
  for (const line of fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean)) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (entry.kind !== 'agent_event') continue;
    const payload = entry.payload || {};
    if (entry.event === 'ToolStarted') {
      if (internalTools.has(payload.Name) || typeof payload.CallId !== 'string') continue;
      const call = {
        name: payload.Name || 'unknown',
        arguments: redactTraceValue(parseJsonOrText(payload.ArgsJson)),
        callId: payload.CallId,
        ...(payload.At && { at: payload.At }),
        ...(payload.ParentCallId && { parentCallId: payload.ParentCallId }),
      };
      calls.push(call);
      byCallId.set(payload.CallId, call);
      continue;
    }
    if (entry.event !== 'ToolCompleted' && entry.event !== 'ToolFailed') continue;
    const call = byCallId.get(payload.CallId);
    if (!call) continue;
    const elapsedMs = elapsedMilliseconds(payload.Elapsed);
    if (elapsedMs !== undefined) call.elapsedMs = elapsedMs;
    if (entry.event === 'ToolFailed' || payload.IsError) {
      call.error = redactTraceValue(payload.Error || payload.FullResult || 'Tool failed');
    } else {
      call.result = redactTraceValue(parseJsonOrText(payload.FullResult));
    }
  }
  return calls;
}

function readTokenUsage(tracePath) {
  if (!tracePath || !fs.existsSync(tracePath)) return undefined;
  const lines = fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean);
  const usageEvents = [];
  for (const line of lines) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (entry.kind === 'agent_event' && entry.event === 'LlmTurnCompleted') {
      usageEvents.push(entry.payload || {});
    }
  }
  if (usageEvents.length === 0) return undefined;

  const sum = (field) => usageEvents.reduce((total, usage) => {
    const value = Number(usage[field] ?? 0);
    return total + (Number.isFinite(value) ? value : 0);
  }, 0);
  const optionalSum = (field) => usageEvents.every((usage) => usage[field] !== undefined && usage[field] !== null)
    ? sum(field)
    : undefined;
  const prompt = sum('InputTokens');
  const completion = sum('OutputTokens');
  const cached = sum('CachedTokens');
  const reasoning = optionalSum('ReasoningTokens');
  const cacheWrite = optionalSum('CacheWriteTokens');

  return {
    prompt,
    completion,
    cached,
    total: prompt + completion,
    numRequests: usageEvents.length,
    completionDetails: {
      ...(reasoning !== undefined && { reasoning }),
      cacheReadInputTokens: cached,
      ...(cacheWrite !== undefined && { cacheCreationInputTokens: cacheWrite }),
    },
  };
}

function readModels(tracePath) {
  if (!tracePath || !fs.existsSync(tracePath)) return [];
  const models = new Set();
  for (const line of fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean)) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (
      entry.kind === 'agent_event' &&
      entry.event === 'LlmTurnCompleted' &&
      typeof entry.payload?.Model === 'string' &&
      entry.payload.Model.trim() &&
      entry.payload.Model.trim().toLowerCase() !== 'unknown'
    ) {
      models.add(entry.payload.Model.trim());
    }
  }
  return [...models].sort();
}

function toolTraceAvailable(tracePath) {
  return Boolean(tracePath && fs.existsSync(tracePath));
}

function timestampMilliseconds(value) {
  if (typeof value !== 'string' || !value.trim()) return undefined;
  const parsed = Date.parse(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function intervalUnionMilliseconds(intervals) {
  const sorted = intervals.filter(([start, end]) => end > start).sort((left, right) => left[0] - right[0]);
  let total = 0;
  let current;
  for (const [start, end] of sorted) {
    if (!current || start > current[1]) {
      if (current) total += current[1] - current[0];
      current = [start, end];
    } else {
      current[1] = Math.max(current[1], end);
    }
  }
  return total + (current ? current[1] - current[0] : 0);
}

// Splits Agent process time into model turns, tool execution and remaining host work.
// Intervals end at payload.At and last payload.Elapsed; unknown phases are omitted.
// propose_plan and submit_response are the Agent's internal plan tools; the renderer shows the
// plan when submit_response starts, and the process continues after that.
const PLAN_TOOLS = new Set(['propose_plan', 'submit_response']);

function readTiming(tracePath, processMs) {
  if (!tracePath || !fs.existsSync(tracePath)) return undefined;
  let traceStart;
  let traceEnd;
  let sessionStart;
  let planProposedAt;
  let answerRenderedAt;
  const llm = [];
  const tools = [];
  const planTools = [];
  for (const line of fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean)) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    const at = timestampMilliseconds(entry.ts);
    if (entry.kind === 'trace_start' && at !== undefined && traceStart === undefined) traceStart = at;
    if (entry.kind === 'trace_end' && at !== undefined) traceEnd = at;
    if (entry.kind === 'session_start' && at !== undefined && sessionStart === undefined) sessionStart = at;
    if (entry.kind !== 'agent_event') continue;
    const end = timestampMilliseconds(entry.payload?.At);
    const elapsed = elapsedMilliseconds(entry.payload?.Elapsed);
    if (end === undefined || elapsed === undefined) continue;
    if (entry.event === 'LlmTurnCompleted') llm.push([end - elapsed, end]);
    else if (entry.event === 'ToolCompleted' || entry.event === 'ToolFailed') {
      tools.push([end - elapsed, end]);
      if (PLAN_TOOLS.has(entry.payload?.Name)) planTools.push([end - elapsed, end]);
    }
    if (entry.event === 'ToolCompleted' && entry.payload?.Name === 'propose_plan' && !entry.payload.IsError &&
      planProposedAt === undefined) {
      planProposedAt = end;
    }
    if (entry.event === 'ToolCompleted' && entry.payload?.Name === 'submit_response' && !entry.payload.IsError &&
      answerRenderedAt === undefined) {
      answerRenderedAt = end - elapsed;
    }
  }
  const timing = {};
  const record = (name, value) => {
    if (Number.isFinite(value) && value >= 0) timing[name] = Math.round(value);
  };
  record('processMs', processMs);
  if (traceStart !== undefined && traceEnd !== undefined) record('traceMs', traceEnd - traceStart);
  if (traceStart !== undefined && sessionStart !== undefined) record('sessionStartMs', sessionStart - traceStart);
  if (traceStart !== undefined && planProposedAt !== undefined) record('planProposedMs', planProposedAt - traceStart);
  if (traceStart !== undefined && answerRenderedAt !== undefined) {
    record('answerRenderedMs', answerRenderedAt - traceStart);
  }
  if (llm.length) {
    if (sessionStart !== undefined) {
      record('firstLlmRequestMs', Math.min(...llm.map(([start]) => start)) - sessionStart);
    }
    record('llmMs', llm.reduce((total, [start, end]) => total + (end - start), 0));
    timing.llmTurns = llm.length;
    record('toolMs', intervalUnionMilliseconds(tools));
    record('planToolMs', intervalUnionMilliseconds(planTools));
    record('hostMs', processMs - intervalUnionMilliseconds([...llm, ...tools]));
  }
  return Object.keys(timing).length > (timing.processMs === undefined ? 0 : 1) ? timing : undefined;
}

// Plans rejected by the host (for example by a preToolUse hook) cost extra model turns but are not
// part of the exported tool calls, which exclude the internal plan tools.
function readPlanActivity(tracePath) {
  if (!tracePath || !fs.existsSync(tracePath)) return undefined;
  const activity = { attempts: 0, rejections: 0, rejectionReasons: [] };
  for (const line of fs.readFileSync(tracePath, 'utf8').split(/\r?\n/).filter(Boolean)) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (entry.kind !== 'agent_event' || entry.payload?.Name !== 'propose_plan') continue;
    if (entry.event === 'ToolStarted') activity.attempts += 1;
    if (entry.event === 'ToolFailed' || (entry.event === 'ToolCompleted' && entry.payload.IsError)) {
      activity.rejections += 1;
      activity.rejectionReasons.push(redactTraceValue(String(entry.payload.Error || entry.payload.FullResult || 'Plan rejected')));
    }
  }
  return activity;
}

function readJsonEnvironment(name, env = process.env) {
  const value = env[name];
  if (typeof value !== 'string' || !value.trim()) return undefined;
  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : undefined;
  } catch {
    return undefined;
  }
}

class AzclipsAgentProvider {
  constructor(options) {
    this.exe = (options && options.config && options.config.exe) || process.env.AZCLIPS_EXE || DEFAULT_EXE;
    this.timeoutMs = (options && options.config && options.config.timeoutMs) || 120000;
    this.traceDir = (options && options.config && options.config.traceDir) || DEFAULT_TRACE_DIR;
    this.recoveryMode = options?.config?.recoveryMode || 'automatic';
    if (!['automatic', 'manual'].includes(this.recoveryMode)) {
      throw new Error('recoveryMode must be automatic or manual');
    }
    // Run-level facts recorded by run-evaluation.ps1 before any case starts.
    this.target = readJsonEnvironment('AZCLIPS_EVALUATION_TARGET');
    this.resourceCache = readJsonEnvironment('AZCLIPS_EVALUATION_RESOURCE_CACHE');
    this.agentSettings = readJsonEnvironment('AZCLIPS_EVALUATION_AGENT_SETTINGS');
    const warmupObservations = Number(process.env.AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS);
    this.warmupObservations = Number.isInteger(warmupObservations) && warmupObservations >= 0
      && /^\d+$/.test(String(process.env.AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS).trim())
      ? warmupObservations
      : undefined;
  }

  id() { return 'azclips-agent'; }

  // The failing az command comes from the case's vars.command (e.g. "profile show").
  // The `prompt` arg is the one-shot.md system prompt (the prompt VERSION under test,
  // shown in the dashboard's prompt column) — NOT the command — so we ignore it here.
  async callApi(prompt, context) {
    const command = String((context && context.vars && context.vars.command) || '').trim();
    const args = command.split(/\s+/).filter(Boolean);
    const flavor = String((context && context.vars && context.vars.flavor) || process.env.AZURECLI_FLAVOR || 'azure cli');
    const runtimePrompt = readRuntimePrompt(this.exe);
    const renderedRuntimePrompt = runtimePrompt.content.replace(/\{\{flavor\}\}/gi, flavor);
    if (String(prompt).replaceAll('\r\n', '\n') !== renderedRuntimePrompt.replaceAll('\r\n', '\n')) {
      throw new Error(`Evaluation prompt does not match runtime prompt ${runtimePrompt.path}`);
    }

    return new Promise((resolve) => {
      const env = buildEvaluationEnvironment(process.env, flavor);
      const runFacts = {
        ...(this.target && { target: this.target }),
        ...(this.resourceCache && { resourceCache: this.resourceCache }),
        ...(this.agentSettings && { agentSettings: this.agentSettings }),
        ...(this.warmupObservations !== undefined && { warmupObservations: this.warmupObservations }),
        removedHostVariables: removedHostVariables(process.env),
      };
      const startedAt = Date.now();
      const recoveryMetadata = { observationStartedAt: new Date(startedAt).toISOString() };
      let troubleshootStartedAt;
      const complete = (err, stdout, stderr, failedToStart = false) => {
        if (troubleshootStartedAt !== undefined) {
          recoveryMetadata.troubleshootDurationMs = Date.now() - troubleshootStartedAt;
        }
        const processMs = troubleshootStartedAt !== undefined
          ? recoveryMetadata.troubleshootDurationMs
          : Date.now() - startedAt;
        const raw = `${stdout || ''}\n${stderr || ''}`;
        const numericExitCode = Number(err?.code ?? 1);
        const exitCode = err
          ? Number.isFinite(numericExitCode) ? numericExitCode : 1
          : 0;
        const tracePath = findTracePath(raw);
        const processFailure = getProcessFailure(err, this.timeoutMs, failedToStart) ||
          (troubleshootStartedAt !== undefined && exitCode !== 0 ? {
            type: 'infrastructure',
            reason: 'troubleshoot',
            code: exitCode,
            message: `Manual troubleshooting failed with exit code ${exitCode}. Check Agent authentication and the target package before scoring the response.`,
          } : undefined);
        if (processFailure) {
          const metadata = {
            ...recoveryMetadata,
            exitCode,
            resultType: 'azclips.plan.v1',
            runtimePromptSha256: runtimePrompt.sha256,
            ...runFacts,
            processFailure,
            models: [],
            toolCalls: [],
            toolTraceAvailable: toolTraceAvailable(tracePath),
          };
          let tokenUsage;
          try {
            metadata.models = readModels(tracePath);
            metadata.toolCalls = readToolCalls(tracePath);
            tokenUsage = readTokenUsage(tracePath);
            const timing = readTiming(tracePath, processMs);
            if (timing) metadata.timing = timing;
            const planActivity = readPlanActivity(tracePath);
            if (planActivity) metadata.planActivity = planActivity;
          } catch {
            metadata.traceExtractionError = 'Unable to read diagnostic trace after process failure';
          }
          resolve({
            error: processFailure.message,
            prompt: renderedRuntimePrompt,
            metadata,
            ...(tokenUsage && { tokenUsage }),
          });
          return;
        }

        // Rendered answers use stdout; stderr remains available for trace paths and CLI errors.
        const rendered = splitAgentOutput(stdout || '', exitCode);
        const output = rendered.output;
        const executionError = rendered.executionError ||
          (exitCode ? splitAgentOutput(raw, exitCode).executionError : undefined);
        const metadata = {
          ...recoveryMetadata,
          exitCode,
          resultType: 'azclips.plan.v1',
          runtimePromptSha256: runtimePrompt.sha256,
          ...runFacts,
          models: readModels(tracePath),
          toolCalls: readToolCalls(tracePath),
          toolTraceAvailable: toolTraceAvailable(tracePath),
          ...(executionError && { executionError }),
          ...(!recoveryMetadata.originalError && executionError && {
            originalError: redactTraceValue(executionError),
          }),
        };
        let tokenUsage;
        try {
          metadata.plan = readStructuredPlan(tracePath);
          if (!metadata.plan) metadata.planExtractionError = 'No propose_plan event found in trace';
        } catch (traceError) {
          metadata.planExtractionError = traceError instanceof Error ? traceError.message : String(traceError);
        }
        try {
          const timing = readTiming(tracePath, processMs);
          if (timing) metadata.timing = timing;
          const planActivity = readPlanActivity(tracePath);
          if (planActivity) metadata.planActivity = planActivity;
        } catch (traceError) {
          metadata.timingExtractionError = traceError instanceof Error ? traceError.message : String(traceError);
        }
        try {
          tokenUsage = readTokenUsage(tracePath);
          if (!tokenUsage) metadata.tokenUsageExtractionError = 'No LlmTurnCompleted event found in trace';
        } catch (traceError) {
          metadata.tokenUsageExtractionError = traceError instanceof Error ? traceError.message : String(traceError);
        }
        // Completed CLI errors are expected in recovery cases; process failures are not scored.
        resolve({
          output,
          prompt: renderedRuntimePrompt,
          metadata,
          ...(tokenUsage && { tokenUsage }),
        });
      };
      const runProcess = (processArgs, callback, timeout = this.timeoutMs) => {
        let child;
        let timedOut = false;
        const timer = setTimeout(() => {
          timedOut = true;
          terminateProcessTree(child);
        }, timeout);
        try {
          // execFile's own timeout is only a backstop if the tree could not be ended.
          child = execFileUnattended(this.exe, processArgs, {
            timeout: timeout + PROCESS_TREE_GRACE_MS,
            maxBuffer: MAX_BUFFER_BYTES,
            env,
          }, (error, stdout, stderr) => {
            clearTimeout(timer);
            const outcome = timedOut
              ? Object.assign(error || new Error('Azclips process timed out'), {
                killed: true,
                signal: error?.signal || 'SIGTERM',
              })
              : error;
            callback(outcome, stdout, stderr);
          });
        } catch (error) {
          clearTimeout(timer);
          complete(error || new Error('Unknown process startup failure'), '', '', true);
        }
      };
      if (this.recoveryMode === 'automatic') {
        runProcess(args, complete);
        return;
      }
      const runStimulus = (attempt, timeout) => runProcess(args, (error, stdout, stderr) => {
        if (getProcessFailure(error, this.timeoutMs)) {
          complete(error, stdout, stderr);
          return;
        }
        try {
          if (!error) throw new Error('The manual evaluation stimulus succeeded instead of recording a failure.');
          recoveryMetadata.originalError = originalCliError(stdout, stderr, error.code);
          let failureId;
          try {
            failureId = findFailureId(`${stdout || ''}\n${stderr || ''}`);
          } catch (missing) {
            const left = this.timeoutMs - (Date.now() - startedAt);
            // A busy or slow failure history can leave one run of the command without an ID. Run the same command
            // once more and record the first attempt; a second miss stays an infrastructure error.
            if (attempt === 1 && missing.code === 'FAILURE_ID_MISSING' && left > 0) {
              recoveryMetadata.stimulusRetry = {
                reason: 'failure-id-missing',
                firstAttemptMs: Date.now() - startedAt,
                captureWarnings: missing.captureWarnings,
              };
              runStimulus(2, left);
              return;
            }
            throw missing;
          }
          recoveryMetadata.recoveryMode = 'manual';
          recoveryMetadata.failureId = failureId;
          recoveryMetadata.failureExitCode = error.code;
          recoveryMetadata.failureCaptureDurationMs = Date.now() - startedAt;
          const remaining = this.timeoutMs - recoveryMetadata.failureCaptureDurationMs;
          if (remaining <= 0) {
            complete(Object.assign(new Error('Evaluation timeout'), {
              code: 'ETIMEDOUT', killed: true,
            }), stdout, stderr);
            return;
          }
          troubleshootStartedAt = Date.now();
          runProcess(['troubleshoot', '--id', failureId, '--style', 'oneshot'], complete, remaining);
        } catch (captureError) {
          const message = captureError instanceof Error ? captureError.message : String(captureError);
          resolve({
            error: message,
            prompt: renderedRuntimePrompt,
            metadata: {
              ...recoveryMetadata,
              resultType: 'azclips.plan.v1',
              runtimePromptSha256: runtimePrompt.sha256,
              ...runFacts,
              recoveryMode: 'manual',
              processFailure: { type: 'infrastructure', reason: 'failure-capture', message },
            },
          });
        }
      }, timeout);
      runStimulus(1, this.timeoutMs);
    });
  }
}

module.exports = AzclipsAgentProvider;
module.exports.buildEvaluationEnvironment = buildEvaluationEnvironment;
module.exports.removedHostVariables = removedHostVariables;
module.exports.execFileUnattended = execFileUnattended;
module.exports.findTracePath = findTracePath;
module.exports.findFailureId = findFailureId;
module.exports.originalCliError = originalCliError;
module.exports.findRuntimePromptPath = findRuntimePromptPath;
module.exports.readRuntimePrompt = readRuntimePrompt;
module.exports.readModels = readModels;
module.exports.readJsonEnvironment = readJsonEnvironment;
module.exports.readPlanActivity = readPlanActivity;
module.exports.readTiming = readTiming;
module.exports.terminateProcessTree = terminateProcessTree;
module.exports.PROCESS_TREE_GRACE_MS = PROCESS_TREE_GRACE_MS;
module.exports.splitAgentOutput = splitAgentOutput;
module.exports.stripNoise = stripNoise;
module.exports.readStructuredPlan = readStructuredPlan;
module.exports.readToolCalls = readToolCalls;
module.exports.readTokenUsage = readTokenUsage;
module.exports.toolTraceAvailable = toolTraceAvailable;
