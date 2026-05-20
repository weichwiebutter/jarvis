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

const learningCandidates = [
  {
    id: 'learn-trading-xauusd-pullback',
    type: 'Trading-Setup',
    title: 'XAUUSD Pullback als Lernkandidat',
    description: 'Setup-Beobachtung war konsistent mit Pullback-Rejection und soll fuer spaetere Bewertung markiert werden.',
    source: 'Setup-Beobachtung / Prediction-Feedback',
    risk: 'high',
    status: 'review',
    action: 'Nur nach Ergebnisvergleich und Freigabe als Regelkandidat speichern.',
  },
  {
    id: 'learn-routing-local-worker',
    type: 'Routing-Hinweis',
    title: 'Lokaler Worker fuer kleine UI-Aufgaben',
    description: 'Wiederkehrende kleine React-Textanpassungen koennten spaeter bevorzugt lokal geroutet werden.',
    source: 'Codex Routing Beobachtung',
    risk: 'medium',
    status: 'open',
    action: 'Als Routing-Hypothese vormerken, nicht automatisch aktivieren.',
  },
  {
    id: 'learn-error-pattern-file-access',
    type: 'Fehlerpattern',
    title: 'Browser blockiert lokale Runtime-Dateien',
    description: 'Vite /@fs-Zugriff kann im statischen Build blockiert sein; Fixture-Fallback muss sichtbar bleiben.',
    source: 'Laufzeitstatus-Loader',
    risk: 'low',
    status: 'approved',
    action: 'Als UI-Hinweis behalten und bei echten Connectors erneut pruefen.',
  },
  {
    id: 'learn-skill-proposal-storage',
    type: 'Skill-Vorschlag',
    title: 'Storage-Retention Dry-Run Skill',
    description: 'Spaeterer Skill koennte nur lesend Datenklassen scannen und Cleanup-Plaene als Vorschlag zeigen.',
    source: 'Speicher-Retention-Policy',
    risk: 'high',
    status: 'rejected',
    action: 'Nicht aktivieren; erst nach Approval-Flow und Dry-Run-UI erneut bewerten.',
  },
];

const runtimeEvents = [
  {
    id: 'runtime-started',
    time: '08:00:03 UTC',
    eventType: 'RuntimeStarted',
    category: 'runtime',
    severity: 'info',
    source: 'RuntimeHost',
    description: 'Hermes Runtime v1 wurde lokal gestartet und initialisiert.',
  },
  {
    id: 'storage-initialized',
    time: '08:00:05 UTC',
    eventType: 'StorageInitialized',
    category: 'runtime',
    severity: 'info',
    source: 'StorageManager',
    description: 'Storage-Pfade, Event Store und Snapshot-Verzeichnisse wurden geprueft.',
  },
  {
    id: 'snapshot-created',
    time: '08:00:11 UTC',
    eventType: 'SnapshotCreated',
    category: 'runtime',
    severity: 'info',
    source: 'SnapshotManager',
    description: 'Ein Runtime-Snapshot wurde geschrieben und mit Hash im Manifest referenziert.',
  },
  {
    id: 'replay-manifest-created',
    time: '08:00:15 UTC',
    eventType: 'ReplayManifestCreated',
    category: 'runtime',
    severity: 'info',
    source: 'ReplayManifestService',
    description: 'Demo-Replay-Manifest fuer spaetere Backtest- und Research-Laeufe ist verfuegbar.',
  },
  {
    id: 'setup-watch-created',
    time: '08:01:20 UTC',
    eventType: 'SetupWatchCreated',
    category: 'trading',
    severity: 'warning',
    source: 'SetupWatchService',
    description: 'XAUUSD Long-Szenario wurde als Beobachtung markiert; keine Orderausfuehrung.',
  },
  {
    id: 'learning-candidate-created',
    time: '08:02:44 UTC',
    eventType: 'LearningCandidateCreated',
    category: 'learning',
    severity: 'warning',
    source: 'LearningQueue',
    description: 'Prediction-Feedback wurde als Lernkandidat vorgemerkt und wartet auf Review.',
  },
  {
    id: 'risk-guard-blocked',
    time: '08:03:08 UTC',
    eventType: 'RiskGuardBlocked',
    category: 'trading',
    severity: 'critical',
    source: 'RiskGuard',
    description: 'Trading-Aktion blockiert: Auto-Trading ist deaktiviert und menschliche Freigabe ist Pflicht.',
  },
  {
    id: 'runtime-stopped',
    time: '08:04:02 UTC',
    eventType: 'RuntimeStopped',
    category: 'runtime',
    severity: 'info',
    source: 'RuntimeHost',
    description: 'Runtime wurde sauber beendet; Event Store bleibt nur lesend im UI sichtbar.',
  },
];

const eventLegend = [
  { label: t.eventTimeline.info, tone: 'info' },
  { label: t.eventTimeline.warning, tone: 'warn' },
  { label: t.eventTimeline.critical, tone: 'danger' },
  { label: t.eventTimeline.trading, tone: 'warn' },
  { label: t.eventTimeline.learning, tone: 'good' },
  { label: t.eventTimeline.runtime, tone: 'info' },
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

function sourceModeLabel(mode) {
  return mode === 'json' ? 'JSON' : t.common.fixtureFallback;
}

function learningStatusTone(status) {
  switch (status) {
    case 'approved':
      return 'good';
    case 'rejected':
      return 'danger';
    case 'review':
      return 'warn';
    default:
      return 'info';
  }
}

function learningStatusLabel(status) {
  switch (status) {
    case 'approved':
      return t.learningQueue.approved;
    case 'rejected':
      return t.learningQueue.rejected;
    case 'review':
      return t.learningQueue.review;
    default:
      return t.learningQueue.open;
  }
}

function riskTone(risk) {
  switch (risk) {
    case 'high':
      return 'danger';
    case 'medium':
      return 'warn';
    default:
      return 'good';
  }
}

function riskLabel(risk) {
  switch (risk) {
    case 'high':
      return t.learningQueue.highRisk;
    case 'medium':
      return t.learningQueue.mediumRisk;
    default:
      return t.learningQueue.lowRisk;
  }
}

function eventSeverityTone(severity) {
  switch (severity) {
    case 'critical':
      return 'danger';
    case 'warning':
      return 'warn';
    default:
      return 'info';
  }
}

function eventSeverityLabel(severity) {
  switch (severity) {
    case 'critical':
      return t.eventTimeline.critical;
    case 'warning':
      return t.eventTimeline.warning;
    default:
      return t.eventTimeline.info;
  }
}

function eventCategoryTone(category) {
  switch (category) {
    case 'trading':
      return 'warn';
    case 'learning':
      return 'good';
    default:
      return 'info';
  }
}

function eventCategoryLabel(category) {
  switch (category) {
    case 'trading':
      return t.eventTimeline.trading;
    case 'learning':
      return t.eventTimeline.learning;
    default:
      return t.eventTimeline.runtime;
  }
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
                <span>{t.setupWatch.timeWindow}: {item.time_window_minutes} {t.setupWatch.minuteWindow}</span>
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
    <Panel
      eyebrow={t.learningQueue.eyebrow}
      title={t.learningQueue.title}
      action={<StatusPill tone="warn">{t.learningQueue.pending}</StatusPill>}
      className="learning-panel"
    >
      <LearningGuardStrip />
      <div className="learning-candidate-list">
        {learningCandidates.map((candidate) => (
          <LearningCandidateCard candidate={candidate} key={candidate.id} />
        ))}
      </div>
    </Panel>
  );
}

function LearningGuardStrip() {
  return (
    <div className="learning-guard-strip">
      <strong>{t.learningQueue.noSilentLearning}</strong>
      <strong>{t.learningQueue.humanApprovalRequired}</strong>
      <strong>{t.learningQueue.noAutoSkillActivation}</strong>
      <strong>{t.learningQueue.noTradingRuleWithoutReview}</strong>
    </div>
  );
}

function LearningCandidateCard({ candidate }) {
  return (
    <article className={`learning-candidate risk-${candidate.risk}`}>
      <div className="learning-card-head">
        <div>
          <span>{t.learningQueue.type}: {candidate.type}</span>
          <strong>{candidate.title}</strong>
        </div>
        <StatusPill tone={learningStatusTone(candidate.status)}>
          {learningStatusLabel(candidate.status)}
        </StatusPill>
      </div>
      <p>{candidate.description}</p>
      <div className="learning-card-meta">
        <div>
          <span>{t.learningQueue.source}</span>
          <strong>{candidate.source}</strong>
        </div>
        <div>
          <span>{t.learningQueue.risk}</span>
          <strong className={toneClass(riskTone(candidate.risk))}>{riskLabel(candidate.risk)}</strong>
        </div>
      </div>
      <div className="learning-action">
        <span>{t.learningQueue.action}</span>
        <strong>{candidate.action}</strong>
      </div>
    </article>
  );
}

function ApprovalQueuePanel() {
  const approvalItems = learningCandidates.filter((candidate) =>
    ['open', 'review'].includes(candidate.status),
  );

  return (
    <Panel
      eyebrow={t.approvalQueue.eyebrow}
      title={t.approvalQueue.title}
      action={<StatusPill tone="warn">{t.approvalQueue.waiting}</StatusPill>}
      className="approval-panel"
    >
      <div className="approval-list">
        {approvalItems.length === 0 ? (
          <p className="panel-copy">{t.approvalQueue.empty}</p>
        ) : (
          approvalItems.map((candidate) => (
            <article className="approval-item" key={candidate.id}>
              <div>
                <strong>{candidate.type}</strong>
                <span>{candidate.title}</span>
              </div>
              <StatusPill tone={riskTone(candidate.risk)}>{riskLabel(candidate.risk)}</StatusPill>
              <p>{candidate.action}</p>
            </article>
          ))
        )}
      </div>
    </Panel>
  );
}

function ReflectiveLearningPanel() {
  return (
    <Panel
      eyebrow={t.reflectiveLearning.eyebrow}
      title={t.reflectiveLearning.title}
      action={<StatusPill tone="info">{t.reflectiveLearning.status}</StatusPill>}
      className="reflective-panel"
    >
      <div className="reflective-stack">
        <div>
          <span>Loop</span>
          <strong>{t.reflectiveLearning.reviewLoop}</strong>
        </div>
        <div>
          <span>Memory</span>
          <strong>{t.reflectiveLearning.memoryGate}</strong>
        </div>
        <div>
          <span>Skills</span>
          <strong>{t.reflectiveLearning.skillGate}</strong>
        </div>
        <div>
          <span>Trading</span>
          <strong>{t.reflectiveLearning.tradingGate}</strong>
        </div>
      </div>
    </Panel>
  );
}

function RuntimeEventTimelinePanel() {
  return (
    <Panel
      eyebrow={t.eventTimeline.eyebrow}
      title={t.eventTimeline.title}
      action={<StatusPill tone="info">{t.eventTimeline.status}</StatusPill>}
      className="event-timeline-panel"
    >
      <div className="event-safety-strip">
        <strong>{t.eventTimeline.autoTradingOff}</strong>
        <strong>{t.eventTimeline.humanReviewRequired}</strong>
      </div>
      <div className="event-filter-legend" aria-label={t.eventTimeline.legend}>
        {eventLegend.map((item) => (
          <span className={`event-filter ${toneClass(item.tone)}`} key={item.label}>
            {item.label}
          </span>
        ))}
      </div>
      <div className="event-timeline-list">
        {runtimeEvents.map((event) => (
          <article
            className={`event-timeline-card severity-${event.severity} category-${event.category}`}
            key={event.id}
          >
            <div className="event-time-block">
              <time>{event.time}</time>
              <span>{event.eventType}</span>
            </div>
            <div className="event-main">
              <div className="event-tags">
                <StatusPill tone={eventSeverityTone(event.severity)}>
                  {eventSeverityLabel(event.severity)}
                </StatusPill>
                <StatusPill tone={eventCategoryTone(event.category)}>
                  {eventCategoryLabel(event.category)}
                </StatusPill>
              </div>
              <strong>{event.eventType}</strong>
              <p>{event.description}</p>
            </div>
            <div className="event-source-block">
              <span>{t.eventTimeline.source}</span>
              <strong>{event.source}</strong>
            </div>
          </article>
        ))}
      </div>
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
        <ApprovalQueuePanel />
        <ReflectiveLearningPanel />
        <RuntimeEventTimelinePanel />
        <SafetyPanel />
        <CostProviderPanel />
      </div>
    </main>
  );
}
