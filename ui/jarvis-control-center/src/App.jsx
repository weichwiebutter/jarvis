import { useEffect, useMemo, useState } from 'react';
import {
  createRuntimeHealthFallback,
  loadRuntimeHealth,
} from './services/runtimeHealthLoader';
import {
  createSetupWatchFallback,
  loadSetupWatches,
} from './services/setupWatchLoader';
import { de } from './i18n/de';

const t = de;
const formatBool = (value) => (value ? 'true' : 'false');
const confidencePercent = (value) => `${Math.round(Number(value || 0) * 100)}%`;
const formatOptionalBool = (value) => {
  if (value === null || value === undefined) {
    return t.common.notReported;
  }

  return value ? t.common.active : t.common.inactive;
};

function buildRuntimeMetrics(runtimeHealth) {
  return [
    { label: t.runtime.runtimeState, value: runtimeHealth.runtime_state, tone: 'info' },
    { label: t.runtime.freeDiskGb, value: `${runtimeHealth.free_disk_gb} GB`, tone: 'good' },
    { label: t.runtime.pendingJobs, value: runtimeHealth.pending_jobs, tone: 'warn' },
    { label: t.runtime.runningJobs, value: runtimeHealth.running_jobs, tone: 'info' },
    { label: t.runtime.failedJobs, value: runtimeHealth.failed_jobs, tone: runtimeHealth.failed_jobs ? 'danger' : 'good' },
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

const learningQueue = [
  { title: 'XAUUSD Pullback-Cluster', meta: 'Prediction -> Ergebnis wartet auf Review', score: '0.74' },
  { title: 'EURUSD Session-Filter', meta: 'No-Trade-Zone als Lernkandidat', score: '0.62' },
  { title: 'GER40 Volatilitaets-Rejection', meta: 'Menschliche Freigabe vor Memory-Write erforderlich', score: '0.58' },
];

const agentActivity = [
  { time: '08:10 UTC', title: 'Research-Agent', detail: 'Overnight-Notizen fuer Review zusammengefasst' },
  { time: '08:14 UTC', title: 'Trading-Beobachter', detail: 'XAUUSD-Setup in Beobachtung verschoben' },
  { time: '08:17 UTC', title: 'Memory-Agent', detail: '3 Lernkandidaten in Warteschlange' },
  { time: '08:22 UTC', title: 'Laufzeit-Beobachter', detail: 'Letzten Health-Report als Demo-Sample geladen' },
];

const providers = [
  { name: 'GPT-5.5', role: t.providers.seniorArchitect, status: t.providers.manualRoute },
  { name: 'Ollama / Qwen', role: t.providers.localWorker, status: t.providers.ready },
  { name: 'OpenRouter', role: t.providers.fallback, status: t.providers.disabled },
];

function toneClass(tone) {
  return `tone-${tone || 'info'}`;
}

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
      return 'beobachtet';
    case 'armed':
      return 'bereit';
    case 'triggered':
      return 'ausgeloest';
    case 'expired':
      return 'abgelaufen';
    case 'invalidated':
      return 'ungueltig';
    default:
      return status;
  }
}

function sourceModeLabel(mode) {
  return mode === 'json' ? 'JSON' : t.common.fixtureFallback;
}

function StatusPill({ children, tone = 'info' }) {
  return <span className={`status-pill ${toneClass(tone)}`}>{children}</span>;
}

function Panel({ eyebrow, title, action, children, className = '' }) {
  return (
    <section className={`panel ${className}`}>
      <div className="panel-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

function MetricGrid({ items }) {
  return (
    <div className="metric-grid">
      {items.map((item) => (
        <div className="metric" key={item.label}>
          <span>{item.label}</span>
          <strong className={toneClass(item.tone)}>{item.value}</strong>
        </div>
      ))}
    </div>
  );
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

function RuntimeHealthCard({ runtimeHealth, mode, warning }) {
  const statusTone = runtimeHealth.last_error ? 'danger' : mode === 'json' ? 'good' : 'warn';

  return (
    <div className="runtime-health-card">
      <div>
        <p className="eyebrow">{t.runtime.statusBadge}</p>
        <strong className={toneClass(statusTone)}>{runtimeHealth.status}</strong>
      </div>
      <div>
        <span>{t.common.source}</span>
        <b>{mode === 'json' ? t.common.jsonSource : t.common.fixtureFallback}</b>
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
      {warning ? <p className="runtime-warning">{warning}</p> : null}
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

function RuntimeEventTimeline({ runtimeHealth, mode }) {
  const events = [
    {
      time: runtimeHealth.timestamp_utc || t.common.latest,
      title: mode === 'json' ? t.runtime.runtimeJsonLoaded : t.runtime.runtimeFixtureLoaded,
      detail: mode === 'json' ? t.common.jsonSource : t.common.fixtureFallback,
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

function Header() {
  return (
    <header className="hero-shell">
      <div className="hero-copy">
        <p className="eyebrow">{t.header.eyebrow}</p>
        <h1>{t.header.title}</h1>
        <p>{t.header.copy}</p>
      </div>
      <div className="hero-status" aria-label="Systemstatus Zusammenfassung">
        <StatusPill tone="good">{t.header.hermesOnline}</StatusPill>
        <StatusPill tone="warn">{t.header.noAutoTrading}</StatusPill>
        <StatusPill tone="info">{t.header.approvalRequired}</StatusPill>
      </div>
    </header>
  );
}

function RuntimePanel() {
  const [runtimeHealthState, setRuntimeHealthState] = useState(() =>
    createRuntimeHealthFallback(),
  );
  const runtimeHealth = runtimeHealthState.data;
  const runtimeMetrics = useMemo(() => buildRuntimeMetrics(runtimeHealth), [runtimeHealth]);
  const sourceTone = runtimeHealthState.mode === 'json' ? 'good' : 'warn';

  useEffect(() => {
    let active = true;

    loadRuntimeHealth().then((nextState) => {
      if (active) {
        setRuntimeHealthState(nextState);
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
      action={<StatusPill tone={sourceTone}>{sourceModeLabel(runtimeHealthState.mode)}</StatusPill>}
      className="runtime-panel"
    >
      <RuntimeHealthCard
        runtimeHealth={runtimeHealth}
        mode={runtimeHealthState.mode}
        warning={runtimeHealthState.warning}
      />
      <RuntimeSafetyFlags runtimeHealth={runtimeHealth} />
      <StorageStatus runtimeHealth={runtimeHealth} />
      <RuntimeCapabilityGrid runtimeHealth={runtimeHealth} />
      <MetricGrid items={runtimeMetrics} />
      <RuntimeEventTimeline runtimeHealth={runtimeHealth} mode={runtimeHealthState.mode} />
      <div className="inline-note">
        {t.runtime.sourceNote} <code>{runtimeHealth.source_path}</code>
      </div>
    </Panel>
  );
}

function HermesBrainPanel() {
  return (
    <Panel eyebrow={t.hermesBrain.eyebrow} title={t.hermesBrain.title} action={<StatusPill tone="info">{t.hermesBrain.status}</StatusPill>}>
      <div className="brain-grid">
        <div>
          <span className="node-label">{t.hermesBrain.planner}</span>
          <strong>{t.hermesBrain.ready}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.memory}</span>
          <strong>{t.hermesBrain.approvalGated}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.learning}</span>
          <strong>{t.hermesBrain.reviewOnly}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.delegation}</span>
          <strong>{t.hermesBrain.visibleChains}</strong>
        </div>
      </div>
      <p className="panel-copy">{t.hermesBrain.copy}</p>
    </Panel>
  );
}

function TradingWatchPanel() {
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
      <div className="watch-source">
        <span>{setupWatchState.sourcePath}</span>
        <strong className="tone-warn">{t.setupWatch.alertsOnly}</strong>
      </div>
      {setupWatchState.warning ? <p className="runtime-warning">{setupWatchState.warning}</p> : null}
      <div className="setup-card-list">
        {setupWatchState.items.map((item) => {
          const tone = setupStatusTone(item.status);

          return (
            <article className={`setup-card ${toneClass(tone)}`} key={item.setup_id}>
              <div className="setup-card-header">
                <div>
                  <strong>{item.symbol}</strong>
                  <span>{item.bias}</span>
                </div>
                <StatusPill tone={tone}>{setupStatusLabel(item.status)}</StatusPill>
              </div>
              <div className="confidence-meter">
                <div>
                  <span>{t.setupWatch.confidence}</span>
                  <strong>{confidencePercent(item.confidence)}</strong>
                </div>
                <i style={{ width: confidencePercent(item.confidence) }} />
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
                <span>{item.time_window_minutes} {t.setupWatch.minuteWindow}</span>
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

function LearningQueuePanel() {
  return (
    <Panel eyebrow={t.learningQueue.eyebrow} title={t.learningQueue.title} action={<StatusPill tone="warn">{t.learningQueue.pending}</StatusPill>}>
      <div className="queue-list">
        {learningQueue.map((item) => (
          <article className="queue-item" key={item.title}>
            <div>
              <strong>{item.title}</strong>
              <span>{item.meta}</span>
            </div>
            <b>{item.score}</b>
          </article>
        ))}
      </div>
    </Panel>
  );
}

function AgentTimelinePanel() {
  return (
    <Panel eyebrow={t.agents.eyebrow} title={t.agents.title} action={<StatusPill tone="info">{t.agents.mockStream}</StatusPill>}>
      <ol className="timeline">
        {agentActivity.map((event) => (
          <li key={`${event.time}-${event.title}`}>
            <time>{event.time}</time>
            <div>
              <strong>{event.title}</strong>
              <span>{event.detail}</span>
            </div>
          </li>
        ))}
      </ol>
    </Panel>
  );
}

function SafetyPanel() {
  return (
    <Panel eyebrow={t.safety.eyebrow} title={t.safety.title} action={<StatusPill tone="danger">{t.safety.locked}</StatusPill>}>
      <div className="safety-stack">
        <div className="safety-row">
          <span>{t.safety.autoTrading}</span>
          <strong className="tone-danger">{t.safety.blocked}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.humanApproval}</span>
          <strong className="tone-warn">{t.safety.required}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.silentLearning}</span>
          <strong className="tone-danger">{t.safety.disabled}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.martingaleGrid}</span>
          <strong className="tone-danger">{t.safety.notAllowed}</strong>
        </div>
      </div>
    </Panel>
  );
}

function CostProviderPanel() {
  return (
    <Panel eyebrow={t.providers.eyebrow} title={t.providers.title} action={<StatusPill tone="good">{t.providers.costVisible}</StatusPill>}>
      <div className="provider-list">
        {providers.map((provider) => (
          <div className="provider-row" key={provider.name}>
            <strong>{provider.name}</strong>
            <span>{provider.role}</span>
            <StatusPill tone={provider.status === t.providers.ready ? 'good' : provider.status === t.providers.disabled ? 'muted' : 'info'}>
              {provider.status}
            </StatusPill>
          </div>
        ))}
      </div>
    </Panel>
  );
}

export default function App() {
  return (
    <main className="app-shell">
      <Header />
      <div className="dashboard-grid">
        <RuntimePanel />
        <HermesBrainPanel />
        <TradingWatchPanel />
        <LearningQueuePanel />
        <AgentTimelinePanel />
        <SafetyPanel />
        <CostProviderPanel />
      </div>
    </main>
  );
}
