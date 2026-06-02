import { runtimeBacktestReportsMock } from '../fixtures/runtimeBacktestReportsMock';
import { runtimeBetaStatusMock } from '../fixtures/runtimeBetaStatusMock';
import { runtimeFeatureSignalExportsMock } from '../fixtures/runtimeFeatureSignalExportsMock';
import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';
import { runtimeJobsMock } from '../fixtures/runtimeJobsMock';
import { runtimeOutcomeReportsMock } from '../fixtures/runtimeOutcomeReportsMock';
import { runtimeStorageMock } from '../fixtures/runtimeStorageMock';
import { setupWatchMock } from '../fixtures/setupWatchMock';
import { operatorDashboardMock } from '../fixtures/operatorDashboardMock';
import { runtimeMasterStatusMock } from '../fixtures/runtimeMasterStatusMock';
import { runtimeEvents } from '../fixtures/controlCenterMockData';
import { de } from '../i18n/de';

export const DATA_SOURCE = {
  LIVE_FILE: 'live_file',
  FIXTURE: 'fixture',
  UNAVAILABLE: 'unavailable',
} as const;

const runtimeHealthDevUrl = __HERMES_RUNTIME_HEALTH_URL__;
const runtimeEventsBaseUrl = __HERMES_RUNTIME_EVENTS_BASE_URL__;
const runtimeJobsUrl = __HERMES_RUNTIME_JOBS_URL__;
const featureExportUrl = __HERMES_FEATURE_EXPORT_URL__;
const signalExportUrl = __HERMES_SIGNAL_EXPORT_URL__;
const backtestReportUrl = __HERMES_BACKTEST_REPORT_URL__;
const outcomeReportUrl = __HERMES_OUTCOME_REPORT_URL__;
const betaReportUrl = __HERMES_BETA_REPORT_URL__;
const replayManifestUrl = __HERMES_REPLAY_MANIFEST_URL__;
const setupWatchUrl = __HERMES_SETUP_WATCH_URL__;
const runtimeHealthPath = __HERMES_RUNTIME_HEALTH_PATH__;
const runtimeJobsPath = __HERMES_RUNTIME_JOBS_PATH__;
const featureExportPath = __HERMES_FEATURE_EXPORT_PATH__;
const signalExportPath = __HERMES_SIGNAL_EXPORT_PATH__;
const backtestReportPath = __HERMES_BACKTEST_REPORT_PATH__;
const outcomeReportPath = __HERMES_OUTCOME_REPORT_PATH__;
const betaReportPath = __HERMES_BETA_REPORT_PATH__;
const setupWatchPath = __HERMES_SETUP_WATCH_PATH__;
const hermesDataRoot = __HERMES_DATA_ROOT__;
const operatorReportsConfig = __HERMES_OPERATOR_REPORTS__;
const operatorDashboardUrl = __HERMES_OPERATOR_DASHBOARD_URL__;
const supervisorLogUrl = __HERMES_SUPERVISOR_LOG_URL__;
const supervisorLogPath = __HERMES_SUPERVISOR_LOG_PATH__;

const SUPPORTED_RUNTIME_EVENT_TYPES = new Set([
  'RuntimeStarted',
  'StorageInitialized',
  'SnapshotCreated',
  'ReplayManifestCreated',
  'SetupWatchCreated',
  'SetupWatchUpdated',
  'LearningCandidateCreated',
  'JobStarted',
  'JobCompleted',
  'RuntimeStopped',
]);

const JOB_STATUSES = ['pending', 'running', 'completed', 'failed', 'quarantined'];

function asBoolean(value, fallback = false) {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'string') {
    return value.toLowerCase() === 'true';
  }

  return fallback;
}

function asNullableBoolean(value) {
  if (value === null || value === undefined) {
    return null;
  }

  return asBoolean(value);
}

function asNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function clampNumber(value, min = 0, max = 100) {
  return Math.min(max, Math.max(min, asNumber(value, min)));
}

function asString(value, fallback = '') {
  if (value === null || value === undefined) {
    return fallback;
  }

  return String(value);
}

function warningFromError(prefix, error) {
  const message = error instanceof Error ? error.message : String(error);
  return `${prefix}: ${message}`;
}

async function readJsonReadOnly(url) {
  if (!url) {
    throw new Error('No read-only JSON URL configured.');
  }

  const response = await fetch(url, {
    cache: 'no-store',
    credentials: 'same-origin',
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`.trim());
  }

  return response.json();
}

function isBridgeResponse(raw) {
  return Boolean(
    raw &&
      typeof raw === 'object' &&
      Object.prototype.hasOwnProperty.call(raw, 'data') &&
      (Object.prototype.hasOwnProperty.call(raw, 'status') ||
        Object.prototype.hasOwnProperty.call(raw, 'data_source') ||
        Object.prototype.hasOwnProperty.call(raw, 'dataSource')),
  );
}

function unwrapBridgeResponse(raw) {
  return isBridgeResponse(raw) ? raw.data : raw;
}

function bridgeResponseWarnings(raw) {
  if (!isBridgeResponse(raw)) {
    return [];
  }

  return Array.isArray(raw.warnings) ? raw.warnings.map(String) : [];
}

async function readTextReadOnly(url) {
  if (!url) {
    throw new Error('No read-only text URL configured.');
  }

  const response = await fetch(url, {
    cache: 'no-store',
    credentials: 'same-origin',
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`.trim());
  }

  return response.text();
}

async function probeReadOnlyFile(url) {
  if (!url) {
    return null;
  }

  try {
    const response = await fetch(url, {
      cache: 'no-store',
      credentials: 'same-origin',
    });

    return response.ok;
  } catch {
    return null;
  }
}

function buildSource(dataSource, path, warning = '') {
  return {
    dataSource,
    path,
    warnings: warning ? [warning] : [],
  };
}

function combineDataSource(sources) {
  if (sources.every((source) => source.dataSource === DATA_SOURCE.LIVE_FILE)) {
    return DATA_SOURCE.LIVE_FILE;
  }

  if (sources.some((source) => source.dataSource === DATA_SOURCE.FIXTURE)) {
    return DATA_SOURCE.FIXTURE;
  }

  return DATA_SOURCE.UNAVAILABLE;
}

function combineWarnings(sources) {
  return sources.flatMap((source) => source.warnings || []);
}

export function normalizeRuntimeHealth(raw, source) {
  return {
    status: String(raw?.status || (raw?.runtime_state ? 'available' : 'unknown')),
    timestamp_utc: raw?.timestamp_utc || raw?.timestampUtc || null,
    runtime_state: String(raw?.runtime_state || raw?.runtimeState || 'unknown'),
    safe_mode: asBoolean(raw?.safe_mode ?? raw?.safeMode, false),
    no_auto_trading: asBoolean(raw?.no_auto_trading ?? raw?.noAutoTrading, true),
    human_review_required: asBoolean(
      raw?.human_review_required ?? raw?.humanReviewRequired,
      true,
    ),
    free_disk_gb: asNumber(raw?.free_disk_gb ?? raw?.freeDiskGb, 0),
    pending_jobs: asNumber(raw?.pending_jobs ?? raw?.pendingJobs, 0),
    running_jobs: asNumber(raw?.running_jobs ?? raw?.runningJobs, 0),
    failed_jobs: asNumber(raw?.failed_jobs ?? raw?.failedJobs, 0),
    quarantined_jobs: asNumber(raw?.quarantined_jobs ?? raw?.quarantinedJobs, 0),
    active_setup_watches: asNumber(raw?.active_setup_watches ?? raw?.activeSetupWatches, 0),
    last_snapshot_id: raw?.last_snapshot_id || raw?.lastSnapshotId || null,
    last_error: raw?.last_error || raw?.lastError || null,
    event_store_active: asNullableBoolean(raw?.event_store_active ?? raw?.eventStoreActive),
    replay_manifest_available: asNullableBoolean(
      raw?.replay_manifest_available ?? raw?.replayManifestAvailable,
    ),
    source_path: raw?.source_path || runtimeHealthPath,
    source,
  };
}

export function normalizeSetupWatch(raw) {
  return {
    setup_id: raw?.setup_id || raw?.setupId || 'unknown_setup',
    symbol: raw?.symbol || 'UNKNOWN',
    bias: raw?.bias || 'unknown',
    status: raw?.status || 'watching',
    confidence: asNumber(raw?.confidence, 0),
    entry_zone: raw?.entry_zone || raw?.entryZone || '-',
    suggested_stop_loss: raw?.suggested_stop_loss || raw?.suggestedStopLoss || '-',
    suggested_target: raw?.suggested_target || raw?.suggestedTarget || '-',
    trigger_condition: raw?.trigger_condition || raw?.triggerCondition || '-',
    invalidation_level: raw?.invalidation_level || raw?.invalidationLevel || '-',
    time_window_minutes: asNumber(raw?.time_window_minutes || raw?.timeWindowMinutes, 0),
    notes: raw?.notes || '',
    created_at_utc: raw?.created_at_utc || raw?.createdAtUtc || null,
  };
}

function normalizeRuntimeEventSeverity(severity) {
  const value = asString(severity, 'info').toLowerCase();

  if (value === 'critical' || value === 'error') {
    return 'critical';
  }

  if (value === 'warning' || value === 'warn') {
    return 'warning';
  }

  return 'info';
}

function normalizeRuntimeEventCategory(eventType) {
  if (eventType.startsWith('SetupWatch')) {
    return 'trading';
  }

  if (eventType.startsWith('Learning')) {
    return 'learning';
  }

  if (eventType.startsWith('Job')) {
    return 'jobs';
  }

  if (eventType.startsWith('Storage')) {
    return 'storage';
  }

  if (eventType.startsWith('Snapshot')) {
    return 'snapshot';
  }

  if (eventType.startsWith('Replay')) {
    return 'replay';
  }

  return 'runtime';
}

function runtimeEventDescription(eventType, payload) {
  const payloadMessage = payload?.message || payload?.Message;

  if (payloadMessage) {
    return String(payloadMessage);
  }

  switch (eventType) {
    case 'RuntimeStarted':
      return 'Hermes Runtime v1 wurde gestartet.';
    case 'StorageInitialized':
      return 'Storage-Pfade und Runtime-Ablage wurden initialisiert.';
    case 'SnapshotCreated':
      return 'Ein Runtime-Snapshot wurde erstellt.';
    case 'ReplayManifestCreated':
      return 'Ein Replay-Manifest wurde fuer spaetere Analyse erzeugt.';
    case 'SetupWatchCreated':
      return 'Eine neue Setup-Beobachtung wurde angelegt.';
    case 'SetupWatchUpdated':
      return 'Eine Setup-Beobachtung wurde aktualisiert.';
    case 'LearningCandidateCreated':
      return 'Ein Lernkandidat wurde fuer Review vorgemerkt.';
    case 'JobStarted':
      return 'Ein lokaler Runtime-Job wurde gestartet.';
    case 'JobCompleted':
      return 'Ein lokaler Runtime-Job wurde abgeschlossen.';
    case 'RuntimeStopped':
      return 'Hermes Runtime v1 wurde sauber beendet.';
    default:
      return SUPPORTED_RUNTIME_EVENT_TYPES.has(eventType)
        ? `${eventType} wurde gemeldet.`
        : `${eventType} wurde gelesen.`;
  }
}

export function normalizeRuntimeEvent(raw, index = 0) {
  const eventType = asString(raw?.event_type ?? raw?.eventType ?? raw?.EventType, 'UnknownEvent');
  const payload = raw?.payload || raw?.Payload || {};
  const timestamp =
    raw?.timestamp_utc ||
    raw?.timestampUtc ||
    raw?.TimestampUtc ||
    raw?.time ||
    raw?.Time ||
    '-';

  return {
    id:
      raw?.event_id ||
      raw?.eventId ||
      raw?.EventId ||
      raw?.id ||
      `${eventType.toLowerCase()}-${index}`,
    time: asString(timestamp, '-'),
    eventType,
    category: raw?.category || normalizeRuntimeEventCategory(eventType),
    severity: normalizeRuntimeEventSeverity(raw?.severity ?? raw?.Severity),
    source: asString(raw?.source ?? raw?.Source, 'HermesRuntime'),
    description: asString(
      raw?.description ?? raw?.Description,
      runtimeEventDescription(eventType, payload),
    ),
  };
}

function normalizeJobStatus(status, fallback = 'pending') {
  const value = asString(status, fallback).toLowerCase();

  return JOB_STATUSES.includes(value) ? value : fallback;
}

export function normalizeRuntimeJob(raw, fallbackStatus = 'pending', index = 0) {
  const status = normalizeJobStatus(raw?.status ?? raw?.Status, fallbackStatus);
  const jobType = asString(raw?.job_type ?? raw?.jobType ?? raw?.JobType, 'UnknownJob');
  const parameters = raw?.parameters || raw?.Parameters || {};

  return {
    job_id:
      raw?.job_id ||
      raw?.jobId ||
      raw?.JobId ||
      raw?.id ||
      `${status}_${jobType.toLowerCase().replace(/\s+/g, '_')}_${index}`,
    job_type: jobType,
    priority: asNumber(raw?.priority ?? raw?.Priority, 0),
    status,
    created_at_utc: raw?.created_at_utc || raw?.createdAtUtc || raw?.CreatedAtUtc || null,
    started_at_utc: raw?.started_at_utc || raw?.startedAtUtc || raw?.StartedAtUtc || null,
    completed_at_utc: raw?.completed_at_utc || raw?.completedAtUtc || raw?.CompletedAtUtc || null,
    requested_by: asString(raw?.requested_by ?? raw?.requestedBy ?? raw?.RequestedBy, 'unknown'),
    resource_profile: asString(
      raw?.resource_profile ?? raw?.resourceProfile ?? raw?.ResourceProfile,
      'local',
    ),
    max_runtime_minutes: asNumber(
      raw?.max_runtime_minutes ?? raw?.maxRuntimeMinutes ?? raw?.MaxRuntimeMinutes,
      0,
    ),
    max_retries: asNumber(raw?.max_retries ?? raw?.maxRetries ?? raw?.MaxRetries, 0),
    retry_count: asNumber(raw?.retry_count ?? raw?.retryCount ?? raw?.RetryCount, 0),
    output_path: raw?.output_path || raw?.outputPath || raw?.OutputPath || null,
    error_message: raw?.error_message || raw?.errorMessage || raw?.ErrorMessage || null,
    summary:
      raw?.summary ||
      raw?.description ||
      raw?.Description ||
      parameters.note ||
      `${jobType} ist im Status ${status}.`,
    parameters,
    metrics: raw?.metrics || raw?.Metrics || {},
  };
}

function createEmptyRuntimeJobs() {
  return JOB_STATUSES.reduce((jobs, status) => {
    jobs[status] = [];
    return jobs;
  }, {});
}

export function normalizeRuntimeJobs(raw) {
  const normalized = createEmptyRuntimeJobs();
  const source = raw?.jobs || raw;

  if (Array.isArray(source)) {
    source.forEach((job, index) => {
      const normalizedJob = normalizeRuntimeJob(job, job?.status || 'pending', index);
      normalized[normalizedJob.status].push(normalizedJob);
    });

    return normalized;
  }

  JOB_STATUSES.forEach((status) => {
    const candidates = source?.[status] || source?.[`${status}_jobs`] || [];
    normalized[status] = Array.isArray(candidates)
      ? candidates.map((job, index) => normalizeRuntimeJob(job, status, index))
      : [];
  });

  return normalized;
}

export function normalizeRuntimeStorage(raw, runtimeHealth) {
  const totalDiskGb = asNumber(raw?.total_disk_gb ?? raw?.totalDiskGb, 0);
  const freeDiskGb = asNumber(
    runtimeHealth?.free_disk_gb,
    asNumber(raw?.free_disk_gb ?? raw?.freeDiskGb, 0),
  );
  const usedPercent = totalDiskGb
    ? clampNumber(Math.round(((totalDiskGb - freeDiskGb) / totalDiskGb) * 100))
    : clampNumber(raw?.used_percent ?? raw?.usedPercent, 0);
  const warningThresholdPercent = asNumber(
    raw?.warning_threshold_percent ?? raw?.warningThresholdPercent,
    75,
  );
  const criticalThresholdPercent = asNumber(
    raw?.critical_threshold_percent ?? raw?.criticalThresholdPercent,
    90,
  );

  return {
    summary: {
      root: asString(raw?.root, hermesDataRoot),
      freeDiskGb,
      totalDiskGb,
      usedPercent,
      warningThreshold: `${warningThresholdPercent}%`,
      criticalThreshold: `${criticalThresholdPercent}%`,
      warningThresholdPercent,
      criticalThresholdPercent,
      safeMode: Boolean(runtimeHealth?.safe_mode),
    },
    buckets: (raw?.buckets || []).map((bucket) => ({
      id: asString(bucket?.id, 'storage_bucket'),
      label: asString(bucket?.label, bucket?.id || 'Storage'),
      path: asString(bucket?.path, '-'),
      used: asString(bucket?.used, '-'),
      percent: clampNumber(bucket?.percent, 0),
      tone: asString(bucket?.tone, 'info'),
      detail: asString(bucket?.detail, ''),
    })),
    retentionRules: raw?.retention_rules || raw?.retentionRules || [],
    storageSafetyRules: raw?.safety_rules || raw?.safetyRules || [],
  };
}

export function normalizeFeatureVector(raw, index = 0) {
  return {
    id: raw?.id || `${raw?.symbol || 'UNKNOWN'}_feature_${index}`,
    timestamp_utc: raw?.timestamp_utc || raw?.timestampUtc || null,
    symbol: asString(raw?.symbol, 'UNKNOWN'),
    timeframe: asString(raw?.timeframe, '-'),
    session: asString(raw?.session, '-'),
    h4_regime: asString(raw?.h4_regime ?? raw?.h4Regime, '-'),
    h1_bias: asString(raw?.h1_bias ?? raw?.h1Bias, '-'),
    m15_setup: asString(raw?.m15_setup ?? raw?.m15Setup, '-'),
    m5_trigger: asString(raw?.m5_trigger ?? raw?.m5Trigger, '-'),
    adx: asNumber(raw?.adx, 0),
    atr: asNumber(raw?.atr, 0),
    rsi: asNumber(raw?.rsi, 0),
    structure_state: asString(raw?.structure_state ?? raw?.structureState, '-'),
    pattern_candidate: asString(raw?.pattern_candidate ?? raw?.patternCandidate, '-'),
    signal_score: asNumber(raw?.signal_score ?? raw?.signalScore, 0),
    spread: asNumber(raw?.spread, 0),
  };
}

export function normalizeSignalResult(raw, index = 0) {
  const reasonCodes = raw?.reason_codes || raw?.reasonCodes || [];

  return {
    id: raw?.id || `${raw?.symbol || 'UNKNOWN'}_signal_${index}`,
    timestamp_utc: raw?.timestamp_utc || raw?.timestampUtc || null,
    symbol: asString(raw?.symbol, 'UNKNOWN'),
    direction: asString(raw?.direction, 'neutral'),
    signal_type: asString(raw?.signal_type ?? raw?.signalType, '-'),
    score: asNumber(raw?.score, 0),
    confidence: asNumber(raw?.confidence, 0),
    theoretical_entry: asNumber(raw?.theoretical_entry ?? raw?.theoreticalEntry, 0),
    theoretical_stop: asNumber(raw?.theoretical_stop ?? raw?.theoreticalStop, 0),
    theoretical_target: asNumber(raw?.theoretical_target ?? raw?.theoreticalTarget, 0),
    reason_codes: Array.isArray(reasonCodes) ? reasonCodes.map(String) : [],
  };
}

export function normalizeBacktestReport(raw, index = 0) {
  return {
    run_id:
      raw?.run_id ||
      raw?.runId ||
      raw?.RunId ||
      `backtest_report_${index}`,
    symbol: asString(raw?.symbol ?? raw?.Symbol, 'UNKNOWN'),
    timeframe: asString(raw?.timeframe ?? raw?.Timeframe, '-'),
    strategy_name: asString(
      raw?.strategy_name ?? raw?.strategyName ?? raw?.StrategyName,
      '-',
    ),
    status: asString(raw?.status ?? raw?.Status, 'unknown'),
    started_at_utc:
      raw?.started_at_utc ||
      raw?.startedAtUtc ||
      raw?.StartedAtUtc ||
      null,
    completed_at_utc:
      raw?.completed_at_utc ||
      raw?.completedAtUtc ||
      raw?.CompletedAtUtc ||
      null,
    trade_count: asNumber(raw?.trade_count ?? raw?.tradeCount ?? raw?.TradeCount, 0),
    winrate: asNumber(raw?.winrate ?? raw?.Winrate, 0),
    profit_factor: asNumber(raw?.profit_factor ?? raw?.profitFactor ?? raw?.ProfitFactor, 0),
    max_drawdown: asNumber(raw?.max_drawdown ?? raw?.maxDrawdown ?? raw?.MaxDrawdown, 0),
    expectancy: asNumber(raw?.expectancy ?? raw?.Expectancy, 0),
    notes: asString(raw?.notes ?? raw?.Notes, ''),
    no_auto_trading: asBoolean(
      raw?.no_auto_trading ?? raw?.noAutoTrading ?? raw?.NoAutoTrading,
      true,
    ),
  };
}

export function normalizeOutcomeReport(raw, index = 0) {
  return {
    outcome_id:
      raw?.outcome_id ||
      raw?.outcomeId ||
      raw?.OutcomeId ||
      `outcome_report_${index}`,
    signal_id:
      raw?.signal_id ||
      raw?.signalId ||
      raw?.SignalId ||
      null,
    symbol: asString(raw?.symbol ?? raw?.Symbol, 'UNKNOWN'),
    timeframe: asString(raw?.timeframe ?? raw?.Timeframe, '-'),
    direction: asString(raw?.direction ?? raw?.Direction, 'neutral'),
    outcome_status: asString(
      raw?.outcome_status ?? raw?.outcomeStatus ?? raw?.OutcomeStatus,
      'unknown',
    ),
    hit_target: asBoolean(raw?.hit_target ?? raw?.hitTarget ?? raw?.HitTarget, false),
    hit_stop: asBoolean(raw?.hit_stop ?? raw?.hitStop ?? raw?.HitStop, false),
    expired: asBoolean(raw?.expired ?? raw?.Expired, false),
    invalidated: asBoolean(raw?.invalidated ?? raw?.Invalidated, false),
    mfe: asNumber(raw?.mfe ?? raw?.Mfe, 0),
    mae: asNumber(raw?.mae ?? raw?.Mae, 0),
    final_r: asNumber(raw?.final_r ?? raw?.finalR ?? raw?.FinalR, 0),
    evaluated_at_utc:
      raw?.evaluated_at_utc ||
      raw?.evaluatedAtUtc ||
      raw?.EvaluatedAtUtc ||
      null,
    notes: asString(raw?.notes ?? raw?.Notes, ''),
  };
}

export function normalizeBetaReport(raw) {
  const warnings = raw?.warnings || raw?.Warnings || [];
  const symbols = raw?.symbols_processed || raw?.symbolsProcessed || raw?.SymbolsProcessed || [];

  return {
    run_id: asString(raw?.run_id ?? raw?.runId ?? raw?.RunId, 'beta_learning_unknown'),
    status: asString(raw?.status ?? raw?.Status, 'unknown'),
    started_at_utc:
      raw?.started_at_utc ||
      raw?.startedAtUtc ||
      raw?.StartedAtUtc ||
      null,
    completed_at_utc:
      raw?.completed_at_utc ||
      raw?.completedAtUtc ||
      raw?.CompletedAtUtc ||
      null,
    symbols_processed: Array.isArray(symbols) ? symbols.map(String) : [],
    candles_processed: asNumber(
      raw?.candles_processed ?? raw?.candlesProcessed ?? raw?.CandlesProcessed,
      0,
    ),
    features_generated: asNumber(
      raw?.features_generated ?? raw?.featuresGenerated ?? raw?.FeaturesGenerated,
      0,
    ),
    signals_generated: asNumber(
      raw?.signals_generated ?? raw?.signalsGenerated ?? raw?.SignalsGenerated,
      0,
    ),
    outcomes_generated: asNumber(
      raw?.outcomes_generated ?? raw?.outcomesGenerated ?? raw?.OutcomesGenerated,
      0,
    ),
    backtests_generated: asNumber(
      raw?.backtests_generated ?? raw?.backtestsGenerated ?? raw?.BacktestsGenerated,
      0,
    ),
    warnings: Array.isArray(warnings) ? warnings.map(String) : [],
    duration_seconds: asNumber(
      raw?.duration_seconds ?? raw?.durationSeconds ?? raw?.DurationSeconds,
      0,
    ),
    learning_ready: asBoolean(
      raw?.learning_ready ?? raw?.learningReady ?? raw?.LearningReady,
      false,
    ),
    no_auto_trading: asBoolean(
      raw?.no_auto_trading ?? raw?.noAutoTrading ?? raw?.NoAutoTrading,
      true,
    ),
    human_review_required: asBoolean(
      raw?.human_review_required ?? raw?.humanReviewRequired ?? raw?.HumanReviewRequired,
      true,
    ),
    beta_report_path:
      raw?.beta_report_path ||
      raw?.betaReportPath ||
      raw?.BetaReportPath ||
      betaReportPath ||
      null,
    research_report_path:
      raw?.research_report_path ||
      raw?.researchReportPath ||
      raw?.ResearchReportPath ||
      null,
    feature_output_path:
      raw?.feature_output_path ||
      raw?.featureOutputPath ||
      raw?.FeatureOutputPath ||
      null,
    signal_output_path:
      raw?.signal_output_path ||
      raw?.signalOutputPath ||
      raw?.SignalOutputPath ||
      null,
    outcome_report_path:
      raw?.outcome_report_path ||
      raw?.outcomeReportPath ||
      raw?.OutcomeReportPath ||
      null,
    backtest_report_path:
      raw?.backtest_report_path ||
      raw?.backtestReportPath ||
      raw?.BacktestReportPath ||
      null,
  };
}

function parseJsonlRows(text, normalizeRow, warningPrefix) {
  const warnings = [];
  const items = text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .flatMap((line, index) => {
      try {
        return [normalizeRow(JSON.parse(line), index)];
      } catch (error) {
        warnings.push(warningFromError(`${warningPrefix} Zeile ${index + 1} nicht lesbar`, error));
        return [];
      }
    });

  return { items, warnings };
}

function latestTimestampFromRows(rows) {
  return rows
    .map((row) => row.timestamp_utc)
    .filter(Boolean)
    .sort((left, right) => Date.parse(right) - Date.parse(left))[0] || null;
}

function uniqueSymbolsFromRows(features, signals) {
  return [...new Set([...features, ...signals].map((row) => row.symbol).filter(Boolean))].sort();
}

function buildFeatureSignalExports(features, signals, exportFiles, dataSource, warnings = []) {
  const latestExportTimestamp = latestTimestampFromRows([...features, ...signals]);

  return {
    features,
    signals,
    exportFiles,
    symbols: uniqueSymbolsFromRows(features, signals),
    latestExportTimestamp,
    counts: {
      features: features.length,
      signals: signals.length,
    },
    status: dataSource === DATA_SOURCE.LIVE_FILE ? 'ready' : 'fixture',
    dataSource,
    warnings,
    sourcePath: [exportFiles.features, exportFiles.signals].filter(Boolean).join(' | '),
  };
}

function buildBacktestReports(reports, reportFiles, dataSource, warnings = []) {
  return {
    reports,
    reportFiles,
    counts: {
      reports: reports.length,
    },
    dataSource,
    warnings,
    sourcePath: reportFiles.filter(Boolean).join(' | '),
  };
}

function buildOutcomeReports(outcomes, reportFiles, dataSource, warnings = []) {
  return {
    outcomes,
    reportFiles,
    counts: {
      outcomes: outcomes.length,
      targetHits: outcomes.filter((outcome) => outcome.hit_target).length,
      stopHits: outcomes.filter((outcome) => outcome.hit_stop).length,
      expired: outcomes.filter((outcome) => outcome.expired).length,
      invalidated: outcomes.filter((outcome) => outcome.invalidated).length,
    },
    dataSource,
    warnings,
    sourcePath: reportFiles.filter(Boolean).join(' | '),
  };
}

function buildBetaReport(report, dataSource, warnings = [], sourcePath = '') {
  return {
    report,
    dataSource,
    warnings,
    sourcePath,
  };
}

function parseRuntimeJsonl(text) {
  const warnings = [];
  const items = text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .flatMap((line, index) => {
      try {
        return [normalizeRuntimeEvent(JSON.parse(line), index)];
      } catch (error) {
        warnings.push(warningFromError(`Runtime Event Zeile ${index + 1} nicht lesbar`, error));
        return [];
      }
    });

  return { items, warnings };
}

export function createRuntimeEventFallback(loadError = '') {
  return {
    items: runtimeEvents.map(normalizeRuntimeEvent),
    dataSource: DATA_SOURCE.FIXTURE,
    warnings: loadError ? [loadError] : [],
    sourcePath: 'src/fixtures/controlCenterMockData.ts',
  };
}

export function createRuntimeJobsFallback(loadError = '') {
  return {
    jobs: normalizeRuntimeJobs(runtimeJobsMock),
    dataSource: DATA_SOURCE.FIXTURE,
    warnings: loadError ? [loadError] : [],
    sourcePath: 'src/fixtures/runtimeJobsMock.ts',
  };
}

export function createRuntimeStorageFallback(loadError = '') {
  const runtimeHealth = normalizeRuntimeHealth(runtimeHealthMock, {
    label: de.common.fixtureFallback,
    url: 'src/fixtures/runtimeHealthMock.ts',
    readOnly: true,
  });

  return {
    ...normalizeRuntimeStorage(runtimeStorageMock, runtimeHealth),
    dataSource: DATA_SOURCE.FIXTURE,
    warnings: loadError ? [loadError] : [],
    sourcePath: 'src/fixtures/runtimeStorageMock.ts',
  };
}

export function createFeatureSignalExportsFallback(loadError = '') {
  const features = runtimeFeatureSignalExportsMock.features.map(normalizeFeatureVector);
  const signals = runtimeFeatureSignalExportsMock.signals.map(normalizeSignalResult);

  return buildFeatureSignalExports(
    features,
    signals,
    runtimeFeatureSignalExportsMock.export_files,
    DATA_SOURCE.FIXTURE,
    loadError ? [loadError] : [],
  );
}

export function createBacktestReportsFallback(loadError = '') {
  return buildBacktestReports(
    runtimeBacktestReportsMock.reports.map(normalizeBacktestReport),
    [...runtimeBacktestReportsMock.report_files],
    DATA_SOURCE.FIXTURE,
    loadError ? [loadError] : [],
  );
}

export function createOutcomeReportsFallback(loadError = '') {
  return buildOutcomeReports(
    runtimeOutcomeReportsMock.outcomes.map(normalizeOutcomeReport),
    [...runtimeOutcomeReportsMock.report_files],
    DATA_SOURCE.FIXTURE,
    loadError ? [loadError] : [],
  );
}

export function createBetaReportFallback(loadError = '') {
  return buildBetaReport(
    normalizeBetaReport(runtimeBetaStatusMock),
    DATA_SOURCE.FIXTURE,
    loadError ? [loadError] : [],
    'src/fixtures/runtimeBetaStatusMock.ts',
  );
}

function getRuntimeEventStoreUrl(timestampUtc) {
  if (!timestampUtc || !runtimeEventsBaseUrl) {
    return '';
  }

  const date = String(timestampUtc).slice(0, 10);
  return date ? `${runtimeEventsBaseUrl}/${date}.runtime.jsonl` : '';
}

async function enrichRuntimeHealthFiles(runtimeHealth) {
  const eventStoreActive =
    runtimeHealth.event_store_active ??
    (await probeReadOnlyFile(getRuntimeEventStoreUrl(runtimeHealth.timestamp_utc)));
  const replayManifestAvailable =
    runtimeHealth.replay_manifest_available ?? (await probeReadOnlyFile(replayManifestUrl));

  return {
    ...runtimeHealth,
    event_store_active: eventStoreActive,
    replay_manifest_available: replayManifestAvailable,
  };
}

function createFixtureRuntimeHealth(warning = '') {
  const source = buildSource(
    DATA_SOURCE.FIXTURE,
    'src/fixtures/runtimeHealthMock.ts',
    warning,
  );

  return {
    runtimeHealth: normalizeRuntimeHealth(runtimeHealthMock, {
      label: de.common.fixtureFallback,
      url: source.path,
      readOnly: true,
    }),
    source,
  };
}

function createFixtureSetupWatches(warning = '') {
  const source = buildSource(
    DATA_SOURCE.FIXTURE,
    'src/fixtures/setupWatchMock.ts',
    warning,
  );

  return {
    setupWatches: setupWatchMock.map(normalizeSetupWatch),
    source,
  };
}

async function loadRuntimeHealthEntry() {
  if (!runtimeHealthDevUrl) {
    return createFixtureRuntimeHealth('Runtime Health URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(runtimeHealthDevUrl);
    const raw = unwrapBridgeResponse(response);
    const source = buildSource(
      DATA_SOURCE.LIVE_FILE,
      runtimeHealthPath,
      bridgeResponseWarnings(response)[0] || '',
    );
    const runtimeHealth = normalizeRuntimeHealth(raw, {
      label: de.common.jsonSource,
      url: runtimeHealthDevUrl,
      readOnly: true,
    });

    return {
      runtimeHealth: await enrichRuntimeHealthFiles(runtimeHealth),
      source,
    };
  } catch (error) {
    return createFixtureRuntimeHealth(
      warningFromError('Runtime Health JSON nicht erreichbar', error),
    );
  }
}

async function loadSetupWatchesEntry() {
  if (!setupWatchUrl) {
    return createFixtureSetupWatches('Setup Watch URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(setupWatchUrl);
    const raw = unwrapBridgeResponse(response);
    const items = Array.isArray(raw) ? raw : raw?.candidates || raw?.setup_watches || [];

    return {
      setupWatches: items.map(normalizeSetupWatch),
      source: buildSource(
        DATA_SOURCE.LIVE_FILE,
        setupWatchPath,
        bridgeResponseWarnings(response)[0] || '',
      ),
    };
  } catch (error) {
    return createFixtureSetupWatches(
      warningFromError('Setup Watch JSON nicht erreichbar', error),
    );
  }
}

function buildRuntimeData(runtimeEntry, setupEntry) {
  const sources = {
    runtimeHealth: runtimeEntry.source,
    setupWatches: setupEntry.source,
  };
  const sourceList = Object.values(sources);

  return {
    runtimeHealth: runtimeEntry.runtimeHealth,
    setupWatches: setupEntry.setupWatches,
    dataSource: combineDataSource(sourceList),
    warnings: combineWarnings(sourceList),
    sources,
  };
}

export function createRuntimeDataFallback(loadError = '') {
  const runtimeEntry = createFixtureRuntimeHealth(loadError);
  const setupEntry = createFixtureSetupWatches(loadError);

  return buildRuntimeData(runtimeEntry, setupEntry);
}

export async function loadRuntimeData() {
  const [runtimeEntry, setupEntry] = await Promise.all([
    loadRuntimeHealthEntry(),
    loadSetupWatchesEntry(),
  ]);

  return buildRuntimeData(runtimeEntry, setupEntry);
}

export function createRuntimeHealthFallback(loadError = '') {
  const runtimeData = createRuntimeDataFallback(loadError);
  const source = runtimeData.sources.runtimeHealth;

  return {
    ...runtimeData,
    data: runtimeData.runtimeHealth,
    mode: source.dataSource === DATA_SOURCE.LIVE_FILE ? 'json' : 'fixture',
    warning: source.warnings[0] || '',
  };
}

export async function loadRuntimeHealth() {
  const runtimeData = await loadRuntimeData();
  const source = runtimeData.sources.runtimeHealth;

  return {
    ...runtimeData,
    data: runtimeData.runtimeHealth,
    mode: source.dataSource === DATA_SOURCE.LIVE_FILE ? 'json' : 'fixture',
    warning: source.warnings[0] || '',
  };
}

export function createSetupWatchFallback(loadError = '') {
  const runtimeData = createRuntimeDataFallback(loadError);
  const source = runtimeData.sources.setupWatches;

  return {
    ...runtimeData,
    items: runtimeData.setupWatches,
    mode: source.dataSource === DATA_SOURCE.LIVE_FILE ? 'json' : 'fixture',
    warning: source.warnings[0] || '',
    sourcePath: source.path,
  };
}

export async function loadSetupWatches() {
  const runtimeData = await loadRuntimeData();
  const source = runtimeData.sources.setupWatches;

  return {
    ...runtimeData,
    items: runtimeData.setupWatches,
    mode: source.dataSource === DATA_SOURCE.LIVE_FILE ? 'json' : 'fixture',
    warning: source.warnings[0] || '',
    sourcePath: source.path,
  };
}

export async function loadRuntimeEvents(timestampUtc, fallbackItems = runtimeEvents) {
  const url = getRuntimeEventStoreUrl(timestampUtc);

  if (!url) {
    return {
      items: fallbackItems.map(normalizeRuntimeEvent),
      dataSource: DATA_SOURCE.FIXTURE,
      warnings: ['Runtime Event URL ist nicht konfiguriert.'],
      sourcePath: 'src/fixtures/controlCenterMockData.ts',
    };
  }

  try {
    const text = await readTextReadOnly(url);
    const parsed = parseRuntimeJsonl(text);

    if (!parsed.items.length) {
      return createRuntimeEventFallback('Runtime Event JSONL enthaelt keine lesbaren Events.');
    }

    return {
      items: parsed.items,
      dataSource: DATA_SOURCE.LIVE_FILE,
      warnings: parsed.warnings,
      sourcePath: url,
    };
  } catch (error) {
    return {
      ...createRuntimeEventFallback(warningFromError('Runtime Events nicht erreichbar', error)),
      items: fallbackItems.map(normalizeRuntimeEvent),
    };
  }
}

export async function loadRuntimeTimelineEvents() {
  const runtimeData = await loadRuntimeData();
  const timestampUtc = runtimeData.runtimeHealth?.timestamp_utc;

  return loadRuntimeEvents(timestampUtc, runtimeEvents);
}

export async function loadRuntimeJobs() {
  if (!runtimeJobsUrl) {
    return createRuntimeJobsFallback('Runtime Jobs URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(runtimeJobsUrl);
    const raw = unwrapBridgeResponse(response);

    return {
      jobs: normalizeRuntimeJobs(raw),
      dataSource: DATA_SOURCE.LIVE_FILE,
      warnings: bridgeResponseWarnings(response),
      sourcePath: runtimeJobsPath,
    };
  } catch (error) {
    return createRuntimeJobsFallback(warningFromError('Runtime Jobs JSON nicht erreichbar', error));
  }
}

export async function loadRuntimeStorage() {
  const runtimeEntry = await loadRuntimeHealthEntry();

  return {
    ...normalizeRuntimeStorage(runtimeStorageMock, runtimeEntry.runtimeHealth),
    dataSource: runtimeEntry.source.dataSource,
    warnings: runtimeEntry.source.warnings,
    sourcePath: runtimeEntry.source.path,
  };
}

export async function loadFeatureSignalExports() {
  if (!featureExportUrl || !signalExportUrl) {
    return createFeatureSignalExportsFallback(
      'Feature-/Signal-Export URLs sind nicht konfiguriert.',
    );
  }

  try {
    const [featureText, signalText] = await Promise.all([
      readTextReadOnly(featureExportUrl),
      readTextReadOnly(signalExportUrl),
    ]);
    const featureRows = parseJsonlRows(featureText, normalizeFeatureVector, 'Feature Export');
    const signalRows = parseJsonlRows(signalText, normalizeSignalResult, 'Signal Export');

    if (!featureRows.items.length && !signalRows.items.length) {
      return createFeatureSignalExportsFallback(
        'Feature-/Signal-Export Dateien enthalten keine lesbaren Zeilen.',
      );
    }

    return buildFeatureSignalExports(
      featureRows.items,
      signalRows.items,
      {
        features: featureExportPath || featureExportUrl,
        signals: signalExportPath || signalExportUrl,
      },
      DATA_SOURCE.LIVE_FILE,
      [...featureRows.warnings, ...signalRows.warnings],
    );
  } catch (error) {
    return createFeatureSignalExportsFallback(
      warningFromError('Feature-/Signal-Exports nicht erreichbar', error),
    );
  }
}

export async function loadBacktestReports() {
  if (!backtestReportUrl) {
    return createBacktestReportsFallback('Backtest-Report URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(backtestReportUrl);
    const raw = unwrapBridgeResponse(response);
    const reports = Array.isArray(raw) ? raw : raw?.reports || [raw];
    const normalizedReports = reports.map(normalizeBacktestReport);

    if (!normalizedReports.length) {
      return createBacktestReportsFallback(
        'Backtest-Report Datei enthaelt keinen lesbaren Report.',
      );
    }

    return buildBacktestReports(
      normalizedReports,
      [backtestReportPath || backtestReportUrl],
      DATA_SOURCE.LIVE_FILE,
    );
  } catch (error) {
    return createBacktestReportsFallback(
      warningFromError('Backtest-Reports nicht erreichbar', error),
    );
  }
}

export async function loadOutcomeReports() {
  if (!outcomeReportUrl) {
    return createOutcomeReportsFallback('Outcome-Report URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(outcomeReportUrl);
    const raw = unwrapBridgeResponse(response);
    const outcomes = Array.isArray(raw) ? raw : raw?.outcomes || [raw];
    const normalizedOutcomes = outcomes.map(normalizeOutcomeReport);

    if (!normalizedOutcomes.length) {
      return createOutcomeReportsFallback(
        'Outcome-Report Datei enthaelt keine lesbaren Outcomes.',
      );
    }

    return buildOutcomeReports(
      normalizedOutcomes,
      [outcomeReportPath || outcomeReportUrl],
      DATA_SOURCE.LIVE_FILE,
    );
  } catch (error) {
    return createOutcomeReportsFallback(
      warningFromError('Outcome-Reports nicht erreichbar', error),
    );
  }
}

export async function loadBetaReport() {
  if (!betaReportUrl) {
    return createBetaReportFallback('Beta-Report URL ist nicht konfiguriert.');
  }

  try {
    const response = await readJsonReadOnly(betaReportUrl);
    const raw = unwrapBridgeResponse(response);
    const normalizedReport = normalizeBetaReport(raw);

    if (!normalizedReport.run_id || normalizedReport.status === 'unknown') {
      return createBetaReportFallback('Beta-Report enthaelt keinen lesbaren Lauf.');
    }

    return buildBetaReport(
      normalizedReport,
      DATA_SOURCE.LIVE_FILE,
      [],
      betaReportPath || betaReportUrl,
    );
  } catch (error) {
    return createBetaReportFallback(
      warningFromError('Beta-Report nicht erreichbar', error),
    );
  }
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function firstDefined(...values) {
  return values.find((value) => value !== undefined && value !== null);
}

function normalizeGoalProgressSummary(value) {
  if (!value) {
    return [];
  }

  if (Array.isArray(value)) {
    return value
      .map((item, index) => {
        if (typeof item === 'string') {
          return {
            goal_id: item,
            progress: 0,
          };
        }

        return {
          goal_id: asString(
            firstDefined(item.goal_id, item.goalId, item.id, item.key),
            `goal_${index}`,
          ),
          progress: clampNumber(
            firstDefined(item.progress, item.progress_score, item.progressScore, item.value),
            0,
            1,
          ),
          current_state: asString(firstDefined(item.current_state, item.currentState), ''),
          blocker_count: asNumber(firstDefined(item.blocker_count, item.blockerCount), 0),
        };
      })
      .filter((item) => item.goal_id);
  }

  if (typeof value === 'object') {
    return Object.entries(value)
      .map(([goalId, progress]) => ({
        goal_id: goalId,
        progress: clampNumber(progress, 0, 1),
      }))
      .sort((left, right) => right.progress - left.progress);
  }

  return [];
}

function formatHeartbeatAgeSeconds(timestampUtc) {
  if (!timestampUtc) {
    return null;
  }

  const parsed = Date.parse(timestampUtc);

  if (!Number.isFinite(parsed)) {
    return null;
  }

  return Math.max(0, Math.round((Date.now() - parsed) / 1000));
}

function formatUptimeMinutes(startedAtUtc, stoppedAtUtc = null) {
  if (!startedAtUtc) {
    return 0;
  }

  const started = Date.parse(startedAtUtc);
  const stopped = stoppedAtUtc ? Date.parse(stoppedAtUtc) : Date.now();

  if (!Number.isFinite(started) || !Number.isFinite(stopped) || stopped < started) {
    return 0;
  }

  return Math.round((stopped - started) / 60000);
}

function normalizeReportEntry(key, config, raw, dataSource, warning = '') {
  return {
    key,
    label: asString(config?.label, key),
    path: asString(config?.path, config?.url || ''),
    dataSource,
    available: dataSource === DATA_SOURCE.LIVE_FILE,
    warning,
    raw,
  };
}

function reportFixtureRaw(key) {
  switch (key) {
    case 'masterStatus':
      return runtimeMasterStatusMock;
    case 'researchInsights':
      return operatorDashboardMock.researchInsights;
    case 'regimeSummary':
      return operatorDashboardMock.regimeSummary;
    case 'strategyRegimePerformance':
      return operatorDashboardMock.strategyRegimePerformance;
    case 'supervisorState':
      return operatorDashboardMock.supervisorState;
    case 'schedulerState':
      return operatorDashboardMock.schedulerState;
    case 'resourceStatus':
      return operatorDashboardMock.resourceStatus;
    case 'cleanupPlan':
      return operatorDashboardMock.cleanupPlan;
    case 'nightlyState':
      return operatorDashboardMock.nightlyState;
    case 'robustStrategies':
      return {
        strategies: operatorDashboardMock.researchInsights.robust_strategies,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'overfitReport':
      return {
        overfit_suspected_strategies:
          operatorDashboardMock.researchInsights.overfit_suspected_strategies,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'regimeDistribution':
      return {
        dominant_regimes: operatorDashboardMock.regimeSummary.dominant_regimes,
        dominant_sessions: operatorDashboardMock.regimeSummary.dominant_sessions,
      };
    default:
      return {};
  }
}

export function normalizeMasterStatus(raw = {}) {
  const supervisor = raw.supervisor || raw.supervisor_status || raw.supervisorStatus || {};
  const scheduler = raw.scheduler || raw.scheduler_status || raw.schedulerStatus || {};
  const resource = raw.resource_status || raw.resourceStatus || {};
  const storage = raw.storage_status || raw.storageStatus || {};
  const trading = raw.trading_domain || raw.tradingDomain || {};
  const safety = raw.safety_flags || raw.safetyFlags || {};
  const goalProgress = normalizeGoalProgressSummary(
    firstDefined(raw.goal_progress_summary, raw.goalProgressSummary),
  );
  const activeGoals = asArray(firstDefined(raw.active_goals, raw.activeGoals)).map(String);
  const blockedGoals = asArray(firstDefined(raw.blocked_goals, raw.blockedGoals)).map(String);
  const topBlockers = asArray(firstDefined(raw.top_blockers, raw.topBlockers)).map(String);
  const rawWarnings = asArray(firstDefined(raw.warnings, raw.Warnings)).map(String);
  const goalWarnings = [
    ...asArray(firstDefined(raw.goal_warnings, raw.goalWarnings, raw.goal_blockers, raw.goalBlockers)).map(String),
    ...topBlockers.filter((item) => item.includes('goal') || item.includes('blocked_goal')),
    ...rawWarnings.filter((item) => item.includes('goal') || item.includes('blocked_goal')),
  ].filter(Boolean);
  const topGoal = asString(firstDefined(raw.top_goal, raw.topGoal), '');
  const goalSystemAvailable = Boolean(
    topGoal || activeGoals.length || blockedGoals.length || goalProgress.length,
  );

  return {
    overall_status: asString(firstDefined(raw.overall_status, raw.overallStatus), 'unknown'),
    current_focus: asString(firstDefined(raw.current_focus, raw.currentFocus), '-'),
    active_domains: asArray(firstDefined(raw.active_domains, raw.activeDomains)).map(String),
    goal_system_available: goalSystemAvailable,
    top_goal: topGoal || 'Goal-System noch nicht verfügbar',
    active_goals: activeGoals,
    blocked_goals: blockedGoals,
    goal_progress_summary: goalProgress,
    goal_warnings: goalWarnings,
    queued_tasks: asNumber(firstDefined(raw.queued_tasks, raw.queuedTasks), 0),
    last_nightly_run: firstDefined(raw.last_nightly_run, raw.lastNightlyRun, null),
    last_autonomous_loop: firstDefined(raw.last_autonomous_loop, raw.lastAutonomousLoop, null),
    last_meta_review: firstDefined(raw.last_meta_review, raw.lastMetaReview, null),
    learning_strategy: asString(firstDefined(raw.learning_strategy, raw.learningStrategy), '-'),
    supervisor_running: asBoolean(
      firstDefined(raw.supervisor_running, raw.supervisorRunning, supervisor.running),
      false,
    ),
    scheduler_enabled: asNumber(
      firstDefined(raw.scheduler_enabled, raw.schedulerEnabled, scheduler.enabled_jobs, scheduler.enabledJobs),
      0,
    ),
    resource_action: asString(
      firstDefined(raw.resource_action, raw.resourceAction, resource.action),
      '-',
    ),
    storage_cleanup: asNumber(
      firstDefined(
        raw.storage_cleanup,
        raw.storageCleanup,
        storage.cleanup_candidates,
        storage.cleanupCandidates,
      ),
      0,
    ),
    robust_strategies: asNumber(
      firstDefined(
        raw.robust_strategies,
        raw.robustStrategies,
        trading.robust_strategies,
        trading.robustStrategies,
      ),
      0,
    ),
    demo_bot_candidates: asNumber(
      firstDefined(
        raw.demo_bot_candidates,
        raw.demoBotCandidates,
        trading.demo_bot_candidates,
        trading.demoBotCandidates,
      ),
      0,
    ),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
    broker_orders_enabled: asBoolean(
      firstDefined(raw.broker_orders_enabled, raw.brokerOrdersEnabled, safety.broker_orders_enabled, safety.brokerOrdersEnabled),
      false,
    ),
    live_trading_enabled: asBoolean(
      firstDefined(raw.live_trading_enabled, raw.liveTradingEnabled, safety.live_trading_enabled, safety.liveTradingEnabled),
      false,
    ),
    top_blockers: topBlockers,
    next_recommended_actions: asArray(
      firstDefined(raw.next_recommended_actions, raw.nextRecommendedActions),
    ).map(String),
    updated_at_utc: firstDefined(raw.updated_at_utc, raw.updatedAtUtc, raw.last_updated_utc, raw.lastUpdatedUtc, null),
    data_root: asString(firstDefined(raw.data_root, raw.dataRoot), hermesDataRoot),
  };
}

export function normalizeSupervisorState(raw = {}) {
  const startedAtUtc = firstDefined(raw.started_at_utc, raw.startedAtUtc, raw.StartedAtUtc);
  const stoppedAtUtc = firstDefined(raw.stopped_at_utc, raw.stoppedAtUtc, raw.StoppedAtUtc);
  const heartbeatUtc = firstDefined(
    raw.heartbeat_utc,
    raw.heartbeatUtc,
    raw.last_heartbeat_utc,
    raw.lastHeartbeatUtc,
    raw.UpdatedUtc,
  );
  const running = asBoolean(
    firstDefined(raw.running, raw.currently_running, raw.currentlyRunning),
    false,
  );

  return {
    status: asString(raw.status ?? raw.Status, running ? 'running' : 'stopped'),
    running,
    pid: firstDefined(raw.pid, raw.process_id, raw.processId, raw.ProcessId, null),
    started_at_utc: startedAtUtc || null,
    stopped_at_utc: stoppedAtUtc || null,
    heartbeat_utc: heartbeatUtc || null,
    heartbeat_age_seconds: asNumber(
      firstDefined(
        raw.heartbeat_age_seconds,
        raw.heartbeatAgeSeconds,
        formatHeartbeatAgeSeconds(heartbeatUtc),
      ),
      0,
    ),
    uptime_minutes: asNumber(
      firstDefined(
        raw.uptime_minutes,
        raw.uptimeMinutes,
        formatUptimeMinutes(startedAtUtc, stoppedAtUtc),
      ),
      0,
    ),
    current_job: asString(
      firstDefined(raw.current_job, raw.currentJob, raw.current_job_id, raw.currentJobId),
      '-',
    ),
    next_action: asString(firstDefined(raw.next_action, raw.nextAction), '-'),
    jobs_started: asNumber(firstDefined(raw.jobs_started, raw.jobsStarted), 0),
    jobs_completed: asNumber(firstDefined(raw.jobs_completed, raw.jobsCompleted), 0),
    jobs_skipped: asNumber(firstDefined(raw.jobs_skipped, raw.jobsSkipped), 0),
    last_error: firstDefined(raw.last_error, raw.lastError, null),
    log_path: asString(firstDefined(raw.log_path, raw.logPath), supervisorLogPath),
    no_auto_trading: asBoolean(
      firstDefined(raw.no_auto_trading, raw.noAutoTrading),
      true,
    ),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
  };
}

export function normalizeSchedulerJobs(raw = []) {
  const source = Array.isArray(raw) ? raw : raw.jobs || raw.scheduled_jobs || raw.ScheduledJobs || [];

  return asArray(source).map((job, index) => ({
    job_id: asString(firstDefined(job.job_id, job.jobId, job.id), `scheduled_job_${index}`),
    job_type: asString(firstDefined(job.job_type, job.jobType, job.type), 'unknown'),
    enabled: asBoolean(job.enabled, true),
    status: asString(job.status, job.enabled === false ? 'disabled' : 'pending'),
    next_run_utc: firstDefined(job.next_run_utc, job.nextRunUtc, job.next_run, job.nextRun, null),
    last_run_utc: firstDefined(job.last_run_utc, job.lastRunUtc, job.last_run, job.lastRun, null),
    run_count: asNumber(firstDefined(job.run_count, job.runCount), 0),
    failure_count: asNumber(firstDefined(job.failure_count, job.failureCount), 0),
    skipped_count: asNumber(firstDefined(job.skipped_count, job.skippedCount), 0),
    currently_running: asBoolean(
      firstDefined(job.currently_running, job.currentlyRunning, job.running),
      false,
    ),
    last_action: firstDefined(job.last_action, job.lastAction, null),
    last_skipped_reason: firstDefined(job.last_skipped_reason, job.lastSkippedReason, null),
  }));
}

export function normalizeResourceStatus(raw = {}) {
  const freeDiskGb = asNumber(
    firstDefined(
      raw.free_disk_gb,
      raw.freeDiskGb,
      raw.free_disk_mb !== undefined ? asNumber(raw.free_disk_mb) / 1024 : undefined,
      raw.freeDiskMb !== undefined ? asNumber(raw.freeDiskMb) / 1024 : undefined,
    ),
    0,
  );

  return {
    cpu_usage_percent: clampNumber(firstDefined(raw.cpu_usage_percent, raw.cpuUsagePercent), 0),
    memory_usage_percent: clampNumber(
      firstDefined(raw.memory_usage_percent, raw.memoryUsagePercent, raw.ram_usage_percent),
      0,
    ),
    free_disk_gb: freeDiskGb,
    free_disk_percent: clampNumber(firstDefined(raw.free_disk_percent, raw.freeDiskPercent), 0),
    storage_root: asString(firstDefined(raw.storage_root, raw.storageRoot), hermesDataRoot),
    action: asString(firstDefined(raw.action, raw.recommended_action, raw.recommendedAction), '-'),
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
    errors: asArray(firstDefined(raw.errors, raw.Errors)).map(String),
    should_pause: asBoolean(firstDefined(raw.should_pause, raw.shouldPause), false),
    should_stop: asBoolean(firstDefined(raw.should_stop, raw.shouldStop), false),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
  };
}

export function normalizeNightlyState(raw = {}) {
  return {
    status: asString(raw.status, 'unknown'),
    current_state: asString(firstDefined(raw.current_state, raw.currentState, raw.status), 'unknown'),
    next_nightly_window: asString(
      firstDefined(raw.next_nightly_window, raw.nextNightlyWindow),
      '23:00-05:00',
    ),
    next_scheduled_start_utc:
      firstDefined(raw.next_scheduled_start_utc, raw.nextScheduledStartUtc, null),
    iterations_completed: asNumber(
      firstDefined(raw.iterations_completed, raw.iterationsCompleted),
      0,
    ),
    work_performed: asNumber(firstDefined(raw.work_performed, raw.workPerformed), 0),
    idle_iterations: asNumber(firstDefined(raw.idle_iterations, raw.idleIterations), 0),
    currently_running: asBoolean(firstDefined(raw.currently_running, raw.currentlyRunning), false),
    last_checkpoint_path:
      firstDefined(raw.last_checkpoint_path, raw.lastCheckpointPath, raw.checkpoint_path, null),
    next_action: asString(firstDefined(raw.next_action, raw.nextAction), '-'),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
  };
}

export function normalizeResearchSummary(
  researchInsights = {},
  regimeSummary = {},
  regimePerformance = {},
  robustReport = {},
  overfitReport = {},
) {
  const topStrategies = asArray(firstDefined(researchInsights.top_strategies, researchInsights.topStrategies));
  const robustStrategies = asArray(
    firstDefined(
      researchInsights.robust_strategies,
      researchInsights.robustStrategies,
      robustReport.strategies,
      robustReport.robust_strategies,
      robustReport.robustStrategies,
      Array.isArray(robustReport) ? robustReport : undefined,
    ),
  );
  const overfitStrategies = asArray(
    firstDefined(
      researchInsights.overfit_suspected_strategies,
      researchInsights.overfitSuspectedStrategies,
      researchInsights.overfit_suspected,
      overfitReport.overfit_suspected_strategies,
      overfitReport.overfitSuspectedStrategies,
      overfitReport.strategies,
      Array.isArray(overfitReport) ? overfitReport : undefined,
    ),
  );

  return {
    strategies_tested: asNumber(
      firstDefined(regimePerformance.strategies_analyzed, regimePerformance.strategiesAnalyzed),
      topStrategies.length,
    ),
    robust_strategies: robustStrategies.length,
    overfit_suspected: overfitStrategies.length,
    regime_distribution: asArray(
      firstDefined(regimeSummary.dominant_regimes, regimeSummary.dominantRegimes),
    ).map(String),
    best_regimes: asArray(firstDefined(researchInsights.best_regimes, researchInsights.bestRegimes)).map(String),
    weak_regimes: asArray(firstDefined(researchInsights.weak_regimes, researchInsights.weakRegimes)).map(String),
    preferred_sessions: asArray(
      firstDefined(researchInsights.preferred_sessions, researchInsights.preferredSessions),
    ).map(String),
    latest_insights: [
      ...asArray(firstDefined(researchInsights.best_regimes, researchInsights.bestRegimes)).slice(0, 2),
      ...asArray(firstDefined(researchInsights.weak_regimes, researchInsights.weakRegimes)).slice(0, 2),
      ...asArray(firstDefined(researchInsights.volatility_preference, researchInsights.volatilityPreference)).slice(0, 2),
    ].map(String),
    regime_consistency_score: asNumber(
      firstDefined(
        researchInsights.regime_consistency_score,
        researchInsights.regimeConsistencyScore,
        regimePerformance.regime_consistency_score,
      ),
      0,
    ),
  };
}

export function normalizeCleanupPlan(raw = {}) {
  const candidates = asArray(firstDefined(raw.candidates, raw.cleanup_candidates, raw.cleanupCandidates));

  return {
    candidates,
    candidate_count: asNumber(firstDefined(raw.candidate_count, raw.candidateCount), candidates.length),
    estimated_bytes_to_free: asNumber(
      firstDefined(raw.estimated_bytes_to_free, raw.estimatedBytesToFree),
      0,
    ),
    safe_to_apply: asBoolean(firstDefined(raw.safe_to_apply, raw.safeToApply), false),
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
  };
}

function buildOperatorDashboard(rawReports, reports, logLines, dataSource, warnings = []) {
  const masterRaw = rawReports.masterStatus || runtimeMasterStatusMock;
  const supervisorRaw = rawReports.supervisorState || operatorDashboardMock.supervisorState;
  const schedulerRaw = rawReports.schedulerState || operatorDashboardMock.schedulerState;
  const resourceRaw = rawReports.resourceStatus || operatorDashboardMock.resourceStatus;
  const storageRaw = rawReports.storageStatus || {};
  const nightlyRaw = rawReports.nightlyState || operatorDashboardMock.nightlyState;
  const insightsRaw = rawReports.researchInsights || operatorDashboardMock.researchInsights;
  const robustRaw = rawReports.robustStrategies || {};
  const overfitRaw = rawReports.overfitReport || {};
  const regimeSummaryRaw = rawReports.regimeSummary || operatorDashboardMock.regimeSummary;
  const regimePerformanceRaw =
    rawReports.strategyRegimePerformance || operatorDashboardMock.strategyRegimePerformance;
  const cleanupRaw = rawReports.cleanupPlan || operatorDashboardMock.cleanupPlan;

  const resource = normalizeResourceStatus(resourceRaw);
  const cleanup = normalizeCleanupPlan(cleanupRaw);
  const storageRoot = asString(
    firstDefined(
      storageRaw.storage_root,
      storageRaw.storageRoot,
      storageRaw.root,
      resource.storage_root,
      cleanupRaw.storage_root,
      cleanupRaw.storageRoot,
    ),
    hermesDataRoot,
  );
  const storageFreeDiskGb = asNumber(
    firstDefined(storageRaw.free_disk_gb, storageRaw.freeDiskGb, resource.free_disk_gb),
    resource.free_disk_gb,
  );
  const liveReportCount = reports.filter((report) => report.dataSource === DATA_SOURCE.LIVE_FILE)
    .length;
  const fixtureReportCount = reports.length - liveReportCount;
  const masterStatusReport = reports.find((report) => report.key === 'masterStatus');

  return {
    masterStatus: normalizeMasterStatus(masterRaw),
    masterStatusSource: masterStatusReport?.dataSource || (rawReports.masterStatus ? dataSource : DATA_SOURCE.FIXTURE),
    masterStatusWarning: masterStatusReport?.warning || '',
    supervisor: normalizeSupervisorState(supervisorRaw),
    schedulerJobs: normalizeSchedulerJobs(schedulerRaw),
    resource,
    nightly: normalizeNightlyState(nightlyRaw),
    research: normalizeResearchSummary(
      insightsRaw,
      regimeSummaryRaw,
      regimePerformanceRaw,
      robustRaw,
      overfitRaw,
    ),
    storage: {
      root: storageRoot,
      status: asString(firstDefined(storageRaw.status, storageRaw.state), resource.action),
      free_disk_gb: storageFreeDiskGb,
      cleanup_candidate_count: cleanup.candidate_count,
      estimated_bytes_to_free: cleanup.estimated_bytes_to_free,
      warnings: [
        ...resource.warnings,
        ...cleanup.warnings,
        ...asArray(firstDefined(storageRaw.warnings, storageRaw.Warnings)).map(String),
      ],
      errors: resource.errors,
      cleanup_safe_to_apply: cleanup.safe_to_apply,
    },
    cleanup,
    reports,
    logLines,
    dataSource,
    bridgeAvailable: dataSource === DATA_SOURCE.LIVE_FILE,
    bridgeUrl: operatorDashboardUrl || '',
    lastUpdatedAt: new Date().toISOString(),
    pollIntervalSeconds: 45,
    liveReportCount,
    fixtureReportCount,
    warnings,
    no_auto_trading: true,
    human_review_required: true,
  };
}

export function createOperatorDashboardFallback(loadError = '') {
  const configs = operatorReportsConfig || {};
  const reports = Object.entries(configs).map(([key, config]) =>
    normalizeReportEntry(
      key,
      config,
      reportFixtureRaw(key),
      DATA_SOURCE.FIXTURE,
      loadError,
    ),
  );

  return buildOperatorDashboard(
    {
      masterStatus: runtimeMasterStatusMock,
      supervisorState: operatorDashboardMock.supervisorState,
      schedulerState: operatorDashboardMock.schedulerState,
      resourceStatus: operatorDashboardMock.resourceStatus,
      nightlyState: operatorDashboardMock.nightlyState,
      researchInsights: operatorDashboardMock.researchInsights,
      regimeSummary: operatorDashboardMock.regimeSummary,
      strategyRegimePerformance: operatorDashboardMock.strategyRegimePerformance,
      cleanupPlan: operatorDashboardMock.cleanupPlan,
    },
    reports,
    [...operatorDashboardMock.logLines],
    DATA_SOURCE.FIXTURE,
    loadError ? [loadError] : [],
  );
}

async function loadOperatorReport(key, config) {
  try {
    const response = await readJsonReadOnly(config?.url);
    const raw = unwrapBridgeResponse(response);
    const warnings = bridgeResponseWarnings(response);
    return normalizeReportEntry(
      key,
      config,
      raw || reportFixtureRaw(key),
      raw ? DATA_SOURCE.LIVE_FILE : DATA_SOURCE.FIXTURE,
      warnings[0] || '',
    );
  } catch (error) {
    const warning = warningFromError(`${config?.label || key} nicht erreichbar`, error);
    return normalizeReportEntry(key, config, reportFixtureRaw(key), DATA_SOURCE.FIXTURE, warning);
  }
}

export async function loadOperatorDashboard() {
  let bridgeDashboardWarning = '';
  const configs = operatorReportsConfig || {};
  const masterStatusConfig = configs.masterStatus;
  const masterStatusEntry = masterStatusConfig
    ? await loadOperatorReport('masterStatus', masterStatusConfig)
    : null;

  if (operatorDashboardUrl) {
    try {
      const response = await readJsonReadOnly(operatorDashboardUrl);
      const dashboard = unwrapBridgeResponse(response) || {};
      const warnings = bridgeResponseWarnings(response);
      const rawReports = {
        masterStatus: masterStatusEntry?.raw,
        supervisorState: dashboard.supervisorState,
        schedulerState: dashboard.schedulerState,
        resourceStatus: dashboard.resourceStatus,
        storageStatus: dashboard.storageStatus,
        cleanupPlan: dashboard.cleanupPlan,
        nightlyState: dashboard.nightlyState,
        researchInsights: dashboard.researchInsights,
        robustStrategies: dashboard.robustStrategies,
        overfitReport: dashboard.overfitReport,
        regimeSummary: dashboard.regimeSummary,
        strategyRegimePerformance: dashboard.strategyRegimePerformance,
        regimeDistribution: dashboard.regimeDistribution,
      };
      const reportIndex = dashboard.reportIndex?.reports || [];
      const reports = Object.entries(operatorReportsConfig || {}).map(([key, config]) => {
        if (key === 'masterStatus' && masterStatusEntry) {
          return masterStatusEntry;
        }

        const indexEntry = reportIndex.find((entry) => entry.key === key);
        const raw = rawReports[key] || reportFixtureRaw(key);
        const available = Boolean(indexEntry?.available && rawReports[key]);
        return normalizeReportEntry(
          key,
          {
            ...config,
            path: indexEntry?.endpoint || config?.path,
          },
          raw,
          available ? DATA_SOURCE.LIVE_FILE : DATA_SOURCE.FIXTURE,
          available ? '' : `${config?.label || key} nicht in der Read-only Bridge verfuegbar.`,
        );
      });

      const dataSource = reports.some((report) => report.dataSource === DATA_SOURCE.LIVE_FILE)
        ? DATA_SOURCE.LIVE_FILE
        : DATA_SOURCE.FIXTURE;

      return buildOperatorDashboard(
        rawReports,
        reports,
        [...operatorDashboardMock.logLines],
        dataSource,
        warnings,
      );
    } catch (error) {
      bridgeDashboardWarning = warningFromError('Read-only Bridge nicht erreichbar', error);
    }
  }

  const reportEntries = await Promise.all(
    Object.entries(configs)
      .filter(([key]) => key !== 'masterStatus')
      .map(([key, config]) => loadOperatorReport(key, config)),
  );
  const allReportEntries = masterStatusEntry ? [masterStatusEntry, ...reportEntries] : reportEntries;
  const rawReports = reportEntries.reduce((next, report) => {
    next[report.key] = report.raw;
    return next;
  }, {});
  if (masterStatusEntry) {
    rawReports.masterStatus = masterStatusEntry.raw;
  }
  let logLines = [...operatorDashboardMock.logLines];
  const warnings = [
    bridgeDashboardWarning,
    ...allReportEntries.flatMap((report) => (report.warning ? [report.warning] : [])),
  ].filter(Boolean);

  if (supervisorLogUrl) {
    try {
      const logText = await readTextReadOnly(supervisorLogUrl);
      logLines = logText
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean)
        .slice(-10);
    } catch (error) {
      warnings.push(warningFromError('Supervisor-Log nicht erreichbar', error));
    }
  }

  const dataSource = allReportEntries.some((report) => report.dataSource === DATA_SOURCE.LIVE_FILE)
    ? allReportEntries.every((report) => report.dataSource === DATA_SOURCE.LIVE_FILE)
      ? DATA_SOURCE.LIVE_FILE
      : DATA_SOURCE.FIXTURE
    : DATA_SOURCE.FIXTURE;

  return buildOperatorDashboard(rawReports, allReportEntries, logLines, dataSource, warnings);
}

export const runtimeDataAdapter = {
  loadRuntimeData,
  loadRuntimeHealth,
  loadSetupWatches,
  loadRuntimeEvents,
  loadRuntimeTimelineEvents,
  loadRuntimeJobs,
  loadRuntimeStorage,
  loadFeatureSignalExports,
  loadBacktestReports,
  loadOutcomeReports,
  loadBetaReport,
  loadOperatorDashboard,
  createRuntimeDataFallback,
  createRuntimeHealthFallback,
  createSetupWatchFallback,
  createRuntimeEventFallback,
  createRuntimeJobsFallback,
  createRuntimeStorageFallback,
  createFeatureSignalExportsFallback,
  createBacktestReportsFallback,
  createOutcomeReportsFallback,
  createBetaReportFallback,
  createOperatorDashboardFallback,
};
