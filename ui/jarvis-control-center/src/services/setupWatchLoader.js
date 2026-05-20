import { setupWatchMock } from '../fixtures/setupWatchMock';
import { de } from '../i18n/de';

const setupWatchUrl = __HERMES_SETUP_WATCH_URL__;
const setupWatchPath = __HERMES_SETUP_WATCH_PATH__;

function asNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function normalizeSetupWatch(raw) {
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
    const response = await fetch(setupWatchUrl, {
      cache: 'no-store',
      credentials: 'same-origin',
    });

    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`.trim());
    }

    const raw = await response.json();
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
