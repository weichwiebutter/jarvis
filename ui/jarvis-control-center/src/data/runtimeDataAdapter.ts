import { runtimeBacktestReportsMock } from '../fixtures/runtimeBacktestReportsMock';
import { runtimeBetaStatusMock } from '../fixtures/runtimeBetaStatusMock';
import { runtimeFeatureSignalExportsMock } from '../fixtures/runtimeFeatureSignalExportsMock';
import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';
import { runtimeJobsMock } from '../fixtures/runtimeJobsMock';
import { runtimeOutcomeReportsMock } from '../fixtures/runtimeOutcomeReportsMock';
import { runtimeStorageMock } from '../fixtures/runtimeStorageMock';
import { setupWatchMock } from '../fixtures/setupWatchMock';
import { operatorDashboardMock } from '../fixtures/operatorDashboardMock';
import { runtimeHumanReviewMock } from '../fixtures/runtimeHumanReviewMock';
import { runtimeMasterStatusMock } from '../fixtures/runtimeMasterStatusMock';
import { runtimeEvents } from '../fixtures/controlCenterMockData';
import { systemBHandoffBundleMock } from '../fixtures/controlCenterMockData';
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

function normalizeTimeControlWindow(raw = {}, label = 'Fenster') {
  return {
    label: asString(firstDefined(raw.label, raw.name), label),
    enabled: asBoolean(firstDefined(raw.enabled, raw.is_enabled, raw.isEnabled), true),
    start: asString(firstDefined(raw.start, raw.start_time, raw.startTime), '00:00'),
    end: asString(firstDefined(raw.end, raw.end_time, raw.endTime), '00:00'),
    active_now: asBoolean(firstDefined(raw.active_now, raw.activeNow), false),
    summary: asString(firstDefined(raw.summary, raw.status), 'inaktiv'),
  };
}

export function normalizeTimeControl(raw = {}) {
  return {
    config_path: asString(firstDefined(raw.config_path, raw.configPath), 'config/schedules.json'),
    time_zone: asString(firstDefined(raw.time_zone, raw.timeZone), 'Europe/Berlin'),
    current_utc: firstDefined(raw.current_utc, raw.currentUtc, null),
    current_local: firstDefined(raw.current_local, raw.currentLocal, null),
    status_label: asString(
      firstDefined(raw.status_label, raw.statusLabel),
      asBoolean(firstDefined(raw.in_work_window, raw.inWorkWindow), false)
        ? 'Derzeit im Arbeitsfenster'
        : 'Außerhalb des Arbeitsfensters',
    ),
    in_work_window: asBoolean(firstDefined(raw.in_work_window, raw.inWorkWindow), false),
    work_window: normalizeTimeControlWindow(firstDefined(raw.work_window, raw.workWindow) || {}, 'Arbeitszeit'),
    nightly_window: normalizeTimeControlWindow(firstDefined(raw.nightly_window, raw.nightlyWindow) || {}, 'Nightly'),
    learning_window: normalizeTimeControlWindow(firstDefined(raw.learning_window, raw.learningWindow) || {}, 'Lernfenster'),
    human_review_window: normalizeTimeControlWindow(
      firstDefined(raw.human_review_window, raw.humanReviewWindow) || {},
      'Human-Review',
    ),
    weekdays: asArray(firstDefined(raw.weekdays, raw.weekDays)).map((item) => ({
      day: asString(firstDefined(item.day, item.Day, item.label), 'unknown'),
      active: asBoolean(firstDefined(item.active, item.is_active, item.isActive), false),
    })),
    active_weekdays: asArray(firstDefined(raw.active_weekdays, raw.activeWeekdays)).map(String),
    inactive_weekdays: asArray(firstDefined(raw.inactive_weekdays, raw.inactiveWeekdays)).map(String),
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
    safety_flags: asArray(firstDefined(raw.safety_flags, raw.safetyFlags)).map(String),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
  };
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
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

function normalizeDistribution(value) {
  if (!value) {
    return [];
  }

  if (Array.isArray(value)) {
    return value
      .map((item, index) => {
        if (typeof item === 'string') {
          const [label, count] = item.split(':');
          return {
            label: asString(label, `verteilung_${index}`).trim(),
            count: asNumber(count, 0),
          };
        }

        return {
          label: asString(firstDefined(item.label, item.key, item.status, item.name), `verteilung_${index}`),
          count: asNumber(firstDefined(item.count, item.value, item.total), 0),
        };
      })
      .filter((item) => item.label);
  }

  if (typeof value === 'object') {
    return Object.entries(value).map(([label, count]) => ({
      label,
      count: asNumber(count, 0),
    }));
  }

  return [];
}

function domainTitle(domain) {
  switch (String(domain || '').toLowerCase()) {
    case 'trading':
      return 'Trading';
    case 'software':
      return 'Software';
    case 'documentation':
      return 'Dokumentation';
    case 'process':
      return 'Prozesse';
    case 'research':
      return 'Recherche';
    default:
      return asString(domain, 'Domäne');
  }
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
    case 'humanReviewQueue':
      return runtimeHumanReviewMock;
    case 'knowledgeValidationAudit':
      return {
        report_version: 'knowledge_validation_audit_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        validation_completion_label: '87% abgeschlossen',
        validation_completion_percent: 87,
        validation_tasks_pending: 33,
        open_validations: 33,
        critical_knowledge_gaps: 3,
        queue_items_open: 33,
        queue_items_processed: 12,
        validation_queue_exists: true,
        validation_queue_filled: true,
        validation_queue_processed: true,
        oldest_open_validation_age_days: 14,
        human_review_pending_reviews: 0,
        human_review_needs_more_evidence_reviews: 20,
        human_review_deferred_reviews: 0,
        human_review_needs_more_evidence_domains: ['documentation', 'trading'],
        validation_tasks_created_last_run: 33,
        evidence_tasks_executed_last_run: 12,
        needs_more_evidence_before: 20,
        needs_more_evidence_after: 20,
        frank_action_required: false,
        missing_automation_jobs: ['collect_evidence', 'generate_validation_plans', 'validate_knowledge_items', 'execute_validation_tasks'],
        missing_queues: ['validation_queue', 'evidence_queue'],
        next_recommended_commands: ['collect_evidence', 'generate_validation_plans', 'validate_knowledge_items', 'execute_validation_tasks'],
        operator_summary: 'Hermes sammelt weitere Evidenz. Frank muss nichts tun.',
        affected_domains: ['trading', 'knowledge'],
        domain_breakdown: [
          { domain: 'trading', open_plans: 21, open_queue_items: 21, open_knowledge_items: 2, oldest_open_validation_age_days: 14 },
          { domain: 'knowledge', open_plans: 12, open_queue_items: 12, open_knowledge_items: 1, oldest_open_validation_age_days: 9 },
        ],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'knowledgeConsolidationAnalyzer':
      return {
        report_version: 'knowledge_consolidation_analyzer_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        total_knowledge_items: 138,
        raw_observation_count: 532,
        raw_hypothesis_count: 32,
        raw_research_result_count: 64,
        cluster_count: 14,
        duplicate_count: 121,
        consolidatable_group_count: 14,
        active_item_count: 411,
        archived_potential_count: 121,
        redundant_item_count: 121,
        trusted_knowledge_items: 0,
        weak_knowledge_items: 73,
        domains: ['trading', 'research', 'software', 'process', 'documentation'],
        warnings: [],
        operator_summary: '532 Einträge beschreiben 14 ähnliche Muster. Hermes kann Muster verdichten, Frank muss nichts freigeben.',
        cleanup_potential_summary: '121 Einträge könnten später archiviert werden; 121 Einträge wirken redundant; 411 Einträge werden aktiv genutzt.',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        clusters: [
          { cluster_id: 'cluster_trading_breakout', domain: 'trading', pattern_description: 'Breakout- und Continuation-Muster', normalized_signature: 'trading_breakout', raw_item_count: 214, knowledge_item_count: 12, hypothesis_count: 8, observation_count: 194, duplicate_count: 193, consolidatable_count: 17, average_trust_score: 0.62, average_evidence_score: 0.68, average_validation_score: 0.54, confidence_score: 0.59, validation_state: 'teilvalidiert', trust_state: 'mittel', next_action: 'Trading-Beobachtungen verdichten', rule_candidate_summary: 'Musterkandidat aus 214 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
          { cluster_id: 'cluster_trading_reversal', domain: 'trading', pattern_description: 'Reversal- und Candle-Muster', normalized_signature: 'trading_reversal', raw_item_count: 176, knowledge_item_count: 10, hypothesis_count: 6, observation_count: 160, duplicate_count: 159, consolidatable_count: 12, average_trust_score: 0.6, average_evidence_score: 0.64, average_validation_score: 0.53, confidence_score: 0.57, validation_state: 'teilvalidiert', trust_state: 'mittel', next_action: 'Trading-Beobachtungen verdichten', rule_candidate_summary: 'Musterkandidat aus 176 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
          { cluster_id: 'cluster_docs_runtime', domain: 'documentation', pattern_description: 'Runtime- und Architektur-Dokumentation', normalized_signature: 'documentation_runtime', raw_item_count: 54, knowledge_item_count: 8, hypothesis_count: 0, observation_count: 46, duplicate_count: 45, consolidatable_count: 6, average_trust_score: 0.58, average_evidence_score: 0.67, average_validation_score: 0.52, confidence_score: 0.56, validation_state: 'teilvalidiert', trust_state: 'mittel', next_action: 'Dokumentationsfunde verdichten', rule_candidate_summary: 'Musterkandidat aus 54 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
          { cluster_id: 'cluster_research_robustness', domain: 'research', pattern_description: 'Research- und Robustness-Muster', normalized_signature: 'research_robustness', raw_item_count: 52, knowledge_item_count: 5, hypothesis_count: 4, observation_count: 43, duplicate_count: 42, consolidatable_count: 4, average_trust_score: 0.57, average_evidence_score: 0.6, average_validation_score: 0.5, confidence_score: 0.55, validation_state: 'offen', trust_state: 'mittel', next_action: 'Research-Hypothesen verdichten', rule_candidate_summary: 'Musterkandidat aus 52 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
          { cluster_id: 'cluster_process_queue', domain: 'process', pattern_description: 'Prozess- und Queue-Muster', normalized_signature: 'process_queue', raw_item_count: 18, knowledge_item_count: 3, hypothesis_count: 2, observation_count: 13, duplicate_count: 12, consolidatable_count: 2, average_trust_score: 0.55, average_evidence_score: 0.58, average_validation_score: 0.5, confidence_score: 0.53, validation_state: 'offen', trust_state: 'mittel', next_action: 'Prozesswissen gruppieren', rule_candidate_summary: 'Musterkandidat aus 18 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
          { cluster_id: 'cluster_generic_support', domain: 'software', pattern_description: 'Software- und Infrastrukturwissen', normalized_signature: 'software_support', raw_item_count: 18, knowledge_item_count: 7, hypothesis_count: 2, observation_count: 9, duplicate_count: 9, consolidatable_count: 4, average_trust_score: 0.56, average_evidence_score: 0.59, average_validation_score: 0.51, confidence_score: 0.54, validation_state: 'offen', trust_state: 'mittel', next_action: 'Softwarewissen verdichten', rule_candidate_summary: 'Musterkandidat aus 18 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel', frank_required: false, safe_to_execute: true, item_ids: [], item_titles: [], sample_sources: [] },
        ],
      };
    case 'knowledgeConsolidationExecutor':
      return {
        report_version: 'knowledge_consolidation_executor_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        analyzer_cluster_count: 240,
        candidates_prepared_count: 97,
        raw_items_count: 115941,
        duplicate_items_count: 115839,
        consolidatable_group_count: 97,
        trusted_knowledge_items: 0,
        weak_knowledge_items: 73,
        domains: ['trading', 'research', 'software', 'process', 'documentation'],
        warnings: [],
        operator_summary: '240 Muster erkannt. Davon wurden 97 als Konsolidierungs-Kandidaten vorbereitet. Frank muss nichts freigeben. Keine Rohdaten wurden gelöscht.',
        safety_summary: 'no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        candidates: [
          {
            consolidation_candidate_id: 'consolidation_cluster_trading_breakout',
            domain: 'trading',
            title: 'Breakout- und Continuation-Muster',
            summary: 'Musterkandidat aus 214 Einträgen · Vertrauen mittel · Evidenz mittel · Validierung mittel',
            pattern_description: 'Breakout- und Continuation-Muster',
            supporting_items_count: 214,
            duplicate_items_count: 193,
            evidence_strength: 0.61,
            validation_status: 'teilvalidiert',
            trust_baseline: 0.62,
            risk_notes: 'Dubletten nur als Kandidat verdichtet, keine Löschung; Validierung noch nicht stark genug',
            recommended_next_action: 'Pattern-Regel als Review-Kandidat vorbereiten',
            frank_required: false,
            item_ids: [],
            item_titles: [],
            sample_sources: [],
          },
        ],
      };
    case 'strategyMutationAnalyzer':
      return {
        report_version: 'strategy_mutation_analyzer_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        patterns_analyzed: 6,
        mutations_prepared: 20,
        candidate_count: 20,
        knowledge_items_analyzed: 138,
        review_items_analyzed: 20,
        research_entries_analyzed: 115941,
        domains: ['trading'],
        warnings: [],
        operator_summary: '6 Muster analysiert. 20 Mutationen vorbereitet. Frank nötig: nein.',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        patterns: [
          {
            pattern_id: 'ema_pullback',
            pattern_name: 'EMA Pullback',
            domain: 'trading',
            pattern_description: 'Pullback into EMA area with continuation confirmation.',
            parameters_available: ['EMA20', 'EMA50', 'EMA100', 'EMA200', 'ATR7', 'ATR14', 'ATR21', 'ATR28', 'SL1ATR', 'SL1.5ATR', 'SL2ATR', 'TP1R', 'TP1.5R', 'TP2R', 'TP3R', 'London', 'New York', 'London + New York'],
            parameters_variations: ['EMA20 + ATR14', 'EMA20 + ATR21', 'EMA50 + ATR14', 'EMA50 + ATR21'],
            supporting_signals: ['london', 'london_new_york_overlap'],
            mutation_count: 4,
          },
        ],
        candidates: [
          {
            mutation_id: 'mutation_ema_pullback_ema20_atr14',
            source_pattern: 'EMA Pullback',
            parameter_changes: ['EMA20 + ATR14'],
            expected_benefit: 'Bessere EMA-/ATR-Filterung und stabilere Pullback-Selektion',
            validation_required: true,
            oos_required: true,
            forward_observation_required: true,
            trust_baseline: 0.55,
          },
        ],
      };
    case 'strategyParameterResearchPlanner':
      return {
        report_version: 'strategy_parameter_research_planner_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        patterns_analyzed: 6,
        mutations_prepared: 24,
        candidate_count: 24,
        knowledge_items_analyzed: 138,
        setup_candidates_analyzed: 8,
        certified_candidates_analyzed: 12,
        forward_observations_analyzed: 24,
        review_items_analyzed: 20,
        research_entries_analyzed: 115941,
        domains: ['trading'],
        warnings: [],
        operator_summary: '6 Muster analysiert. Begründete Parameterbereiche vorbereitet. Frank nötig: nein.',
        safety_summary: 'no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        patterns: [
          {
            pattern_id: 'ema_pullback',
            pattern_name: 'EMA Pullback',
            domain: 'trading',
            pattern_description: 'Pullback into EMA area with continuation confirmation.',
            asset_contexts: ['EURUSD', 'XAUUSD'],
            timeframe_contexts: ['M15', 'H1'],
            session_contexts: ['London', 'London + New York'],
            suggested_ranges: [
              { name: 'EMA', values: ['20', '50', '100'], reason: 'EMA Pullback reagiert auf Trend-/Pullback-Bereiche.' },
              { name: 'ATR', values: ['14', '21', '28'], reason: 'Moderate Volatilitätsanpassung ist robust.' },
              { name: 'Stop', values: ['1 ATR', '1.5 ATR', '2 ATR'], reason: 'Stops orientieren sich an Trendtiefe.' },
              { name: 'Take Profit', values: ['1.5R', '2R', '3R'], reason: 'Pullbacks erlauben höhere RR-Bereiche.' },
            ],
            mutation_count: 12,
            evidence_basis: 'Certified candidates, forward observations and research memory indicate liquid trend/pullback contexts.',
          },
        ],
        candidates: [
          {
            mutation_id: 'mutation_plan_ema_pullback_ema',
            source_pattern: 'EMA Pullback',
            domain: 'trading',
            pattern_description: 'Pullback into EMA area with continuation confirmation.',
            parameter_ranges: [
              { name: 'EMA', values: ['20', '50', '100'], reason: 'EMA Pullback reagiert auf Trend-/Pullback-Bereiche.' },
              { name: 'ATR', values: ['14', '21', '28'], reason: 'Moderate Volatilitätsanpassung ist robust.' },
            ],
            asset_context: 'EURUSD, XAUUSD',
            timeframe_context: 'M15, H1',
            expected_benefit: 'EMA-/ATR-Bereiche auf reale Markt- und Setup-Kontexte abstimmen',
            trust_baseline: 0.62,
            validation_required: true,
            oos_required: true,
            forward_observation_required: true,
            evidence_basis: 'Certified candidates and research memory prefer ATR14/21 and EMA20/50 clusters.',
          },
        ],
      };
    case 'tradingResearchSynthesizer':
      return {
        report_version: 'trading_research_synthesizer_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        patterns_analyzed: 6,
        internal_sources_analyzed: 209,
        external_sources_analyzed: 9,
        hypotheses_count: 24,
        high_priority_count: 12,
        medium_priority_count: 8,
        low_priority_count: 4,
        external_research_source: 'existing_artifacts_only',
        domains: ['trading'],
        warnings: [],
        operator_summary: 'Externe Forschung analysiert. 24 Hypothesen erkannt. 12 hohe Priorität. Frank nötig: nein.',
        safety_summary: 'no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        internal_sources: ['Knowledge Catalog: 138', 'Research Memory: 115936', 'Setup Registry: 10', 'Certified Candidates: 31'],
        external_sources: ['Trading.de strategy_overview', 'Trading.de breakout_trading', 'Trading.de backtesting'],
        comparisons: [
          {
            pattern_id: 'ema_pullback',
            pattern_name: 'EMA Pullback',
            domain: 'trading',
            internal_evidence: 'Research Memory vorhanden; Setup Registry mit 10 Assets; Forward Observations: 2; Reviews: 20',
            external_evidence: 'existing_artifacts_only: Trading.de',
            agreements: ['Pullback- und Trendkontext wird intern wie extern bestätigt'],
            contradictions: [],
            open_questions: ['Welche Sessionfilter erhöhen die Signalqualität?', 'Welche Volatilitätsregime sind robust?'],
            relevant_parameter_classes: ['VWAP', 'EMA', 'ATR', 'session filter'],
          },
        ],
        hypotheses: [
          {
            hypothesis_id: 'trading_research_ema_pullback_vwap',
            pattern_id: 'ema_pullback',
            pattern_name: 'EMA Pullback',
            domain: 'trading',
            title: 'EMA Pullback: VWAP',
            hypothesis: 'Hypothese: EMA Pullback könnte mit VWAP als Kontextfilter stabiler sein als nur mit klassischen Trendfiltern.',
            internal_evidence: 'Research Memory vorhanden; Setup Registry mit 10 Assets; Forward Observations: 2; Reviews: 20',
            external_evidence: 'existing_artifacts_only: Trading.de',
            agreement_summary: 'Pullback- und Trendkontext wird intern wie extern bestätigt',
            contradiction_summary: 'keine wesentlichen Widersprüche',
            open_question_summary: 'Welche Sessionfilter erhöhen die Signalqualität?; Welche Volatilitätsregime sind robust?',
            parameter_classes: ['VWAP'],
            expected_information_gain: 0.86,
            validation_effort: 0.55,
            risk_level: 'low',
            priority: 'high',
            suggested_next_validation: 'EMA Pullback: VWAP in bestehende Validierungs-/Forward-Landschaft überführen',
            frank_required: false,
          },
        ],
      };
    case 'strategyMutationValidationPlanner':
      return {
        report_version: 'strategy_mutation_validation_planner_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        hypotheses_analyzed: 48,
        validation_plans_prepared: 12,
        domains: ['trading'],
        warnings: [],
        operator_summary: '48 Hypothesen analysiert. 12 Validierungsaufträge vorbereitet. Frank nötig: nein. Keine Backtests gestartet. Keine Broker-Aktionen.',
        safety_summary: 'no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true',
        frank_required: false,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
        validation_plans: [
          {
            validation_plan_id: 'validation_plan_trading_research_ema_pullback_vwap',
            source_hypothesis_id: 'trading_research_ema_pullback_vwap',
            strategy_pattern: 'EMA Pullback',
            asset: 'XAUUSD',
            timeframe: 'M15',
            parameters_to_validate: ['VWAP', 'EMA', 'ATR', 'session filter', 'pullback depth'],
            required_backtest: true,
            required_oos_test: true,
            required_walk_forward: true,
            required_monte_carlo: true,
            required_cost_spread_test: true,
            required_forward_observation: true,
            expected_information_gain: 0.86,
            validation_effort: 0.55,
            priority: 'high',
            safety_flags: ['no_auto_trading=true', 'no_broker_action=true', 'no_live_trading=true', 'human_review_required=true'],
          },
        ],
        sources_used: ['trading_research_synthesizer.json', 'strategy_parameter_research_planner.json', 'strategy_mutation_analyzer.json'],
      };
    case 'reviewStatusConsistencyAudit':
      return {
        report_version: 'review_status_consistency_audit_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        total_reviews: 20,
        pending_reviews_queue: 20,
        pending_reviews_master: 20,
        needs_more_evidence_queue: 0,
        needs_more_evidence_master: 0,
        abnormal_review_count: 0,
        same_count: 20,
        different_count: 0,
        source_of_truth: 'HumanReviewQueue',
        leading_queue_source: 'HumanReviewQueue',
        leading_master_source: 'master-status',
        master_snapshots: [
          {
            source: 'master-status',
            path: '/reports/master-status/master_status.json',
            last_updated_utc: runtimeMasterStatusMock.updated_at_utc,
            pending_reviews: 20,
            needs_more_evidence_reviews: 0,
            top_review_priorities: runtimeHumanReviewMock.items?.slice?.(0, 10)?.map?.((item) => item.review_id) || [],
          },
        ],
        reviews: runtimeHumanReviewMock.items || [],
        deviations: [],
        cause: 'Die Statusquellen sind aktuell konsistent. HumanReviewQueue und Master-Status zeigen denselben Zählstand.',
        recommended_correction: 'HumanReviewQueue als führende Quelle verwenden; Master-Status nur als Snapshot/Anzeige verstehen; Dashboard direkt aus der Queue lesen.',
        operator_summary: 'Die Statusquellen sind aktuell konsistent. Frank sieht dieselbe Review-Wahrheit im Prüfzentrum und im Master Status.',
        queue_path: '/reports/human-review-queue',
        review_queue_path: '/reports/human-review-queue',
        review_decision_assistant_path: '/reports/review_decision_assistant/review_decision_assistant.json',
        review_prioritization_path: '/reports/review_prioritization_audit/review_prioritization_audit.json',
        review_evidence_refresh_path: '/reports/review_evidence_refresh/review_evidence_refresh.json',
        master_status_path: '/reports/master-status/master_status.json',
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'validationBacklogExecutor':
      return {
        report_version: 'validation_backlog_executor_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        configured: true,
        enabled: true,
        mode: 'Aktiviert – wartet auf Lernfenster',
        status_label: 'Aktiviert',
        window_label: 'Lernfenster',
        in_work_window: true,
        in_nightly_window: false,
        resource_healthy: true,
        max_tasks_per_run: 20,
        last_run_utc: runtimeMasterStatusMock.updated_at_utc,
        next_run_utc: '2026-06-17T20:30:00.0000000+02:00',
        next_run_hint: 'Aktiviert – wartet auf Lern- oder Nightly-Fenster.',
        backlog_items_analyzed: 223,
        planned_work_items: 20,
        executed_work_items: 20,
        skipped_work_items: 203,
        planned_steps: 7,
        executed_steps: 7,
        skipped_steps: 0,
        validation_tasks_created: 50,
        evidence_tasks_executed: 20,
        reviews_refreshed: 20,
        frank_required: 0,
        priority_areas: [
          { area_id: 'gather_more_evidence', area_title: 'Evidenz sammeln', item_count: 138, priority: 'high', status: 'bereit', next_action: 'Mehr Evidenz sammeln', reason: 'Trust- und Qualitätsblocker werden zuerst adressiert.', automatically_allowed: true, requires_human_review: false, safe_to_execute: true, window_hint: 'Arbeitsfenster' },
          { area_id: 'source_expansion', area_title: 'Quellen erweitern', item_count: 138, priority: 'high', status: 'bereit', next_action: 'Quellen erweitern', reason: 'Zusätzliche Quellen werden zuerst geprüft.', automatically_allowed: true, requires_human_review: false, safe_to_execute: true, window_hint: 'Arbeitsfenster' },
          { area_id: 'schedule_revalidation', area_title: 'Re-Validierung', item_count: 92, priority: 'high', status: 'wartet auf Nightly', next_action: 'Re-Validierung planen', reason: 'Validierungsläufe und OOS-Absicherung werden priorisiert.', automatically_allowed: true, requires_human_review: false, safe_to_execute: true, window_hint: 'Nightly' },
          { area_id: 'contradiction_analysis', area_title: 'Widersprüche prüfen', item_count: 5, priority: 'high', status: 'bereit', next_action: 'Widersprüche prüfen', reason: 'Aktive Widersprüche werden analysiert, nicht automatisch aufgelöst.', automatically_allowed: true, requires_human_review: true, safe_to_execute: true, window_hint: 'Arbeitsfenster' },
          { area_id: 'systempflege', area_title: 'Systempflege', item_count: 1, priority: 'low', status: 'geplant', next_action: 'Cleanup-Plan aktualisieren', reason: 'Cleanup bleibt Wartung, kein Löschlauf.', automatically_allowed: true, requires_human_review: false, safe_to_execute: true, window_hint: 'bei Bedarf' },
        ],
        steps: [
          { step_id: 'validation_queue_refill', title: 'Validation Queue nachfüllen', area_id: 'schedule_revalidation', area_title: 'Re-Validierung', priority: 'high', status: 'executed', result: 'executed', planned_count: 50, executed_count: 50, skipped_count: 0, next_action: 'Offene Validierungspläne in Tasks überführen.', frank_required: false, automatically_allowed: true, safe_to_execute: true, window_hint: 'Nightly', output_report_path: '/reports/validation-queue-refill', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'evidence_auto_loop', title: 'Evidence Auto-Loop ausführen', area_id: 'gather_more_evidence', area_title: 'Evidenz sammeln', priority: 'high', status: 'executed', result: 'executed', planned_count: 20, executed_count: 20, skipped_count: 0, next_action: 'Weitere Evidenzläufe planen.', frank_required: false, automatically_allowed: true, safe_to_execute: true, window_hint: 'Arbeitsfenster', output_report_path: '/reports/evidence-auto-loop', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'run_evidence_tasks', title: 'Evidenzaufgaben abarbeiten', area_id: 'schedule_revalidation', area_title: 'Re-Validierung', priority: 'high', status: 'executed', result: 'executed', planned_count: 20, executed_count: 20, skipped_count: 0, next_action: 'Sichere Evidenz- und Validierungsaufgaben ausführen.', frank_required: false, automatically_allowed: true, safe_to_execute: true, window_hint: 'Nightly', output_report_path: '/reports/evidence-task-execution', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'review_evidence_refresh', title: 'Review Evidence Refresh', area_id: 'contradiction_analysis', area_title: 'Widersprüche prüfen', priority: 'high', status: 'executed', result: 'executed', planned_count: 20, executed_count: 20, skipped_count: 0, next_action: 'Reviews mit neuer Evidenz aktualisieren.', frank_required: true, automatically_allowed: true, safe_to_execute: true, window_hint: 'Arbeitsfenster', output_report_path: '/reports/review-evidence-refresh', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'review_decision_assistant', title: 'Review Decision Assistant aktualisieren', area_id: 'contradiction_analysis', area_title: 'Widersprüche prüfen', priority: 'high', status: 'executed', result: 'executed', planned_count: 20, executed_count: 20, skipped_count: 0, next_action: 'Empfehlungen für Frank aktualisieren.', frank_required: true, automatically_allowed: true, safe_to_execute: true, window_hint: 'Arbeitsfenster', output_report_path: '/reports/review-decision-assistant', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'knowledge_validation_audit', title: 'Knowledge Validation Audit aktualisieren', area_id: 'schedule_revalidation', area_title: 'Re-Validierung', priority: 'high', status: 'executed', result: 'executed', planned_count: 223, executed_count: 223, skipped_count: 0, next_action: 'Audit und Konsistenz neu schreiben.', frank_required: false, automatically_allowed: true, safe_to_execute: true, window_hint: 'Nightly', output_report_path: '/reports/knowledge-validation-audit', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
          { step_id: 'validation_backlog_analyzer', title: 'Validation Backlog Analyzer aktualisieren', area_id: 'systempflege', area_title: 'Systempflege', priority: 'low', status: 'executed', result: 'executed', planned_count: 223, executed_count: 223, skipped_count: 0, next_action: 'Validierungsstau neu analysieren.', frank_required: false, automatically_allowed: true, safe_to_execute: true, window_hint: 'bei Bedarf', output_report_path: '/reports/validation-backlog-analyzer', executed_at_utc: runtimeMasterStatusMock.updated_at_utc, warnings: [] },
        ],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'reviewPrioritizationAudit':
      return {
        report_version: 'review_prioritization_audit_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        total_pending_reviews: 20,
        trading_reviews: 12,
        documentation_reviews: 8,
        research_reviews: 0,
        software_reviews: 0,
        process_reviews: 0,
        domain_groups: [
          { domain: 'trading', count: 12, reviews: [] },
          { domain: 'documentation', count: 8, reviews: [] },
        ],
        top_priority_reviews: [],
        operator_summary: '🔴 12 wichtige Entscheidungen\n🟡 0 Wissensprüfungen\n🟢 8 Dokumentationsprüfungen\n\nFrank muss nichts tun. Hermes bereitet die Reviews nur vor.',
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'reviewDecisionAssistant':
      return {
        report_version: 'review_decision_assistant_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        review_count: 20,
        high_priority_count: 12,
        recommended_approve: 6,
        recommended_more_evidence: 8,
        recommended_reject: 6,
        entries: [
          {
            review_id: 'decision_assistant_trading_1',
            knowledge_item_id: 'liquidity_sweep',
            title: 'Liquidity Sweep',
            domain: 'trading',
            priority: 'hoch',
            trust_before: 0.6317,
            evidence_quality: 0.5086,
            validation_score: 0.586,
            trading_risk: 'mittel',
            recommendation_key: 'more_evidence',
            recommendation_label: 'Mehr Evidenz empfohlen',
            recommendation_reason: 'Mehr Evidenz sinnvoll. Vertrauen mittel. Evidenzqualität mittel. Validierung noch nicht stark genug. Trading-Risiko mittel.',
            frank_action: 'Prüfzentrum: mehr Evidenz prüfen',
            requires_human_review: true,
          },
        ],
        operator_summary: '🔴 12 wichtige Entscheidungen\n🟡 8 Reviews brauchen mehr Evidenz\n🟢 6 Freigaben plausibel\n⚫ 6 Ablehnungen empfohlen\n\nFrank muss weiterhin selbst entscheiden. Hermes liefert nur die Empfehlung.',
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'evidenceAutoLoop':
      return {
        report_version: 'evidence_auto_loop_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        review_count: 20,
        more_evidence_reviews: 20,
        planned_tasks: 72,
        trading_tasks: 48,
        documentation_tasks: 24,
        validation_tasks: 44,
        evidence_tasks: 28,
        frank_required: 0,
        scheduler_status: 'configured',
        scheduler_configured: true,
        scheduler_enabled: true,
        last_run_utc: runtimeMasterStatusMock.updated_at_utc,
        next_run_utc: '2026-06-16T20:30:00.0000000+02:00',
        next_run_hint: 'Nächster Lauf wird beim Scheduler-Lauf berechnet.',
        next_action: 'Hermes plant weitere Evidenzläufe.',
        domain_summaries: [
          { domain: 'trading', review_count: 12, evidence_tasks: 24, validation_tasks: 24, highest_priority: 'hoch', status: 'geplant', next_action: 'Trading-Themen werden zuerst validiert.' },
          { domain: 'documentation', review_count: 8, evidence_tasks: 4, validation_tasks: 12, highest_priority: 'mittel', status: 'geplant', next_action: 'Dokumentationsprüfungen folgen danach.' },
        ],
        planned_tasks_list: [],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'evidenceTaskExecution':
      return {
        report_version: 'evidence_task_execution_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        tasks_found: 72,
        tasks_executed: 72,
        tasks_skipped: 0,
        unsupported_tasks: 0,
        evidence_collected: 28,
        validation_tasks_executed: 44,
        needs_more_evidence_before: 20,
        needs_more_evidence_after: 12,
        pending_reviews_before: 0,
        pending_reviews_after: 8,
        updated_knowledge_items: 8,
        updated_reviews: 8,
        frank_action_required: true,
        source_report_path: '/reports/evidence_auto_loop/evidence_auto_loop.json',
        queue_path: '/reports/evidence_auto_loop/evidence_auto_loop.json',
        report_path: '/reports/evidence_task_execution/evidence_task_execution.json',
        markdown_path: '/reports/evidence_task_execution/evidence_task_execution.md',
        next_action: 'Hermes hat 72 Evidenzaufgaben ausgeführt.',
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'validationQueueRefill':
      return {
        report_version: 'validation_queue_refill_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        open_plans: 50,
        plans_with_queued_tasks: 50,
        plans_skipped: 0,
        tasks_created: 50,
        domains: ['documentation', 'trading'],
        created_tasks: [],
        skipped_plans: [],
        next_action: 'Validation Tasks ausführen.',
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'evidenceValidationRunner':
      return {
        report_version: 'evidence_validation_runner_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        validation_tasks_executed: 50,
        evidence_tasks_executed: 50,
        needs_more_evidence_before: 20,
        needs_more_evidence_after: 12,
        pending_reviews_before: 0,
        pending_reviews_after: 4,
        new_pending_reviews: 4,
        prepared_for_review_count: 8,
        still_needs_more_evidence_count: 12,
        frank_action_required: true,
        domains: ['documentation', 'trading'],
        executed_tasks: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'cognitiveStatus':
      return {
        status: runtimeMasterStatusMock.knowledge_health,
        active_domains: runtimeMasterStatusMock.active_domains,
        queued_research_items: runtimeMasterStatusMock.queued_tasks,
        last_updated_utc: runtimeMasterStatusMock.updated_at_utc,
        warnings: runtimeMasterStatusMock.top_blockers,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'planningStatus':
      return {
        status: 'needs_attention',
        planned_tasks: runtimeMasterStatusMock.next_recommended_actions,
        detected_needs: runtimeMasterStatusMock.top_blockers,
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'taskExecutionState':
      return {
        status: 'idle',
        latest_results: [],
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        warnings: [],
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'autonomousLoopState':
      return {
        status: 'completed',
        last_iteration_utc: runtimeMasterStatusMock.last_autonomous_loop,
        next_action: 'plan_next_tasks',
        warnings: runtimeMasterStatusMock.top_blockers,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'metaReview':
      return {
        status: 'completed',
        learning_strategy: runtimeMasterStatusMock.learning_strategy,
        updated_at_utc: runtimeMasterStatusMock.last_meta_review,
        observations: runtimeMasterStatusMock.top_blockers,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'domainStatus':
      return {
        active_domains: runtimeMasterStatusMock.active_domains,
        domains: runtimeMasterStatusMock.active_domains.map((domain) => ({
          domain,
          status: domain === 'trading' ? 'needs_validation' : 'prepared',
          knowledge_items: domain === 'trading' ? 33 : 20,
          open_needs: domain === 'trading' ? ['oos_data_missing'] : ['source_check_required'],
          last_scan_utc: runtimeMasterStatusMock.updated_at_utc,
          next_recommended_task: domain === 'trading' ? 'run_walkforward_validation' : `scan_${domain}_domain`,
        })),
        no_auto_trading: true,
        human_review_required: true,
      };
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
    case 'timeControl':
      return {
        config_path: '/home/home/jarvis/HermesRuntime/config/schedules.json',
        time_zone: 'Europe/Berlin',
        current_utc: new Date().toISOString(),
        current_local: new Date().toISOString(),
        status_label: 'Derzeit im Arbeitsfenster',
        in_work_window: true,
        work_window: { label: 'Arbeitszeit', enabled: true, start: '08:00', end: '18:00', active_now: true, summary: 'aktiv' },
        nightly_window: { label: 'Nightly', enabled: true, start: '23:00', end: '05:00', active_now: false, summary: 'inaktiv' },
        learning_window: { label: 'Lernfenster', enabled: true, start: '05:30', end: '07:00', active_now: false, summary: 'inaktiv' },
        human_review_window: { label: 'Human-Review', enabled: true, start: '08:00', end: '18:00', active_now: true, summary: 'aktiv' },
        weekdays: [
          { day: 'Monday', active: true },
          { day: 'Tuesday', active: true },
          { day: 'Wednesday', active: true },
          { day: 'Thursday', active: true },
          { day: 'Friday', active: true },
          { day: 'Saturday', active: false },
          { day: 'Sunday', active: false },
        ],
        active_weekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
        inactive_weekdays: ['Saturday', 'Sunday'],
        warnings: [],
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'resourceStatus':
      return operatorDashboardMock.resourceStatus;
    case 'cleanupPlan':
      return operatorDashboardMock.cleanupPlan;
    case 'nightlyState':
      return operatorDashboardMock.nightlyState;
    case 'demoSignalFeedStatus':
      return operatorDashboardMock.demoSignalFeedStatus;
    case 'latestDemoSignals':
      return operatorDashboardMock.latestDemoSignals;
    case 'ensemblePortfolioStatus':
      return {
        portfolio_readiness: 'needs_validation',
        package_validation_status: 'ok',
        bundle_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle',
        package_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/scalping_portfolio/ensemble_portfolio/ensemble_signal_agent_package.json',
        assets: [
          { asset: 'GER40', readiness: 'bot_ready', primary_setup: 'ger40_range_breakout_m5', backup_setups: ['ger40_ema_pullback_m5'], candidate_count: 5, signal_spec_count: 5 },
          { asset: 'XAUUSD', readiness: 'bot_ready', primary_setup: 'xauusd_micro_trend_continuation_m5', backup_setups: ['xauusd_liquidity_rejection_m5', 'xauusd_ema_pullback_m5', 'xauusd_range_breakout_m5'], candidate_count: 8, signal_spec_count: 8 },
          { asset: 'EURUSD', readiness: 'needs_more_validation', primary_setup: '-', backup_setups: [], candidate_count: 0, signal_spec_count: 0 },
        ],
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
        no_auto_trading: true,
        human_review_required: true,
        broker_orders_enabled: false,
        live_trading_enabled: false,
        research_only: true,
      };
    case 'systemBHandoffBundle':
      return {
        bundle_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle',
        files: ['README.md', 'ensemble_signal_agent_package.json', 'ensemble_signal_agent_package.schema.json', 'system_b_signal_agent_export_contract.md', 'portfolio_summary.json', 'portfolio_summary.md', 'bundle-manifest.json'],
        asset_count: 3,
        portfolio_status: 'needs_validation',
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
        no_auto_trading: true,
        human_review_required: true,
        broker_orders_enabled: false,
        live_trading_enabled: false,
        research_only: true,
      };
    case 'validateEnsembleSignalPackage':
      return {
        package_id: 'ensemble_signal_agent_package_20260611052025',
        package_version: 'ensemble_signal_agent_package_v1',
        validation_status: 'ok',
        warnings: ['asset_not_tradeable:EURUSD:needs_more_validation'],
        asset_count: 3,
        package_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/scalping_portfolio/ensemble_portfolio/ensemble_signal_agent_package.json',
        no_auto_trading: true,
        human_review_required: true,
        broker_orders_enabled: false,
        live_trading_enabled: false,
        research_only: true,
      };
    case 'setupRegistry':
      return {
        setup_count_total: 6,
        setup_counts_by_asset: { GER40: 2, XAUUSD: 4, EURUSD: 0 },
        best_setup_by_asset: { GER40: 'ger40_range_breakout_m5', XAUUSD: 'xauusd_micro_trend_continuation_m5', EURUSD: '-' },
        readiness_by_asset: { GER40: 'bot_ready', XAUUSD: 'bot_ready', EURUSD: 'needs_more_validation' },
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
      };
    case 'signalAgentSpecs':
      return {
        specs_ready: 23,
        by_asset: { GER40: 5, XAUUSD: 8, EURUSD: 8 },
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
      };
    case 'multiAssetResearchStatus':
      return {
        multi_asset_research_status: 'ready',
        assets_ready: ['GER40', 'XAUUSD'],
        assets_setup_ready: ['GER40', 'XAUUSD'],
        assets_data_ready_only: ['EURUSD'],
        assets_missing_data: [],
        safety_flags: ['no_auto_trading=true', 'human_review_required=true', 'broker_orders_enabled=false', 'live_trading_enabled=false', 'research_only=true'],
      };
    case 'forwardTestStatus':
      return operatorDashboardMock.forwardTestStatus;
    case 'autonomousImprovementQueue':
      return {
        report_version: 'autonomous_improvement_queue_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        active_improvements: 421,
        highest_priority: 'high',
        hermes_can_handle: 421,
        frank_items: 0,
        grouped_improvement_areas: [
          { group_id: 'group_gather_more_evidence_trading_medium_trust_score_too_low', group_title: 'Mehr Evidenz sammeln', action_type: 'gather_more_evidence', domain: 'trading', priority: 'medium', source_warning: 'trust_score_too_low', item_count: 138, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Mehr Evidenz sammeln' },
          { group_id: 'group_source_expansion_trading_medium_quality_score_too_low', group_title: 'Quellen erweitern', action_type: 'source_expansion', domain: 'trading', priority: 'medium', source_warning: 'quality_score_too_low', item_count: 138, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Quellen erweitern' },
          { group_id: 'group_schedule_revalidation_trading_medium_validation_score_too_low', group_title: 'Re-Validierung planen', action_type: 'schedule_revalidation', domain: 'trading', priority: 'medium', source_warning: 'validation_score_too_low', item_count: 92, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Re-Validierung planen' },
          { group_id: 'group_contradiction_analysis_trading_high_active_contradiction', group_title: 'Widersprüche prüfen', action_type: 'contradiction_analysis', domain: 'trading', priority: 'high', source_warning: 'active_contradiction', item_count: 5, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Widerspruchsanalyse ausführen' },
          { group_id: 'group_validation_queue_repair_research_high_knowledge_validation_queue_missing', group_title: 'Validation Queue reparieren', action_type: 'validation_queue_repair', domain: 'research', priority: 'high', source_warning: 'knowledge_validation_queue_missing', item_count: 1, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Validation Queue prüfen/reparieren' },
          { group_id: 'group_cleanup_plan_update_process_low_storage_cleanup_candidates', group_title: 'Systempflege', action_type: 'cleanup_plan_update', domain: 'process', priority: 'low', source_warning: 'storage_cleanup_candidates', item_count: 1, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Cleanup-Plan aktualisieren' },
        ],
        top_priority_groups: [
          { group_id: 'group_contradiction_analysis_trading_high_active_contradiction', group_title: 'Widersprüche prüfen', action_type: 'contradiction_analysis', domain: 'trading', priority: 'high', source_warning: 'active_contradiction', item_count: 5, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Widerspruchsanalyse ausführen' },
          { group_id: 'group_validation_queue_repair_research_high_knowledge_validation_queue_missing', group_title: 'Validation Queue reparieren', action_type: 'validation_queue_repair', domain: 'research', priority: 'high', source_warning: 'knowledge_validation_queue_missing', item_count: 1, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Validation Queue prüfen/reparieren' },
          { group_id: 'group_gather_more_evidence_trading_medium_trust_score_too_low', group_title: 'Mehr Evidenz sammeln', action_type: 'gather_more_evidence', domain: 'trading', priority: 'medium', source_warning: 'trust_score_too_low', item_count: 138, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Mehr Evidenz sammeln' },
          { group_id: 'group_source_expansion_trading_medium_quality_score_too_low', group_title: 'Quellen erweitern', action_type: 'source_expansion', domain: 'trading', priority: 'medium', source_warning: 'quality_score_too_low', item_count: 138, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Quellen erweitern' },
          { group_id: 'group_schedule_revalidation_trading_medium_validation_score_too_low', group_title: 'Re-Validierung planen', action_type: 'schedule_revalidation', domain: 'trading', priority: 'medium', source_warning: 'validation_score_too_low', item_count: 92, completed_count: 0, failed_count: 0, status: 'open', next_action: 'Re-Validierung planen' },
        ],
        tasks: [],
        source_warnings: ['validation_queue_active', 'no_trusted_knowledge', 'not_yet_trusted_or_robust', 'trust_score_too_low', 'quality_score_too_low', 'insufficient_sources', 'validation_score_too_low', 'not_recently_validated', 'pending_human_review', 'active_contradiction', 'storage_cleanup_candidates'],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'autonomousImprovementQueueSummary':
      return {
        report_version: 'autonomous_improvement_queue_summary_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        active_areas: 5,
        active_items: 421,
        hermes_can_handle: 421,
        frank_items: 0,
        grouped_improvement_areas: reportFixtureRaw('autonomousImprovementWorkAreas').work_areas,
        top_priority_groups: reportFixtureRaw('autonomousImprovementWorkAreas').work_areas,
        source_warnings: reportFixtureRaw('autonomousImprovementQueue').source_warnings,
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'autonomousImprovementWorkAreas':
      return {
        report_version: 'autonomous_improvement_work_areas_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        active_areas: 5,
        active_items: 421,
        hermes_can_handle: 421,
        frank_items: 0,
        work_areas: [
          { area_id: 'gather_more_evidence', area_title: 'Evidenz sammeln', item_count: 138, status: 'open', highest_priority: 'medium', frank_required: false, next_action: 'Mehr Evidenz sammeln' },
          { area_id: 'source_expansion', area_title: 'Quellen erweitern', item_count: 138, status: 'open', highest_priority: 'medium', frank_required: false, next_action: 'Quellen erweitern' },
          { area_id: 'schedule_revalidation', area_title: 'Re-Validierung', item_count: 92, status: 'open', highest_priority: 'medium', frank_required: false, next_action: 'Re-Validierung planen' },
          { area_id: 'contradiction_analysis', area_title: 'Widersprüche prüfen', item_count: 5, status: 'open', highest_priority: 'high', frank_required: false, next_action: 'Widersprüche prüfen' },
          { area_id: 'systempflege', area_title: 'Systempflege', item_count: 48, status: 'open', highest_priority: 'low', frank_required: false, next_action: 'Systempflege aktualisieren' },
        ],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'workAreaExecutorPolicy':
      return {
        report_version: 'work_area_executor_policy_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        config_path: 'HermesRuntime/config/work_area_executor_policy.json',
        time_control_path: 'HermesRuntime/config/schedules.json',
        resource_path: 'HermesRuntime/reports/resource/resource_status.json',
        in_work_window: true,
        in_nightly_window: false,
        resource_healthy: true,
        active_areas: 5,
        active_improvements: 421,
        frank_items: 0,
        work_areas: [
          { area_id: 'gather_more_evidence', area_title: 'Evidenz sammeln', automatically_allowed: true, status: 'bereit', highest_priority: 'medium', frank_required: false, next_execution_window: 'Arbeitsfenster', planned_action: 'Evidenz sammeln', item_count: 138, completed_count: 0, failed_count: 0, result: 'geplant' },
          { area_id: 'source_expansion', area_title: 'Quellen erweitern', automatically_allowed: true, status: 'bereit', highest_priority: 'medium', frank_required: false, next_execution_window: 'Arbeitsfenster', planned_action: 'Quellen erweitern', item_count: 138, completed_count: 0, failed_count: 0, result: 'geplant' },
          { area_id: 'schedule_revalidation', area_title: 'Re-Validierung', automatically_allowed: true, status: 'wartet auf Nightly', highest_priority: 'medium', frank_required: false, next_execution_window: 'Nightly', planned_action: 'Re-Validierung planen', item_count: 139, completed_count: 0, failed_count: 0, result: 'geplant' },
          { area_id: 'contradiction_analysis', area_title: 'Widersprüche prüfen', automatically_allowed: true, status: 'bereit', highest_priority: 'high', frank_required: true, next_execution_window: 'Arbeitsfenster', planned_action: 'Widersprüche analysieren', item_count: 5, completed_count: 0, failed_count: 0, result: 'geplant' },
          { area_id: 'systempflege', area_title: 'Systempflege', automatically_allowed: true, status: 'bereit', highest_priority: 'low', frank_required: false, next_execution_window: 'bei Bedarf', planned_action: 'Cleanup-Plan aktualisieren', item_count: 1, completed_count: 0, failed_count: 0, result: 'geplant' },
        ],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'nightlyWorkAreaStatus':
      return {
        report_version: 'nightly_work_area_runner_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        time_control_path: 'HermesRuntime/config/schedules.json',
        resource_path: 'HermesRuntime/reports/resource/resource_status.json',
        in_nightly_window: false,
        resource_healthy: true,
        revalidation: {
          area_id: 'schedule_revalidation',
          area_title: 'Re-Validierung',
          status: 'wartet auf Nightly',
          next_execution_window: 'Nightly',
          next_execution_at_utc: '2026-06-12T23:00:00Z',
          resource_healthy: true,
          in_nightly_window: false,
          planned_action: 'Re-Validierung planen',
          result: 'geplant',
          output_report_path: 'reports/knowledge_validation_audit/knowledge_validation_audit.json',
          executed_at_utc: null,
          warnings: ['wartet_auf_nightly'],
        },
        warnings: ['wartet_auf_nightly'],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'trustedKnowledgeReviewGate':
      return {
        report_version: 'trusted_knowledge_review_gate_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        total_knowledge_items: 138,
        trusted_items_count: 3,
        eligible_for_trusted_review: 2,
        blocked_items: 136,
        rejection_reasons: {
          not_yet_trusted_or_robust: 90,
          trust_score_too_low: 24,
          quality_score_too_low: 17,
          insufficient_evidence: 11,
          active_contradiction: 4,
        },
        top_candidates: [
          {
            knowledge_id: 'trading:ema_pullback',
            domain: 'trading',
            title: 'EMA Pullback',
            trust_score: 0.88,
            quality_score: 0.86,
            evidence_score: 0.81,
            evidence_count: 5,
            source_count: 3,
            last_validated_utc: runtimeMasterStatusMock.updated_at_utc,
            reasons: [],
            blocking_reasons: [],
            requires_human_review: true,
            review_status: 'none',
          },
          {
            knowledge_id: 'trading:range_breakout',
            domain: 'trading',
            title: 'Range Breakout',
            trust_score: 0.87,
            quality_score: 0.85,
            evidence_score: 0.8,
            evidence_count: 4,
            source_count: 2,
            last_validated_utc: runtimeMasterStatusMock.updated_at_utc,
            reasons: [],
            blocking_reasons: [],
            requires_human_review: true,
            review_status: 'none',
          },
        ],
        warnings: [],
        requires_human_review: true,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'knowledgeTrustImprovementPlan':
      return {
        report_version: 'knowledge_trust_improvement_plan_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        total_blocked_items: 138,
        blocker_counts: {
          trust_score_too_low: 50,
          quality_score_too_low: 42,
          insufficient_sources: 33,
          validation_score_too_low: 28,
          not_recently_validated: 21,
          active_contradiction: 5,
          pending_human_review: 20,
          not_yet_trusted_or_robust: 138,
        },
        planned_actions: [
          { action_id: 'gather_more_evidence_trading:liquidity_sweep', blocker: 'trust_score_too_low', title: 'Mehr Evidenz sammeln', domain: 'trading', priority: 'medium', reason: 'trust_score_too_low', suggested_action: 'Mehr Evidenz sammeln.', auto_fixable: true, requires_human_review: false, status: 'planned' },
          { action_id: 'source_expansion_trading:liquidity_sweep', blocker: 'insufficient_sources', title: 'Zusätzliche Quellen identifizieren', domain: 'trading', priority: 'medium', reason: 'insufficient_sources', suggested_action: 'Zusätzliche Quellen identifizieren.', auto_fixable: true, requires_human_review: false, status: 'planned' },
          { action_id: 'schedule_revalidation_trading:liquidity_sweep', blocker: 'validation_score_too_low', title: 'Re-Validierung planen', domain: 'trading', priority: 'medium', reason: 'validation_score_too_low', suggested_action: 'Re-Validierung planen.', auto_fixable: true, requires_human_review: false, status: 'planned' },
          { action_id: 'contradiction_analysis_trading:liquidity_sweep', blocker: 'active_contradiction', title: 'Widerspruchsanalyse erzeugen', domain: 'trading', priority: 'high', reason: 'active_contradiction', suggested_action: 'Widerspruchsanalyse erzeugen.', auto_fixable: true, requires_human_review: false, status: 'planned' },
        ],
        estimated_effort: 'hoch',
        auto_fixable_count: 4,
        human_review_count: 0,
        top_priority_items: [
          { knowledge_id: 'trading:liquidity_sweep', domain: 'trading', title: 'Liquidity Sweep', blockers: ['trust_score_too_low'], trust_score: 0.6411, quality_score: 0.6394, validation_score: 0.5068, planned_actions: ['gather_more_evidence_trading:liquidity_sweep'], priority: 'high', auto_fixable: true, requires_human_review: false },
        ],
        next_recommended_action: 'Mehr Evidenz sammeln.',
        requires_human_review: true,
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
    case 'autonomousImprovementExecution':
      return {
        report_version: 'autonomous_improvement_execution_v1',
        updated_at_utc: runtimeMasterStatusMock.updated_at_utc,
        pending: 0,
        planned: 0,
        executed: 4,
        skipped: 0,
        failed: 0,
        needs_human_review: 0,
        last_executed_at_utc: runtimeMasterStatusMock.updated_at_utc,
        tasks: [],
        warnings: [],
        no_trading_execution: true,
        no_broker_action: true,
        no_auto_trading: true,
        human_review_required: true,
      };
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
    scalping_asset: asString(firstDefined(raw.scalping_asset, raw.scalpingAsset, trading.scalping_asset, trading.scalpingAsset), 'XAUUSD'),
    scalping_candidates_total: asNumber(firstDefined(raw.scalping_candidates_total, raw.scalpingCandidatesTotal, trading.scalping_candidates_total, trading.scalpingCandidatesTotal), 0),
    scalping_robust_candidates: asNumber(firstDefined(raw.scalping_robust_candidates, raw.scalpingRobustCandidates, trading.scalping_robust_candidates, trading.scalpingRobustCandidates), 0),
    scalping_final_candidates: asNumber(firstDefined(raw.scalping_final_candidates, raw.scalpingFinalCandidates, trading.scalping_final_candidates, trading.scalpingFinalCandidates), 0),
    best_scalping_candidate: asString(firstDefined(raw.best_scalping_candidate, raw.bestScalpingCandidate, trading.best_scalping_candidate, trading.bestScalpingCandidate), '-'),
    scalping_monte_carlo_health: asString(firstDefined(raw.scalping_monte_carlo_health, raw.scalpingMonteCarloHealth, trading.scalping_monte_carlo_health, trading.scalpingMonteCarloHealth), 'missing'),
    scalping_parameter_sensitivity_health: asString(firstDefined(raw.scalping_parameter_sensitivity_health, raw.scalpingParameterSensitivityHealth, trading.scalping_parameter_sensitivity_health, trading.scalpingParameterSensitivityHealth), 'missing'),
    scalping_regime_validation_health: asString(firstDefined(raw.scalping_regime_validation_health, raw.scalpingRegimeValidationHealth, trading.scalping_regime_validation_health, trading.scalpingRegimeValidationHealth), 'missing'),
    ctrader_bot_specs_ready: asNumber(firstDefined(raw.ctrader_bot_specs_ready, raw.cTraderBotSpecsReady, raw.CTraderBotSpecsReady, trading.ctrader_bot_specs_ready, trading.cTraderBotSpecsReady), 0),
    signal_agent_specs_ready: asNumber(firstDefined(raw.signal_agent_specs_ready, raw.signalAgentSpecsReady, trading.signal_agent_specs_ready, trading.signalAgentSpecsReady), 0),
    demo_signal_feed_status: asString(
      firstDefined(raw.demo_signal_feed_status, raw.demoSignalFeedStatus, trading.demo_signal_feed_status, trading.demoSignalFeedStatus),
      'unknown',
    ),
    demo_signals_available: asBoolean(
      firstDefined(raw.demo_signals_available, raw.demoSignalsAvailable, trading.demo_signals_available, trading.demoSignalsAvailable),
      false,
    ),
    latest_demo_signal_count: asNumber(
      firstDefined(raw.latest_demo_signal_count, raw.latestDemoSignalCount, trading.latest_demo_signal_count, trading.latestDemoSignalCount),
      0,
    ),
    demo_signal_feed_health: asString(
      firstDefined(raw.demo_signal_feed_health, raw.demoSignalFeedHealth, trading.demo_signal_feed_health, trading.demoSignalFeedHealth),
      'unknown',
    ),
    demo_signal_feed_mode: asString(
      firstDefined(raw.demo_signal_feed_mode, raw.demoSignalFeedMode, trading.demo_signal_feed_mode, trading.demoSignalFeedMode),
      'unknown',
    ),
    forward_test_status: asString(
      firstDefined(raw.forward_test_status, raw.forwardTestStatus, trading.forward_test_status, trading.forwardTestStatus),
      'unknown',
    ),
    forward_test_mode: asString(
      firstDefined(raw.forward_test_mode, raw.forwardTestMode, trading.forward_test_mode, trading.forwardTestMode),
      'unknown',
    ),
    forward_test_assets: asArray(
      firstDefined(raw.forward_test_assets, raw.forwardTestAssets, trading.forward_test_assets, trading.forwardTestAssets),
    ).map(String),
    forward_test_signals_observed: asNumber(
      firstDefined(raw.forward_test_signals_observed, raw.forwardTestSignalsObserved, trading.forward_test_signals_observed, trading.forwardTestSignalsObserved),
      0,
    ),
    forward_test_health: asString(
      firstDefined(raw.forward_test_health, raw.forwardTestHealth, trading.forward_test_health, trading.forwardTestHealth),
      'unknown',
    ),
    forward_test_requires_human_review: asBoolean(
      firstDefined(raw.forward_test_requires_human_review, raw.forwardTestRequiresHumanReview, trading.forward_test_requires_human_review, trading.forwardTestRequiresHumanReview),
      true,
    ),
    trusted_knowledge: asNumber(
      firstDefined(raw.trusted_knowledge, raw.trustedKnowledge, raw.knowledge_health?.trusted_knowledge, raw.knowledgeHealth?.trustedKnowledge),
      0,
    ),
    weak_knowledge: asNumber(
      firstDefined(raw.weak_knowledge, raw.weakKnowledge, raw.knowledge_health?.weak_knowledge, raw.knowledgeHealth?.weakKnowledge),
      0,
    ),
    deprecated_knowledge: asNumber(
      firstDefined(raw.deprecated_knowledge, raw.deprecatedKnowledge, raw.knowledge_health?.deprecated_knowledge, raw.knowledgeHealth?.deprecatedKnowledge),
      0,
    ),
    average_quality_score: asNumber(
      firstDefined(raw.average_quality_score, raw.averageQualityScore, raw.knowledge_health?.average_quality_score, raw.knowledgeHealth?.averageQualityScore),
      0,
    ),
    average_trust_score: asNumber(
      firstDefined(raw.average_trust_score, raw.averageTrustScore, raw.knowledge_health?.average_trust_score, raw.knowledgeHealth?.averageTrustScore),
      0,
    ),
    knowledge_health: asString(
      firstDefined(raw.knowledge_health, raw.knowledgeHealth?.status, raw.knowledgeHealth),
      'unbekannt',
    ),
    knowledge_trend: asString(
      firstDefined(raw.knowledge_trend, raw.knowledgeTrend, raw.knowledge_health?.knowledge_trend, raw.knowledgeHealth?.knowledgeTrend),
      '-',
    ),
    evidence_coverage: asNumber(
      firstDefined(raw.evidence_coverage, raw.evidenceCoverage, raw.knowledge_health?.evidence_coverage, raw.knowledgeHealth?.evidenceCoverage),
      0,
    ),
    validation_coverage: asNumber(
      firstDefined(raw.validation_coverage, raw.validationCoverage, raw.knowledge_health?.validation_coverage, raw.knowledgeHealth?.validationCoverage),
      0,
    ),
    contradiction_count: asNumber(
      firstDefined(raw.contradiction_count, raw.contradictionCount, raw.contradictions, raw.knowledge_health?.contradiction_count, raw.knowledgeHealth?.contradictionCount),
      0,
    ),
    human_reviewed_items: asNumber(
      firstDefined(raw.human_reviewed_items, raw.humanReviewedItems, raw.knowledge_health?.human_reviewed_items, raw.knowledgeHealth?.humanReviewedItems),
      0,
    ),
    trust_distribution: normalizeDistribution(
      firstDefined(raw.trust_distribution, raw.trustDistribution, raw.knowledge_health?.trust_distribution, raw.knowledgeHealth?.trustDistribution),
    ),
    pending_reviews: asNumber(
      firstDefined(raw.pending_reviews, raw.pendingReviews, raw.knowledge_health?.pending_reviews, raw.knowledgeHealth?.pendingReviews),
      0,
    ),
    approved_reviews: asNumber(
      firstDefined(raw.approved_reviews, raw.approvedReviews, raw.knowledge_health?.approved_reviews, raw.knowledgeHealth?.approvedReviews),
      0,
    ),
    rejected_reviews: asNumber(
      firstDefined(raw.rejected_reviews, raw.rejectedReviews, raw.knowledge_health?.rejected_reviews, raw.knowledgeHealth?.rejectedReviews),
      0,
    ),
    needs_more_evidence_reviews: asNumber(
      firstDefined(raw.needs_more_evidence_reviews, raw.needsMoreEvidenceReviews, raw.needs_more_evidence, raw.needsMoreEvidence, raw.knowledge_health?.needs_more_evidence_reviews, raw.knowledgeHealth?.needsMoreEvidenceReviews),
      0,
    ),
    deferred_reviews: asNumber(
      firstDefined(raw.deferred_reviews, raw.deferredReviews, raw.knowledge_health?.deferred_reviews, raw.knowledgeHealth?.deferredReviews),
      0,
    ),
    review_coverage: asNumber(
      firstDefined(raw.review_coverage, raw.reviewCoverage, raw.knowledge_health?.review_coverage, raw.knowledgeHealth?.reviewCoverage),
      0,
    ),
    top_review_priorities: asArray(
      firstDefined(raw.top_review_priorities, raw.topReviewPriorities, raw.knowledge_health?.top_review_priorities, raw.knowledgeHealth?.topReviewPriorities),
    ).map(String),
    validation_plans_open: asNumber(
      firstDefined(raw.validation_plans_open, raw.validationPlansOpen, raw.knowledge_health?.validation_plans_open, raw.knowledgeHealth?.validationPlansOpen),
      0,
    ),
    validation_tasks_pending: asNumber(
      firstDefined(raw.validation_tasks_pending, raw.validationTasksPending, raw.knowledge_health?.validation_tasks_pending, raw.knowledgeHealth?.validationTasksPending),
      0,
    ),
    trusted_candidate_count: asNumber(
      firstDefined(raw.trusted_candidate_count, raw.trustedCandidateCount, raw.knowledge_health?.trusted_candidate_count, raw.knowledgeHealth?.trustedCandidateCount),
      0,
    ),
    knowledge_items_needing_oos: asNumber(
      firstDefined(raw.knowledge_items_needing_oos, raw.knowledgeItemsNeedingOos, raw.knowledge_health?.knowledge_items_needing_oos, raw.knowledgeHealth?.knowledgeItemsNeedingOos),
      0,
    ),
    knowledge_items_needing_source_check: asNumber(
      firstDefined(raw.knowledge_items_needing_source_check, raw.knowledgeItemsNeedingSourceCheck, raw.knowledge_health?.knowledge_items_needing_source_check, raw.knowledgeHealth?.knowledgeItemsNeedingSourceCheck),
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

export function normalizeDemoSignalFeedStatus(raw = {}, masterStatus = normalizeMasterStatus({})) {
  return {
    feed_status: asString(
      firstDefined(raw.feed_status, raw.feedStatus, masterStatus.demo_signal_feed_status),
      'unknown',
    ),
    feed_mode: asString(
      firstDefined(raw.feed_mode, raw.feedMode, masterStatus.demo_signal_feed_mode),
      'unknown',
    ),
    signal_count: asNumber(
      firstDefined(raw.signal_count, raw.signalCount, masterStatus.latest_demo_signal_count),
      0,
    ),
    demo_signals_available: asBoolean(
      firstDefined(raw.demo_signals_available, raw.demoSignalsAvailable, masterStatus.demo_signals_available),
      false,
    ),
    health: asString(
      firstDefined(raw.health, raw.demo_signal_feed_health, raw.demoSignalFeedHealth, masterStatus.demo_signal_feed_health),
      masterStatus.demo_signal_feed_health || 'unknown',
    ),
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired),
      true,
    ),
    broker_orders_enabled: asBoolean(
      firstDefined(raw.broker_orders_enabled, raw.brokerOrdersEnabled),
      false,
    ),
    live_trading_enabled: asBoolean(
      firstDefined(raw.live_trading_enabled, raw.liveTradingEnabled),
      false,
    ),
  };
}

export function normalizeLatestDemoSignals(raw = []) {
  return asArray(raw).map((signal, index) => ({
    signal_id: asString(firstDefined(signal.signal_id, signal.signalId), `demo_signal_${index}`),
    created_utc: firstDefined(signal.created_utc, signal.createdUtc, null),
    asset: asString(signal.asset, 'n/a'),
    timeframe: asString(signal.timeframe, 'n/a'),
    candidate_id: asString(firstDefined(signal.candidate_id, signal.candidateId), 'n/a'),
    setup_type: asString(firstDefined(signal.setup_type, signal.setupType), 'n/a'),
    direction: asString(signal.direction, 'n/a'),
    entry_level: firstDefined(signal.entry_level, signal.entryLevel, null),
    stop_loss: firstDefined(signal.stop_loss, signal.stopLoss, null),
    take_profit: firstDefined(signal.take_profit, signal.takeProfit, null),
    invalidation_level: firstDefined(
      signal.invalidation_level,
      signal.invalidationLevel,
      null,
    ),
    confidence: firstDefined(signal.confidence, null),
    status: asString(signal.status, 'n/a'),
    reason: asString(signal.reason, 'n/a'),
    human_review_required: asBoolean(
      firstDefined(signal.human_review_required, signal.humanReviewRequired),
      true,
    ),
    no_auto_trading: asBoolean(firstDefined(signal.no_auto_trading, signal.noAutoTrading), true),
    broker_orders_enabled: asBoolean(
      firstDefined(signal.broker_orders_enabled, signal.brokerOrdersEnabled),
      false,
    ),
    live_trading_enabled: asBoolean(
      firstDefined(signal.live_trading_enabled, signal.liveTradingEnabled),
      false,
    ),
  }));
}

export function normalizeForwardTestStatus(raw = {}, masterStatus = normalizeMasterStatus({})) {
  return {
    forward_test_status: asString(
      firstDefined(raw.forward_test_status, raw.forwardTestStatus, masterStatus.forward_test_status),
      'unknown',
    ),
    forward_test_mode: asString(
      firstDefined(raw.forward_test_mode, raw.forwardTestMode, masterStatus.forward_test_mode),
      'unknown',
    ),
    forward_test_assets: asArray(
      firstDefined(raw.forward_test_assets, raw.forwardTestAssets, masterStatus.forward_test_assets),
    ).map(String),
    forward_test_signals_observed: asNumber(
      firstDefined(raw.forward_test_signals_observed, raw.forwardTestSignalsObserved, masterStatus.forward_test_signals_observed),
      0,
    ),
    forward_test_health: asString(
      firstDefined(raw.forward_test_health, raw.forwardTestHealth, masterStatus.forward_test_health),
      'unknown',
    ),
    forward_test_requires_human_review: asBoolean(
      firstDefined(raw.forward_test_requires_human_review, raw.forwardTestRequiresHumanReview, masterStatus.forward_test_requires_human_review),
      true,
    ),
    blockers: asArray(firstDefined(raw.blockers, raw.Blockers)).map(String),
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
    plan_path: asString(firstDefined(raw.plan_path, raw.planPath), ''),
    log_path: asString(firstDefined(raw.log_path, raw.logPath), ''),
    latest_observation_count: asNumber(
      firstDefined(raw.forward_test_signals_observed, raw.forwardTestSignalsObserved, masterStatus.forward_test_signals_observed),
      0,
    ),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading), true),
    human_review_required: asBoolean(firstDefined(raw.human_review_required, raw.humanReviewRequired), true),
    broker_orders_enabled: asBoolean(firstDefined(raw.broker_orders_enabled, raw.brokerOrdersEnabled), false),
    live_trading_enabled: asBoolean(firstDefined(raw.live_trading_enabled, raw.liveTradingEnabled), false),
  };
}

export function normalizeHumanReviewQueue(raw = {}, masterStatus = normalizeMasterStatus({})) {
  const items = asArray(firstDefined(raw.items, raw.Items, raw.review_items, raw.reviewItems))
    .map((item, index) => ({
      review_id: asString(firstDefined(item.review_id, item.reviewId, item.ReviewId, item.id), `review_${index}`),
      knowledge_item_id: asString(
        firstDefined(item.knowledge_item_id, item.knowledgeItemId, item.KnowledgeItemId),
        '-',
      ),
      domain: asString(firstDefined(item.domain, item.Domain), '-'),
      title: asString(firstDefined(item.title, item.Title), '-'),
      reason: asString(firstDefined(item.reason, item.Reason), '-'),
      evidence_summary: asString(
        firstDefined(item.evidence_summary, item.evidenceSummary, item.EvidenceSummary),
        '-',
      ),
      trust_before: asNumber(firstDefined(item.trust_before, item.trustBefore, item.TrustBefore), 0),
      recommendation: asString(firstDefined(item.recommendation, item.Recommendation), '-'),
      requested_by_task_id: asString(
        firstDefined(item.requested_by_task_id, item.requestedByTaskId, item.RequestedByTaskId),
        '-',
      ),
      priority: asString(firstDefined(item.priority, item.Priority), 'low'),
      created_at_utc: firstDefined(item.created_at_utc, item.createdAtUtc, item.CreatedAtUtc, null),
      updated_at_utc: firstDefined(item.updated_at_utc, item.updatedAtUtc, item.UpdatedAtUtc, null),
      status: asString(firstDefined(item.status, item.Status), 'pending'),
      evidence_refs: asArray(firstDefined(item.evidence_refs, item.evidenceRefs, item.EvidenceRefs)).map(String),
    }));
  const pendingItems = items.filter((item) => item.status === 'pending');
  const fallbackPriorities = masterStatus.top_review_priorities.map((entry, index) => {
    const parts = entry.split(':');
    const priority = parts[0] || 'medium';
    const domain = parts[1] || '-';
    const knowledgeId = parts.slice(2, -1).join(':') || entry;

    return {
      review_id: `master_status_priority_${index}`,
      knowledge_item_id: knowledgeId,
      domain,
      title: knowledgeId.replace(/^.*:/, '').replace(/_/g, ' '),
      reason: 'Master Status meldet ein Wissenselement mit offenem Human Review.',
      evidence_summary: entry,
      trust_before: asNumber((entry.match(/trust=([0-9.]+)/) || [])[1], 0),
      recommendation: parts.at(-1) || 'review_required',
      requested_by_task_id: 'master_status',
      priority,
      created_at_utc: masterStatus.updated_at_utc,
      updated_at_utc: null,
      status: 'pending',
      evidence_refs: [],
    };
  });
  const displayItems = items.length ? items : fallbackPriorities;

  return {
    updated_at_utc: firstDefined(raw.updated_at_utc, raw.updatedAtUtc, raw.UpdatedAtUtc, masterStatus.updated_at_utc),
    pending_reviews: asNumber(firstDefined(raw.pending_reviews, raw.pendingReviews, raw.PendingReviews), masterStatus.pending_reviews || pendingItems.length),
    approved_reviews: asNumber(firstDefined(raw.approved_reviews, raw.approvedReviews, raw.ApprovedReviews), masterStatus.approved_reviews),
    rejected_reviews: asNumber(firstDefined(raw.rejected_reviews, raw.rejectedReviews, raw.RejectedReviews), masterStatus.rejected_reviews),
    needs_more_evidence_reviews: asNumber(
      firstDefined(raw.needs_more_evidence_reviews, raw.needsMoreEvidenceReviews, raw.NeedsMoreEvidenceReviews),
      masterStatus.needs_more_evidence_reviews,
    ),
    deferred_reviews: asNumber(firstDefined(raw.deferred_reviews, raw.deferredReviews, raw.DeferredReviews), masterStatus.deferred_reviews),
    items: displayItems,
    warnings: asArray(firstDefined(raw.warnings, raw.Warnings)).map(String),
    no_auto_trading: asBoolean(firstDefined(raw.no_auto_trading, raw.noAutoTrading, raw.NoAutoTrading), true),
    human_review_required: asBoolean(
      firstDefined(raw.human_review_required, raw.humanReviewRequired, raw.HumanReviewRequired),
      true,
    ),
  };
}

function buildCognitiveControl(masterStatus, rawReports, reports) {
  const reportSource = (key) => reports.find((report) => report.key === key);
  const steps = [
    {
      id: 'need_detection',
      title: 'Bedarfserkennung',
      status: masterStatus.top_blockers.length ? 'Warnungen erkannt' : 'ruhig',
      last_activity: masterStatus.updated_at_utc,
      next_step: masterStatus.next_recommended_actions[0] || 'Keine Aktion gemeldet',
      warnings: masterStatus.top_blockers.slice(0, 3),
      report_key: 'planningStatus',
    },
    {
      id: 'goal_planning',
      title: 'Zielplanung',
      status: masterStatus.top_goal || '-',
      last_activity: masterStatus.updated_at_utc,
      next_step: masterStatus.goal_progress_summary?.[0]?.goal_id || 'Goal Review',
      warnings: masterStatus.goal_warnings || [],
      report_key: 'masterStatus',
    },
    {
      id: 'task_planning',
      title: 'Aufgabenplanung',
      status: `${masterStatus.queued_tasks} offene Aufgaben`,
      last_activity: rawReports.planningStatus?.updated_at_utc || masterStatus.updated_at_utc,
      next_step: masterStatus.next_recommended_actions[0] || 'Plan aktualisieren',
      warnings: asArray(rawReports.planningStatus?.warnings).map(String),
      report_key: 'planningStatus',
    },
    {
      id: 'execution',
      title: 'Ausführung',
      status: asString(rawReports.taskExecutionState?.status, 'kontrolliert'),
      last_activity: rawReports.taskExecutionState?.updated_at_utc || masterStatus.last_autonomous_loop,
      next_step: 'Nur erlaubte interne Tasks',
      warnings: asArray(rawReports.taskExecutionState?.warnings).map(String),
      report_key: 'taskExecutionState',
    },
    {
      id: 'outcome_feedback',
      title: 'Ergebnisbewertung',
      status: 'Feedback aktiv',
      last_activity: masterStatus.last_autonomous_loop,
      next_step: 'Planner Feedback einbeziehen',
      warnings: [],
      report_key: 'autonomousLoopState',
    },
    {
      id: 'meta_review',
      title: 'Lernanalyse',
      status: asString(rawReports.metaReview?.status, masterStatus.learning_strategy),
      last_activity: rawReports.metaReview?.updated_at_utc || masterStatus.last_meta_review,
      next_step: masterStatus.learning_strategy,
      warnings: asArray(firstDefined(rawReports.metaReview?.warnings, rawReports.metaReview?.observations)).map(String),
      report_key: 'metaReview',
    },
    {
      id: 'trust',
      title: 'Wissensvertrauen',
      status: masterStatus.knowledge_health,
      last_activity: masterStatus.updated_at_utc,
      next_step: masterStatus.pending_reviews ? 'Prüfungen bearbeiten' : 'Evidenz konsolidieren',
      warnings: masterStatus.top_review_priorities.slice(0, 3),
      report_key: 'masterStatus',
    },
  ];

  return steps.map((step) => ({
    ...step,
    report_path: reportSource(step.report_key)?.path || 'read-only Bridge',
    report_available: reportSource(step.report_key)?.available || false,
  }));
}

function buildDomainOverview(masterStatus, rawDomainStatus = {}) {
  const rawDomains = asArray(firstDefined(rawDomainStatus.domains, rawDomainStatus.domain_status, rawDomainStatus.domainStatus));
  const byDomain = new Map(rawDomains.map((item) => [asString(firstDefined(item.domain, item.id), ''), item]));

  return masterStatus.active_domains.map((domain) => {
    const raw = byDomain.get(domain) || {};
    const tradingPending =
      domain === 'trading' ? masterStatus.knowledge_items_needing_oos + masterStatus.validation_tasks_pending : 0;

    return {
      domain,
      title: domainTitle(domain),
      status: asString(firstDefined(raw.status, raw.health), domain === 'trading' ? 'needs_validation' : 'prepared'),
      knowledge_items: asNumber(firstDefined(raw.knowledge_items, raw.knowledgeItems, raw.items), domain === 'trading' ? 33 : 0),
      open_needs: asArray(firstDefined(raw.open_needs, raw.openNeeds, raw.needs))
        .map(String)
        .concat(tradingPending ? ['OOS-/Validierungsbedarf'] : []),
      last_check_utc: firstDefined(raw.last_check_utc, raw.lastCheckUtc, raw.last_scan_utc, raw.lastScanUtc, masterStatus.updated_at_utc),
      next_recommended_task: asString(
        firstDefined(raw.next_recommended_task, raw.nextRecommendedTask),
        domain === 'trading' ? 'run_walkforward_validation' : `scan_${domain}_domain`,
      ),
    };
  });
}

function buildRoleOverview(masterStatus, rawReports) {
  return [
    {
      role: 'Aufklärer',
      status: 'Quellen und Domänen prüfen',
      last_work: rawReports.cognitiveStatus?.last_checked_utc || masterStatus.updated_at_utc,
      result: `${masterStatus.active_domains.length} aktive Domänen`,
      warnings: masterStatus.top_blockers.filter((item) => item.includes('knowledge') || item.includes('source')).slice(0, 2),
    },
    {
      role: 'Analyst',
      status: 'Wissen strukturieren',
      last_work: masterStatus.updated_at_utc,
      result: `${masterStatus.weak_knowledge} schwache Wissenselemente`,
      warnings: masterStatus.weak_knowledge ? ['Wissensvertrauen niedrig'] : [],
    },
    {
      role: 'Planer',
      status: 'Tasks priorisieren',
      last_work: rawReports.planningStatus?.updated_at_utc || masterStatus.updated_at_utc,
      result: `${masterStatus.queued_tasks} offene Aufgaben`,
      warnings: masterStatus.top_blockers.slice(0, 2),
    },
    {
      role: 'Ausführer',
      status: asString(rawReports.taskExecutionState?.status, 'wartet'),
      last_work: rawReports.taskExecutionState?.updated_at_utc || masterStatus.last_autonomous_loop,
      result: 'Nur interne erlaubte Tasktypen',
      warnings: asArray(rawReports.taskExecutionState?.warnings).map(String),
    },
    {
      role: 'Prüfer',
      status: masterStatus.pending_reviews ? 'menschliche Prüfung offen' : 'keine offene Prüfung',
      last_work: masterStatus.updated_at_utc,
      result: `${masterStatus.pending_reviews} offene Prüfungen`,
      warnings: masterStatus.pending_reviews ? ['Human Review erforderlich'] : [],
    },
    {
      role: 'Lernanalyse',
      status: masterStatus.learning_strategy,
      last_work: masterStatus.last_meta_review,
      result: masterStatus.current_focus,
      warnings: masterStatus.top_blockers.filter((item) => item.includes('quality') || item.includes('trust')).slice(0, 2),
    },
  ];
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
  const timeControlRaw = rawReports.timeControl || reportFixtureRaw('timeControl');
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
  const humanReviewRaw = rawReports.humanReviewQueue || runtimeHumanReviewMock;
  const demoSignalFeedRaw =
    rawReports.demoSignalFeedStatus || operatorDashboardMock.demoSignalFeedStatus;
  const latestDemoSignalsRaw =
    rawReports.latestDemoSignals || operatorDashboardMock.latestDemoSignals;
  const forwardTestRaw =
    rawReports.forwardTestStatus || operatorDashboardMock.forwardTestStatus;

  const resource = normalizeResourceStatus(resourceRaw);
  const cleanup = normalizeCleanupPlan(cleanupRaw);
  const masterStatus = normalizeMasterStatus(masterRaw);
  const demoSignalFeed = normalizeDemoSignalFeedStatus(demoSignalFeedRaw, masterStatus);
  const latestDemoSignals = normalizeLatestDemoSignals(latestDemoSignalsRaw);
  const forwardTest = normalizeForwardTestStatus(forwardTestRaw, masterStatus);
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
  const reviewDecisionAssistantReport = reports.find((report) => report.key === 'reviewDecisionAssistant');
  const evidenceAutoLoopReport = reports.find((report) => report.key === 'evidenceAutoLoop');
  const normalizedReports = reviewDecisionAssistantReport || evidenceAutoLoopReport || (!rawReports.reviewDecisionAssistant && !rawReports.evidenceAutoLoop)
    ? reports
    : [
        ...reports,
        normalizeReportEntry(
          'reviewDecisionAssistant',
          {
            label: 'Review Decision Assistant',
            path: '/reports/review_decision_assistant/review_decision_assistant.json',
          },
          rawReports.reviewDecisionAssistant,
          DATA_SOURCE.FIXTURE,
          '',
        ),
        normalizeReportEntry(
          'evidenceAutoLoop',
          {
            label: 'Evidence Auto Loop',
            path: '/reports/evidence_auto_loop/evidence_auto_loop.json',
          },
          rawReports.evidenceAutoLoop,
          DATA_SOURCE.FIXTURE,
          '',
        ),
      ];

  return {
    masterStatus,
    masterStatusSource: masterStatusReport?.dataSource || (rawReports.masterStatus ? dataSource : DATA_SOURCE.FIXTURE),
    masterStatusWarning: masterStatusReport?.warning || '',
    humanReview: normalizeHumanReviewQueue(humanReviewRaw, masterStatus),
    cognitiveControl: buildCognitiveControl(masterStatus, rawReports, reports),
    domains: buildDomainOverview(masterStatus, rawReports.domainStatus),
    roles: buildRoleOverview(masterStatus, rawReports),
    supervisor: normalizeSupervisorState(supervisorRaw),
    schedulerJobs: normalizeSchedulerJobs(schedulerRaw),
    timeControl: normalizeTimeControl(timeControlRaw),
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
    demoSignalFeed,
    latestDemoSignals,
    forwardTest,
    reports: normalizedReports,
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
      humanReviewQueue: runtimeHumanReviewMock,
      cognitiveStatus: reportFixtureRaw('cognitiveStatus'),
      planningStatus: reportFixtureRaw('planningStatus'),
      taskExecutionState: reportFixtureRaw('taskExecutionState'),
      autonomousLoopState: reportFixtureRaw('autonomousLoopState'),
      metaReview: reportFixtureRaw('metaReview'),
      domainStatus: reportFixtureRaw('domainStatus'),
      reviewStatusConsistencyAudit: reportFixtureRaw('reviewStatusConsistencyAudit'),
      validationBacklogAnalyzer: reportFixtureRaw('validationBacklogAnalyzer'),
      knowledgeConsolidationExecutor: reportFixtureRaw('knowledgeConsolidationExecutor'),
      validationBacklogExecutor: reportFixtureRaw('validationBacklogExecutor'),
      strategyParameterResearchPlanner: reportFixtureRaw('strategyParameterResearchPlanner'),
      supervisorState: operatorDashboardMock.supervisorState,
      schedulerState: operatorDashboardMock.schedulerState,
      timeControl: reportFixtureRaw('timeControl'),
      resourceStatus: operatorDashboardMock.resourceStatus,
      nightlyState: operatorDashboardMock.nightlyState,
      demoSignalFeedStatus: operatorDashboardMock.demoSignalFeedStatus,
      latestDemoSignals: operatorDashboardMock.latestDemoSignals,
      forwardTestStatus: operatorDashboardMock.forwardTestStatus,
      knowledgeValidationAudit: reportFixtureRaw('knowledgeValidationAudit'),
      reviewPrioritizationAudit: reportFixtureRaw('reviewPrioritizationAudit'),
      autonomousImprovementQueue: reportFixtureRaw('autonomousImprovementQueue'),
      autonomousImprovementQueueSummary: reportFixtureRaw('autonomousImprovementQueueSummary'),
      autonomousImprovementWorkAreas: reportFixtureRaw('autonomousImprovementWorkAreas'),
      workAreaExecutorPolicy: reportFixtureRaw('workAreaExecutorPolicy'),
      nightlyWorkAreaStatus: reportFixtureRaw('nightlyWorkAreaStatus'),
      autonomousImprovementExecution: reportFixtureRaw('autonomousImprovementExecution'),
      trustedKnowledgeReviewGate: reportFixtureRaw('trustedKnowledgeReviewGate'),
      knowledgeTrustImprovementPlan: reportFixtureRaw('knowledgeTrustImprovementPlan'),
      ensemblePortfolioStatus: reportFixtureRaw('ensemblePortfolioStatus'),
      systemBHandoffBundle: systemBHandoffBundleMock,
      validateEnsembleSignalPackage: reportFixtureRaw('validateEnsembleSignalPackage'),
      setupRegistry: reportFixtureRaw('setupRegistry'),
      signalAgentSpecs: reportFixtureRaw('signalAgentSpecs'),
      multiAssetResearchStatus: reportFixtureRaw('multiAssetResearchStatus'),
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
      humanReviewQueue: dashboard.humanReviewQueue,
        reviewStatusConsistencyAudit: dashboard.reviewStatusConsistencyAudit,
        cognitiveStatus: dashboard.cognitiveStatus,
        planningStatus: dashboard.planningStatus,
        taskExecutionState: dashboard.taskExecutionState,
        autonomousLoopState: dashboard.autonomousLoopState,
        metaReview: dashboard.metaReview,
        domainStatus: dashboard.domainStatus,
        supervisorState: dashboard.supervisorState,
        schedulerState: dashboard.schedulerState,
        timeControl: dashboard.timeControl,
        resourceStatus: dashboard.resourceStatus,
        storageStatus: dashboard.storageStatus,
        cleanupPlan: dashboard.cleanupPlan,
        nightlyState: dashboard.nightlyState,
        demoSignalFeedStatus: dashboard.demoSignalFeedStatus,
        latestDemoSignals: dashboard.latestDemoSignals,
        forwardTestStatus: dashboard.forwardTestStatus,
        knowledgeValidationAudit: dashboard.knowledgeValidationAudit,
        validationBacklogAnalyzer: dashboard.validationBacklogAnalyzer,
        validationBacklogExecutor: dashboard.validationBacklogExecutor,
      reviewPrioritizationAudit: dashboard.reviewPrioritizationAudit,
      reviewDecisionAssistant: dashboard.reviewDecisionAssistant,
      tradingResearchSynthesizer: dashboard.tradingResearchSynthesizer,
      strategyMutationValidationPlanner: dashboard.strategyMutationValidationPlanner,
      evidenceAutoLoop: dashboard.evidenceAutoLoop,
        validationQueueRefill: dashboard.validationQueueRefill,
        evidenceValidationRunner: dashboard.evidenceValidationRunner,
        autonomousImprovementQueue: dashboard.autonomousImprovementQueue,
        autonomousImprovementQueueSummary: dashboard.autonomousImprovementQueueSummary,
        autonomousImprovementWorkAreas: dashboard.autonomousImprovementWorkAreas,
        workAreaExecutorPolicy: dashboard.workAreaExecutorPolicy,
        nightlyWorkAreaStatus: dashboard.nightlyWorkAreaStatus,
        autonomousImprovementExecution: dashboard.autonomousImprovementExecution,
        trustedKnowledgeReviewGate: dashboard.trustedKnowledgeReviewGate,
        knowledgeTrustImprovementPlan: dashboard.knowledgeTrustImprovementPlan,
        ensemblePortfolioStatus: dashboard.ensemblePortfolioStatus,
        systemBHandoffBundle: dashboard.systemBHandoffBundle,
        validateEnsembleSignalPackage: dashboard.validateEnsembleSignalPackage,
        setupRegistry: dashboard.setupRegistry,
        signalAgentSpecs: dashboard.signalAgentSpecs,
        multiAssetResearchStatus: dashboard.multiAssetResearchStatus,
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
  rawReports.ensemblePortfolioStatus = reportFixtureRaw('ensemblePortfolioStatus');
  rawReports.systemBHandoffBundle = reportFixtureRaw('systemBHandoffBundle');
  rawReports.validateEnsembleSignalPackage = reportFixtureRaw('validateEnsembleSignalPackage');
  rawReports.setupRegistry = reportFixtureRaw('setupRegistry');
  rawReports.signalAgentSpecs = reportFixtureRaw('signalAgentSpecs');
  rawReports.multiAssetResearchStatus = reportFixtureRaw('multiAssetResearchStatus');
  rawReports.knowledgeValidationAudit = reportFixtureRaw('knowledgeValidationAudit');
  rawReports.reviewStatusConsistencyAudit = reportFixtureRaw('reviewStatusConsistencyAudit');
  rawReports.validationBacklogAnalyzer = reportFixtureRaw('validationBacklogAnalyzer');
  rawReports.knowledgeConsolidationExecutor = reportFixtureRaw('knowledgeConsolidationExecutor');
  rawReports.validationBacklogExecutor = reportFixtureRaw('validationBacklogExecutor');
  rawReports.strategyParameterResearchPlanner = reportFixtureRaw('strategyParameterResearchPlanner');
  rawReports.tradingResearchSynthesizer = reportFixtureRaw('tradingResearchSynthesizer');
  rawReports.strategyMutationValidationPlanner = reportFixtureRaw('strategyMutationValidationPlanner');
  rawReports.reviewPrioritizationAudit = reportFixtureRaw('reviewPrioritizationAudit');
  rawReports.reviewDecisionAssistant = reportFixtureRaw('reviewDecisionAssistant');
  rawReports.evidenceAutoLoop = reportFixtureRaw('evidenceAutoLoop');
  rawReports.validationQueueRefill = reportFixtureRaw('validationQueueRefill');
  rawReports.evidenceValidationRunner = reportFixtureRaw('evidenceValidationRunner');
  rawReports.autonomousImprovementQueue = reportFixtureRaw('autonomousImprovementQueue');
  rawReports.autonomousImprovementQueueSummary = reportFixtureRaw('autonomousImprovementQueueSummary');
  rawReports.autonomousImprovementWorkAreas = reportFixtureRaw('autonomousImprovementWorkAreas');
  rawReports.workAreaExecutorPolicy = reportFixtureRaw('workAreaExecutorPolicy');
  rawReports.nightlyWorkAreaStatus = reportFixtureRaw('nightlyWorkAreaStatus');
  rawReports.autonomousImprovementExecution = reportFixtureRaw('autonomousImprovementExecution');
  rawReports.trustedKnowledgeReviewGate = reportFixtureRaw('trustedKnowledgeReviewGate');
  rawReports.knowledgeTrustImprovementPlan = reportFixtureRaw('knowledgeTrustImprovementPlan');
  rawReports.timeControl = reportFixtureRaw('timeControl');
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
