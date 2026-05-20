import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';
import { setupWatchMock } from '../fixtures/setupWatchMock';
import { de } from '../i18n/de';

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

export function createRuntimeHealthFallback(loadError = '') {
  const warning = loadError
    ? `Echte Laufzeitstatus-JSON konnte in diesem Browser-Kontext nicht geladen werden: ${loadError}`
    : 'Lokale Laufzeitstatus-Fixture wird verwendet.';

  return {
    data: normalizeRuntimeHealth(runtimeHealthMock, {
      label: de.common.fixtureFallback,
      url: 'src/fixtures/runtimeHealthMock.ts',
      readOnly: true,
    }),
    mode: 'fixture',
    warning,
  };
}

export async function loadRuntimeHealth() {
  if (!runtimeHealthDevUrl) {
    return createRuntimeHealthFallback('No runtime health URL configured.');
  }

  try {
    const raw = await readJsonReadOnly(runtimeHealthDevUrl);
    const runtimeHealth = normalizeRuntimeHealth(raw, {
      label: de.common.jsonSource,
      url: runtimeHealthDevUrl,
      readOnly: true,
    });

    return {
      data: await enrichRuntimeHealthFiles(runtimeHealth),
      mode: 'json',
      warning: '',
    };
  } catch (error) {
    return createRuntimeHealthFallback(error instanceof Error ? error.message : String(error));
  }
}

export function createSetupWatchFallback(loadError = '') {
  const warning = loadError
    ? `Echte Setup-Beobachtungs-JSON konnte in diesem Browser-Kontext nicht geladen werden: ${loadError}`
    : 'Lokale Setup-Beobachtungs-Fixture wird verwendet.';

  return {
    items: setupWatchMock.map(normalizeSetupWatch),
    mode: 'fixture',
    warning,
    sourcePath: `src/fixtures/setupWatchMock.ts (${de.common.fixtureFallback})`,
  };
}

export async function loadSetupWatches() {
  if (!setupWatchUrl) {
    return createSetupWatchFallback('No setup watch URL configured.');
  }

  try {
    const raw = await readJsonReadOnly(setupWatchUrl);
    const items = Array.isArray(raw) ? raw : raw?.candidates || raw?.setup_watches || [];

    return {
      items: items.map(normalizeSetupWatch),
      mode: 'json',
      warning: '',
      sourcePath: setupWatchPath,
    };
  } catch (error) {
    return createSetupWatchFallback(error instanceof Error ? error.message : String(error));
  }
}

export async function loadRuntimeEvents(timestampUtc, fallbackItems = []) {
  const url = getRuntimeEventStoreUrl(timestampUtc);

  if (!url) {
    return {
      items: fallbackItems,
      mode: 'fixture',
      warning: 'No runtime event URL configured.',
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
      mode: 'jsonl',
      warning: '',
      sourcePath: url,
    };
  } catch (error) {
    return {
      items: fallbackItems,
      mode: 'fixture',
      warning: `Echte Runtime-Events konnten in diesem Browser-Kontext nicht geladen werden: ${
        error instanceof Error ? error.message : String(error)
      }`,
      sourcePath: 'runtime events fixture',
    };
  }
}

export const runtimeDataAdapter = {
  loadRuntimeHealth,
  loadSetupWatches,
  loadRuntimeEvents,
  createRuntimeHealthFallback,
  createSetupWatchFallback,
};
