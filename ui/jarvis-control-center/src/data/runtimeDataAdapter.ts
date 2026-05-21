import { runtimeFeatureSignalExportsMock } from '../fixtures/runtimeFeatureSignalExportsMock';
import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';
import { runtimeJobsMock } from '../fixtures/runtimeJobsMock';
import { runtimeStorageMock } from '../fixtures/runtimeStorageMock';
import { setupWatchMock } from '../fixtures/setupWatchMock';
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
const replayManifestUrl = __HERMES_REPLAY_MANIFEST_URL__;
const setupWatchUrl = __HERMES_SETUP_WATCH_URL__;
const runtimeHealthPath = __HERMES_RUNTIME_HEALTH_PATH__;
const runtimeJobsPath = __HERMES_RUNTIME_JOBS_PATH__;
const featureExportPath = __HERMES_FEATURE_EXPORT_PATH__;
const signalExportPath = __HERMES_SIGNAL_EXPORT_PATH__;
const setupWatchPath = __HERMES_SETUP_WATCH_PATH__;

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
      root: asString(raw?.root, 'HermesRuntime/data'),
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
    const raw = await readJsonReadOnly(runtimeHealthDevUrl);
    const source = buildSource(DATA_SOURCE.LIVE_FILE, runtimeHealthPath);
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
    const raw = await readJsonReadOnly(setupWatchUrl);
    const items = Array.isArray(raw) ? raw : raw?.candidates || raw?.setup_watches || [];

    return {
      setupWatches: items.map(normalizeSetupWatch),
      source: buildSource(DATA_SOURCE.LIVE_FILE, setupWatchPath),
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
    const raw = await readJsonReadOnly(runtimeJobsUrl);

    return {
      jobs: normalizeRuntimeJobs(raw),
      dataSource: DATA_SOURCE.LIVE_FILE,
      warnings: [],
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

export const runtimeDataAdapter = {
  loadRuntimeData,
  loadRuntimeHealth,
  loadSetupWatches,
  loadRuntimeEvents,
  loadRuntimeTimelineEvents,
  loadRuntimeJobs,
  loadRuntimeStorage,
  loadFeatureSignalExports,
  createRuntimeDataFallback,
  createRuntimeHealthFallback,
  createSetupWatchFallback,
  createRuntimeEventFallback,
  createRuntimeJobsFallback,
  createRuntimeStorageFallback,
  createFeatureSignalExportsFallback,
};
