import { runtimeHealthMock } from './fixtures/runtimeHealthMock';

const formatBool = (value) => (value ? 'true' : 'false');

const runtimeMetrics = [
  { label: 'runtime_state', value: runtimeHealthMock.runtime_state, tone: 'info' },
  { label: 'free_disk_gb', value: `${runtimeHealthMock.free_disk_gb} GB`, tone: 'good' },
  { label: 'pending_jobs', value: runtimeHealthMock.pending_jobs, tone: 'warn' },
  { label: 'running_jobs', value: runtimeHealthMock.running_jobs, tone: 'info' },
  { label: 'failed_jobs', value: runtimeHealthMock.failed_jobs, tone: 'good' },
  { label: 'quarantined_jobs', value: runtimeHealthMock.quarantined_jobs, tone: 'good' },
  { label: 'last_snapshot_id', value: runtimeHealthMock.last_snapshot_id, tone: 'info' },
];

const runtimeSafetyFlags = [
  {
    label: 'safe_mode',
    value: runtimeHealthMock.safe_mode,
    expected: false,
    tone: 'good',
    detail: 'Runtime is not forced into degraded safety mode.',
  },
  {
    label: 'no_auto_trading',
    value: runtimeHealthMock.no_auto_trading,
    expected: true,
    tone: 'warn',
    detail: 'Trading automation is blocked in this prototype phase.',
  },
  {
    label: 'human_review_required',
    value: runtimeHealthMock.human_review_required,
    expected: true,
    tone: 'warn',
    detail: 'Frank approval remains mandatory before any durable action.',
  },
];

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

function RuntimeSafetyFlags() {
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
  return (
    <Panel
      eyebrow="Runtime Health"
      title="Hermes Runtime v1"
      action={<StatusPill tone="good">read-only sample</StatusPill>}
      className="runtime-panel"
    >
      <RuntimeSafetyFlags />
      <MetricGrid items={runtimeMetrics} />
      <div className="inline-note">
        Example structure mirrors <code>HermesRuntime/data/reports/runtime_health.json</code>, but
        this prototype keeps all values in <code>src/fixtures/runtimeHealthMock.ts</code>.
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
