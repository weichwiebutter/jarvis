import { Component, useEffect, useMemo, useState } from 'react';
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

class ViewErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, message: '' };
  }

  static getDerivedStateFromError(error) {
    return {
      hasError: true,
      message: error instanceof Error ? error.message : String(error),
    };
  }

  render() {
    if (this.state.hasError) {
      return (
        <section className="control-view-panel" aria-label="Fehler">
          <div className="control-view-head">
            <div>
              <p className="eyebrow">Fehler</p>
              <h2>Ansicht konnte nicht geladen werden</h2>
            </div>
          </div>
          <p className="control-view-note">
            Prüfdaten konnten nicht geladen werden. Bridge prüfen oder später erneut versuchen.
          </p>
          <p className="control-view-note">
            Details: {this.state.message || 'unbekannter Fehler'}
          </p>
        </section>
      );
    }

    return this.props.children;
  }
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

function statusDeutsch(value) {
  const normalized = String(value || '').toLowerCase();
  const labels = {
    ok: 'in Ordnung',
    warning: 'Warnung',
    critical: 'kritisch',
    weak: 'schwach',
    promising: 'vielversprechend',
    validated: 'validiert',
    trusted: 'vertrauenswürdig',
    pending: 'offen',
    approved: 'freigegeben',
    rejected: 'abgelehnt',
    needs_more_evidence: 'mehr Evidenz nötig',
    deferred: 'zurückgestellt',
    high: 'hoch',
    medium: 'mittel',
    low: 'niedrig',
    consolidation: 'Konsolidierung',
    exploration: 'Erkundung',
    validation: 'Validierung',
    quality_improvement: 'Qualitätsverbesserung',
    source_expansion: 'Quellenerweiterung',
    running: 'läuft',
    stopped: 'gestoppt',
    continue: 'weiter',
    prepared: 'vorbereitet',
    needs_validation: 'Validierung nötig',
    needs_attention: 'Aufmerksamkeit nötig',
    completed: 'abgeschlossen',
    idle: 'wartet',
  };

  return labels[normalized] || String(value || '-');
}

function trustLabel(value) {
  return statusDeutsch(value);
}

function domainLabel(value) {
  const normalized = String(value || '').toLowerCase();
  const labels = {
    trading: 'Trading',
    software: 'Software',
    documentation: 'Dokumentation',
    process: 'Prozesse',
    research: 'Recherche',
  };

  return labels[normalized] || String(value || '-');
}

function priorityTone(priority) {
  const value = String(priority || '').toLowerCase();

  if (value === 'high') {
    return 'danger';
  }

  if (value === 'medium') {
    return 'warn';
  }

  return 'info';
}

function cliReviewCommand(action, reviewId) {
  const commands = {
    approve: `dotnet run --project ./cli/Hermes.Cli.csproj -- approve-review --id ${reviewId} --note "Manuell geprüft und plausibel."`,
    reject: `dotnet run --project ./cli/Hermes.Cli.csproj -- reject-review --id ${reviewId} --note "Manuell geprüft und abgelehnt."`,
    more: `dotnet run --project ./cli/Hermes.Cli.csproj -- request-more-evidence --id ${reviewId} --note "Bitte weitere Evidenz sammeln."`,
    defer: `dotnet run --project ./cli/Hermes.Cli.csproj -- defer-review --id ${reviewId} --note "Später prüfen."`,
  };

  return commands[action] || '';
}

function reviewReasonDeutsch(value) {
  const text = String(value || '');

  if (text.includes('Trust v2 requires human review')) {
    return 'Trust v2 benötigt eine menschliche Prüfung, bevor dieses Wissen höher eingestuft werden darf.';
  }

  if (text.includes('Master Status meldet')) {
    return text.replace('Master Status', 'Gesamtstatus');
  }

  return text || '-';
}

function reviewRecommendationDeutsch(value) {
  const normalized = String(value || '').toLowerCase();
  const labels = {
    human_review_can_unlock_validated_trust:
      'Menschliche Prüfung kann den validierten Vertrauensstatus freischalten.',
    review_for_quality_gate: 'Für das Qualitätsgate menschlich prüfen.',
    review_required: 'Menschliche Prüfung erforderlich.',
  };

  return labels[normalized] || String(value || '-');
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
    <details className="goal-system-card" open>
      <summary>
        <span>Hermes Ziele</span>
        <strong>{goalAvailable ? goalLabel(masterStatus.top_goal) : 'nicht verfügbar'}</strong>
        <StatusPill tone={blockedGoals.length ? 'warn' : goalAvailable ? 'good' : 'info'}>
          {goalAvailable ? `${blockedGoals.length} blockiert` : 'offline'}
        </StatusPill>
      </summary>

      {goalAvailable ? (
        <>
          <div className="goal-system-metrics">
            <Metric label="Hauptziel" value={goalLabel(masterStatus.top_goal)} tone={blockedGoals.includes(masterStatus.top_goal) ? 'warn' : 'info'} />
            <Metric label="Aktive Ziele" value={formatNumber(activeGoals.length)} tone="info" />
            <Metric label="Blockiert" value={formatNumber(blockedGoals.length)} tone={blockedGoals.length ? 'warn' : 'good'} />
            <Metric label="Letzte Bewertung" value={shortDateTime(masterStatus.updated_at_utc)} />
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
        <p>Goal-System noch nicht verfügbar.</p>
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
    <details className="knowledge-health-card" open>
      <summary>
        <span>Wissenszustand</span>
        <strong>{statusDeutsch(health)}</strong>
        <StatusPill tone={tone}>{masterStatus.knowledge_trend || '-'}</StatusPill>
      </summary>
      <div className="goal-system-metrics">
        <Metric label="Vertrauenswürdig" value={formatNumber(masterStatus.trusted_knowledge)} tone="good" />
        <Metric label="Schwach" value={formatNumber(masterStatus.weak_knowledge)} tone={masterStatus.weak_knowledge ? 'warn' : 'good'} />
        <Metric label="Veraltet" value={formatNumber(masterStatus.deprecated_knowledge)} tone={masterStatus.deprecated_knowledge ? 'warn' : 'good'} />
        <Metric label="Ø Vertrauen" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
        <Metric label="Ø Qualität" value={scorePercent(masterStatus.average_quality_score)} tone={tone} />
        <Metric label="Trend" value={masterStatus.knowledge_trend || '-'} tone="info" />
        <Metric label="Offene Pläne" value={formatNumber(masterStatus.validation_plans_open)} tone={masterStatus.validation_plans_open ? 'warn' : 'good'} />
        <Metric label="OOS nötig" value={formatNumber(masterStatus.knowledge_items_needing_oos)} tone={masterStatus.knowledge_items_needing_oos ? 'warn' : 'good'} />
        <Metric label="Vertrauenskandidaten" value={formatNumber(masterStatus.trusted_candidate_count)} tone={masterStatus.trusted_candidate_count ? 'good' : 'info'} />
        <Metric label="Offene Prüfungen" value={formatNumber(masterStatus.pending_reviews)} tone={masterStatus.pending_reviews ? 'warn' : 'good'} />
      </div>
    </details>
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
      value: operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status),
      detail: operatorState.supervisor.next_action,
      tone: operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status),
      meta: `Heartbeat ${shortDateTime(operatorState.supervisor.heartbeat_utc)}`,
    },
    {
      id: 'open_scheduler',
      title: 'Scheduler',
      value: `${activeJobs.length} aktiv`,
      detail: nextJob ? `${nextJob.job_type} / ${shortDateTime(nextJob.next_run_utc)}` : 'Keine nächsten Jobs gemeldet',
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
      title: 'Recherche',
      value: `${formatNumber(operatorState.research.strategies_tested)} Tests`,
      detail: `${formatNumber(operatorState.research.robust_strategies)} robust / ${formatNumber(operatorState.research.overfit_suspected)} overfit`,
      tone: operatorState.research.overfit_suspected ? 'warn' : 'good',
      meta: 'Bridge-Reports',
    },
    {
      id: 'open_strategies',
      title: 'Strategien',
      value: `${formatNumber(operatorState.research.robust_strategies)} robust`,
      detail: 'Robuste und auffällige Kandidaten nur zur Bewertung.',
      tone: operatorState.research.robust_strategies ? 'good' : 'warn',
      meta: 'nur lesend',
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
      title: 'Speicher',
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
      title: 'Sicherheit',
      value: 'gesperrt',
      detail: 'Auto-Trading aus, menschliche Freigabe Pflicht.',
      tone: 'warn',
      meta: 'keine Orders',
    },
    {
      id: 'open_logs',
      title: 'Protokolle',
      value: warningCount ? `${warningCount} Warnungen` : 'ruhig',
      detail: operatorState.logLines.at(-1) || 'Keine Live-Protokolle in Bridge v1',
      tone: warningCount ? 'warn' : 'info',
      meta: 'nur lesend',
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
          <strong>Sprachzentrale</strong>
          <p>Spracheingabe geplant · Sprachausgabe geplant</p>
        </div>
      </div>

      <div className="voice-status-strip">
        <StatusPill tone={supervisorTone}>Supervisor {operatorState.supervisor.running ? 'läuft' : 'gestoppt'}</StatusPill>
        <StatusPill tone="warn">Auto-Trading gesperrt</StatusPill>
        <StatusPill tone={sourceTone(operatorState.dataSource)}>{sourceModeLabel(operatorState.dataSource)}</StatusPill>
        <StatusPill tone={isRefreshing ? 'info' : 'good'}>
          {isRefreshing ? 'liest Bridge' : `Update ${shortTime(operatorState.lastUpdatedAt)}`}
        </StatusPill>
      </div>

      <div className="chat-fallback" aria-label="Chat-Ersatz">
        <input
          aria-label="Chat-Ersatz Eingabe"
          placeholder="Chat-Ersatz: später Frage oder Sprachbefehl eingeben..."
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

function DashboardAccordion({ id, title, badge, tone = 'info', summary, children, defaultOpen = false }) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <details className={`cockpit-accordion ${toneClass(tone)}`} open={open} onToggle={(event) => setOpen(event.currentTarget.open)}>
      <summary>
        <div className="cockpit-accordion-head">
          <span>{title}</span>
          <strong>{summary}</strong>
        </div>
        <div className="cockpit-accordion-badges">
          {badge ? <StatusPill tone={tone}>{badge}</StatusPill> : null}
          <StatusPill tone={open ? 'good' : 'info'}>{open ? 'geöffnet' : 'eingeklappt'}</StatusPill>
        </div>
      </summary>
      {open ? <div className="cockpit-accordion-body">{children}</div> : null}
    </details>
  );
}

function MasterStatusOverview({ masterStatus, source }) {
  const statusTone = toneFromStatus(masterStatus.overall_status);

  return (
    <section className="cockpit-master-status" aria-label="Hermes Gesamtstatus">
      <div className="cockpit-master-head">
        <span>Hermes Gesamtstatus</span>
        <div className="cockpit-master-badges">
          <StatusPill tone={statusTone}>{statusDeutsch(masterStatus.overall_status)}</StatusPill>
          <StatusPill tone={sourceTone(source)}>
            {source === DATA_SOURCE.LIVE_FILE ? 'Live-Snapshot aktiv' : sourceModeLabel(source)}
          </StatusPill>
        </div>
      </div>
      {source !== DATA_SOURCE.LIVE_FILE ? (
        <p className="cockpit-master-source-warning">Demo-/Snapshot-Daten aktiv</p>
      ) : null}
      <div className="cockpit-master-grid">
        <Metric label="Fokus" value={masterStatus.current_focus} tone="info" />
        <Metric label="Domänen" value={masterStatus.active_domains.map(domainLabel).join(', ') || '-'} tone="info" />
        <Metric label="Offene Aufgaben" value={formatNumber(masterStatus.queued_tasks)} tone={masterStatus.queued_tasks ? 'warn' : 'good'} />
        <Metric label="Letzter Nachtlauf" value={shortDateTime(masterStatus.last_nightly_run)} />
        <Metric label="Autonomer Lernzyklus" value={shortDateTime(masterStatus.last_autonomous_loop)} />
        <Metric label="Letzte Lernanalyse" value={shortDateTime(masterStatus.last_meta_review)} />
        <Metric label="Lernstrategie" value={masterStatus.learning_strategy} />
        <Metric label="Supervisor" value={masterStatus.supervisor_running ? 'läuft' : 'gestoppt'} tone={masterStatus.supervisor_running ? 'good' : 'warn'} />
        <Metric label="Scheduler" value={`${formatNumber(masterStatus.scheduler_enabled)} aktiv`} />
        <Metric label="Ressourcen" value={masterStatus.resource_action} tone={toneFromStatus(masterStatus.resource_action)} />
        <Metric label="Speicherbereinigung" value={formatNumber(masterStatus.storage_cleanup)} tone={masterStatus.storage_cleanup ? 'warn' : 'good'} />
        <Metric label="Robust" value={formatNumber(masterStatus.robust_strategies)} tone={masterStatus.robust_strategies ? 'good' : 'warn'} />
        <Metric label="Demo-Kandidaten" value={formatNumber(masterStatus.demo_bot_candidates)} tone={masterStatus.demo_bot_candidates ? 'good' : 'warn'} />
        <Metric label="no_auto_trading" value={String(masterStatus.no_auto_trading)} tone={masterStatus.no_auto_trading ? 'good' : 'danger'} />
        <Metric label="human_review" value={String(masterStatus.human_review_required)} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <Metric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <Metric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
      <GoalSystemCard masterStatus={masterStatus} />
      <KnowledgeHealthCard masterStatus={masterStatus} />
      <ScalpingProgressPanel masterStatus={masterStatus} />
    </section>
  );
}

function compactStatusLabel(status) {
  const normalized = String(status || '').toLowerCase();

  if (normalized.includes('bot_ready')) return 'Bot-bereit';
  if (normalized.includes('signal_ready')) return 'Signal-bereit';
  if (normalized.includes('needs_more_validation')) return 'Weitere Prüfung nötig';
  if (normalized.includes('needs_validation')) return 'Prüfung erforderlich';
  if (normalized.includes('ready')) return 'Bereit';
  if (normalized.includes('warning')) return 'Warnung';
  if (normalized.includes('critical')) return 'Kritisch';
  return statusDeutsch(status);
}

function DashboardSystemStatus({ operatorState }) {
  return (
    <div className="cockpit-accordion-grid">
      <Metric label="Gesamtstatus" value={statusDeutsch(operatorState.masterStatus.overall_status)} tone={toneFromStatus(operatorState.masterStatus.overall_status)} />
      <Metric label="Supervisor" value={operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status)} tone={operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status)} />
      <Metric label="Scheduler" value={`${formatNumber(operatorState.schedulerJobs.filter((job) => job.enabled).length)} aktiv`} tone="info" />
      <Metric label="Nightly" value={statusDeutsch(operatorState.nightly.current_state)} tone={toneFromStatus(operatorState.nightly.current_state)} />
      <Metric label="Letzte Analyse" value={shortDateTime(operatorState.masterStatus.last_meta_review)} tone="info" />
      <Metric label="Lernstrategie" value={operatorState.masterStatus.learning_strategy} tone="info" />
    </div>
  );
}

function DashboardTradingIntelligence({ operatorState }) {
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  const validationReport = reportByKey(operatorState, 'validateEnsembleSignalPackage')?.raw || {};
  const handoffReport = reportByKey(operatorState, 'systemBHandoffBundle')?.raw || {};

  const assets = Array.isArray(portfolioReport.assets) && portfolioReport.assets.length
    ? portfolioReport.assets
    : [
        { asset: 'GER40', readiness: 'bot_ready' },
        { asset: 'XAUUSD', readiness: 'bot_ready' },
        { asset: 'EURUSD', readiness: 'needs_more_validation' },
      ];

  return (
    <div className="cockpit-accordion-grid">
      {assets.map((asset) => (
        <Metric
          key={asset.asset}
          label={asset.asset}
          value={compactStatusLabel(asset.readiness)}
          tone={asset.readiness === 'bot_ready' ? 'good' : asset.readiness.includes('need') ? 'warn' : 'info'}
        />
      ))}
      <Metric label="Portfolio" value={compactStatusLabel(portfolioReport.portfolio_readiness || portfolioReport.portfolio_status)} tone={toneFromStatus(portfolioReport.portfolio_readiness || portfolioReport.portfolio_status)} />
      <Metric label="Paketvalidierung" value={compactStatusLabel(validationReport.validation_status || validationReport.status)} tone={toneFromStatus(validationReport.validation_status || validationReport.status)} />
      <Metric label="Übergabepaket" value={handoffReport.bundle_path || portfolioReport.bundle_path || '-'} tone="info" />
    </div>
  );
}

function DashboardReviewSummary({ operatorState }) {
  const review = operatorState.humanReview || {};
  return (
    <div className="cockpit-accordion-grid">
      <Metric label="Offene Prüfungen" value={formatNumber(review.pending_reviews)} tone={review.pending_reviews ? 'warn' : 'good'} />
      <Metric label="Freigegeben" value={formatNumber(review.approved_reviews)} tone="good" />
      <Metric label="Zurückgestellt" value={formatNumber(review.deferred_reviews)} tone="info" />
      <Metric label="Evidenz angefordert" value={formatNumber(review.needs_more_evidence_reviews)} tone={review.needs_more_evidence_reviews ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardLearningSummary({ operatorState }) {
  const masterStatus = operatorState.masterStatus;
  return (
    <div className="cockpit-accordion-grid">
      <Metric label="Wissensqualität" value={statusDeutsch(masterStatus.knowledge_health)} tone={toneFromStatus(masterStatus.knowledge_health)} />
      <Metric label="Vertrauen" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
      <Metric label="Offene Pläne" value={formatNumber(masterStatus.validation_plans_open)} tone={masterStatus.validation_plans_open ? 'warn' : 'good'} />
      <Metric label="OOS nötig" value={formatNumber(masterStatus.knowledge_items_needing_oos)} tone={masterStatus.knowledge_items_needing_oos ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardStorageResources({ operatorState }) {
  return (
    <div className="cockpit-accordion-grid">
      <Metric label="RAM" value={formatGb(operatorState.resource.free_memory_gb || operatorState.resource.memory_free_gb || 0)} tone="info" />
      <Metric label="CPU" value={`${Math.round(operatorState.resource.cpu_usage_percent)}%`} tone={operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good'} />
      <Metric label="Speicherplatz" value={formatGb(operatorState.storage.free_disk_gb)} tone={operatorState.storage.errors.length ? 'warn' : 'good'} />
      <Metric label="Cleanup" value={formatNumber(operatorState.storage.cleanup_candidate_count)} tone={operatorState.storage.cleanup_candidate_count ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardSafety({ operatorState }) {
  return (
    <div className="cockpit-accordion-grid">
      <Metric label="Auto-Trading" value={operatorState.masterStatus.no_auto_trading ? 'aus' : 'an'} tone={operatorState.masterStatus.no_auto_trading ? 'good' : 'danger'} />
      <Metric label="Menschliche Prüfung" value={operatorState.masterStatus.human_review_required ? 'erforderlich' : 'frei'} tone={operatorState.masterStatus.human_review_required ? 'warn' : 'good'} />
      <Metric label="Broker-Orders" value={operatorState.masterStatus.broker_orders_enabled ? 'an' : 'aus'} tone={operatorState.masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
      <Metric label="Live-Trading" value={operatorState.masterStatus.live_trading_enabled ? 'an' : 'aus'} tone={operatorState.masterStatus.live_trading_enabled ? 'danger' : 'good'} />
        <Metric label="Nur Forschung" value={operatorState.masterStatus.research_only ? 'aktiv' : 'inaktiv'} tone={operatorState.masterStatus.research_only ? 'good' : 'warn'} />
    </div>
  );
}

function DashboardLogs({ operatorState }) {
  const warnings = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ].filter(Boolean);

  return (
    <div className="cockpit-accordion-grid">
      <div className="operator-warning-list">
        {(warnings.length ? warnings : ['Keine kritischen Warnungen.']).slice(0, 6).map((warning) => (
          <span key={warning}>{warning}</span>
        ))}
      </div>
      <div className="operator-log-list">
        {operatorState.logLines.slice(-6).map((line) => (
          <code key={line}>{line}</code>
        ))}
      </div>
    </div>
  );
}

function ScalpingProgressPanel({ masterStatus }) {
  const finalCandidates = masterStatus.scalping_final_candidates || 0;
  const robustCandidates = masterStatus.scalping_robust_candidates || 0;
  const tone = finalCandidates ? 'good' : robustCandidates ? 'warn' : 'info';

  return (
    <section className="cockpit-sub-card" aria-label="Scalping Progress read-only">
      <div className="cockpit-sub-card-head">
        <span>Scalping Progress</span>
        <StatusPill tone={tone}>read-only</StatusPill>
      </div>
      <div className="cockpit-master-grid">
        <Metric label="Asset" value={masterStatus.scalping_asset || '-'} tone="info" />
        <Metric label="Candidates" value={formatNumber(masterStatus.scalping_candidates_total)} tone="info" />
        <Metric label="Robust" value={formatNumber(masterStatus.scalping_robust_candidates)} tone={robustCandidates ? 'good' : 'warn'} />
        <Metric label="Final" value={formatNumber(masterStatus.scalping_final_candidates)} tone={finalCandidates ? 'good' : 'warn'} />
        <Metric label="Best Candidate" value={masterStatus.best_scalping_candidate || '-'} tone="info" />
        <Metric label="Monte Carlo" value={masterStatus.scalping_monte_carlo_health || 'missing'} tone={toneFromStatus(masterStatus.scalping_monte_carlo_health)} />
        <Metric label="Parameter Sensitivity" value={masterStatus.scalping_parameter_sensitivity_health || 'missing'} tone={toneFromStatus(masterStatus.scalping_parameter_sensitivity_health)} />
        <Metric label="Regime Validation" value={masterStatus.scalping_regime_validation_health || 'missing'} tone={toneFromStatus(masterStatus.scalping_regime_validation_health)} />
        <Metric label="Bot Specs" value={formatNumber(masterStatus.ctrader_bot_specs_ready)} tone={masterStatus.ctrader_bot_specs_ready ? 'good' : 'info'} />
        <Metric label="Signal-Spezifikationen" value={formatNumber(masterStatus.signal_agent_specs_ready)} tone={masterStatus.signal_agent_specs_ready ? 'good' : 'info'} />
        <Metric label="no_auto_trading" value={String(masterStatus.no_auto_trading)} tone={masterStatus.no_auto_trading ? 'good' : 'danger'} />
        <Metric label="human_review" value={String(masterStatus.human_review_required)} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <Metric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <Metric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
      <p className="cockpit-master-source-warning">Read-only: uses master-status/report snapshots only. No runtime commands or trading actions.</p>
    </section>
  );
}

const CONTROL_VIEWS = [
  { id: 'overview', label: 'Übersicht' },
  { id: 'review', label: 'Prüfzentrum' },
  { id: 'brain', label: 'Hermes Gehirn' },
  { id: 'trust', label: 'Wissensvertrauen' },
  { id: 'domains', label: 'Domänen' },
  { id: 'roles', label: 'Rollen' },
];

function ControlViewTabs({ activeView, onChange }) {
  return (
    <nav className="control-view-tabs" aria-label="Kontrollzentrum Ansichten">
      {CONTROL_VIEWS.map((view) => (
        <button
          className={view.id === activeView ? 'is-active' : ''}
          key={view.id}
          onClick={() => onChange(view.id)}
          type="button"
        >
          {view.label}
        </button>
      ))}
    </nav>
  );
}

function ReviewCommandList({ reviewId }) {
  return (
    <div className="review-command-list" aria-label="CLI-Befehle für menschliche Prüfung">
      <div>
        <span>Freigeben</span>
        <code>{cliReviewCommand('approve', reviewId)}</code>
      </div>
      <div>
        <span>Ablehnen</span>
        <code>{cliReviewCommand('reject', reviewId)}</code>
      </div>
      <div>
        <span>Mehr Evidenz anfordern</span>
        <code>{cliReviewCommand('more', reviewId)}</code>
      </div>
      <div>
        <span>Zurückstellen</span>
        <code>{cliReviewCommand('defer', reviewId)}</code>
      </div>
    </div>
  );
}

const REVIEW_ACTIONS = {
  approve: {
    label: 'Freigeben',
    endpoint: 'approve-review',
    decisionLabel: 'approved',
    prompt: 'Freigabe begründen',
  },
  reject: {
    label: 'Ablehnen',
    endpoint: 'reject-review',
    decisionLabel: 'rejected',
    prompt: 'Ablehnung begründen',
  },
  more: {
    label: 'Mehr Evidenz',
    endpoint: 'request-more-evidence',
    decisionLabel: 'needs_more_evidence',
    prompt: 'Welche Evidenz fehlt?',
  },
  defer: {
    label: 'Zurückstellen',
    endpoint: 'defer-review',
    decisionLabel: 'deferred',
    prompt: 'Warum zurückstellen?',
  },
};

function HumanReviewCenter({ operatorState, onRefresh }) {
  const review = operatorState.humanReview || {
    pending_reviews: 0,
    approved_reviews: 0,
    rejected_reviews: 0,
    items: [],
  };
  const items = Array.isArray(review.items) ? review.items : [];
  const [actionMessage, setActionMessage] = useState('');
  const [actionBusyId, setActionBusyId] = useState('');

  const runReviewAction = async (actionKey, item) => {
    const action = REVIEW_ACTIONS[actionKey];
    if (!action || !item?.review_id) {
      return;
    }

    const confirmText = [
      `${action.label} Review?`,
      `Review-ID: ${item.review_id}`,
      `Knowledge Item: ${item.knowledge_item_id}`,
      `Domain: ${item.domain}`,
      `Safety: no_auto_trading=true, human_review_required=true`,
    ].join('\n');
    if (!window.confirm(confirmText)) {
      return;
    }

    const note = window.prompt(action.prompt, `${action.label.toLowerCase()} via UI review`);
    if (note === null) {
      return;
    }

    setActionBusyId(item.review_id);
    setActionMessage('');
    try {
      const response = await fetch(`${__HERMES_READONLY_BRIDGE_URL__}/bridge/review/${action.endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          review_id: item.review_id,
          note: note.trim(),
          reviewer: 'ui_operator',
          source: 'jarvis-control-center',
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload?.error || payload?.message || `${response.status} ${response.statusText}`.trim());
      }

      setActionMessage(`Review ${item.review_id}: ${payload?.decision || action.decisionLabel} gespeichert.`);
      if (typeof onRefresh === 'function') {
        await onRefresh();
      }
    } catch (error) {
      setActionMessage(`Review ${item.review_id}: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setActionBusyId('');
    }
  };

  return (
    <section className="control-view-panel" aria-label="Prüfzentrum">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Menschliche Prüfung</p>
          <h2>Prüfzentrum</h2>
        </div>
        <div className="control-view-badges">
          <StatusPill tone={review.pending_reviews ? 'warn' : 'good'}>
            {formatNumber(review.pending_reviews)} offen
          </StatusPill>
          <StatusPill tone="info">{formatNumber(review.approved_reviews)} freigegeben</StatusPill>
          <StatusPill tone={sourceTone(operatorState.dataSource)}>
            {sourceModeLabel(operatorState.dataSource)}
          </StatusPill>
        </div>
      </div>

      <p className="control-view-note">
        Die UI kann Review-Aktionen auslösen, aber niemals Trading-Aktionen. Jede Entscheidung läuft über den Human-Review-Workflow und bleibt menschlich kontrolliert.
      </p>
      <div className="operator-safety-flags">
        <StatusPill tone="warn">no_auto_trading=true</StatusPill>
        <StatusPill tone="warn">human_review_required=true</StatusPill>
        <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
        <StatusPill tone="good">live_trading_enabled=false</StatusPill>
        <StatusPill tone="good">research_only=true</StatusPill>
      </div>
      {actionMessage ? <p className="control-view-note">{actionMessage}</p> : null}

      <div className="review-grid">
        {items.slice(0, 8).map((item) => (
          <article className="review-card" key={item.review_id}>
            <div className="review-card-head">
              <div>
                <span>{domainLabel(item.domain)}</span>
                <h3>{item.title}</h3>
              </div>
              <StatusPill tone={priorityTone(item.priority)}>
                {statusDeutsch(item.priority)}
              </StatusPill>
            </div>

            <div className="review-card-metrics">
              <Metric label="Wissenselement" value={item.knowledge_item_id} />
              <Metric label="Status" value={statusDeutsch(item.status)} tone={item.status === 'pending' ? 'warn' : 'info'} />
              <Metric label="Vertrauen vorher" value={scorePercent(item.trust_before)} tone="info" />
              <Metric label="Angefordert durch" value={item.requested_by_task_id} />
            </div>

            <p><strong>Grund:</strong> {reviewReasonDeutsch(item.reason)}</p>
            <p><strong>Empfehlung:</strong> {reviewRecommendationDeutsch(item.recommendation)}</p>
            <p><strong>Evidenz:</strong> {item.evidence_summary}</p>

            <div className="review-action-row" aria-label="Vorbereitete Prüfaktionen">
              <button disabled={actionBusyId === item.review_id} onClick={() => runReviewAction('approve', item)} type="button">Freigeben</button>
              <button disabled={actionBusyId === item.review_id} onClick={() => runReviewAction('reject', item)} type="button">Ablehnen</button>
              <button disabled={actionBusyId === item.review_id} onClick={() => runReviewAction('more', item)} type="button">Mehr Evidenz</button>
              <button disabled={actionBusyId === item.review_id} onClick={() => runReviewAction('defer', item)} type="button">Zurückstellen</button>
            </div>
            <ReviewCommandList reviewId={item.review_id} />
          </article>
        ))}

        {items.length === 0 ? (
          <article className="review-card">
            <h3>Keine offenen Prüfungen</h3>
            <p>Keine offenen Prüfungen.</p>
          </article>
        ) : null}
      </div>
    </section>
  );
}

function CognitiveCenter({ operatorState }) {
  return (
    <section className="control-view-panel" aria-label="Hermes Gehirn">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Kognitiver Kern</p>
          <h2>Hermes Gehirn</h2>
        </div>
        <StatusPill tone="info">{operatorState.masterStatus.learning_strategy}</StatusPill>
      </div>

      <div className="cognitive-step-grid">
        {operatorState.cognitiveControl.map((step) => (
          <article className="cognitive-step-card" key={step.id}>
            <div>
              <h3>{step.title}</h3>
              <StatusPill tone={step.warnings.length ? 'warn' : 'good'}>
                {step.warnings.length ? 'Warnung' : 'aktiv'}
              </StatusPill>
            </div>
            <Metric label="Status" value={step.status} tone={step.warnings.length ? 'warn' : 'info'} />
            <Metric label="Letzte Aktivität" value={shortDateTime(step.last_activity)} />
            <Metric label="Nächster Schritt" value={step.next_step} />
            <Metric label="Report" value={step.report_path} tone={step.report_available ? 'good' : 'warn'} />
            {step.warnings.length ? (
              <div className="operator-warning-list">
                {step.warnings.slice(0, 4).map((warning) => (
                  <span key={warning}>{warning}</span>
                ))}
              </div>
            ) : null}
          </article>
        ))}
      </div>
    </section>
  );
}

function KnowledgeTrustView({ operatorState }) {
  const masterStatus = operatorState.masterStatus;

  return (
    <section className="control-view-panel" aria-label="Wissensvertrauen">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Vertrauen und Evidenz</p>
          <h2>Wissensvertrauen</h2>
        </div>
        <StatusPill tone={masterStatus.knowledge_health === 'critical' ? 'danger' : 'warn'}>
          {statusDeutsch(masterStatus.knowledge_health)}
        </StatusPill>
      </div>

      <div className="trust-summary-grid">
        <Metric label="Evidenzabdeckung" value={scorePercent(masterStatus.evidence_coverage)} tone="info" />
        <Metric label="Validierungsabdeckung" value={scorePercent(masterStatus.validation_coverage)} tone="info" />
        <Metric label="Widersprüche" value={formatNumber(masterStatus.contradiction_count)} tone={masterStatus.contradiction_count ? 'danger' : 'good'} />
        <Metric label="Menschlich geprüft" value={formatNumber(masterStatus.human_reviewed_items)} tone={masterStatus.human_reviewed_items ? 'good' : 'warn'} />
        <Metric label="Offene Prüfungen" value={formatNumber(masterStatus.pending_reviews)} tone={masterStatus.pending_reviews ? 'warn' : 'good'} />
        <Metric label="Ø Vertrauen" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
      </div>

      <div className="trust-distribution-grid">
        {masterStatus.trust_distribution.map((item) => (
          <div className="trust-distribution-row" key={item.label}>
            <span>{trustLabel(item.label)}</span>
            <strong>{formatNumber(item.count)}</strong>
          </div>
        ))}
      </div>

      <div className="control-split-grid">
        <article className="control-mini-panel">
          <h3>Wichtigste Vertrauenslücken</h3>
          <div className="operator-warning-list">
            {masterStatus.top_blockers.slice(0, 8).map((blocker) => (
              <span key={blocker}>{blocker}</span>
            ))}
          </div>
        </article>
        <article className="control-mini-panel">
          <h3>Prüfprioritäten</h3>
          <div className="operator-warning-list">
            {masterStatus.top_review_priorities.slice(0, 8).map((priority) => (
              <span key={priority}>{priority}</span>
            ))}
          </div>
        </article>
      </div>
    </section>
  );
}

function DomainView({ operatorState }) {
  return (
    <section className="control-view-panel" aria-label="Domänen">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Mehrdomänen-Kern</p>
          <h2>Domänen</h2>
        </div>
        <StatusPill tone="info">{formatNumber(operatorState.domains.length)} aktiv</StatusPill>
      </div>

      <div className="domain-grid">
        {operatorState.domains.map((domain) => (
          <article className="domain-card" key={domain.domain}>
            <div className="review-card-head">
              <div>
                <span>{domain.domain}</span>
                <h3>{domain.title}</h3>
              </div>
              <StatusPill tone={domain.status.includes('need') ? 'warn' : 'info'}>
                {statusDeutsch(domain.status)}
              </StatusPill>
            </div>
            <Metric label="Wissenselemente" value={formatNumber(domain.knowledge_items)} />
            <Metric label="Letzte Prüfung" value={shortDateTime(domain.last_check_utc)} />
            <Metric label="Nächste Aufgabe" value={domain.next_recommended_task} tone="info" />
            <div className="operator-token-list">
              {domain.open_needs.slice(0, 6).map((need) => (
                <span key={need}>{need}</span>
              ))}
              {domain.open_needs.length === 0 ? <span>Keine offenen Needs gemeldet</span> : null}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function RoleView({ operatorState }) {
  return (
    <section className="control-view-panel" aria-label="Rollen">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Interne Rollen</p>
          <h2>Agenten- und Rollenansicht</h2>
        </div>
        <StatusPill tone="warn">keine neuen Agenten</StatusPill>
      </div>

      <div className="role-grid">
        {operatorState.roles.map((role) => (
          <article className="role-card" key={role.role}>
            <div className="review-card-head">
              <div>
                <span>Rolle</span>
                <h3>{role.role}</h3>
              </div>
              <StatusPill tone={role.warnings.length ? 'warn' : 'good'}>
                {role.warnings.length ? 'Warnung' : 'stabil'}
              </StatusPill>
            </div>
            <Metric label="Status" value={role.status} />
            <Metric label="Zuletzt" value={shortDateTime(role.last_work)} />
            <Metric label="Ergebnis" value={role.result} />
            {role.warnings.length ? (
              <div className="operator-warning-list">
                {role.warnings.map((warning) => (
                  <span key={warning}>{warning}</span>
                ))}
              </div>
            ) : null}
          </article>
        ))}
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
          <button className="cockpit-close-button" onClick={onClose} type="button">Schließen</button>
        </div>

        <div className="cockpit-detail-grid">
          {moduleId === 'open_supervisor' ? (
            <>
              <Metric label="Status" value={operatorState.supervisor.status} tone={module.tone} />
              <Metric label="Heartbeat" value={shortDateTime(operatorState.supervisor.heartbeat_utc)} />
              <Metric label="Laufzeit" value={`${formatNumber(operatorState.supervisor.uptime_minutes)} min`} />
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
              <Metric label="Nächster Start" value={shortDateTime(operatorState.nightly.next_scheduled_start_utc)} />
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
              <Metric label="Menschliche Prüfung" value="erforderlich" tone="warn" />
              <Metric label="Orderbuttons" value="nicht vorhanden" tone="danger" />
              <Metric label="Bridge" value="nur lesend" tone="good" />
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
            <span>Berichtsauszug</span>
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
  const [activeView, setActiveView] = useState('overview');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const refreshOperatorState = () => {
    setIsRefreshing(true);
    return loadOperatorDashboard()
      .then((nextState) => {
        setOperatorState(nextState);
        return nextState;
      })
      .finally(() => {
        setIsRefreshing(false);
      });
  };

  useEffect(() => {
    let mounted = true;
    let refreshTimer;

    const refresh = () => {
      refreshOperatorState().catch(() => {
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
  const fixtureActive = operatorState.dataSource !== DATA_SOURCE.LIVE_FILE;

  return (
    <section className="cockpit-shell" aria-label="Jarvis Cockpit Hauptansicht">
      <div className="cockpit-meta-strip">
        <StatusPill tone={sourceTone(operatorState.dataSource)}>
          {sourceModeLabel(operatorState.dataSource)}
        </StatusPill>
        <span>Zuletzt aktualisiert: {shortTime(operatorState.lastUpdatedAt)}</span>
        <span>Polling: {operatorState.pollIntervalSeconds || COCKPIT_REFRESH_SECONDS}s</span>
        <span>{formatNumber(operatorState.liveReportCount)} Live-Berichte / {formatNumber(operatorState.fixtureReportCount)} Ersatzdaten</span>
      </div>

      {fixtureActive ? (
        <p className="cockpit-warning">
          Nur lesende Bridge nicht vollständig verfügbar. Die Cockpit-Ansicht nutzt stabile Demo-/Snapshot-Daten.
        </p>
      ) : null}

      <ControlViewTabs activeView={activeView} onChange={setActiveView} />

      {activeView === 'overview' ? (
        <>
          <MasterStatusOverview
            masterStatus={operatorState.masterStatus}
            source={operatorState.masterStatusSource}
          />

          <div className="cockpit-accordion-stack">
            <DashboardAccordion
              badge={statusDeutsch(operatorState.masterStatus.overall_status)}
              summary={`Supervisor ${operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status)} · Scheduler ${formatNumber(operatorState.schedulerJobs.filter((job) => job.enabled).length)} aktiv`}
              title="Systemstatus"
              tone={toneFromStatus(operatorState.masterStatus.overall_status)}
            >
              <DashboardSystemStatus operatorState={operatorState} />
            </DashboardAccordion>

            <DashboardAccordion
              badge={compactStatusLabel(operatorState.masterStatus.ensemble_portfolio_status || operatorState.masterStatus.scalping_status || 'bereit')}
              summary="GER40 · XAUUSD · EURUSD"
              title="Handelsintelligenz"
              tone="info"
            >
              <DashboardTradingIntelligence operatorState={operatorState} />
            </DashboardAccordion>

            <DashboardAccordion
              badge={`${formatNumber(operatorState.humanReview?.pending_reviews || 0)} offen`}
              summary={`Freigegeben ${formatNumber(operatorState.humanReview?.approved_reviews || 0)} · Zurückgestellt ${formatNumber(operatorState.humanReview?.deferred_reviews || 0)}`}
              title="Prüfzentrum"
              tone={(operatorState.humanReview?.pending_reviews || 0) ? 'warn' : 'good'}
            >
              <DashboardReviewSummary operatorState={operatorState} />
              <ViewErrorBoundary>
                <HumanReviewCenter operatorState={operatorState} onRefresh={refreshOperatorState} />
              </ViewErrorBoundary>
            </DashboardAccordion>

            <DashboardAccordion
              badge={statusDeutsch(operatorState.masterStatus.knowledge_health)}
              summary={`Vertrauen ${scorePercent(operatorState.masterStatus.average_trust_score)} · OOS ${formatNumber(operatorState.masterStatus.knowledge_items_needing_oos)}`}
              title="Lernen & Wissen"
              tone={toneFromStatus(operatorState.masterStatus.knowledge_health)}
            >
              <DashboardLearningSummary operatorState={operatorState} />
              <ViewErrorBoundary>
                <KnowledgeTrustView operatorState={operatorState} />
              </ViewErrorBoundary>
            </DashboardAccordion>

            <DashboardAccordion
              badge={`${formatGb(operatorState.storage.free_disk_gb)} frei`}
              summary={`CPU ${Math.round(operatorState.resource.cpu_usage_percent)}% · RAM ${Math.round(operatorState.resource.memory_usage_percent)}%`}
              title="Speicher & Ressourcen"
              tone={operatorState.storage.errors.length ? 'warn' : 'good'}
            >
              <DashboardStorageResources operatorState={operatorState} />
            </DashboardAccordion>

            <DashboardAccordion
              badge={operatorState.masterStatus.no_auto_trading ? 'aus' : 'an'}
              summary={`Menschliche Prüfung ${operatorState.masterStatus.human_review_required ? 'erforderlich' : 'frei'} · Live ${operatorState.masterStatus.live_trading_enabled ? 'an' : 'aus'}`}
              title="Sicherheit"
              tone={operatorState.masterStatus.broker_orders_enabled || operatorState.masterStatus.live_trading_enabled ? 'danger' : 'good'}
            >
              <DashboardSafety operatorState={operatorState} />
            </DashboardAccordion>

            <DashboardAccordion
              badge={operatorState.logLines.length ? `${operatorState.logLines.length} Einträge` : 'leer'}
              summary={`${operatorState.warnings.length + operatorState.storage.warnings.length + operatorState.storage.errors.length} Warnungen`}
              title="Protokolle"
              tone={(operatorState.warnings.length + operatorState.storage.warnings.length + operatorState.storage.errors.length) ? 'warn' : 'info'}
            >
              <DashboardLogs operatorState={operatorState} />
            </DashboardAccordion>
          </div>
        </>
      ) : null}

      {activeView === 'review' ? (
        <ViewErrorBoundary>
          <HumanReviewCenter operatorState={operatorState} onRefresh={refreshOperatorState} />
        </ViewErrorBoundary>
      ) : null}
      {activeView === 'brain' ? (
        <ViewErrorBoundary>
          <CognitiveCenter operatorState={operatorState} />
        </ViewErrorBoundary>
      ) : null}
      {activeView === 'trust' ? (
        <ViewErrorBoundary>
          <KnowledgeTrustView operatorState={operatorState} />
        </ViewErrorBoundary>
      ) : null}
      {activeView === 'domains' ? (
        <ViewErrorBoundary>
          <DomainView operatorState={operatorState} />
        </ViewErrorBoundary>
      ) : null}
      {activeView === 'roles' ? (
        <ViewErrorBoundary>
          <RoleView operatorState={operatorState} />
        </ViewErrorBoundary>
      ) : null}

      <DetailOverlay
        moduleId={activeModule}
        modules={modules}
        operatorState={operatorState}
        onClose={() => setActiveModule('')}
      />
    </section>
  );
}
