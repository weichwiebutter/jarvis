import { useEffect, useMemo, useState } from 'react';
import {
  createOperatorDashboardFallback,
  DATA_SOURCE,
  loadOperatorDashboard,
} from '../data/runtimeDataAdapter';
import { sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
import { StatusPill, toneClass } from './StatusCard';

const COCKPIT_REFRESH_SECONDS = 45;

function formatNumber(value) {
  return new Intl.NumberFormat('de-DE').format(Number(value || 0));
}

function formatGb(value) {
  return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(Number(value || 0))} GB`;
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

function toneFromStatus(status) {
  const value = String(status || '').toLowerCase();

  if (value.includes('running') || value.includes('completed') || value.includes('continue')) {
    return 'good';
  }

  if (value.includes('stop') || value.includes('fail') || value.includes('critical')) {
    return 'danger';
  }

  if (value.includes('outside') || value.includes('pending') || value.includes('skip')) {
    return 'warn';
  }

  return 'info';
}

function reportByKey(operatorState, key) {
  return operatorState.reports.find((report) => report.key === key);
}

function jsonPreview(raw) {
  const text = JSON.stringify(raw || {}, null, 2);
  return text.length > 2200 ? `${text.slice(0, 2200)}\n... gekuerzt` : text;
}

function Metric({ label, value, tone = 'info' }) {
  return (
    <div className="cockpit-detail-metric">
      <span>{label}</span>
      <strong className={toneClass(tone)}>{value}</strong>
    </div>
  );
}

function buildModules(operatorState) {
  const activeJobs = operatorState.schedulerJobs.filter((job) => job.enabled);
  const nextJob = activeJobs
    .filter((job) => job.next_run_utc)
    .sort((left, right) => Date.parse(left.next_run_utc) - Date.parse(right.next_run_utc))[0];
  const warningCount = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ].filter(Boolean).length;

  return [
    {
      id: 'open_supervisor',
      title: 'Supervisor',
      value: operatorState.supervisor.running ? 'running' : operatorState.supervisor.status,
      detail: operatorState.supervisor.next_action,
      tone: operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status),
      meta: `Heartbeat ${shortDateTime(operatorState.supervisor.heartbeat_utc)}`,
    },
    {
      id: 'open_scheduler',
      title: 'Scheduler',
      value: `${activeJobs.length} aktiv`,
      detail: nextJob ? `${nextJob.job_type} / ${shortDateTime(nextJob.next_run_utc)}` : 'Keine naechsten Jobs gemeldet',
      tone: activeJobs.length ? 'info' : 'warn',
      meta: 'config-gesteuert',
    },
    {
      id: 'open_nightly',
      title: 'Nightly',
      value: operatorState.nightly.current_state,
      detail: operatorState.nightly.next_action,
      tone: toneFromStatus(operatorState.nightly.current_state),
      meta: operatorState.nightly.next_nightly_window,
    },
    {
      id: 'open_research',
      title: 'Research',
      value: `${formatNumber(operatorState.research.strategies_tested)} Tests`,
      detail: `${formatNumber(operatorState.research.robust_strategies)} robust / ${formatNumber(operatorState.research.overfit_suspected)} overfit`,
      tone: operatorState.research.overfit_suspected ? 'warn' : 'good',
      meta: 'Bridge Reports',
    },
    {
      id: 'open_strategies',
      title: 'Strategien',
      value: `${formatNumber(operatorState.research.robust_strategies)} robust`,
      detail: 'Robuste und auffaellige Kandidaten nur zur Bewertung.',
      tone: operatorState.research.robust_strategies ? 'good' : 'warn',
      meta: 'read-only',
    },
    {
      id: 'open_regime',
      title: 'Regime',
      value: `${Math.round(Number(operatorState.research.regime_consistency_score || 0) * 100)}% Konsistenz`,
      detail: operatorState.research.regime_distribution.slice(0, 2).join(', ') || 'Noch keine Regime-Verteilung',
      tone: operatorState.research.regime_consistency_score ? 'good' : 'warn',
      meta: 'Marktumfeld',
    },
    {
      id: 'open_storage',
      title: 'Storage',
      value: formatGb(operatorState.storage.free_disk_gb),
      detail: `${formatNumber(operatorState.storage.cleanup_candidate_count)} Cleanup-Kandidaten`,
      tone: operatorState.storage.errors.length ? 'danger' : 'good',
      meta: operatorState.storage.root,
    },
    {
      id: 'open_resources',
      title: 'Ressourcen',
      value: `${Math.round(operatorState.resource.cpu_usage_percent)}% CPU`,
      detail: `${Math.round(operatorState.resource.memory_usage_percent)}% RAM / ${formatGb(operatorState.resource.free_disk_gb)} frei`,
      tone: operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good',
      meta: operatorState.resource.action,
    },
    {
      id: 'open_safety',
      title: 'Safety',
      value: 'gesperrt',
      detail: 'Auto-Trading aus, menschliche Freigabe Pflicht.',
      tone: 'warn',
      meta: 'keine Orders',
    },
    {
      id: 'open_logs',
      title: 'Logs',
      value: warningCount ? `${warningCount} Warnungen` : 'ruhig',
      detail: operatorState.logLines.at(-1) || 'Keine Live-Logs in Bridge v1',
      tone: warningCount ? 'warn' : 'info',
      meta: 'read-only',
    },
  ];
}

function VoiceSphere({ operatorState, isRefreshing }) {
  const bridgeLive = operatorState.dataSource === DATA_SOURCE.LIVE_FILE;
  const supervisorTone = operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status);

  return (
    <section className="voice-core" aria-label="Jarvis Sprachmodul">
      <div className={`voice-sphere ${bridgeLive ? 'is-live' : 'is-fixture'}`}>
        <div className="voice-sphere-ring" />
        <div className="voice-sphere-content">
          <span>Jarvis aktiv</span>
          <strong>Voice Sphere</strong>
          <p>Spracheingabe geplant · Sprachausgabe geplant</p>
        </div>
      </div>

      <div className="voice-status-strip">
        <StatusPill tone={supervisorTone}>Supervisor {operatorState.supervisor.running ? 'running' : 'stopped'}</StatusPill>
        <StatusPill tone="warn">Auto-Trading gesperrt</StatusPill>
        <StatusPill tone={sourceTone(operatorState.dataSource)}>{sourceModeLabel(operatorState.dataSource)}</StatusPill>
        <StatusPill tone={isRefreshing ? 'info' : 'good'}>
          {isRefreshing ? 'liest Bridge' : `Update ${shortTime(operatorState.lastUpdatedAt)}`}
        </StatusPill>
      </div>

      <div className="chat-fallback" aria-label="Chat-Fallback">
        <input
          aria-label="Chat-Fallback Eingabe"
          placeholder="Chat-Fallback: spaeter Frage oder Sprachbefehl eingeben..."
          readOnly
          value=""
        />
        <button disabled type="button">Senden geplant</button>
      </div>
    </section>
  );
}

function OrbitPanel({ module, onOpen }) {
  return (
    <button
      className={`orbit-panel ${toneClass(module.tone)}`}
      data-command={module.id}
      onClick={() => onOpen(module.id)}
      type="button"
    >
      <div>
        <span>{module.title}</span>
        <StatusPill tone={module.tone}>{module.value}</StatusPill>
      </div>
      <p>{module.detail}</p>
      <small>{module.meta}</small>
    </button>
  );
}

function MasterStatusOverview({ masterStatus, source }) {
  const statusTone = toneFromStatus(masterStatus.overall_status);

  return (
    <section className="cockpit-master-status" aria-label="Hermes Master Status">
      <div className="cockpit-master-head">
        <span>Hermes Master Status</span>
        <div className="cockpit-master-badges">
          <StatusPill tone={statusTone}>{masterStatus.overall_status}</StatusPill>
          <StatusPill tone={sourceTone(source)}>
            {source === DATA_SOURCE.LIVE_FILE ? 'Live Snapshot aktiv' : sourceModeLabel(source)}
          </StatusPill>
        </div>
      </div>
      {source !== DATA_SOURCE.LIVE_FILE ? (
        <p className="cockpit-master-source-warning">Demo-/Snapshot-Daten aktiv</p>
      ) : null}
      <div className="cockpit-master-grid">
        <Metric label="Fokus" value={masterStatus.current_focus} tone="info" />
        <Metric label="Domaenen" value={masterStatus.active_domains.join(', ') || '-'} tone="info" />
        <Metric label="Queue" value={formatNumber(masterStatus.queued_tasks)} tone={masterStatus.queued_tasks ? 'warn' : 'good'} />
        <Metric label="Nightly" value={shortDateTime(masterStatus.last_nightly_run)} />
        <Metric label="Autonomer Loop" value={shortDateTime(masterStatus.last_autonomous_loop)} />
        <Metric label="Meta Review" value={shortDateTime(masterStatus.last_meta_review)} />
        <Metric label="Lernstrategie" value={masterStatus.learning_strategy} />
        <Metric label="Supervisor" value={masterStatus.supervisor_running ? 'running' : 'stopped'} tone={masterStatus.supervisor_running ? 'good' : 'warn'} />
        <Metric label="Scheduler" value={`${formatNumber(masterStatus.scheduler_enabled)} aktiv`} />
        <Metric label="Ressourcen" value={masterStatus.resource_action} tone={toneFromStatus(masterStatus.resource_action)} />
        <Metric label="Storage Cleanup" value={formatNumber(masterStatus.storage_cleanup)} tone={masterStatus.storage_cleanup ? 'warn' : 'good'} />
        <Metric label="Robust" value={formatNumber(masterStatus.robust_strategies)} tone={masterStatus.robust_strategies ? 'good' : 'warn'} />
        <Metric label="Demo-Kandidaten" value={formatNumber(masterStatus.demo_bot_candidates)} tone={masterStatus.demo_bot_candidates ? 'good' : 'warn'} />
        <Metric label="no_auto_trading" value={String(masterStatus.no_auto_trading)} tone={masterStatus.no_auto_trading ? 'good' : 'danger'} />
        <Metric label="human_review" value={String(masterStatus.human_review_required)} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <Metric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <Metric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
    </section>
  );
}

function DetailOverlay({ moduleId, modules, operatorState, onClose }) {
  const module = modules.find((item) => item.id === moduleId);

  if (!module) {
    return null;
  }

  const schedulerJobs = operatorState.schedulerJobs.filter((job) => job.enabled).slice(0, 8);
  const warnings = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ].filter(Boolean);

  return (
    <div className="cockpit-overlay" role="dialog" aria-modal="true" aria-labelledby="cockpit-detail-title">
      <div className="cockpit-overlay-backdrop" onClick={onClose} />
      <section className="cockpit-detail-panel">
        <div className="cockpit-detail-head">
          <div>
            <p className="eyebrow">{module.id}</p>
            <h2 id="cockpit-detail-title">{module.title}</h2>
          </div>
          <button className="cockpit-close-button" onClick={onClose} type="button">Schliessen</button>
        </div>

        <div className="cockpit-detail-grid">
          {moduleId === 'open_supervisor' ? (
            <>
              <Metric label="Status" value={operatorState.supervisor.status} tone={module.tone} />
              <Metric label="Heartbeat" value={shortDateTime(operatorState.supervisor.heartbeat_utc)} />
              <Metric label="Uptime" value={`${formatNumber(operatorState.supervisor.uptime_minutes)} min`} />
              <Metric label="Aktueller Job" value={operatorState.supervisor.current_job} />
            </>
          ) : null}

          {moduleId === 'open_scheduler' ? schedulerJobs.map((job) => (
            <Metric
              key={job.job_id}
              label={job.job_type}
              value={shortDateTime(job.next_run_utc)}
              tone={toneFromStatus(job.status)}
            />
          )) : null}

          {moduleId === 'open_nightly' ? (
            <>
              <Metric label="Status" value={operatorState.nightly.current_state} tone={module.tone} />
              <Metric label="Fenster" value={operatorState.nightly.next_nightly_window} />
              <Metric label="Naechster Start" value={shortDateTime(operatorState.nightly.next_scheduled_start_utc)} />
              <Metric label="Iterationen" value={formatNumber(operatorState.nightly.iterations_completed)} />
            </>
          ) : null}

          {moduleId === 'open_research' || moduleId === 'open_strategies' || moduleId === 'open_regime' ? (
            <>
              <Metric label="Strategien getestet" value={formatNumber(operatorState.research.strategies_tested)} />
              <Metric label="Robust" value={formatNumber(operatorState.research.robust_strategies)} tone="good" />
              <Metric label="Overfit-Verdacht" value={formatNumber(operatorState.research.overfit_suspected)} tone="warn" />
              <Metric label="Regime-Konsistenz" value={`${Math.round(operatorState.research.regime_consistency_score * 100)}%`} tone="good" />
            </>
          ) : null}

          {moduleId === 'open_storage' ? (
            <>
              <Metric label="Root" value={operatorState.storage.root} />
              <Metric label="Status" value={operatorState.storage.status} tone="good" />
              <Metric label="Freier Speicher" value={formatGb(operatorState.storage.free_disk_gb)} tone="good" />
              <Metric label="Cleanup-Kandidaten" value={formatNumber(operatorState.storage.cleanup_candidate_count)} tone="warn" />
            </>
          ) : null}

          {moduleId === 'open_resources' ? (
            <>
              <Metric label="CPU" value={`${Math.round(operatorState.resource.cpu_usage_percent)}%`} />
              <Metric label="RAM" value={`${Math.round(operatorState.resource.memory_usage_percent)}%`} />
              <Metric label="Disk frei" value={formatGb(operatorState.resource.free_disk_gb)} tone="good" />
              <Metric label="Aktion" value={operatorState.resource.action} tone={module.tone} />
            </>
          ) : null}

          {moduleId === 'open_safety' ? (
            <>
              <Metric label="Auto-Trading" value="deaktiviert" tone="warn" />
              <Metric label="Human Review" value="erforderlich" tone="warn" />
              <Metric label="Orderbuttons" value="nicht vorhanden" tone="danger" />
              <Metric label="Bridge" value="read-only" tone="good" />
            </>
          ) : null}

          {moduleId === 'open_logs' ? warnings.slice(0, 8).map((warning) => (
            <Metric key={warning} label="Warnung" value={warning} tone="warn" />
          )) : null}

          {moduleId === 'open_logs' && warnings.length === 0 ? (
            <Metric label="Status" value="Keine Warnungen gemeldet" tone="good" />
          ) : null}
        </div>

        <div className="cockpit-report-preview">
          <div className="cockpit-report-head">
            <span>Report-Auszug</span>
            <StatusPill tone={sourceTone(operatorState.dataSource)}>
              {sourceModeLabel(operatorState.dataSource)}
            </StatusPill>
          </div>
          <pre>{detailPreview(moduleId, operatorState)}</pre>
        </div>
      </section>
    </div>
  );
}

function detailPreview(moduleId, operatorState) {
  const map = {
    open_supervisor: 'supervisorState',
    open_scheduler: 'schedulerState',
    open_research: 'researchInsights',
    open_strategies: 'robustStrategies',
    open_regime: 'regimeSummary',
    open_storage: 'cleanupPlan',
    open_resources: 'resourceStatus',
    open_logs: 'nightlyState',
  };
  const reportKey = map[moduleId] || 'researchInsights';
  const report = reportByKey(operatorState, reportKey);

  if (moduleId === 'open_safety') {
    return jsonPreview({
      no_auto_trading: true,
      human_review_required: true,
      ui_mode: 'read_only_monitoring',
      disabled: ['orders', 'runtime_commands', 'broker_actions', 'write_access'],
    });
  }

  return jsonPreview(report?.raw || {});
}

export function CockpitShell() {
  const [operatorState, setOperatorState] = useState(() => createOperatorDashboardFallback());
  const [activeModule, setActiveModule] = useState('');
  const [isRefreshing, setIsRefreshing] = useState(false);

  useEffect(() => {
    let mounted = true;
    let refreshTimer;

    const refresh = () => {
      setIsRefreshing(true);
      loadOperatorDashboard()
        .then((nextState) => {
          if (mounted) {
            setOperatorState(nextState);
          }
        })
        .finally(() => {
          if (mounted) {
            setIsRefreshing(false);
          }
        });
    };

    refresh();
    refreshTimer = window.setInterval(refresh, COCKPIT_REFRESH_SECONDS * 1000);

    return () => {
      mounted = false;
      window.clearInterval(refreshTimer);
    };
  }, []);

  const modules = useMemo(() => buildModules(operatorState), [operatorState]);
  const leftModules = modules.slice(0, 5);
  const rightModules = modules.slice(5);
  const fixtureActive = operatorState.dataSource !== DATA_SOURCE.LIVE_FILE;

  return (
    <section className="cockpit-shell" aria-label="Jarvis Cockpit Hauptansicht">
      <div className="cockpit-meta-strip">
        <StatusPill tone={sourceTone(operatorState.dataSource)}>
          {sourceModeLabel(operatorState.dataSource)}
        </StatusPill>
        <span>Zuletzt aktualisiert: {shortTime(operatorState.lastUpdatedAt)}</span>
        <span>Polling: {operatorState.pollIntervalSeconds || COCKPIT_REFRESH_SECONDS}s</span>
        <span>{formatNumber(operatorState.liveReportCount)} Bridge / {formatNumber(operatorState.fixtureReportCount)} Fixture</span>
      </div>

      {fixtureActive ? (
        <p className="cockpit-warning">
          Read-only Bridge nicht vollstaendig verfuegbar. Die Cockpit-Ansicht nutzt stabile Fixture-/Demo-Daten.
        </p>
      ) : null}

      <MasterStatusOverview
        masterStatus={operatorState.masterStatus}
        source={operatorState.masterStatusSource}
      />

      <div className="cockpit-layout">
        <div className="orbit-column orbit-column-left">
          {leftModules.map((module) => (
            <OrbitPanel key={module.id} module={module} onOpen={setActiveModule} />
          ))}
        </div>

        <VoiceSphere operatorState={operatorState} isRefreshing={isRefreshing} />

        <div className="orbit-column orbit-column-right">
          {rightModules.map((module) => (
            <OrbitPanel key={module.id} module={module} onOpen={setActiveModule} />
          ))}
        </div>
      </div>

      <div className="cockpit-command-strip" aria-label="Vorbereitete Sprachbefehle">
        {['open_supervisor', 'open_scheduler', 'open_research', 'open_storage', 'open_safety', 'open_logs'].map((command) => (
          <code key={command}>{command}</code>
        ))}
      </div>

      <DetailOverlay
        moduleId={activeModule}
        modules={modules}
        operatorState={operatorState}
        onClose={() => setActiveModule('')}
      />
    </section>
  );
}
