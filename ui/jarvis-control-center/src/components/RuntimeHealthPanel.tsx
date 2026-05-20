import { useEffect, useMemo, useState } from 'react';
import { createRuntimeDataFallback, loadRuntimeData } from '../data/runtimeDataAdapter';
import { de as t } from '../i18n/de';
import {
  formatBool,
  formatOptionalBool,
  sourceModeLabel,
  sourceTone,
} from '../utils/controlCenterFormatters';
import { MetricGrid, Panel, StatusPill, toneClass } from './StatusCard';

function buildRuntimeMetrics(runtimeHealth) {
  return [
    { label: t.runtime.runtimeState, value: runtimeHealth.runtime_state, tone: 'info' },
    { label: t.runtime.freeDiskGb, value: `${runtimeHealth.free_disk_gb} GB`, tone: 'good' },
    { label: t.runtime.pendingJobs, value: runtimeHealth.pending_jobs, tone: 'warn' },
    { label: t.runtime.runningJobs, value: runtimeHealth.running_jobs, tone: 'info' },
    {
      label: t.runtime.failedJobs,
      value: runtimeHealth.failed_jobs,
      tone: runtimeHealth.failed_jobs ? 'danger' : 'good',
    },
    {
      label: t.runtime.quarantinedJobs,
      value: runtimeHealth.quarantined_jobs,
      tone: runtimeHealth.quarantined_jobs ? 'danger' : 'good',
    },
    { label: t.runtime.activeSetupWatches, value: runtimeHealth.active_setup_watches, tone: 'warn' },
    { label: t.runtime.lastSnapshotId, value: runtimeHealth.last_snapshot_id || '-', tone: 'info' },
  ];
}

function buildRuntimeSafetyFlags(runtimeHealth) {
  return [
    {
      label: t.safety.safeMode,
      value: runtimeHealth.safe_mode,
      expected: false,
      tone: 'good',
      detail: t.safety.safeModeDetail,
    },
    {
      label: t.safety.noAutoTrading,
      value: runtimeHealth.no_auto_trading,
      expected: true,
      tone: 'warn',
      detail: t.safety.noAutoTradingDetail,
    },
    {
      label: t.safety.humanReviewRequired,
      value: runtimeHealth.human_review_required,
      expected: true,
      tone: 'warn',
      detail: t.safety.humanReviewDetail,
    },
  ];
}

function RuntimeSafetyFlags({ runtimeHealth }) {
  const runtimeSafetyFlags = buildRuntimeSafetyFlags(runtimeHealth);

  return (
    <div className="runtime-safety-strip" aria-label="Hermes Runtime v1 safety flags">
      {runtimeSafetyFlags.map((flag) => {
        const matchesExpected = flag.value === flag.expected;
        const tone = matchesExpected ? flag.tone : 'danger';

        return (
          <article className={`runtime-flag ${toneClass(tone)}`} key={flag.label}>
            <div>
              <span>{flag.label}</span>
              <strong>{formatBool(flag.value)}</strong>
            </div>
            <p>{flag.detail}</p>
          </article>
        );
      })}
    </div>
  );
}

function RuntimeHealthCard({ runtimeHealth, dataSource }) {
  const statusTone =
    runtimeHealth.last_error ? 'danger' : dataSource === 'live_file' ? 'good' : 'warn';
  const fixtureActive = dataSource === 'fixture';

  return (
    <div className="runtime-health-card">
      <div>
        <p className="eyebrow">{t.runtime.statusBadge}</p>
        <strong className={toneClass(statusTone)}>{runtimeHealth.status}</strong>
      </div>
      <div>
        <span>{t.common.source}</span>
        <b>{sourceModeLabel(dataSource)}</b>
      </div>
      <div>
        <span>{t.common.timestamp}</span>
        <b>{runtimeHealth.timestamp_utc || t.common.notReported}</b>
      </div>
      <div>
        <span>{t.common.lastError}</span>
        <b className={runtimeHealth.last_error ? 'tone-danger' : 'tone-good'}>
          {runtimeHealth.last_error || t.common.none}
        </b>
      </div>
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
    </div>
  );
}

function StorageStatus({ runtimeHealth }) {
  const hasJobIssues = runtimeHealth.failed_jobs > 0 || runtimeHealth.quarantined_jobs > 0;

  return (
    <div className="storage-status">
      <div>
        <span>{t.runtime.storageStatus}</span>
        <strong className="tone-good">{runtimeHealth.free_disk_gb} GB frei</strong>
      </div>
      <div>
        <span>{t.runtime.queue}</span>
        <strong className={hasJobIssues ? 'tone-danger' : 'tone-info'}>
          {runtimeHealth.pending_jobs} wartend / {runtimeHealth.running_jobs} laufend
        </strong>
      </div>
      <div>
        <span>{t.runtime.problemJobs}</span>
        <strong className={hasJobIssues ? 'tone-danger' : 'tone-good'}>
          {runtimeHealth.failed_jobs} fehlgeschlagen / {runtimeHealth.quarantined_jobs} in Quarantaene
        </strong>
      </div>
    </div>
  );
}

function RuntimeCapabilityGrid({ runtimeHealth }) {
  const capabilities = [
    {
      label: t.runtime.eventStoreActive,
      value: runtimeHealth.event_store_active,
      detail: 'Optionales Laufzeit-Flag; Fixture meldet aktiv.',
    },
    {
      label: t.runtime.replayManifestAvailable,
      value: runtimeHealth.replay_manifest_available,
      detail: 'Optionales Laufzeit-Flag; Fixture meldet vorhanden.',
    },
  ];

  return (
    <div className="runtime-capability-grid">
      {capabilities.map((capability) => {
        const tone =
          capability.value === null || capability.value === undefined
            ? 'muted'
            : capability.value
              ? 'good'
              : 'warn';

        return (
          <article className={`runtime-capability ${toneClass(tone)}`} key={capability.label}>
            <span>{capability.label}</span>
            <strong>{formatOptionalBool(capability.value)}</strong>
            <p>{capability.detail}</p>
          </article>
        );
      })}
    </div>
  );
}

function RuntimeEventTimeline({ runtimeHealth, dataSource }) {
  const events = [
    {
      time: runtimeHealth.timestamp_utc || t.common.latest,
      title:
        dataSource === 'live_file' ? t.runtime.runtimeJsonLoaded : t.runtime.runtimeFixtureLoaded,
      detail: sourceModeLabel(dataSource),
    },
    {
      time: t.common.readOnly,
      title: t.runtime.storageObserved,
      detail: `${runtimeHealth.free_disk_gb} GB freier Speicher gemeldet`,
    },
    {
      time: t.common.readOnly,
      title: t.runtime.snapshotObserved,
      detail: runtimeHealth.last_snapshot_id || 'Kein Snapshot gemeldet',
    },
  ];

  return (
    <ol className="runtime-event-timeline">
      {events.map((event) => (
        <li key={`${event.title}-${event.detail}`}>
          <span>{event.time}</span>
          <div>
            <strong>{event.title}</strong>
            <p>{event.detail}</p>
          </div>
        </li>
      ))}
    </ol>
  );
}

export function RuntimeHealthPanel() {
  const [runtimeData, setRuntimeData] = useState(() => createRuntimeDataFallback());
  const runtimeHealth = runtimeData.runtimeHealth;
  const runtimeHealthSource = runtimeData.sources.runtimeHealth;
  const runtimeMetrics = useMemo(() => buildRuntimeMetrics(runtimeHealth), [runtimeHealth]);

  useEffect(() => {
    let active = true;

    loadRuntimeData().then((nextState) => {
      if (active) {
        setRuntimeData(nextState);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return (
    <Panel
      eyebrow={t.runtime.eyebrow}
      title={t.runtime.title}
      action={
        <StatusPill tone={sourceTone(runtimeHealthSource.dataSource)}>
          {sourceModeLabel(runtimeHealthSource.dataSource)}
        </StatusPill>
      }
      className="runtime-panel"
    >
      <RuntimeHealthCard
        runtimeHealth={runtimeHealth}
        dataSource={runtimeHealthSource.dataSource}
      />
      <RuntimeSafetyFlags runtimeHealth={runtimeHealth} />
      <StorageStatus runtimeHealth={runtimeHealth} />
      <RuntimeCapabilityGrid runtimeHealth={runtimeHealth} />
      <MetricGrid items={runtimeMetrics} />
      <RuntimeEventTimeline
        runtimeHealth={runtimeHealth}
        dataSource={runtimeHealthSource.dataSource}
      />
      <div className="inline-note">
        {t.runtime.sourceNote} <code>{runtimeHealthSource.path || runtimeHealth.source_path}</code>
      </div>
    </Panel>
  );
}
