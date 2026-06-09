import { useEffect, useMemo, useState } from 'react';
import {
  createOperatorDashboardFallback,
  loadOperatorDashboard,
  DATA_SOURCE,
} from '../data/runtimeDataAdapter';
import { sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
import { Panel, StatusPill, toneClass } from './StatusCard';

const OPERATOR_REFRESH_SECONDS = 45;

function formatNumber(value) {
  return new Intl.NumberFormat('de-DE').format(Number(value || 0));
}

function formatGb(value) {
  return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(Number(value || 0))} GB`;
}

function formatBytes(value) {
  const bytes = Number(value || 0);

  if (bytes >= 1024 ** 3) {
    return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(bytes / 1024 ** 3)} GB`;
  }

  if (bytes >= 1024 ** 2) {
    return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(bytes / 1024 ** 2)} MB`;
  }

  return `${formatNumber(bytes)} B`;
}

function formatPercent(value) {
  return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(Number(value || 0))}%`;
}

function formatMaybeNumber(value, digits = 4) {
  if (value === null || value === undefined || value === '') {
    return 'n/a';
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return 'n/a';
  }

  return new Intl.NumberFormat('de-DE', { maximumFractionDigits: digits }).format(parsed);
}

function shortDateTime(value) {
  if (!value) {
    return '-';
  }

  const parsed = Date.parse(value);

  if (!Number.isFinite(parsed)) {
    return String(value);
  }

  return new Intl.DateTimeFormat('de-DE', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parsed);
}

function shortTime(value) {
  if (!value) {
    return '-';
  }

  const parsed = Date.parse(value);

  if (!Number.isFinite(parsed)) {
    return String(value);
  }

  return new Intl.DateTimeFormat('de-DE', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(parsed);
}

function statusTone(status) {
  const value = String(status || '').toLowerCase();

  if (value.includes('running') || value.includes('active') || value.includes('completed')) {
    return 'good';
  }

  if (value.includes('stop') || value.includes('fail') || value.includes('critical')) {
    return 'danger';
  }

  if (value.includes('skip') || value.includes('pending') || value.includes('outside')) {
    return 'warn';
  }

  return 'info';
}

function MiniMetric({ label, value, tone = 'info' }) {
  return (
    <div className="operator-mini-metric">
      <span>{label}</span>
      <strong className={toneClass(tone)}>{value}</strong>
    </div>
  );
}

function goalLabel(goalId) {
  return String(goalId || '-')
    .replace(/^improve_/, '')
    .replace(/_/g, ' ');
}

function goalProgressPercent(progress) {
  return `${Math.round(Number(progress || 0) * 100)}%`;
}

function scorePercent(value) {
  return `${Math.round(Number(value || 0) * 100)}%`;
}

function GoalSystemCard({ masterStatus }) {
  const goalAvailable = masterStatus.goal_system_available;
  const progressItems = masterStatus.goal_progress_summary || [];
  const blockedGoals = masterStatus.blocked_goals || [];
  const activeGoals = masterStatus.active_goals || [];
  const warnings = masterStatus.goal_warnings?.length
    ? masterStatus.goal_warnings
    : masterStatus.top_blockers.filter((item) => item.includes('goal') || item.includes('blocked_goal'));

  return (
    <details className="goal-system-card operator-goal-card" open>
      <summary>
        <span>Hermes Ziele</span>
        <strong>{goalAvailable ? goalLabel(masterStatus.top_goal) : 'nicht verfuegbar'}</strong>
        <StatusPill tone={blockedGoals.length ? 'warn' : goalAvailable ? 'good' : 'info'}>
          {goalAvailable ? `${blockedGoals.length} blockiert` : 'offline'}
        </StatusPill>
      </summary>

      {goalAvailable ? (
        <>
          <div className="goal-system-metrics">
            <MiniMetric label="Hauptziel" value={goalLabel(masterStatus.top_goal)} tone={blockedGoals.includes(masterStatus.top_goal) ? 'warn' : 'info'} />
            <MiniMetric label="Aktive Ziele" value={formatNumber(activeGoals.length)} tone="info" />
            <MiniMetric label="Blockiert" value={formatNumber(blockedGoals.length)} tone={blockedGoals.length ? 'warn' : 'good'} />
            <MiniMetric label="Letzte Bewertung" value={shortDateTime(masterStatus.updated_at_utc)} />
          </div>

          <div className="goal-progress-list" aria-label="Fortschritt je Ziel">
            {progressItems.slice(0, 8).map((goal) => (
              <div className="goal-progress-row" key={goal.goal_id}>
                <div>
                  <span>{goalLabel(goal.goal_id)}</span>
                  <strong>{goalProgressPercent(goal.progress)}</strong>
                </div>
                <i style={{ width: goalProgressPercent(goal.progress) }} />
              </div>
            ))}
          </div>

          {blockedGoals.length ? (
            <div className="goal-token-list" aria-label="Blockierte Ziele">
              {blockedGoals.slice(0, 8).map((goal) => (
                <span key={goal}>{goalLabel(goal)}</span>
              ))}
            </div>
          ) : null}

          {warnings.length ? (
            <div className="goal-warning-list" aria-label="Goal-Blocker">
              {warnings.slice(0, 6).map((warning) => (
                <span key={warning}>{warning}</span>
              ))}
            </div>
          ) : null}
        </>
      ) : (
        <p>Goal-System noch nicht verfuegbar.</p>
      )}
    </details>
  );
}

function KnowledgeHealthCard({ masterStatus }) {
  const health = masterStatus.knowledge_health || 'unbekannt';
  const tone = health.includes('critical')
    ? 'danger'
    : health.includes('needs') || masterStatus.weak_knowledge
      ? 'warn'
      : health.includes('healthy')
        ? 'good'
        : 'info';

  return (
    <details className="knowledge-health-card operator-goal-card" open>
      <summary>
        <span>Knowledge Health</span>
        <strong>{health}</strong>
        <StatusPill tone={tone}>{masterStatus.knowledge_trend || '-'}</StatusPill>
      </summary>
      <div className="goal-system-metrics">
        <MiniMetric label="Trusted Knowledge" value={formatNumber(masterStatus.trusted_knowledge)} tone="good" />
        <MiniMetric label="Weak Knowledge" value={formatNumber(masterStatus.weak_knowledge)} tone={masterStatus.weak_knowledge ? 'warn' : 'good'} />
        <MiniMetric label="Deprecated" value={formatNumber(masterStatus.deprecated_knowledge)} tone={masterStatus.deprecated_knowledge ? 'warn' : 'good'} />
        <MiniMetric label="Avg Trust" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
        <MiniMetric label="Avg Quality" value={scorePercent(masterStatus.average_quality_score)} tone={tone} />
        <MiniMetric label="Knowledge Trend" value={masterStatus.knowledge_trend || '-'} tone="info" />
        <MiniMetric label="Open Validation Plans" value={formatNumber(masterStatus.validation_plans_open)} tone={masterStatus.validation_plans_open ? 'warn' : 'good'} />
        <MiniMetric label="Needs OOS" value={formatNumber(masterStatus.knowledge_items_needing_oos)} tone={masterStatus.knowledge_items_needing_oos ? 'warn' : 'good'} />
        <MiniMetric label="Trusted Candidates" value={formatNumber(masterStatus.trusted_candidate_count)} tone={masterStatus.trusted_candidate_count ? 'good' : 'info'} />
      </div>
    </details>
  );
}

function ScalpingProgressCard({ masterStatus }) {
  const finalCandidates = masterStatus.scalping_final_candidates || 0;
  const robustCandidates = masterStatus.scalping_robust_candidates || 0;

  return (
    <details className="knowledge-health-card operator-goal-card" open>
      <summary>
        <span>Scalping Progress</span>
        <strong>{masterStatus.scalping_asset || '-'}</strong>
        <StatusPill tone={finalCandidates ? 'good' : robustCandidates ? 'warn' : 'info'}>read-only</StatusPill>
      </summary>
      <div className="goal-system-metrics">
        <MiniMetric label="Candidates" value={formatNumber(masterStatus.scalping_candidates_total)} tone="info" />
        <MiniMetric label="Robust" value={formatNumber(masterStatus.scalping_robust_candidates)} tone={robustCandidates ? 'good' : 'warn'} />
        <MiniMetric label="Final" value={formatNumber(masterStatus.scalping_final_candidates)} tone={finalCandidates ? 'good' : 'warn'} />
        <MiniMetric label="Best" value={masterStatus.best_scalping_candidate || '-'} tone="info" />
        <MiniMetric label="Monte Carlo" value={masterStatus.scalping_monte_carlo_health || 'missing'} tone={statusTone(masterStatus.scalping_monte_carlo_health)} />
        <MiniMetric label="Sensitivity" value={masterStatus.scalping_parameter_sensitivity_health || 'missing'} tone={statusTone(masterStatus.scalping_parameter_sensitivity_health)} />
        <MiniMetric label="Regime" value={masterStatus.scalping_regime_validation_health || 'missing'} tone={statusTone(masterStatus.scalping_regime_validation_health)} />
        <MiniMetric label="Bot Specs" value={formatNumber(masterStatus.ctrader_bot_specs_ready)} tone={masterStatus.ctrader_bot_specs_ready ? 'good' : 'info'} />
        <MiniMetric label="Signal Specs" value={formatNumber(masterStatus.signal_agent_specs_ready)} tone={masterStatus.signal_agent_specs_ready ? 'good' : 'info'} />
        <MiniMetric label="no_auto_trading" value={String(masterStatus.no_auto_trading)} tone={masterStatus.no_auto_trading ? 'good' : 'danger'} />
        <MiniMetric label="human_review" value={String(masterStatus.human_review_required)} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <MiniMetric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <MiniMetric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
      <p>Read-only snapshot panel. No runtime commands, broker actions, or trading actions.</p>
    </details>
  );
}

function DemoSignalFeedCard({ masterStatus, demoSignalFeed, latestDemoSignals }) {
  const warnings = demoSignalFeed.warnings?.length ? demoSignalFeed.warnings : ['none'];

  return (
    <OperatorCard
      badge={demoSignalFeed.feed_status || 'unknown'}
      title="Demo Signal Feed"
      tone={statusTone(demoSignalFeed.health || demoSignalFeed.feed_status)}
    >
      <div className="operator-metric-grid">
        <MiniMetric label="Feed Status" value={demoSignalFeed.feed_status || 'n/a'} tone={statusTone(demoSignalFeed.feed_status)} />
        <MiniMetric label="Feed Mode" value={demoSignalFeed.feed_mode || 'n/a'} tone="info" />
        <MiniMetric label="Signal Count" value={formatNumber(demoSignalFeed.signal_count ?? masterStatus.latest_demo_signal_count)} tone={demoSignalFeed.signal_count ? 'good' : 'info'} />
        <MiniMetric label="Health" value={demoSignalFeed.health || masterStatus.demo_signal_feed_health || 'n/a'} tone={statusTone(demoSignalFeed.health || masterStatus.demo_signal_feed_health)} />
      </div>

      <div className="operator-warning-list" aria-label="Demo Signal Feed Warnings">
        {warnings.map((warning) => (
          <span key={warning}>{warning}</span>
        ))}
      </div>

      <div className="operator-safety-flags">
        <StatusPill tone="warn">Demo Signal Feed only</StatusPill>
        <StatusPill tone="good">no_auto_trading=true</StatusPill>
        <StatusPill tone="good">human_review_required=true</StatusPill>
        <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
        <StatusPill tone="good">live_trading_enabled=false</StatusPill>
      </div>

      <div className="demo-signal-list" aria-label="Aktuelle Demo-Signale">
        {(latestDemoSignals?.length ? latestDemoSignals : [{ signal_id: 'empty' }]).map((signal) => (
          <article className="demo-signal-item" key={signal.signal_id}>
            <div className="demo-signal-head">
              <strong>{signal.asset || 'n/a'}</strong>
              <span>{signal.timeframe || 'n/a'}</span>
            </div>
            <div className="demo-signal-grid">
              <MiniMetric label="candidate_id" value={signal.candidate_id || 'n/a'} tone="info" />
              <MiniMetric label="setup_type" value={signal.setup_type || 'n/a'} tone="info" />
              <MiniMetric label="direction" value={signal.direction || 'n/a'} tone="info" />
              <MiniMetric label="entry_level" value={formatMaybeNumber(signal.entry_level, 5)} tone="info" />
              <MiniMetric label="stop_loss" value={formatMaybeNumber(signal.stop_loss, 5)} tone="warn" />
              <MiniMetric label="take_profit" value={formatMaybeNumber(signal.take_profit, 5)} tone="good" />
              <MiniMetric label="invalidation_level" value={formatMaybeNumber(signal.invalidation_level, 5)} tone="warn" />
              <MiniMetric label="confidence" value={formatMaybeNumber(signal.confidence, 4)} tone="info" />
              <MiniMetric label="status" value={signal.status || 'n/a'} tone={statusTone(signal.status)} />
            </div>
            <p>{signal.reason || 'n/a'}</p>
          </article>
        ))}
      </div>

      <p>Read-only panel. No runtime commands, no trading actions, no broker actions, no cTrader Order API.</p>
    </OperatorCard>
  );
}

function ForwardTestCard({ masterStatus, forwardTest }) {
  const warnings = forwardTest.warnings?.length ? forwardTest.warnings : ['none'];
  const blockers = forwardTest.blockers?.length ? forwardTest.blockers : [];

  return (
    <OperatorCard
      badge={forwardTest.forward_test_status || 'unknown'}
      title="Forward Test"
      tone={statusTone(forwardTest.forward_test_health || forwardTest.forward_test_status)}
    >
      <div className="operator-metric-grid">
        <MiniMetric label="Status" value={forwardTest.forward_test_status || 'n/a'} tone={statusTone(forwardTest.forward_test_status)} />
        <MiniMetric label="Mode" value={forwardTest.forward_test_mode || 'n/a'} tone="info" />
        <MiniMetric label="Assets" value={forwardTest.forward_test_assets?.join(', ') || masterStatus.forward_test_assets?.join(', ') || 'n/a'} tone="info" />
        <MiniMetric label="Signals Observed" value={formatNumber(forwardTest.forward_test_signals_observed ?? masterStatus.forward_test_signals_observed)} tone="info" />
        <MiniMetric label="Health" value={forwardTest.forward_test_health || masterStatus.forward_test_health || 'n/a'} tone={statusTone(forwardTest.forward_test_health || masterStatus.forward_test_health)} />
        <MiniMetric label="Human Review" value={String(forwardTest.forward_test_requires_human_review ?? masterStatus.forward_test_requires_human_review ?? true)} tone={(forwardTest.forward_test_requires_human_review ?? true) ? 'good' : 'danger'} />
      </div>

      <div className="operator-warning-list" aria-label="Forward Test Warnings">
        {warnings.map((warning) => (
          <span key={warning}>{warning}</span>
        ))}
      </div>

      {blockers.length ? (
        <div className="operator-token-list" aria-label="Forward Test Blockers">
          {blockers.map((blocker) => (
            <span key={blocker}>{blocker}</span>
          ))}
        </div>
      ) : null}

      <div className="operator-metric-grid">
        <MiniMetric label="Plan Path" value={forwardTest.plan_path || 'n/a'} tone="info" />
        <MiniMetric label="Log Path" value={forwardTest.log_path || 'n/a'} tone="info" />
        <MiniMetric label="Latest Observation Count" value={formatNumber(forwardTest.latest_observation_count)} tone="info" />
      </div>

      <div className="operator-safety-flags">
        <StatusPill tone="warn">Observation only</StatusPill>
        <StatusPill tone="warn">Demo signal tracking</StatusPill>
        <StatusPill tone="good">no_auto_trading=true</StatusPill>
        <StatusPill tone="good">human_review_required=true</StatusPill>
        <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
        <StatusPill tone="good">live_trading_enabled=false</StatusPill>
      </div>

      <p>Read-only panel. No runtime commands, no trading actions, no broker actions, no cTrader Order API.</p>
    </OperatorCard>
  );
}

function OperatorCard({ title, badge, tone = 'info', children }) {
  return (
    <article className={`operator-card ${toneClass(tone)}`}>
      <div className="operator-card-head">
        <h3>{title}</h3>
        {badge && <StatusPill tone={tone}>{badge}</StatusPill>}
      </div>
      {children}
    </article>
  );
}

function ReportViewer({ reports }) {
  const [selectedKey, setSelectedKey] = useState(reports[0]?.key || '');
  const selectedReport = reports.find((report) => report.key === selectedKey) || reports[0];

  useEffect(() => {
    if (!reports.some((report) => report.key === selectedKey)) {
      setSelectedKey(reports[0]?.key || '');
    }
  }, [reports, selectedKey]);

  return (
    <div className="operator-report-viewer">
      <div className="operator-report-list" role="list">
        {reports.map((report) => (
          <button
            className={report.key === selectedReport?.key ? 'is-active' : ''}
            key={report.key}
            onClick={() => setSelectedKey(report.key)}
            type="button"
          >
            <span>{report.label}</span>
            <StatusPill tone={report.available ? 'good' : 'warn'}>
              {report.available ? 'Bridge' : 'Fixture'}
            </StatusPill>
          </button>
        ))}
      </div>
      <div className="operator-report-json">
        <div>
          <span>{selectedReport?.path || '-'}</span>
          {selectedReport?.warning && <b>{selectedReport.warning}</b>}
        </div>
        <pre>{JSON.stringify(selectedReport?.raw || {}, null, 2)}</pre>
      </div>
    </div>
  );
}

function SafetyPlaceholder({ title, value, tone = 'warn' }) {
  return (
    <div className={`operator-placeholder-control ${toneClass(tone)}`}>
      <span>{title}</span>
      <button disabled type="button">
        {value}
      </button>
    </div>
  );
}

export function OperatorDashboardPanel() {
  const [operatorState, setOperatorState] = useState(() => createOperatorDashboardFallback());
  const [isRefreshing, setIsRefreshing] = useState(false);

  useEffect(() => {
    let isMounted = true;
    let refreshTimer;

    const refreshOperatorState = () => {
      setIsRefreshing(true);
      loadOperatorDashboard()
        .then((nextState) => {
          if (isMounted) {
            setOperatorState(nextState);
          }
        })
        .finally(() => {
          if (isMounted) {
            setIsRefreshing(false);
          }
        });
    };

    refreshOperatorState();
    refreshTimer = window.setInterval(refreshOperatorState, OPERATOR_REFRESH_SECONDS * 1000);

    return () => {
      isMounted = false;
      window.clearInterval(refreshTimer);
    };
  }, []);

  const activeJobs = useMemo(
    () => operatorState.schedulerJobs.filter((job) => job.enabled),
    [operatorState.schedulerJobs],
  );
  const nextJobs = useMemo(
    () =>
      activeJobs
        .filter((job) => job.next_run_utc)
        .sort((left, right) => Date.parse(left.next_run_utc) - Date.parse(right.next_run_utc))
        .slice(0, 5),
    [activeJobs],
  );
  const warningLines = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ].filter(Boolean);
  const bridgeLive = operatorState.dataSource === DATA_SOURCE.LIVE_FILE;

  return (
    <Panel
      action={
        <div className="operator-panel-actions">
          <StatusPill tone={bridgeLive ? 'good' : 'warn'}>
            {bridgeLive ? 'Live Bridge' : 'Fixture-Fallback'}
          </StatusPill>
          <span className="operator-refresh-meta">
            Zuletzt: {shortTime(operatorState.lastUpdatedAt)} · Refresh {operatorState.pollIntervalSeconds || OPERATOR_REFRESH_SECONDS}s
            · {formatNumber(operatorState.liveReportCount)} Bridge / {formatNumber(operatorState.fixtureReportCount)} Fixture
            {isRefreshing ? ' · liest' : ''}
          </span>
          <StatusPill tone="warn">UI-only Controls</StatusPill>
        </div>
      }
      className="operator-panel"
      eyebrow="Beta 3 Operator"
      title="Operator Dashboard"
    >
      <OperatorCard
        badge={operatorState.masterStatus.overall_status}
        title="Hermes Master Status"
        tone={statusTone(operatorState.masterStatus.overall_status)}
      >
        <div className="operator-safety-flags">
          <StatusPill tone={sourceTone(operatorState.masterStatusSource)}>
            {operatorState.masterStatusSource === DATA_SOURCE.LIVE_FILE
              ? 'Live Snapshot aktiv'
              : sourceModeLabel(operatorState.masterStatusSource)}
          </StatusPill>
          {operatorState.masterStatusWarning ? (
            <StatusPill tone="warn">Demo-/Snapshot-Daten aktiv</StatusPill>
          ) : null}
        </div>
        <div className="operator-master-grid">
          <MiniMetric label="Fokus" value={operatorState.masterStatus.current_focus} tone="info" />
          <MiniMetric label="Aktive Domaenen" value={operatorState.masterStatus.active_domains.join(', ') || '-'} tone="info" />
          <MiniMetric label="Geplante Aufgaben" value={formatNumber(operatorState.masterStatus.queued_tasks)} tone={operatorState.masterStatus.queued_tasks ? 'warn' : 'good'} />
          <MiniMetric label="Letzter Nightly" value={shortDateTime(operatorState.masterStatus.last_nightly_run)} />
          <MiniMetric label="Autonomer Loop" value={shortDateTime(operatorState.masterStatus.last_autonomous_loop)} />
          <MiniMetric label="Meta Review" value={shortDateTime(operatorState.masterStatus.last_meta_review)} />
          <MiniMetric label="Lernstrategie" value={operatorState.masterStatus.learning_strategy} />
          <MiniMetric label="Supervisor" value={operatorState.masterStatus.supervisor_running ? 'running' : 'stopped'} tone={operatorState.masterStatus.supervisor_running ? 'good' : 'warn'} />
          <MiniMetric label="Scheduler Jobs" value={formatNumber(operatorState.masterStatus.scheduler_enabled)} />
          <MiniMetric label="Resource Action" value={operatorState.masterStatus.resource_action} tone={statusTone(operatorState.masterStatus.resource_action)} />
          <MiniMetric label="Storage Cleanup" value={formatNumber(operatorState.masterStatus.storage_cleanup)} tone={operatorState.masterStatus.storage_cleanup ? 'warn' : 'good'} />
          <MiniMetric label="Robuste Strategien" value={formatNumber(operatorState.masterStatus.robust_strategies)} tone={operatorState.masterStatus.robust_strategies ? 'good' : 'warn'} />
          <MiniMetric label="Demo-Bot-Kandidaten" value={formatNumber(operatorState.masterStatus.demo_bot_candidates)} tone={operatorState.masterStatus.demo_bot_candidates ? 'good' : 'warn'} />
          <MiniMetric label="no_auto_trading" value={String(operatorState.masterStatus.no_auto_trading)} tone={operatorState.masterStatus.no_auto_trading ? 'good' : 'danger'} />
          <MiniMetric label="human_review_required" value={String(operatorState.masterStatus.human_review_required)} tone={operatorState.masterStatus.human_review_required ? 'good' : 'danger'} />
          <MiniMetric label="broker_orders_enabled" value={String(operatorState.masterStatus.broker_orders_enabled)} tone={operatorState.masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
          <MiniMetric label="live_trading_enabled" value={String(operatorState.masterStatus.live_trading_enabled)} tone={operatorState.masterStatus.live_trading_enabled ? 'danger' : 'good'} />
        </div>
        <div className="operator-token-list">
          {operatorState.masterStatus.top_blockers.slice(0, 6).map((blocker) => (
            <span key={blocker}>{blocker}</span>
          ))}
        </div>
        <GoalSystemCard masterStatus={operatorState.masterStatus} />
        <KnowledgeHealthCard masterStatus={operatorState.masterStatus} />
        <ScalpingProgressCard masterStatus={operatorState.masterStatus} />
      </OperatorCard>

      <div className="operator-top-grid">
        <OperatorCard
          badge={operatorState.supervisor.running ? 'running' : 'stopped'}
          title="Supervisor Status"
          tone={operatorState.supervisor.running ? 'good' : statusTone(operatorState.supervisor.status)}
        >
          <div className="operator-metric-grid">
            <MiniMetric label="Heartbeat" value={shortDateTime(operatorState.supervisor.heartbeat_utc)} />
            <MiniMetric label="Alter" value={`${formatNumber(operatorState.supervisor.heartbeat_age_seconds)} s`} />
            <MiniMetric label="Uptime" value={`${formatNumber(operatorState.supervisor.uptime_minutes)} min`} />
            <MiniMetric label="Aktueller Job" value={operatorState.supervisor.current_job} />
          </div>
          <p>{operatorState.supervisor.next_action}</p>
        </OperatorCard>

        <OperatorCard badge={`${activeJobs.length} aktiv`} title="Scheduler Status" tone="info">
          <div className="operator-job-list">
            {nextJobs.map((job) => (
              <div className="operator-job-row" key={job.job_id}>
                <div>
                  <strong>{job.job_type}</strong>
                  <span>{shortDateTime(job.next_run_utc)}</span>
                </div>
                <StatusPill tone={statusTone(job.status)}>{job.status}</StatusPill>
              </div>
            ))}
          </div>
        </OperatorCard>

        <OperatorCard
          badge={operatorState.resource.should_stop ? 'stop' : operatorState.resource.action}
          title="Resource Status"
          tone={operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good'}
        >
          <div className="operator-meter-stack">
            <div>
              <span>CPU</span>
              <b>{formatPercent(operatorState.resource.cpu_usage_percent)}</b>
              <i style={{ width: `${operatorState.resource.cpu_usage_percent}%` }} />
            </div>
            <div>
              <span>RAM</span>
              <b>{formatPercent(operatorState.resource.memory_usage_percent)}</b>
              <i style={{ width: `${operatorState.resource.memory_usage_percent}%` }} />
            </div>
            <div>
              <span>Freier Speicher</span>
              <b>{formatGb(operatorState.resource.free_disk_gb)}</b>
              <i style={{ width: `${operatorState.resource.free_disk_percent}%` }} />
            </div>
          </div>
        </OperatorCard>

        <OperatorCard badge={operatorState.nightly.current_state} title="Nightly Status" tone={statusTone(operatorState.nightly.current_state)}>
          <div className="operator-metric-grid">
            <MiniMetric label="Fenster" value={operatorState.nightly.next_nightly_window} />
            <MiniMetric label="Naechster Start" value={shortDateTime(operatorState.nightly.next_scheduled_start_utc)} />
            <MiniMetric label="Iterationen" value={formatNumber(operatorState.nightly.iterations_completed)} />
            <MiniMetric label="Arbeit" value={formatNumber(operatorState.nightly.work_performed)} />
          </div>
          <p>{operatorState.nightly.next_action}</p>
        </OperatorCard>
      </div>

      <div className="operator-middle-grid">
        <DemoSignalFeedCard
          demoSignalFeed={operatorState.demoSignalFeed}
          latestDemoSignals={operatorState.latestDemoSignals}
          masterStatus={operatorState.masterStatus}
        />

        <ForwardTestCard
          forwardTest={operatorState.forwardTest}
          masterStatus={operatorState.masterStatus}
        />

        <OperatorCard title="Research Summary" tone="info">
          <div className="operator-research-grid">
            <MiniMetric label="Strategien getestet" value={formatNumber(operatorState.research.strategies_tested)} tone="info" />
            <MiniMetric label="Robust" value={formatNumber(operatorState.research.robust_strategies)} tone="good" />
            <MiniMetric label="Overfit-Verdacht" value={formatNumber(operatorState.research.overfit_suspected)} tone="warn" />
            <MiniMetric label="Regime-Konsistenz" value={formatPercent(operatorState.research.regime_consistency_score * 100)} tone="good" />
          </div>
          <div className="operator-token-list">
            {operatorState.research.regime_distribution.slice(0, 6).map((item) => (
              <span key={item}>{item}</span>
            ))}
          </div>
        </OperatorCard>

        <OperatorCard title="Safety Control Layer" tone="warn">
          <div className="operator-safety-grid">
            <SafetyPlaceholder title="Auto-Trading" value="deaktiviert" />
            <SafetyPlaceholder title="Demo/Paper Mode" value="Platzhalter" tone="info" />
            <SafetyPlaceholder title="Emergency Stop" value="nicht verdrahtet" tone="danger" />
            <SafetyPlaceholder title="Risk Limits" value="geplant" />
            <SafetyPlaceholder title="Strategy Whitelist" value="geplant" tone="info" />
            <SafetyPlaceholder title="Symbol Whitelist" value="geplant" tone="info" />
          </div>
          <div className="operator-safety-flags">
            <StatusPill tone="warn">no_auto_trading=true</StatusPill>
            <StatusPill tone="warn">human_review_required=true</StatusPill>
            <StatusPill tone="danger">keine Orderbuttons</StatusPill>
          </div>
        </OperatorCard>
      </div>

      <div className="operator-bottom-grid">
        <OperatorCard title="Report Viewer" tone="info">
          <ReportViewer reports={operatorState.reports} />
        </OperatorCard>

        <OperatorCard title="Storage / Logs" tone={operatorState.storage.errors.length ? 'danger' : 'good'}>
          <div className="operator-storage-card">
            <MiniMetric label="Root" value={operatorState.storage.root} tone="info" />
            <MiniMetric label="Status" value={operatorState.storage.status} tone="good" />
            <MiniMetric label="Frei" value={formatGb(operatorState.storage.free_disk_gb)} tone="good" />
            <MiniMetric label="Cleanup Candidates" value={formatNumber(operatorState.storage.cleanup_candidate_count)} tone="warn" />
            <MiniMetric label="Potenzial" value={formatBytes(operatorState.storage.estimated_bytes_to_free)} tone="info" />
          </div>
          <div className="operator-warning-list">
            {(warningLines.length ? warningLines : ['Keine kritischen Warnungen im Dashboard-Zustand.']).slice(0, 5).map((warning) => (
              <span key={warning}>{warning}</span>
            ))}
          </div>
          <div className="operator-log-list">
            {operatorState.logLines.slice(-8).map((line) => (
              <code key={line}>{line}</code>
            ))}
          </div>
        </OperatorCard>
      </div>
    </Panel>
  );
}
