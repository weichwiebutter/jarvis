import { useEffect, useState } from 'react';
import { createSetupWatchFallback, loadSetupWatches } from '../services/setupWatchLoader';
import { de as t } from '../i18n/de';
import { confidencePercent, sourceModeLabel } from '../utils/controlCenterFormatters';
import { Panel, StatusPill, toneClass } from './StatusCard';

function setupStatusTone(status) {
  switch (status) {
    case 'watching':
      return 'warn';
    case 'armed':
      return 'info';
    case 'triggered':
      return 'good';
    case 'expired':
      return 'muted';
    case 'invalidated':
      return 'danger';
    default:
      return 'info';
  }
}

function setupStatusLabel(status) {
  switch (status) {
    case 'watching':
      return t.setupWatch.watching;
    case 'armed':
      return t.setupWatch.armed;
    case 'triggered':
      return t.setupWatch.triggered;
    case 'expired':
      return t.setupWatch.expired;
    case 'invalidated':
      return t.setupWatch.invalidated;
    default:
      return status;
  }
}

function setupBiasKey(bias) {
  if (bias === 'long') {
    return 'long';
  }

  if (bias === 'short') {
    return 'short';
  }

  if (bias === 'neutral') {
    return 'neutral';
  }

  return 'breakout';
}

function setupBiasLabel(bias) {
  switch (setupBiasKey(bias)) {
    case 'long':
      return t.setupWatch.long;
    case 'short':
      return t.setupWatch.short;
    case 'neutral':
      return t.setupWatch.neutral;
    default:
      return t.setupWatch.possibleBreakout;
  }
}

function setupLifecycle(status) {
  const activeIndex = {
    watching: 0,
    armed: 1,
    triggered: 2,
    expired: 3,
    invalidated: 3,
  }[status] ?? 0;

  return [
    t.setupWatch.lifecycleWatching,
    t.setupWatch.lifecycleArmed,
    t.setupWatch.lifecycleTriggered,
    t.setupWatch.lifecycleReview,
  ].map((label, index) => ({
    label,
    state: index < activeIndex ? 'complete' : index === activeIndex ? 'active' : 'pending',
  }));
}

export function SetupWatchPanel() {
  const [setupWatchState, setSetupWatchState] = useState(() => createSetupWatchFallback());
  const sourceTone = setupWatchState.mode === 'json' ? 'good' : 'warn';

  useEffect(() => {
    let active = true;

    loadSetupWatches().then((nextState) => {
      if (active) {
        setSetupWatchState(nextState);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return (
    <Panel
      eyebrow={t.setupWatch.eyebrow}
      title={t.setupWatch.title}
      action={<StatusPill tone={sourceTone}>{sourceModeLabel(setupWatchState.mode)}</StatusPill>}
      className="trading-panel"
    >
      <div className="setup-safety-strip">
        <strong>{t.setupWatch.alertsOnly}</strong>
        <strong>{t.setupWatch.approvalRequired}</strong>
        <strong>{t.setupWatch.noOrders}</strong>
      </div>
      <div className="watch-source">
        <span>{setupWatchState.sourcePath}</span>
        <strong className="tone-warn">{t.header.noAutoTrading}</strong>
      </div>
      {setupWatchState.warning ? <p className="runtime-warning">{setupWatchState.warning}</p> : null}
      <div className="setup-card-list">
        {setupWatchState.items.map((item) => {
          const tone = setupStatusTone(item.status);
          const biasKey = setupBiasKey(item.bias);
          const lifecycle = setupLifecycle(item.status);

          return (
            <article className={`setup-card ${toneClass(tone)} bias-${biasKey}`} key={item.setup_id}>
              <div className="setup-card-top">
                <div className="setup-symbol-block">
                  <span>{t.setupWatch.direction}</span>
                  <strong>{item.symbol}</strong>
                </div>
                <div className={`setup-bias setup-bias-${biasKey}`}>
                  <span>{setupBiasLabel(item.bias)}</span>
                </div>
              </div>
              <div className="setup-card-header">
                <span>{t.setupWatch.status}</span>
                <StatusPill tone={tone}>{setupStatusLabel(item.status)}</StatusPill>
              </div>
              <div className="confidence-meter">
                <div>
                  <span>{t.setupWatch.confidence}</span>
                  <strong>{confidencePercent(item.confidence)}</strong>
                </div>
                <i style={{ width: confidencePercent(item.confidence) }} />
              </div>
              <div className="setup-lifecycle" aria-label={t.setupWatch.lifecycle}>
                <span>{t.setupWatch.lifecycle}</span>
                <div className="setup-lifecycle-steps">
                  {lifecycle.map((step) => (
                    <b className={`setup-lifecycle-step is-${step.state}`} key={step.label}>
                      {step.label}
                    </b>
                  ))}
                </div>
              </div>
              <div className="setup-levels">
                <div>
                  <span>{t.setupWatch.entry}</span>
                  <strong>{item.entry_zone}</strong>
                </div>
                <div>
                  <span>{t.setupWatch.stop}</span>
                  <strong>{item.suggested_stop_loss}</strong>
                </div>
                <div>
                  <span>{t.setupWatch.target}</span>
                  <strong>{item.suggested_target}</strong>
                </div>
                <div>
                  <span>{t.setupWatch.invalidation}</span>
                  <strong>{item.invalidation_level}</strong>
                </div>
              </div>
              <div className="setup-trigger">
                <span>{t.setupWatch.trigger}</span>
                <p>{item.trigger_condition}</p>
              </div>
              <div className="setup-foot">
                <span>
                  {t.setupWatch.timeWindow}: {item.time_window_minutes} {t.setupWatch.minuteWindow}
                </span>
                <span>{item.notes}</span>
              </div>
            </article>
          );
        })}
      </div>
      <div className="inline-note">{t.setupWatch.note}</div>
    </Panel>
  );
}
