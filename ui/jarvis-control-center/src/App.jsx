import { useEffect, useMemo, useState } from 'react';
import {
  createRuntimeHealthFallback,
  loadRuntimeHealth,
} from './services/runtimeHealthLoader';

const formatBool = (value) => (value ? 'true' : 'false');
const formatOptionalBool = (value) => {
  if (value === null || value === undefined) {
    return 'not reported';
  }

  return value ? 'active' : 'inactive';
};

function buildRuntimeMetrics(runtimeHealth) {
  return [
    { label: 'runtime_state', value: runtimeHealth.runtime_state, tone: 'info' },
    { label: 'free_disk_gb', value: `${runtimeHealth.free_disk_gb} GB`, tone: 'good' },
    { label: 'pending_jobs', value: runtimeHealth.pending_jobs, tone: 'warn' },
    { label: 'running_jobs', value: runtimeHealth.running_jobs, tone: 'info' },
    { label: 'failed_jobs', value: runtimeHealth.failed_jobs, tone: runtimeHealth.failed_jobs ? 'danger' : 'good' },
    {
      label: 'quarantined_jobs',
      value: runtimeHealth.quarantined_jobs,
      tone: runtimeHealth.quarantined_jobs ? 'danger' : 'good',
    },
    { label: 'last_snapshot_id', value: runtimeHealth.last_snapshot_id || '-', tone: 'info' },
  ];
}

function buildRuntimeSafetyFlags(runtimeHealth) {
  return [
    {
      label: 'safe_mode',
      value: runtimeHealth.safe_mode,
      expected: false,
      tone: 'good',
      detail: 'Runtime is not forced into degraded safety mode.',
    },
    {
      label: 'no_auto_trading',
      value: runtimeHealth.no_auto_trading,
      expected: true,
      tone: 'warn',
      detail: 'Trading automation is blocked in this prototype phase.',
    },
    {
      label: 'human_review_required',
      value: runtimeHealth.human_review_required,
      expected: true,
      tone: 'warn',
      detail: 'Frank approval remains mandatory before any durable action.',
    },
  ];
}

const tradingWatch = [
  {
    symbol: 'XAUUSD',
    status: 'Setup watching',
    confidence: '68%',
    zone: 'Entry zone 2368.20 - 2371.80',
    risk: 'SL 2361.40 / TP 2382.00',
    tone: 'warn',
  },
  {
    symbol: 'EURUSD',
    status: 'Neutral',
    confidence: '42%',
    zone: 'No active trigger',
    risk: 'Wait for London session structure',
    tone: 'muted',
  },
  {
    symbol: 'GER40',
    status: 'No-trade filter',
    confidence: '31%',
    zone: 'Spread and volatility filter active',
    risk: 'No signal allowed',
    tone: 'danger',
  },
];

const learningQueue = [
  { title: 'XAUUSD pullback cluster', meta: 'Prediction -> outcome pending review', score: '0.74' },
  { title: 'EURUSD session filter', meta: 'No-trade zone candidate', score: '0.62' },
  { title: 'GER40 volatility rejection', meta: 'Needs human approval before memory write', score: '0.58' },
];

const agentActivity = [
  { time: '08:10 UTC', title: 'Research Agent', detail: 'Overnight notes summarized for review' },
  { time: '08:14 UTC', title: 'Trading Watch Agent', detail: 'XAUUSD setup moved to watching' },
  { time: '08:17 UTC', title: 'Memory Agent', detail: '3 learning candidates queued' },
  { time: '08:22 UTC', title: 'Runtime Observer', detail: 'Last health report loaded as mock sample' },
];

const providers = [
  { name: 'GPT-5.5', role: 'Senior architect', status: 'manual route' },
  { name: 'Ollama / Qwen', role: 'local worker', status: 'ready' },
  { name: 'OpenRouter', role: 'fallback', status: 'disabled' },
];

function toneClass(tone) {
  return `tone-${tone || 'info'}`;
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
        <p className="eyebrow">Runtime Status Badge</p>
        <strong className={toneClass(statusTone)}>{runtimeHealth.status}</strong>
      </div>
      <div>
        <span>Source</span>
        <b>{runtimeHealth.source.label}</b>
      </div>
      <div>
        <span>Timestamp</span>
        <b>{runtimeHealth.timestamp_utc || 'not reported'}</b>
      </div>
      <div>
        <span>Last Error</span>
        <b className={runtimeHealth.last_error ? 'tone-danger' : 'tone-good'}>
          {runtimeHealth.last_error || 'none'}
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
        <span>Storage Status</span>
        <strong className="tone-good">{runtimeHealth.free_disk_gb} GB free</strong>
      </div>
      <div>
        <span>Queue</span>
        <strong className={hasJobIssues ? 'tone-danger' : 'tone-info'}>
          {runtimeHealth.pending_jobs} pending / {runtimeHealth.running_jobs} running
        </strong>
      </div>
      <div>
        <span>Problem Jobs</span>
        <strong className={hasJobIssues ? 'tone-danger' : 'tone-good'}>
          {runtimeHealth.failed_jobs} failed / {runtimeHealth.quarantined_jobs} quarantined
        </strong>
      </div>
    </div>
  );
}

function RuntimeCapabilityGrid({ runtimeHealth }) {
  const capabilities = [
    {
      label: 'Event Store Active',
      value: runtimeHealth.event_store_active,
      detail: 'Optional runtime-health flag; fixture reports active.',
    },
    {
      label: 'Replay Manifest Available',
      value: runtimeHealth.replay_manifest_available,
      detail: 'Optional runtime-health flag; fixture reports available.',
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
      time: runtimeHealth.timestamp_utc || 'latest',
      title: mode === 'json' ? 'RuntimeHealthJsonLoaded' : 'RuntimeHealthFixtureLoaded',
      detail: runtimeHealth.source.label,
    },
    {
      time: 'read-only',
      title: 'StorageStatusObserved',
      detail: `${runtimeHealth.free_disk_gb} GB free disk reported`,
    },
    {
      time: 'read-only',
      title: 'SnapshotReferenceObserved',
      detail: runtimeHealth.last_snapshot_id || 'No snapshot id reported',
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
        <p className="eyebrow">Prototype v0.1 / mock data only</p>
        <h1>Jarvis / Hermes Control Center</h1>
        <p>
          Separate React/Vite prototype for the future Jarvis cockpit. Gradio remains the
          development and test UI; this screen does not call APIs, read runtime files, or stream live
          market data.
        </p>
      </div>
      <div className="hero-status" aria-label="System status summary">
        <StatusPill tone="good">Hermes foundation online</StatusPill>
        <StatusPill tone="warn">no_auto_trading active</StatusPill>
        <StatusPill tone="info">Manual approval required</StatusPill>
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
      eyebrow="Runtime Health"
      title="Hermes Runtime v1"
      action={<StatusPill tone={sourceTone}>{runtimeHealthState.mode}</StatusPill>}
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
        The loader attempts a read-only browser fetch from <code>{runtimeHealth.source_path}</code>.
        If the browser blocks that file path, the panel uses <code>src/fixtures/runtimeHealthMock.ts</code>.
      </div>
    </Panel>
  );
}

function HermesBrainPanel() {
  return (
    <Panel eyebrow="Brain Layer" title="Hermes Brain" action={<StatusPill tone="info">foundation</StatusPill>}>
      <div className="brain-grid">
        <div>
          <span className="node-label">Planner</span>
          <strong>ready</strong>
        </div>
        <div>
          <span className="node-label">Memory</span>
          <strong>approval gated</strong>
        </div>
        <div>
          <span className="node-label">Learning</span>
          <strong>review only</strong>
        </div>
        <div>
          <span className="node-label">Delegation</span>
          <strong>visible chains</strong>
        </div>
      </div>
      <p className="panel-copy">
        Hermes is shown as evaluator and orchestrator. Trading indicators provide features; Hermes
        scores, reviews, and asks for approval before durable learning.
      </p>
    </Panel>
  );
}

function TradingWatchPanel() {
  return (
    <Panel eyebrow="Trading Watch" title="Setup Watch" action={<StatusPill tone="warn">alerts only</StatusPill>}>
      <div className="watch-list">
        {tradingWatch.map((item) => (
          <article className="watch-row" key={item.symbol}>
            <div className="watch-symbol">
              <strong>{item.symbol}</strong>
              <StatusPill tone={item.tone}>{item.status}</StatusPill>
            </div>
            <div className="watch-detail">
              <span>Confidence {item.confidence}</span>
              <span>{item.zone}</span>
              <span>{item.risk}</span>
            </div>
          </article>
        ))}
      </div>
    </Panel>
  );
}

function LearningQueuePanel() {
  return (
    <Panel eyebrow="Learning" title="Learning Queue" action={<StatusPill tone="warn">3 pending</StatusPill>}>
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
    <Panel eyebrow="Agents" title="Agent Activity Timeline" action={<StatusPill tone="info">mock stream</StatusPill>}>
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
    <Panel eyebrow="Safety" title="Safety / Approval" action={<StatusPill tone="danger">locked</StatusPill>}>
      <div className="safety-stack">
        <div className="safety-row">
          <span>Auto trading</span>
          <strong className="tone-danger">blocked</strong>
        </div>
        <div className="safety-row">
          <span>Human approval</span>
          <strong className="tone-warn">required</strong>
        </div>
        <div className="safety-row">
          <span>Silent learning</span>
          <strong className="tone-danger">disabled</strong>
        </div>
        <div className="safety-row">
          <span>Martingale / Grid</span>
          <strong className="tone-danger">not allowed</strong>
        </div>
      </div>
    </Panel>
  );
}

function CostProviderPanel() {
  return (
    <Panel eyebrow="Routing" title="Cost / Provider" action={<StatusPill tone="good">cost visible</StatusPill>}>
      <div className="provider-list">
        {providers.map((provider) => (
          <div className="provider-row" key={provider.name}>
            <strong>{provider.name}</strong>
            <span>{provider.role}</span>
            <StatusPill tone={provider.status === 'ready' ? 'good' : provider.status === 'disabled' ? 'muted' : 'info'}>
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
