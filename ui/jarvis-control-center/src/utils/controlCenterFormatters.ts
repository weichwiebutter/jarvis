import { de as t } from '../i18n/de';

export const formatBool = (value) => (value ? 'true' : 'false');

export const confidencePercent = (value) => `${Math.round(Number(value || 0) * 100)}%`;

export const formatOptionalBool = (value) => {
  if (value === null || value === undefined) {
    return t.common.notReported;
  }

  return value ? t.common.active : t.common.inactive;
};

export const sourceModeLabel = (source) => {
  if (source === 'live_file' || source === 'json') {
    return t.common.liveBridgeSource;
  }

  if (source === 'unavailable') {
    return t.common.unavailable;
  }

  return t.common.fixtureFallback;
};

export const sourceTone = (source) => {
  if (source === 'live_file' || source === 'json') {
    return 'good';
  }

  if (source === 'unavailable') {
    return 'danger';
  }

  return 'warn';
};
