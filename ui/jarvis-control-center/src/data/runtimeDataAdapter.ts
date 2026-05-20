import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';
import { setupWatchMock } from '../fixtures/setupWatchMock';
import { de } from '../i18n/de';

export const DATA_SOURCE = {
  LIVE_FILE: 'live_file',
  FIXTURE: 'fixture',
  UNAVAILABLE: 'unavailable',
} as const;

const runtimeHealthDevUrl = __HERMES_RUNTIME_HEALTH_URL__;
const runtimeEventsBaseUrl = __HERMES_RUNTIME_EVENTS_BASE_URL__;
const replayManifestUrl = __HERMES_REPLAY_MANIFEST_URL__;
const setupWatchUrl = __HERMES_SETUP_WATCH_URL__;
const runtimeHealthPath = __HERMES_RUNTIME_HEALTH_PATH__;
const setupWatchPath = __HERMES_SETUP_WATCH_PATH__;

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

export async function loadRuntimeEvents(timestampUtc, fallbackItems = []) {
  const url = getRuntimeEventStoreUrl(timestampUtc);

  if (!url) {
    return {
      items: fallbackItems,
      dataSource: DATA_SOURCE.FIXTURE,
      warnings: ['Runtime Event URL ist nicht konfiguriert.'],
      sourcePath: 'runtime events fixture',
    };
  }

  try {
    const text = await readTextReadOnly(url);
    const items = text
      .split('\n')
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => JSON.parse(line));

    return {
      items,
      dataSource: DATA_SOURCE.LIVE_FILE,
      warnings: [],
      sourcePath: url,
    };
  } catch (error) {
    return {
      items: fallbackItems,
      dataSource: DATA_SOURCE.FIXTURE,
      warnings: [warningFromError('Runtime Events nicht erreichbar', error)],
      sourcePath: 'runtime events fixture',
    };
  }
}

export const runtimeDataAdapter = {
  loadRuntimeData,
  loadRuntimeHealth,
  loadSetupWatches,
  loadRuntimeEvents,
  createRuntimeDataFallback,
  createRuntimeHealthFallback,
  createSetupWatchFallback,
};
