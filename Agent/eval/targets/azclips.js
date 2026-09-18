// promptfoo custom provider — drives the installed azclips agent (one-shot AI
// error-recovery) and returns the rendered plan as the provider "output".
//
// PoC-level: azclips has no structured plan output yet, so we run the failing
// command, capture stdout, and strip the spinner noise ("⠋ Analyzing…") lines.
// Downstream assertions match on the cleaned plan text. When azclips gains a
// structured (--output json) plan, swap the strip logic for a JSON parse.
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

// Braille spinner frames azclips prints while the AI works — pure noise for eval.
const SPINNER = /[⠀-⣿]/g;
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

function buildEvaluationEnvironment(baseEnvironment, flavor = baseEnvironment.AZURECLI_FLAVOR || 'azure cli') {
  return {
    ...baseEnvironment,
    AZURECLI_FLAVOR: flavor,
  };
}

function execFileUnattended(file, args, options, callback) {
  const child = childProcess.execFile(file, args, options, callback);
  child.stdin?.end();
  return child;
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
  return raw
    .split(/\r?\n/)
    // drop lines that are just spinner + status ("⠋ Analyzing error root cause…")
    .filter((line) => {
      const t = line.replace(SPINNER, '').trim();
      return t.length > 0 &&
        !/^(Analyzing|running|thinking|querying|planning|wrapping)/i.test(t) &&
        !/^trace:\s*.+\.jsonl$/i.test(t);
    })
    .map((line) => line.replace(SPINNER, '').replace(/\s+$/g, ''))
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
const SECRET_VALUE = /(Bearer\s+)[^\s"']+|([?&](?:sig|token|key|code)=)[^&\s"']+/gi;

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
  const prompt = sum('InputTokens');
  const completion = sum('OutputTokens');
  const cached = sum('CachedTokens');
  const reasoning = sum('ReasoningTokens');
  const cacheWrite = sum('CacheWriteTokens');

  return {
    prompt,
    completion,
    cached,
    total: prompt + completion,
    numRequests: usageEvents.length,
    completionDetails: {
      reasoning,
      cacheReadInputTokens: cached,
      cacheCreationInputTokens: cacheWrite,
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
      entry.payload.Model !== 'unknown'
    ) {
      models.add(entry.payload.Model.trim());
    }
  }
  return [...models].sort();
}

function toolTraceAvailable(tracePath) {
  return Boolean(tracePath && fs.existsSync(tracePath));
}

class AzclipsAgentProvider {
  constructor(options) {
    this.exe = (options && options.config && options.config.exe) || process.env.AZCLIPS_EXE || DEFAULT_EXE;
    this.timeoutMs = (options && options.config && options.config.timeoutMs) || 120000;
    this.traceDir = (options && options.config && options.config.traceDir) || DEFAULT_TRACE_DIR;
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
      const complete = (err, stdout, stderr, failedToStart = false) => {
        const raw = `${stdout || ''}\n${stderr || ''}`;
        const numericExitCode = Number(err?.code ?? 1);
        const exitCode = err
          ? Number.isFinite(numericExitCode) ? numericExitCode : 1
          : 0;
        const tracePath = findTracePath(raw);
        const processFailure = getProcessFailure(err, this.timeoutMs, failedToStart);
        if (processFailure) {
          const metadata = {
            exitCode,
            resultType: 'azclips.plan.v1',
            runtimePromptSha256: runtimePrompt.sha256,
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

        const { output, executionError } = splitAgentOutput(raw, exitCode);
        const metadata = {
          exitCode,
          resultType: 'azclips.plan.v1',
          runtimePromptSha256: runtimePrompt.sha256,
          models: readModels(tracePath),
          toolCalls: readToolCalls(tracePath),
          toolTraceAvailable: toolTraceAvailable(tracePath),
          ...(executionError && { executionError }),
        };
        let tokenUsage;
        try {
          metadata.plan = readStructuredPlan(tracePath);
          if (!metadata.plan) metadata.planExtractionError = 'No propose_plan event found in trace';
        } catch (traceError) {
          metadata.planExtractionError = traceError instanceof Error ? traceError.message : String(traceError);
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
      try {
        execFileUnattended(this.exe, args, {
          timeout: this.timeoutMs,
          maxBuffer: MAX_BUFFER_BYTES,
          env,
        }, complete);
      } catch (error) {
        complete(error || new Error('Unknown process startup failure'), '', '', true);
      }
    });
  }
}

module.exports = AzclipsAgentProvider;
module.exports.buildEvaluationEnvironment = buildEvaluationEnvironment;
module.exports.execFileUnattended = execFileUnattended;
module.exports.findTracePath = findTracePath;
module.exports.findRuntimePromptPath = findRuntimePromptPath;
module.exports.readRuntimePrompt = readRuntimePrompt;
module.exports.readModels = readModels;
module.exports.splitAgentOutput = splitAgentOutput;
module.exports.stripNoise = stripNoise;
module.exports.readStructuredPlan = readStructuredPlan;
module.exports.readToolCalls = readToolCalls;
module.exports.readTokenUsage = readTokenUsage;
module.exports.toolTraceAvailable = toolTraceAvailable;
