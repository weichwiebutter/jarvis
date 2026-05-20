import { runtimeHealthMock } from '../fixtures/runtimeHealthMock';

const runtimeHealthDevUrl = __HERMES_RUNTIME_HEALTH_URL__;
const runtimeEventsBaseUrl = __HERMES_RUNTIME_EVENTS_BASE_URL__;
const replayManifestUrl = __HERMES_REPLAY_MANIFEST_URL__;
const runtimeHealthPath = __HERMES_RUNTIME_HEALTH_PATH__;

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

function getRuntimeEventStoreUrl(timestampUtc) {
  if (!timestampUtc || !runtimeEventsBaseUrl) {
    return '';
  }

  const date = String(timestampUtc).slice(0, 10);
  return date ? `${runtimeEventsBaseUrl}/${date}.runtime.jsonl` : '';
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
    ? `Real runtime health JSON could not be loaded in this browser context: ${loadError}`
    : 'Using local runtime health fixture.';

  return {
    data: normalizeRuntimeHealth(runtimeHealthMock, {
      label: 'Fixture fallback',
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
    const response = await fetch(runtimeHealthDevUrl, {
      cache: 'no-store',
      credentials: 'same-origin',
    });

    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`.trim());
    }

    const raw = await response.json();

    const runtimeHealth = normalizeRuntimeHealth(raw, {
      label: 'HermesRuntime JSON',
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
