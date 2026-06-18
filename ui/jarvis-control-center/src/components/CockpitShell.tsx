import { Component, useEffect, useMemo, useState } from 'react';
import {
  createOperatorDashboardFallback,
  DATA_SOURCE,
  loadOperatorDashboard,
} from '../data/runtimeDataAdapter';
import {
  compactOperatorExplanation,
  describeMustFrankAct,
  operatorTrafficLight,
  translateOperatorCode,
} from '../data/operatorTranslations';
import { sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
import { StatusPill, toneClass } from './StatusCard';

const COCKPIT_REFRESH_SECONDS = 45;
const DEFAULT_SYSTEM_B_BUNDLE_PATH = '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle';
const DEFAULT_ENSEMBLE_PACKAGE_PATH = '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/scalping_portfolio/ensemble_portfolio/ensemble_signal_agent_package.json';

function formatNumber(value) {
  return new Intl.NumberFormat('de-DE').format(Number(value || 0));
}

function formatGb(value) {
  return `${new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(Number(value || 0))} GB`;
}

function truncateText(value, maxLength = 28) {
  const text = String(value || '-').trim();
  if (text.length <= maxLength) {
    return text;
  }

  return `${text.slice(0, Math.max(1, maxLength - 1)).trimEnd()}…`;
}

function visiblePath(value, fallback = '-') {
  const text = String(value || '').trim();
  if (!text || text === '[redacted_path]') {
    return fallback;
  }

  return text;
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

function shortTimeOnly(value) {
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
  }).format(parsed);
}

function toneFromStatus(status) {
  const value = String(status || '').toLowerCase();

  if (value.includes('running') || value.includes('completed') || value.includes('continue') || value.includes('ready') || value === 'ok') {
    return 'good';
  }

  if (value.includes('stop') || value.includes('fail') || value.includes('critical')) {
    return 'danger';
  }

  if (value.includes('outside') || value.includes('pending') || value.includes('skip') || value.includes('needs') || value.includes('warning')) {
    return 'warn';
  }

  return 'info';
}

function reportByKey(operatorState, key) {
  return operatorState.reports.find((report) => report.key === key);
}

function portfolioEntries(operatorState) {
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  if (Array.isArray(portfolioReport.entries) && portfolioReport.entries.length) {
    return portfolioReport.entries;
  }

  if (Array.isArray(portfolioReport.assets) && portfolioReport.assets.length) {
    return portfolioReport.assets;
  }

  return [
    { asset: 'GER40', readiness: 'bot_ready', primary_setup: 'ger40_range_breakout_m5', primary_candidate: 'scalp_ger40_160c06ea86' },
    { asset: 'XAUUSD', readiness: 'bot_ready', primary_setup: 'xauusd_micro_trend_continuation_m5', primary_candidate: 'scalp_xauusd_5564a8e2b6' },
    { asset: 'EURUSD', readiness: 'needs_more_validation', primary_setup: '-', primary_candidate: '-' },
  ];
}

function setupRegistryEntries(operatorState) {
  const setupRegistry = reportByKey(operatorState, 'setupRegistry')?.raw || {};
  return Array.isArray(setupRegistry.assets) ? setupRegistry.assets : [];
}

function botSpecActions(operatorState) {
  const setups = setupRegistryEntries(operatorState);
  return portfolioEntries(operatorState)
    .filter((entry) => String(entry.readiness || entry.portfolio_readiness || '').toLowerCase().includes('bot_ready'))
    .map((entry) => {
      const setup = setups.find((item) =>
        String(item.asset || '').toUpperCase() === String(entry.asset || '').toUpperCase()
        && String(item.setup_id || '') === String(entry.primary_setup || item.setup_id || ''),
      );
      return {
        asset: entry.asset,
        setup_id: entry.primary_setup || setup?.setup_id || '-',
        candidate_id: entry.primary_candidate || setup?.primary_candidate || '-',
        timeframe: setup?.primary_timeframe || entry.timeframe || '-',
        readiness: entry.readiness || setup?.readiness_status || '-',
      };
    })
    .filter((action) => action.candidate_id && action.candidate_id !== '-');
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

function OperatorSummary({ code, fallback = null }) {
  const item = operatorStatusOverview(code, fallback);
  const trafficLight = operatorTrafficLight(item.severity);

  return (
    <div className="operator-summary-card">
      <div className="operator-summary-head">
        <div>
          <span>{item.title}</span>
          <strong>{trafficLight.label}</strong>
        </div>
        <StatusPill tone={trafficLight.tone}>{trafficLight.symbol}</StatusPill>
      </div>
      <p>{item.meaning}</p>
      <p><strong>Hermes arbeitet an:</strong> {item.whatHermesDoes}</p>
      <p><strong>Aktion für Frank:</strong> {item.franksAction}</p>
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

function operatorStatusOverview(value) {
  const translation = translateOperatorCode(value);
  return {
    title: translation.title,
    meaning: translation.meaning,
    action: translation.action,
    severity: translation.severity,
    whatHermesDoes: translation.whatHermesDoes,
    franksAction: translation.franksAction,
  };
}

function topicDeutsch(value) {
  const text = String(value || '').trim();
  if (!text) {
    return '';
  }

  const lowered = text.toLowerCase();
  if (lowered.includes('eurusd')) return 'EURUSD validieren';
  if (lowered.includes('xauusd')) return 'XAUUSD prüfen';
  if (lowered.includes('ger40') || lowered.includes('de40') || lowered.includes('dax')) return 'GER40 prüfen';
  if (lowered.includes('validation') || lowered.includes('validierung') || lowered.includes('knowledge')) return 'Wissensvalidierung vorbereiten';
  if (lowered.includes('cleanup') || lowered.includes('storage') || lowered.includes('speicher')) return 'Speicheranalyse';
  if (lowered.includes('signal')) return 'Signalpaket prüfen';
  if (lowered.includes('review')) return 'Prüfzentrum bearbeiten';
  if (lowered.includes('nightly')) return 'Nightly kontrollieren';

  return text
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .replace(/\bvalidate\b/gi, 'validieren')
    .replace(/\bvalidation\b/gi, 'Validierung');
}

function uniqueList(values) {
  return [...new Set(values.filter(Boolean).map((value) => String(value).trim()).filter(Boolean))];
}

function reviewTopicLabel(item) {
  const raw = String(
    item.title
    || item.topic
    || item.subject
    || item.name
    || item.reason
    || item.recommendation
    || '',
  ).trim();

  if (!raw) {
    return 'Offene Prüfung';
  }

  const translated = translateOperatorCode(raw, null);
  if (translated && translated.title && translated.title !== raw && translated.title !== 'Technische Information') {
    return translated.title;
  }

  return topicDeutsch(reviewReasonDeutsch(raw));
}

function frankActionCenterModel(operatorState) {
  const review = operatorState.humanReview || {};
  const items = Array.isArray(review.items) ? review.items : [];
  const pendingItems = items.filter((item) => item.status === 'pending');
  const openReviews = Number(review.pending_reviews || 0);
  const needsMoreEvidence = Number(review.needs_more_evidence_reviews || 0);
  const masterStatus = operatorState.masterStatus || {};
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  const trustedGate = reportByKey(operatorState, 'trustedKnowledgeReviewGate')?.raw || {};
  const trustPlan = reportByKey(operatorState, 'knowledgeTrustImprovementPlan')?.raw || {};
  const trustedEligible = Number(trustedGate.eligible_for_trusted_review || trustedGate.eligibleForTrustedReview || 0);
  const trustBlocked = Number(trustPlan.total_blocked_items || trustPlan.totalBlockedItems || 0);
  const trustMainReasons = Object.entries(trustPlan.blocker_counts || trustPlan.blockerCounts || {})
    .sort((a, b) => Number(b[1]) - Number(a[1]))
    .slice(0, 4)
    .map(([key, value]) => `${formatNumber(value)} ${translateOperatorCode(key).title.toLowerCase()}`);
  const warningCandidates = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
    ...(masterStatus.top_blockers || []),
    ...(masterStatus.next_recommended_actions || []),
  ].filter(Boolean);
  const translatedWarnings = warningCandidates
    .map(classifyOperatorWarning)
    .filter(Boolean);

  const decisionRows = pendingItems
    .slice(0, 3)
    .map((item) => ({
      topic: reviewTopicLabel(item),
      recommendation: reviewRecommendationDeutsch(item.recommendation),
      risk: reviewRisk(item).label,
      tone: reviewTrafficLight(item).tone,
    }));

  if (openReviews > 0) {
    return {
      mode: 'red',
      title: 'Entscheidungen erforderlich',
      summary: `${formatNumber(openReviews)} offene Reviews`,
      headline: 'Frank muss jetzt entscheiden.',
      action: 'Ja, im Prüfzentrum',
      buttonVisible: true,
      buttonLabel: 'Prüfzentrum öffnen',
      buttonTarget: 'review',
      items: decisionRows.length ? decisionRows : [
        {
          topic: 'Offene Prüfungen',
          recommendation: 'Menschliche Prüfung erforderlich.',
          risk: 'mittel',
          tone: 'warn',
        },
      ],
      details: ['Hermes wartet auf eine Freigabe im Prüfzentrum.'],
    };
  }

  if (needsMoreEvidence > 0) {
    return {
      mode: 'yellow',
      title: 'Hermes sammelt weitere Evidenz',
      summary: `${formatNumber(needsMoreEvidence)} Prüfungen benötigen mehr Evidenz`,
      headline: 'Frank muss nichts tun.',
      action: 'Nein',
      buttonVisible: false,
      buttonLabel: '',
      buttonTarget: '',
      items: pendingItems.slice(0, 3).map((item) => ({
        topic: reviewTopicLabel(item),
        recommendation: 'Hermes sammelt weitere Evidenz.',
        risk: 'mittel',
        tone: 'warn',
      })),
      details: ['Hermes sammelt weitere Evidenz und bereitet weitere Validierungsschritte vor.'],
    };
  }

  if (trustedEligible > 0) {
    return {
      mode: 'red',
      title: `${formatNumber(trustedEligible)} Wissenselemente bereit für Trusted-Freigabe`,
      summary: 'Trusted bleibt eine menschliche Entscheidung.',
      headline: 'Frank muss diese Freigaben im Prüfzentrum prüfen.',
      action: 'Ja, im Prüfzentrum',
      buttonVisible: true,
      buttonLabel: 'Prüfzentrum öffnen',
      buttonTarget: 'review',
      items: (trustedGate.top_candidates || trustedGate.topCandidates || []).slice(0, 3).map((candidate) => ({
        topic: reviewTopicLabel(candidate),
        recommendation: 'Im Prüfzentrum prüfen.',
        risk: candidate.blocking_reasons?.length ? 'mittel' : 'niedrig',
        tone: candidate.blocking_reasons?.length ? 'warn' : 'good',
      })),
      details: ['Keine automatische Trusted-Promotion.'],
    };
  }

  if (trustBlocked > 0) {
    return {
      mode: 'yellow',
      title: `Hermes arbeitet an ${formatNumber(trustBlocked)} Vertrauensverbesserungen`,
      summary: trustMainReasons[0] || 'Hermes verbessert Vertrauen, Evidenz und Validierung.',
      headline: 'Frank muss nichts tun.',
      action: 'Nein',
      buttonVisible: false,
      buttonLabel: '',
      buttonTarget: '',
      items: trustMainReasons.slice(0, 3).map((reason) => ({
        topic: reason,
        recommendation: 'Hermes arbeitet daran.',
        risk: 'niedrig',
        tone: 'warn',
      })),
      details: ['Keine Aktion für Frank. Hermes plant Evidenz-, Re-Validierungs- und Quellenarbeit selbstständig.'],
    };
  }

  const yellowReasons = uniqueList([
    ...translatedWarnings
      .filter((warning) => warning.tone === 'warn')
      .map((warning) => warning.label),
    masterStatus.knowledge_items_needing_oos ? translateOperatorCode('oos_data_missing').title : '',
    masterStatus.validation_plans_open || masterStatus.validation_tasks_pending ? translateOperatorCode('validation_queue_missing').title : '',
    operatorState.storage.cleanup_candidate_count ? translateOperatorCode('storage_cleanup_candidates').title : '',
  ]);

  if (yellowReasons.length) {
    return {
      mode: 'yellow',
      title: 'Hermes arbeitet daran',
      summary: yellowReasons[0],
      headline: 'Hermes bearbeitet die offenen Themen selbstständig.',
      action: yellowReasons.some((reason) => /Speicher/i.test(reason)) ? 'Ja, Speicher prüfen' : 'Ja, Konfiguration prüfen',
      buttonVisible: false,
      buttonLabel: '',
      buttonTarget: '',
      items: yellowReasons.slice(0, 3).map((reason) => ({
        topic: reason,
        recommendation: 'Hermes arbeitet daran.',
        risk: 'mittel',
        tone: 'warn',
      })),
      details: yellowReasons.length > 1 ? yellowReasons.slice(1, 3) : [],
    };
  }

  const ongoingTopics = uniqueList([
    masterStatus.next_recommended_actions?.[0],
    portfolioReport.eurusd_readiness && String(portfolioReport.eurusd_readiness).includes('validation') ? 'EURUSD validieren' : '',
    masterStatus.validation_plans_open || masterStatus.knowledge_items_needing_oos ? 'Wissensvalidierung vorbereiten' : '',
    operatorState.storage.cleanup_candidate_count ? 'Speicheranalyse' : '',
    portfolioReport.signal_agent_specs_ready ? 'Signalpaket prüfen' : '',
    trustedEligible > 0 ? 'Trusted-Freigabe prüfen' : '',
    trustBlocked > 0 ? 'Vertrauensverbesserungen planen' : '',
  ]);

  return {
    mode: 'green',
    title: 'Keine Aktion erforderlich',
    summary: 'Hermes arbeitet selbstständig weiter.',
    headline: 'Hermes arbeitet selbstständig weiter.',
    action: 'Nein',
    buttonVisible: false,
    buttonLabel: '',
    buttonTarget: '',
      items: ongoingTopics.slice(0, 3).length
      ? ongoingTopics.slice(0, 3).map((topic) => ({
          topic: topicDeutsch(topic),
          recommendation: 'Keine Aktion erforderlich.',
          risk: 'niedrig',
          tone: 'good',
        }))
      : [
          { topic: 'EURUSD validieren', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
          { topic: 'Wissensvalidierung vorbereiten', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
          { topic: 'Speicheranalyse', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
        ],
      details: trustedEligible > 0
        ? ['Trusted-Kandidaten liegen bereit und warten auf die manuelle Freigabe im Prüfzentrum.']
        : trustBlocked > 0
          ? ['Hermes arbeitet an Vertrauensverbesserungen. Keine Aktion erforderlich.']
          : ['Noch keine Trusted-Kandidaten. Hermes arbeitet weiter an Validierung.'],
  };
}

function improvementQueueSummaryModel(operatorState) {
  const policy = reportByKey(operatorState, 'workAreaExecutorPolicy')?.raw || {};
  const nightly = reportByKey(operatorState, 'nightlyWorkAreaStatus')?.raw || {};
  const workAreas = policy.work_areas || policy.workAreas || reportByKey(operatorState, 'autonomousImprovementWorkAreas')?.raw?.work_areas || [];
  const revalidation = nightly.revalidation || nightly.Revalidation || {};
  const revalidationStatus = String(revalidation.status || revalidation.Status || 'bereit').toLowerCase();
  const nextExecutionWindow = revalidation.next_execution_window || revalidation.NextExecutionWindow || 'jetzt';
  const nextExecutionAtUtc = revalidation.next_execution_at_utc || revalidation.NextExecutionAtUtc || null;
  const lastExecutionAtUtc = revalidation.executed_at_utc || revalidation.ExecutedAtUtc || null;
  const revalidationHeadline =
    revalidationStatus === 'ausgeführt'
      ? `Zuletzt ausgeführt um ${shortTimeOnly(lastExecutionAtUtc)}`
      : revalidationStatus.includes('fehler')
        ? `Fehlgeschlagen${revalidation.result ? `: ${revalidation.result}` : ''}`
        : nextExecutionAtUtc
          ? `wartet auf Nightly bis ${shortTimeOnly(nextExecutionAtUtc)}`
          : (String(nextExecutionWindow).toLowerCase().includes('wartet')
            || String(nextExecutionWindow).toLowerCase().includes('nightly'))
            ? 'wartet auf Nightly'
            : `wartet auf Nightly bis ${nextExecutionWindow}`;

  return {
    title: 'Selbstverbesserung',
    summary: `Hermes arbeitet an ${formatNumber(policy.active_areas || workAreas.length || 0)} Verbesserungsbereichen`,
    headline: 'Hermes arbeitet selbstständig an Wissensvalidierung und OOS-Planung. Keine Aktion erforderlich.',
    action: 'Nein',
    detail: workAreas.length
      ? workAreas.map((area) => `${area.area_title || area.areaTitle}: ${formatNumber(area.item_count || area.itemCount)} · ${area.status || 'bereit'}`).join(' · ')
      : 'Hermes arbeitet selbstständig weiter.',
    meta: `Frank nötig: ${formatNumber(policy.frank_items || 0)}`,
    tone: workAreas.length ? 'warn' : 'good',
    items: workAreas.length
        ? workAreas.map((area) => ({
          topic: area.area_title || area.areaTitle || 'Verbesserung',
          recommendation: `Status: ${area.status || 'bereit'} · Automatisch erlaubt: ${area.automatically_allowed ?? area.automaticallyAllowed ? 'ja' : 'nein'} · Nächstes Fenster: ${area.next_execution_window || area.nextExecutionWindow || 'jetzt'} · Frank nötig: ${area.frank_required || area.frankRequired ? 'ja' : 'nein'}`,
          risk: area.status || 'bereit',
          tone: area.frank_required || area.frankRequired ? 'action' : area.status === 'wartet auf Nightly' ? 'warn' : area.highest_priority === 'high' ? 'danger' : 'good',
        }))
      : [
          { topic: 'Evidenz sammeln', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
          { topic: 'Validierung planen', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
          { topic: 'Systempflege', recommendation: 'Keine Aktion erforderlich.', risk: 'niedrig', tone: 'good' },
        ],
    details: [
      `Frank nötig: ${policy.frank_items || 0}`,
      `Im Arbeitsfenster: ${policy.in_work_window ? 'ja' : 'nein'}`,
      `Im Nightly: ${policy.in_nightly_window ? 'ja' : 'nein'}`,
      `Re-Validierung: ${revalidationHeadline}${nextExecutionAtUtc ? ` (${shortTimeOnly(nextExecutionAtUtc)} UTC)` : ''}`,
    ],
  };
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

function reviewPriorityRank(priority) {
  const lowered = String(priority || '').toLowerCase();

  if (lowered === 'hoch' || lowered === 'high') {
    return 3;
  }

  if (lowered === 'mittel' || lowered === 'medium') {
    return 2;
  }

  return 1;
}

function reviewDomainRank(domain) {
  const lowered = String(domain || '').toLowerCase();
  if (lowered === 'trading') return 0;
  if (lowered === 'research') return 1;
  if (lowered === 'software') return 2;
  if (lowered === 'process') return 3;
  if (lowered === 'documentation') return 4;
  return 5;
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

function evidenceMetric(summary, key) {
  const match = String(summary || '').match(new RegExp(`${key}=([0-9.]+)`, 'i'));
  return match ? Number(match[1]) : 0;
}

function reviewEvidenceQuality(item) {
  const quality = evidenceMetric(item.evidence_summary, 'quality');
  const evidence = evidenceMetric(item.evidence_summary, 'evidence');
  const validation = evidenceMetric(item.evidence_summary, 'validation');
  const values = [quality, evidence, validation].filter((value) => Number.isFinite(value) && value > 0);
  if (!values.length) {
    return 0;
  }

  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function reviewRisk(item) {
  const trust = Number(item.trust_before || 0);
  const evidenceQuality = reviewEvidenceQuality(item);
  const validation = evidenceMetric(item.evidence_summary, 'validation');

  if (trust < 0.45 || evidenceQuality < 0.45 || validation < 0.45) {
    return { label: 'hoch', tone: 'danger' };
  }

  if (trust < 0.65 || evidenceQuality < 0.62 || validation < 0.55) {
    return { label: 'mittel', tone: 'warn' };
  }

  return { label: 'niedrig', tone: 'good' };
}

function reviewTrafficLight(item) {
  const recommendation = String(item.recommendation || '').toLowerCase();
  const trust = Number(item.trust_before || 0);
  const evidenceQuality = reviewEvidenceQuality(item);
  const risk = reviewRisk(item);

  if (recommendation.includes('reject') || risk.tone === 'danger') {
    return {
      label: 'Ablehnung empfohlen',
      tone: 'danger',
      className: 'is-red',
    };
  }

  if (recommendation.includes('more_evidence') || recommendation.includes('quality_gate') || trust < 0.68 || evidenceQuality < 0.66) {
    return {
      label: 'Mehr Evidenz empfohlen',
      tone: 'warn',
      className: 'is-yellow',
    };
  }

  return {
    label: 'Freigabe empfohlen',
    tone: 'good',
    className: 'is-green',
  };
}

function reviewDecisionAssistant(item) {
  const trust = Number(item.trust_before || 0);
  const evidenceQuality = reviewEvidenceQuality(item);
  const validationScore = evidenceMetric(item.evidence_summary, 'validation');
  const risk = reviewRisk(item);
  const criticalWarning = /contradict|widerspruch/i.test(`${item.reason || ''} ${item.evidence_summary || ''}`);
  let recommendationKey = 'more_evidence';

  if (criticalWarning || trust < 0.45 || evidenceQuality < 0.45 || validationScore < 0.45) {
    recommendationKey = 'reject';
  } else if (trust >= 0.70 && evidenceQuality >= 0.65 && validationScore >= 0.65) {
    recommendationKey = 'approve';
  } else if (trust >= 0.45 && trust < 0.70) {
    recommendationKey = 'more_evidence';
  } else if (validationScore < 0.65 || evidenceQuality < 0.70) {
    recommendationKey = 'more_evidence';
  }

  const recommendation = recommendationKey === 'approve'
    ? 'Freigabe empfohlen'
    : recommendationKey === 'reject'
      ? 'Ablehnung empfohlen'
      : 'Mehr Evidenz empfohlen';

  const trustText = trust >= 0.70 ? 'Vertrauen ausreichend' : trust >= 0.45 ? 'Vertrauen mittel' : 'Vertrauen zu niedrig';
  const evidenceText = evidenceQuality >= 0.65 ? 'Evidenzqualität ausreichend' : evidenceQuality >= 0.45 ? 'Evidenzqualität mittel' : 'Evidenzqualität zu schwach';
  const validationText = validationScore >= 0.65 ? 'Validierung vollständig genug' : 'Validierung noch nicht stark genug';
  const riskText = risk.label === 'niedrig' ? 'Trading-Risiko niedrig' : risk.label === 'mittel' ? 'Trading-Risiko mittel' : 'Trading-Risiko hoch';
  const tone = recommendationKey === 'approve' ? 'good' : recommendationKey === 'reject' ? 'danger' : 'warn';
  const frankAction = recommendationKey === 'approve'
    ? 'Freigabe im Prüfzentrum prüfen'
    : recommendationKey === 'reject'
      ? 'Ablehnung im Prüfzentrum prüfen'
      : 'Mehr Evidenz im Prüfzentrum prüfen';
  const reviewActionScore = Number(item.review_action_score || item.reviewActionScore || 0);
  const reviewActionBand = String(item.review_action_band || item.reviewActionBand || (reviewActionScore >= 75 ? 'A' : reviewActionScore >= 45 ? 'B' : 'C'));
  const recommendationClass = String(item.recommendation_class || item.recommendationClass || (
    trust >= 0.65 && evidenceQuality >= 0.60 && validationScore >= 0.60 && !criticalWarning
      ? 'Fast bereit'
      : criticalWarning || trust < 0.50 || validationScore < 0.45
        ? 'Schwach'
        : 'Unsicher'
  ));
  const missingEvidence = Array.isArray(item.missing_evidence || item.missingEvidence)
    ? (item.missing_evidence || item.missingEvidence).map(String)
    : [
        /oos/i.test(item.evidence_summary || '') ? '☑ OOS Validation' : '☐ OOS Validation',
        /forward/i.test(item.evidence_summary || '') ? '☑ Forward Observation' : '☐ Forward Observation',
        criticalWarning ? '☑ Contradiction Check' : '☐ Contradiction Check',
      ];
  const whyNow = String(item.why_now || item.whyNow || `${recommendationClass}: Trust ${trust.toFixed(2)}, Evidence ${evidenceQuality.toFixed(2)}, Validation ${validationScore.toFixed(2)}.`);
  const nextStep = String(item.next_step || item.nextStep || (recommendationKey === 'approve' ? 'Freigabe prüfen' : recommendationKey === 'reject' ? 'Review ablehnen' : 'Forward Validation'));

  return {
    recommendationKey,
    recommendation,
    tone,
    reason: `${trustText}. ${evidenceText}. ${validationText}. ${riskText}.`,
    frankAction,
    reviewActionScore,
    reviewActionBand,
    recommendationClass,
    missingEvidence,
    whyNow,
    nextStep,
    trustText,
    evidenceText,
    validationText,
    riskText,
  };
}

function reviewActionPriority(entry) {
  const band = String(entry.reviewActionBand || '').toUpperCase();
  if (band === 'A') return 3;
  if (band === 'B') return 2;
  if (band === 'C') return 1;
  return Number(entry.reviewActionScore || 0) / 100;
}

function reviewClearReason(item) {
  const assistant = reviewDecisionAssistant(item);
  return `${reviewReasonDeutsch(item.reason)} ${assistant.reason}`;
}

async function assertReviewEndpointAvailable(endpoint) {
  try {
    const response = await fetch(`${__HERMES_READONLY_BRIDGE_URL__}/bridge/health`, {
      cache: 'no-store',
    });
    const payload = await response.json().catch(() => ({}));
    const bridgeVersion = payload?.data?.bridge_version || payload?.bridge_version || 'unbekannte Bridge-Version';
    const endpoints = payload?.data?.endpoints || payload?.endpoints || [];
    const expected = `/bridge/review/${endpoint}`;

    if (Array.isArray(endpoints) && endpoints.length && !endpoints.includes(expected)) {
      throw new Error(`Laufende Bridge ${bridgeVersion} unterstützt ${expected} nicht. Bridge neu starten.`);
    }
  } catch (error) {
    if (error instanceof Error && error.message.includes('unterstützt')) {
      throw error;
    }
  }
}

function postErrorMessage(payload, responseText, response) {
  const warning = Array.isArray(payload?.warnings) ? payload.warnings[0] : '';
  return warning
    || payload?.error
    || payload?.message
    || responseText
    || `${response.status} ${response.statusText}`.trim();
}

function cleanOperatorEventText(value) {
  const text = String(value || '').trim();
  if (!text) {
    return '';
  }

  if (text.includes('{') || text.includes('}') || text.includes('launcher invoked')) {
    return '';
  }

  if (text.includes('/home/') || text.includes('/mnt/') || text.includes('\\')) {
    return '';
  }

  if (/exception|stack trace| at \w+\./i.test(text)) {
    return '';
  }

  return truncateText(text, 92);
}

function warningFingerprint(text) {
  const normalized = String(text || '')
    .toLowerCase()
    .replace(/\[[^\]]+\]/g, '')
    .replace(/\/[^\s]+/g, '[path]')
    .replace(/[0-9a-f]{8,}/g, '[id]')
    .replace(/\d{4}-\d{2}-\d{2}[^\s]*/g, '[date]')
    .replace(/\s+/g, ' ')
    .trim();

  if (/runtime report|runtime.*fehlt|runtime.*missing/i.test(normalized)) {
    return 'missing-runtime-report';
  }

  if (/setup watch/i.test(normalized)) {
    return 'missing-setup-watch-report';
  }

  return normalized;
}

function warningToneInfo(tone) {
  if (tone === 'danger') {
    return { icon: '🔴', label: 'Kritisch' };
  }

  if (tone === 'warn') {
    return { icon: '🟡', label: 'Warnung' };
  }

  return { icon: '🔵', label: 'Hinweis' };
}

function classifyOperatorWarning(rawWarning) {
  const text = cleanOperatorEventText(rawWarning);
  if (!text) {
    return null;
  }

  const lower = text.toLowerCase();
  if (lower.includes('outside_nightly_window')) {
    return {
      key: 'outside_nightly_window',
      label: translateOperatorCode('outside_nightly_window').title,
      detail: translateOperatorCode('outside_nightly_window').meaning,
      tone: 'info',
      action: translateOperatorCode('outside_nightly_window').action,
    };
  }

  if (lower.includes('safe_stop_requested')) {
    return {
      key: 'safe_stop_requested',
      label: translateOperatorCode('safe_stop_requested').title,
      detail: translateOperatorCode('safe_stop_requested').meaning,
      tone: 'info',
      action: translateOperatorCode('safe_stop_requested').action,
    };
  }

  if (lower.includes('storage_cleanup_candidates')) {
    return {
      key: 'storage_cleanup_candidates',
      label: translateOperatorCode('storage_cleanup_candidates').title,
      detail: translateOperatorCode('storage_cleanup_candidates').meaning,
      tone: 'info',
      action: translateOperatorCode('storage_cleanup_candidates').action,
    };
  }

  if (lower.includes('oos_data_missing')) {
    return {
      key: 'oos_data_missing',
      label: translateOperatorCode('oos_data_missing').title,
      detail: translateOperatorCode('oos_data_missing').meaning,
      tone: 'warn',
      action: translateOperatorCode('oos_data_missing').action,
    };
  }

  if (lower.includes('knowledge_validation_queue_missing')) {
    return {
      key: 'knowledge_validation_queue_missing',
      label: translateOperatorCode('knowledge_validation_queue_missing').title,
      detail: translateOperatorCode('knowledge_validation_queue_missing').meaning,
      tone: 'warn',
      action: translateOperatorCode('knowledge_validation_queue_missing').action,
    };
  }

  if (lower.includes('knowledge_items_need_oos_validation')) {
    return {
      key: 'knowledge_items_need_oos_validation',
      label: translateOperatorCode('knowledge_items_need_oos_validation').title,
      detail: translateOperatorCode('knowledge_items_need_oos_validation').meaning,
      tone: 'warn',
      action: translateOperatorCode('knowledge_items_need_oos_validation').action,
    };
  }

  if (lower.includes('validation_queue_missing')) {
    return {
      key: 'validation_queue_missing',
      label: translateOperatorCode('validation_queue_missing').title,
      detail: translateOperatorCode('validation_queue_missing').meaning,
      tone: 'warn',
      action: translateOperatorCode('validation_queue_missing').action,
    };
  }

  if (lower.includes('human_review_required')) {
    return {
      key: 'human_review_required',
      label: translateOperatorCode('human_review_required').title,
      detail: translateOperatorCode('human_review_required').meaning,
      tone: 'warn',
      action: translateOperatorCode('human_review_required').action,
    };
  }

  if (lower.includes('review_required')) {
    return {
      key: 'review_required',
      label: translateOperatorCode('review_required').title,
      detail: translateOperatorCode('review_required').meaning,
      tone: 'warn',
      action: translateOperatorCode('review_required').action,
    };
  }

  if (lower.includes('evidence_requested') || lower.includes('more evidence')) {
    return {
      key: 'evidence_requested',
      label: translateOperatorCode('evidence_requested').title,
      detail: translateOperatorCode('evidence_requested').meaning,
      tone: 'warn',
      action: translateOperatorCode('evidence_requested').action,
    };
  }

  if (lower.includes('deferred_reviews') || lower.includes('deferred review') || lower.includes('zurückgestellt')) {
    return {
      key: 'deferred_reviews',
      label: translateOperatorCode('deferred_reviews').title,
      detail: translateOperatorCode('deferred_reviews').meaning,
      tone: 'info',
      action: translateOperatorCode('deferred_reviews').action,
    };
  }

  if (lower.includes('hypotheses_without_validation_queue')) {
    return {
      key: 'hypotheses_without_validation_queue',
      label: translateOperatorCode('hypotheses_without_validation_queue').title,
      detail: translateOperatorCode('hypotheses_without_validation_queue').meaning,
      tone: 'warn',
      action: translateOperatorCode('hypotheses_without_validation_queue').action,
    };
  }

  if (lower.includes('no_robust_strategies')) {
    return {
      key: 'no_robust_strategies',
      label: translateOperatorCode('no_robust_strategies').title,
      detail: translateOperatorCode('no_robust_strategies').meaning,
      tone: 'warn',
      action: translateOperatorCode('no_robust_strategies').action,
    };
  }

  if (/no_auto_trading=false|broker_orders_enabled=true|live_trading_enabled=true|safety.*false|human_review_required=false/.test(lower)) {
    return {
      key: warningFingerprint(text),
      label: 'Safety-Verstoß',
      detail: 'Eine Sicherheitsregel ist verletzt.',
      tone: 'danger',
      action: 'Sofort stoppen und Safety-Status prüfen.',
    };
  }

  if (/bridge.*offline|bridge.*nicht erreichbar|read-only bridge nicht erreichbar|failed to fetch|networkerror/.test(lower)) {
    return {
      key: 'bridge-offline',
      label: 'Bridge offline',
      detail: 'Die UI kann die Hermes Bridge nicht erreichen.',
      tone: 'danger',
      action: 'Read-only Bridge neu starten und Statusleiste prüfen.',
    };
  }

  if (/review|human review|signal|trading|ensemble|package|certification|bot_ready|needs_more_validation/.test(lower)) {
    return {
      key: warningFingerprint(text),
      label: 'Trading/Signal/Review prüfen',
      detail: text,
      tone: 'warn',
      action: 'Prüfzentrum oder Handelsintelligenz öffnen.',
    };
  }

  if (/setup watch/i.test(text)) {
    return {
      key: 'missing-setup-watch-report',
      label: 'fehlender Setup Watch Report',
      detail: 'Setup Watch Snapshot fehlt oder ist veraltet.',
      tone: 'info',
      action: 'Bei Bedarf Setup Watch Status aktualisieren.',
    };
  }

  if (/missing|fehlt|nicht gefunden|unavailable|nicht verfuegbar|nicht verfügbar|snapshot|report/i.test(text)) {
    return {
      key: warningFingerprint(text),
      label: /runtime/i.test(text) ? 'fehlender Runtime Report' : 'optionaler Report fehlt',
      detail: /runtime/i.test(text)
        ? 'Ein Runtime Report fehlt oder wurde noch nicht erzeugt.'
        : 'Ein optionaler Report fehlt oder ein Snapshot ist alt.',
      tone: 'info',
      action: 'Nur prüfen, wenn das zugehörige Panel Daten benötigt.',
    };
  }

  return {
    key: warningFingerprint(text),
    label: 'Hinweis',
    detail: text,
    tone: 'info',
    action: 'Keine Sofortaktion nötig.',
  };
}

function consolidateOperatorWarnings(rawWarnings) {
  const byKey = new Map();
  rawWarnings
    .map(classifyOperatorWarning)
    .filter(Boolean)
    .forEach((warning) => {
      const current = byKey.get(warning.key);
      if (!current) {
        byKey.set(warning.key, { ...warning, count: 1 });
        return;
      }

      byKey.set(warning.key, {
        ...current,
        count: current.count + 1,
        tone: current.tone === 'danger' || warning.tone === 'danger'
          ? 'danger'
          : current.tone === 'warn' || warning.tone === 'warn'
            ? 'warn'
            : 'info',
      });
    });

  return [...byKey.values()].sort((left, right) => {
    const rank = { danger: 0, warn: 1, info: 2, good: 3 };
    return (rank[left.tone] ?? 9) - (rank[right.tone] ?? 9);
  });
}

function operatorLogView(operatorState) {
  const systemEvents = [
    {
      label: 'Supervisor gestartet',
      detail: operatorState.supervisor.running
        ? 'Hermes arbeitet im aktiven Betriebsmodus.'
        : translateOperatorCode(operatorState.supervisor.status).meaning,
      tone: operatorState.supervisor.running ? 'good' : operatorTrafficLight(operatorState.supervisor.status).tone,
    },
    {
      label: 'Nightly abgeschlossen',
      detail: translateOperatorCode(operatorState.nightly.current_state).meaning,
      tone: operatorTrafficLight(operatorState.nightly.current_state).tone,
    },
    {
      label: 'Zertifizierung abgeschlossen',
      detail: `${formatNumber(operatorState.masterStatus.scalping_final_candidates || operatorState.masterStatus.ctrader_bot_specs_ready || 0)} Kandidaten/Specs`,
      tone: (operatorState.masterStatus.scalping_final_candidates || operatorState.masterStatus.ctrader_bot_specs_ready) ? 'good' : 'info',
    },
    {
      label: 'Export erstellt',
      detail: `${formatNumber(operatorState.masterStatus.signal_agent_specs_ready || 0)} Signal-Spezifikationen`,
      tone: operatorState.masterStatus.signal_agent_specs_ready ? 'good' : 'warn',
    },
    {
      label: 'Review offen',
      detail: `${formatNumber(operatorState.humanReview?.pending_reviews || 0)} offene Prüfungen`,
      tone: operatorState.humanReview?.pending_reviews ? 'warn' : 'good',
    },
  ];
  const reportWarnings = consolidateOperatorWarnings([
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ]);

  return {
    systemEvents,
    warnings: reportWarnings.length ? reportWarnings : [{ label: 'Keine relevanten Hinweise', detail: 'Hermes kann die vorhandenen Berichte lesen.', tone: 'good', action: 'Keine Aktion nötig.', count: 1 }],
  };
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

function KnowledgeHealthCard({ operatorState }) {
  const masterStatus = operatorState.masterStatus;
  const audit = reportByKey(operatorState, 'knowledgeValidationAudit')?.raw || {};
  const rootCause = reportByKey(operatorState, 'knowledgeHealthRootCause')?.raw || {};
  const openValidations = audit.open_validations ?? audit.openValidations ?? masterStatus.validation_plans_open;
  const criticalGaps = audit.critical_knowledge_gaps ?? audit.criticalKnowledgeGaps ?? masterStatus.knowledge_items_needing_oos;
  const createdLastRun = audit.validation_tasks_created_last_run ?? audit.validationTasksCreatedLastRun ?? 0;
  const executedLastRun = audit.evidence_tasks_executed_last_run ?? audit.evidenceTasksExecutedLastRun ?? 0;
  const needsMoreEvidenceBefore = audit.needs_more_evidence_before ?? audit.needsMoreEvidenceBefore ?? Number(operatorState.humanReview?.needs_more_evidence_reviews || 0);
  const needsMoreEvidenceAfter = audit.needs_more_evidence_after ?? audit.needsMoreEvidenceAfter ?? needsMoreEvidenceBefore;
  const oldestOpenValidationAgeDays = audit.oldest_open_validation_age_days ?? audit.oldestOpenValidationAgeDays ?? 0;
  const validationCompletionPercent =
    audit.validation_completion_percent
    ?? audit.validationCompletionPercent
    ?? Math.max(
      0,
      Math.round(
        (1 - (openValidations / Math.max(1, openValidations + criticalGaps))) * 100,
      ),
    );
  const validationCompletion =
    audit.validation_completion_label
    || audit.validationCompletionLabel
    || `${validationCompletionPercent}% abgeschlossen`;
  const health = masterStatus.knowledge_health || 'unbekannt';
  const rootCauses = Array.isArray(rootCause.drivers) ? rootCause.drivers.slice(0, 3) : [];
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
        <Metric label="Validierung" value={validationCompletion} tone={criticalGaps ? 'warn' : 'good'} />
        <Metric label="Offene Validierungen" value={formatNumber(openValidations)} tone={openValidations ? 'warn' : 'good'} />
        <Metric label="Kritische Wissenslücken" value={formatNumber(criticalGaps)} tone={criticalGaps ? 'warn' : 'good'} />
        <Metric label="Älteste offene Validierung" value={`${formatNumber(oldestOpenValidationAgeDays)} Tage`} tone={oldestOpenValidationAgeDays >= 14 ? 'warn' : 'info'} />
        <Metric label="Tasks neu" value={formatNumber(createdLastRun)} tone={createdLastRun ? 'good' : 'info'} />
        <Metric label="Evidenz ausgeführt" value={formatNumber(executedLastRun)} tone={executedLastRun ? 'good' : 'info'} />
        <Metric label="Needs More Evidence vorher" value={formatNumber(needsMoreEvidenceBefore)} tone={needsMoreEvidenceBefore ? 'warn' : 'good'} />
        <Metric label="Needs More Evidence nachher" value={formatNumber(needsMoreEvidenceAfter)} tone={needsMoreEvidenceAfter ? 'warn' : 'good'} />
        <Metric label="Vertrauenswürdig" value={formatNumber(masterStatus.trusted_knowledge)} tone="good" />
        <Metric label="Schwach" value={formatNumber(masterStatus.weak_knowledge)} tone={masterStatus.weak_knowledge ? 'warn' : 'good'} />
        <Metric label="Veraltet" value={formatNumber(masterStatus.deprecated_knowledge)} tone={masterStatus.deprecated_knowledge ? 'warn' : 'good'} />
        <Metric label="Ø Vertrauen" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
        <Metric label="Ø Qualität" value={scorePercent(masterStatus.average_quality_score)} tone={tone} />
        <Metric label="Trend" value={masterStatus.knowledge_trend || '-'} tone="info" />
        <Metric label="Offene Pläne" value={formatNumber(masterStatus.validation_plans_open)} tone={masterStatus.validation_plans_open ? 'warn' : 'good'} />
        <Metric label="OOS nötig" value={formatNumber(masterStatus.knowledge_items_needing_oos)} tone={masterStatus.knowledge_items_needing_oos ? 'warn' : 'good'} />
        <Metric label="Vertrauenskandidaten" value={formatNumber(masterStatus.trusted_candidate_count)} tone={masterStatus.trusted_candidate_count ? 'good' : 'info'} />
      <Metric label="Offene Prüfungen" value={formatNumber(operatorState.humanReview?.pending_reviews || 0)} tone={operatorState.humanReview?.pending_reviews ? 'warn' : 'good'} />
      </div>
      <div className="knowledge-health-root-cause">
        <p className="control-view-note">Warum Vertrauen niedrig?</p>
        {rootCauses.length ? rootCauses.map((driver) => (
          <div key={driver.title} className="status-line">
            <span>{driver.title}</span>
            <strong>{driver.impact}</strong>
          </div>
        )) : (
          <p className="control-view-note">Ursachen werden gerade aus den vorhandenen Reports abgeleitet.</p>
        )}
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
      title: 'Aufsicht',
      value: operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status),
      detail: operatorState.supervisor.next_action,
      tone: operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status),
      meta: `Heartbeat ${shortDateTime(operatorState.supervisor.heartbeat_utc)}`,
    },
    {
      id: 'open_scheduler',
      title: 'Planer',
      value: `${activeJobs.length} aktiv`,
      detail: nextJob ? `${nextJob.job_type} / ${shortDateTime(nextJob.next_run_utc)}` : 'Keine nächsten Planer-Jobs gemeldet',
      tone: activeJobs.length ? 'info' : 'warn',
      meta: 'config-gesteuert',
    },
    {
      id: 'open_nightly',
      title: 'Nachtlauf',
      value: operatorState.nightly.current_state,
      detail: operatorState.nightly.next_action,
      tone: toneFromStatus(operatorState.nightly.current_state),
      meta: operatorState.nightly.next_nightly_window,
    },
    {
      id: 'open_research',
      title: 'Lernen & Wissen',
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
      title: 'Marktregime',
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
      title: 'Berichte',
      value: warningCount ? `${warningCount} Warnungen` : 'ruhig',
      detail: operatorState.logLines.at(-1) || 'Keine Live-Protokolle in Bridge V1',
      tone: warningCount ? 'warn' : 'info',
      meta: 'nur lesend',
    },
  ];
}

function buildCommandCenterModules(operatorState) {
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  const validationReport = reportByKey(operatorState, 'validateEnsembleSignalPackage')?.raw || {};
  const handoffReport = reportByKey(operatorState, 'systemBHandoffBundle')?.raw || {};
  const specsReport = reportByKey(operatorState, 'signalAgentSpecs')?.raw || {};
  const trustedGateReport = reportByKey(operatorState, 'trustedKnowledgeReviewGate')?.raw || {};
  const reviewPrioritization = reportByKey(operatorState, 'reviewPrioritizationAudit')?.raw || {};
  const nightly = reportByKey(operatorState, 'nightlyWorkAreaStatus')?.raw || {};
  const revalidation = nightly.revalidation || nightly.Revalidation || {};
  const revalidationStatus = String(revalidation.status || revalidation.Status || 'bereit').toLowerCase();
  const revalidationNext = revalidation.next_execution_window || revalidation.NextExecutionWindow || 'jetzt';
  const revalidationAt = revalidation.next_execution_at_utc || revalidation.NextExecutionAtUtc || '';
  const revalidationLabel = revalidationStatus === 'ausgeführt'
    ? `zuletzt ${shortTimeOnly(revalidation.executed_at_utc || revalidation.ExecutedAtUtc)}`
    : revalidationStatus.includes('fehler')
      ? `fehlgeschlagen${revalidation.result ? ` · ${revalidation.result}` : ''}`
      : `wartet auf Nightly bis ${revalidationNext}${revalidationAt ? ` (${shortTimeOnly(revalidationAt)})` : ''}`;
  const openReviews = Number(operatorState.humanReview?.pending_reviews || 0);
  const needsMoreEvidence = Number(operatorState.humanReview?.needs_more_evidence_reviews || 0);
  const tradingReviews = Number(reviewPrioritization.trading_reviews || 0);
  const documentationReviews = Number(reviewPrioritization.documentation_reviews || 0);
  const warnings = [
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ].filter(Boolean).length;

  return [
    {
      id: 'trading',
      title: 'Handelsintelligenz',
      value: `${formatNumber(portfolioReport.ready_assets || portfolioReport.bot_ready_assets || 2)} bereit`,
      detail: 'GER40 und XAUUSD sind bereit',
      tone: toneFromStatus(operatorState.masterStatus.ensemble_portfolio_status || portfolioReport.portfolio_readiness || 'ready'),
      meta: `EURUSD: ${translateOperatorCode(portfolioReport.eurusd_readiness || 'needs_more_validation').title}`,
    },
    {
      id: 'signal-package',
      title: 'Signalpaket',
      value: compactStatusLabel(validationReport.validation_status || validationReport.status || 'bereit'),
      detail: `${formatNumber(specsReport.spec_count || operatorState.masterStatus.signal_agent_specs_ready || 0)} Spezifikationen verfügbar`,
      tone: toneFromStatus(validationReport.validation_status || validationReport.status || 'completed'),
      meta: 'Übergabeordner vorbereitet',
    },
    {
      id: 'learning',
      title: 'Lernen & Wissen',
      value: statusDeutsch(operatorState.masterStatus.knowledge_health),
      detail: `Vertrauen ${scorePercent(operatorState.masterStatus.average_trust_score)}`,
      tone: toneFromStatus(operatorState.masterStatus.knowledge_health),
      meta: `${formatNumber(operatorState.masterStatus.knowledge_items_needing_oos)} OOS offen`,
    },
    {
      id: 'self-improvement',
      title: 'Selbstverbesserung',
      value: `${formatNumber(reportByKey(operatorState, 'workAreaExecutorPolicy')?.raw?.active_areas || 0)} Bereiche`,
      detail: `Re-Validierung: ${revalidationLabel}`,
      tone: nightly.in_nightly_window || nightly.InNightlyWindow ? 'good' : 'warn',
      meta: `${formatNumber(reportByKey(operatorState, 'workAreaExecutorPolicy')?.raw?.frank_items || 0)} Frank`,
    },
    {
      id: 'trust',
      title: 'Wissen & Vertrauen',
      value: scorePercent(operatorState.masterStatus.average_trust_score),
      detail: trustedGateReport.eligible_for_trusted_review || trustedGateReport.eligibleForTrustedReview
        ? `${formatNumber(trustedGateReport.eligible_for_trusted_review || trustedGateReport.eligibleForTrustedReview)} Wissenselemente bereit`
        : tradingReviews > 0
          ? `${formatNumber(tradingReviews)} Trading-Entscheidungen warten`
          : documentationReviews > 0
            ? `${formatNumber(documentationReviews)} Dokumentationsprüfungen können später erfolgen`
            : openReviews > 0
              ? `${formatNumber(openReviews)} Prüfungen`
              : needsMoreEvidence > 0
                ? 'Hermes sammelt weitere Evidenz'
                : 'Keine Aktion erforderlich',
      tone: trustedGateReport.eligible_for_trusted_review || trustedGateReport.eligibleForTrustedReview ? 'warn' : operatorState.masterStatus.knowledge_items_needing_oos ? 'warn' : 'info',
      meta: trustedGateReport.eligible_for_trusted_review || trustedGateReport.eligibleForTrustedReview
        ? 'Im Prüfzentrum prüfen'
        : `${formatNumber(operatorState.masterStatus.validation_plans_open)} Pläne`,
    },
    {
      id: 'review',
      title: 'Prüfzentrum',
      value: `${formatNumber(openReviews)} offen`,
      detail: tradingReviews > 0
        ? `${formatNumber(tradingReviews)} Trading-Entscheidungen warten`
        : documentationReviews > 0
          ? `${formatNumber(documentationReviews)} Dokumentationsprüfungen können später erfolgen`
          : openReviews
            ? 'Prüfungen offen'
            : 'Kein Eingriff nötig',
      tone: tradingReviews > 0 ? 'warn' : openReviews ? 'info' : 'good',
      meta: `${formatNumber(operatorState.humanReview?.deferred_reviews || 0)} zurückgestellt`,
    },
    {
      id: 'safety',
      title: 'Sicherheit',
      value: operatorState.masterStatus.no_auto_trading ? 'gesichert' : 'kritisch',
      detail: 'Auto-Trading aus',
      tone: operatorState.masterStatus.no_auto_trading ? 'good' : 'danger',
      meta: 'Broker gesperrt',
    },
    {
      id: 'system',
      title: 'System',
      value: operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status),
      detail: `${formatNumber(operatorState.schedulerJobs.filter((job) => job.enabled).length)} Planer aktiv`,
      tone: operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status),
      meta: `Aktivität ${shortDateTime(operatorState.masterStatus.last_meta_review || operatorState.lastUpdatedAt)}`,
    },
    {
      id: 'time-control',
      title: 'Zeitsteuerung',
      value: operatorState.timeControl?.status_label || 'Außerhalb des Arbeitsfensters',
      detail: `${operatorState.timeControl?.work_window?.start || '08:00'} - ${operatorState.timeControl?.work_window?.end || '18:00'}`,
      tone: operatorState.timeControl?.in_work_window ? 'good' : 'warn',
      meta: `${operatorState.timeControl?.time_zone || 'Europe/Berlin'} · ${truncateText((operatorState.timeControl?.active_weekdays || []).join(', '), 26)}`,
    },
    {
      id: 'roles',
      title: 'Rollen & Aufgaben',
      value: `${formatNumber(operatorState.roles?.length || 0)} Rollen`,
      detail: `${formatNumber(operatorState.masterStatus.queued_tasks)} geplante Aufgaben`,
      tone: operatorState.masterStatus.queued_tasks ? 'warn' : 'info',
      meta: 'Subsysteme',
    },
    {
      id: 'storage',
      title: 'Speicher & Ressourcen',
      value: formatGb(operatorState.storage.free_disk_gb),
      detail: `${formatNumber(operatorState.storage.cleanup_candidate_count)} Cleanup`,
      tone: operatorState.storage.errors.length ? 'warn' : 'good',
      meta: `${Math.round(operatorState.resource.memory_usage_percent)}% RAM`,
    },
    {
      id: 'reports',
      title: 'Berichte',
      value: warnings ? `${warnings} Warnungen` : 'geordnet',
      detail: `Paket ${compactStatusLabel(validationReport.validation_status || validationReport.status || 'bereit')}`,
      tone: warnings ? 'warn' : 'info',
      meta: 'Export bereit',
    },
    {
      id: 'logs',
      title: 'Protokolle',
      value: warnings ? `${warnings} Warnungen` : 'ruhig',
      detail: warnings ? 'Aufmerksamkeit nötig' : 'Keine Warnung',
      tone: warnings ? 'warn' : 'info',
      meta: 'Nur lesen',
    },
  ];
}

function moduleById(modules, id) {
  return modules.find((module) => module.id === id);
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
        <StatusPill tone={supervisorTone}>Aufsicht {operatorState.supervisor.running ? 'läuft' : 'gestoppt'}</StatusPill>
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

function OrbitPanel({ module, onOpen, className = '', style = {} }) {
  return (
    <button
      className={`orbit-panel ${className} ${toneClass(module.tone)}`}
      style={style}
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
        <p className="cockpit-master-source-warning">Ersatzdaten aktiv - Bridge liest sichere lokale Berichte.</p>
      ) : null}
      <div className="cockpit-master-grid">
        <Metric label="Fokus" value={masterStatus.current_focus} tone="info" />
        <Metric label="Domänen" value={masterStatus.active_domains.map(domainLabel).join(', ') || '-'} tone="info" />
        <Metric label="Offene Aufgaben" value={formatNumber(masterStatus.queued_tasks)} tone={masterStatus.queued_tasks ? 'warn' : 'good'} />
        <Metric label="Letzter Nachtlauf" value={shortDateTime(masterStatus.last_nightly_run)} />
        <Metric label="Autonomer Lernzyklus" value={shortDateTime(masterStatus.last_autonomous_loop)} />
        <Metric label="Letzte Lernanalyse" value={shortDateTime(masterStatus.last_meta_review)} />
        <Metric label="Lernstrategie" value={masterStatus.learning_strategy} />
        <Metric label="Aufsicht" value={masterStatus.supervisor_running ? 'läuft' : 'gestoppt'} tone={masterStatus.supervisor_running ? 'good' : 'warn'} />
        <Metric label="Planer" value={`${formatNumber(masterStatus.scheduler_enabled)} aktiv`} />
        <Metric label="Ressourcen" value={masterStatus.resource_action} tone={toneFromStatus(masterStatus.resource_action)} />
        <Metric label="Speicherbereinigung" value={formatNumber(masterStatus.storage_cleanup)} tone={masterStatus.storage_cleanup ? 'warn' : 'good'} />
        <Metric label="Robust" value={formatNumber(masterStatus.robust_strategies)} tone={masterStatus.robust_strategies ? 'good' : 'warn'} />
        <Metric label="Demo-Kandidaten" value={formatNumber(masterStatus.demo_bot_candidates)} tone={masterStatus.demo_bot_candidates ? 'good' : 'warn'} />
        <Metric label="no_auto_trading" value={String(masterStatus.no_auto_trading)} tone={masterStatus.no_auto_trading ? 'good' : 'danger'} />
        <Metric label="Menschliche Prüfung" value={masterStatus.human_review_required ? 'erforderlich' : 'frei'} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <Metric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <Metric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
      <GoalSystemCard masterStatus={masterStatus} />
      <KnowledgeHealthCard operatorState={operatorState} />
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

const TIME_CONTROL_WEEKDAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

const TIME_CONTROL_PRESETS = [
  {
    id: 'normal',
    label: 'Normal',
    description: 'Arbeitszeit tagsüber, Nightly nachts, Lernen am frühen Morgen.',
    apply: () => ({
      timeZone: 'Europe/Berlin',
      workEnabled: true,
      workStart: '08:00',
      workEnd: '18:00',
      nightlyEnabled: true,
      nightlyStart: '23:00',
      nightlyEnd: '05:00',
      learningEnabled: true,
      learningStart: '05:30',
      learningEnd: '07:00',
      reviewEnabled: true,
      reviewStart: '08:00',
      reviewEnd: '18:00',
      activeWeekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
    }),
  },
  {
    id: 'intensiv',
    label: 'Intensiv',
    description: 'Längeres Arbeitsfenster und engeres Review-Fenster für aktive Tage.',
    apply: () => ({
      timeZone: 'Europe/Berlin',
      workEnabled: true,
      workStart: '07:00',
      workEnd: '19:30',
      nightlyEnabled: true,
      nightlyStart: '22:30',
      nightlyEnd: '05:30',
      learningEnabled: true,
      learningStart: '05:00',
      learningEnd: '06:30',
      reviewEnabled: true,
      reviewStart: '07:00',
      reviewEnd: '19:30',
      activeWeekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
    }),
  },
  {
    id: 'away-3-days',
    label: '3 Tage abwesend',
    description: 'Arbeitsfenster reduziert, Review nur kurz erreichbar, Lernen aus.',
    apply: () => ({
      timeZone: 'Europe/Berlin',
      workEnabled: false,
      workStart: '09:00',
      workEnd: '09:00',
      nightlyEnabled: true,
      nightlyStart: '23:00',
      nightlyEnd: '05:00',
      learningEnabled: false,
      learningStart: '05:30',
      learningEnd: '07:00',
      reviewEnabled: true,
      reviewStart: '10:00',
      reviewEnd: '11:00',
      activeWeekdays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
    }),
  },
  {
    id: 'observe-only',
    label: 'Nur Beobachten',
    description: 'Nur Sichtfenster aktiv, keine Lern- oder Arbeitsfenster.',
    apply: () => ({
      timeZone: 'Europe/Berlin',
      workEnabled: false,
      workStart: '08:00',
      workEnd: '08:00',
      nightlyEnabled: false,
      nightlyStart: '23:00',
      nightlyEnd: '05:00',
      learningEnabled: false,
      learningStart: '05:30',
      learningEnd: '07:00',
      reviewEnabled: true,
      reviewStart: '12:00',
      reviewEnd: '12:00',
      activeWeekdays: [],
    }),
  },
];

function createTimeControlDraft(timeControl = {}) {
  return {
    timeZone: timeControl.time_zone || 'Europe/Berlin',
    workEnabled: Boolean(timeControl.work_window?.enabled ?? true),
    workStart: timeControl.work_window?.start || '08:00',
    workEnd: timeControl.work_window?.end || '18:00',
    nightlyEnabled: Boolean(timeControl.nightly_window?.enabled ?? true),
    nightlyStart: timeControl.nightly_window?.start || '23:00',
    nightlyEnd: timeControl.nightly_window?.end || '05:00',
    learningEnabled: Boolean(timeControl.learning_window?.enabled ?? true),
    learningStart: timeControl.learning_window?.start || '05:30',
    learningEnd: timeControl.learning_window?.end || '07:00',
    reviewEnabled: Boolean(timeControl.human_review_window?.enabled ?? true),
    reviewStart: timeControl.human_review_window?.start || '08:00',
    reviewEnd: timeControl.human_review_window?.end || '18:00',
    activeWeekdays: Array.isArray(timeControl.active_weekdays) && timeControl.active_weekdays.length
      ? [...timeControl.active_weekdays]
      : ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
  };
}

function timeControlPayload(draft) {
  return {
    time_zone: draft.timeZone,
    work_window: { start: draft.workStart, end: draft.workEnd, enabled: draft.workEnabled },
    nightly_window: { start: draft.nightlyStart, end: draft.nightlyEnd, enabled: draft.nightlyEnabled },
    learning_window: { start: draft.learningStart, end: draft.learningEnd, enabled: draft.learningEnabled },
    human_review_window: { start: draft.reviewStart, end: draft.reviewEnd, enabled: draft.reviewEnabled },
    active_weekdays: draft.activeWeekdays,
  };
}

function timeControlWindowWarning(start, end, enabled) {
  return start && end && start === end
    ? `Startzeit und Endzeit sind identisch${enabled ? '.' : ' (Fenster ist inaktiv).'}`
    : '';
}

function evidenceAutoLoopStatusText(evidenceAutoLoop, timeControl) {
  const configured = Boolean(evidenceAutoLoop.scheduler_configured ?? evidenceAutoLoop.schedulerConfigured);
  const enabled = Boolean(evidenceAutoLoop.scheduler_enabled ?? evidenceAutoLoop.schedulerEnabled);
  const active = enabled && Boolean(
    timeControl.learning_window?.active_now
    ?? timeControl.learningWindow?.active_now
    ?? timeControl.learning_window?.activeNow
    ?? timeControl.learningWindow?.activeNow
    ?? timeControl.nightly_window?.active_now
    ?? timeControl.nightlyWindow?.active_now
    ?? timeControl.nightly_window?.activeNow
    ?? timeControl.nightlyWindow?.activeNow,
  );
  const nextRun = evidenceAutoLoop.next_run_utc || evidenceAutoLoop.nextRunUtc;
  const hint = evidenceAutoLoop.next_run_hint || evidenceAutoLoop.nextRunHint || 'Nächster Lauf wird beim Scheduler-Lauf berechnet.';

  return {
    configured,
    enabled,
    active,
    label: !enabled
      ? 'Deaktiviert'
      : active
        ? 'Aktiv – wartet auf Ausführung oder läuft'
        : 'Aktiviert – wartet auf Lernfenster',
    nextRun: nextRun || hint,
    hint,
  };
}

function DashboardSystemStatus({ operatorState }) {
  const supervisorSummary = translateOperatorCode(operatorState.supervisor.next_action || operatorState.supervisor.status);
  const resourceSummary = translateOperatorCode(operatorState.resource.action || 'continue');
  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={operatorState.supervisor.next_action || operatorState.supervisor.status} />
      <Metric label="Bedeutung" value={supervisorSummary.meaning} tone="info" />
      <Metric label="Hermes arbeitet an" value={supervisorSummary.whatHermesDoes} tone="info" />
      <Metric label="Aktion für Frank" value={supervisorSummary.franksAction} tone={supervisorSummary.franksAction !== 'Nein' ? 'warn' : 'good'} />
      <Metric label="Gesamtstatus" value={statusDeutsch(operatorState.masterStatus.overall_status)} tone={toneFromStatus(operatorState.masterStatus.overall_status)} />
      <Metric label="Aufsicht" value={operatorState.supervisor.running ? 'läuft' : statusDeutsch(operatorState.supervisor.status)} tone={operatorState.supervisor.running ? 'good' : toneFromStatus(operatorState.supervisor.status)} />
      <Metric label="Planer" value={`${formatNumber(operatorState.schedulerJobs.filter((job) => job.enabled).length)} aktiv`} tone="info" />
      <Metric label="Nachtlauf" value={translateOperatorCode(operatorState.nightly.current_state).title} tone={toneFromStatus(operatorState.nightly.current_state)} />
      <Metric label="Letzte Analyse" value={shortDateTime(operatorState.masterStatus.last_meta_review)} tone="info" />
      <Metric label="Lernstrategie" value={operatorState.masterStatus.learning_strategy} tone="info" />
      <Metric label="Ressourcenaktion" value={resourceSummary.title} tone={resourceSummary.severity === 'warn' ? 'warn' : 'good'} />
      <Metric label="Ressourcenbedeutung" value={resourceSummary.meaning} tone="info" />
      <Metric label="Aktion für Frank" value={resourceSummary.franksAction} tone={resourceSummary.franksAction !== 'Nein' ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardJarvisCenter({ operatorState }) {
  const activeAssets = Array.isArray(operatorState.masterStatus.scalping_assets)
    ? operatorState.masterStatus.scalping_assets
    : ['GER40', 'XAUUSD', 'EURUSD'];
  const reviewPrioritization = reportByKey(operatorState, 'reviewPrioritizationAudit')?.raw || {};
  const reviewOpen = Number(operatorState.humanReview?.pending_reviews || 0);
  const evidenceOpen = Number(operatorState.humanReview?.needs_more_evidence_reviews || 0);
  const tradingReviews = Number(reviewPrioritization.trading_reviews || 0);
  const documentationReviews = Number(reviewPrioritization.documentation_reviews || 0);
  const frankState = reviewOpen > 0 ? 'human_review_required' : evidenceOpen > 0 ? 'evidence_requested' : 'no_action_required';
  const tradingReady = String(operatorState.masterStatus.bot_ready_assets || operatorState.masterStatus.setup_ready_assets || '').includes('GER40')
    || String(operatorState.masterStatus.ensemble_portfolio_status || '').includes('ready');

  return (
    <section className="cockpit-jarvis-orb" aria-label="Jarvis Zentrale">
      <div className="cockpit-jarvis-orb-ring" />
      <div className={`cockpit-jarvis-status-ring ${operatorState.supervisor.running ? 'is-active' : 'is-warning'}`} />
      <div className="cockpit-jarvis-orb-core">
        <p className="eyebrow">Jarvis</p>
        <strong>{operatorState.supervisor.running ? 'Läuft' : 'Prüfen'}</strong>
        <span>Fokus: {truncateText(operatorState.masterStatus.current_focus, 24)}</span>
        <div className="cockpit-decision-row">
          <i className={operatorState.supervisor.running ? 'is-good' : 'is-warn'} title="Jarvis läuft" />
          <i className={reviewOpen ? 'is-warn' : evidenceOpen ? 'is-warn' : 'is-good'} title="Frank muss handeln" />
          <i className={tradingReady ? 'is-good' : 'is-warn'} title="Trading bereit" />
        </div>
      </div>
      <div className="cockpit-jarvis-orb-caption cockpit-jarvis-orb-caption-top">
        <small>Status</small>
        <strong>{statusDeutsch(operatorState.masterStatus.overall_status)}</strong>
      </div>
      <div className="cockpit-jarvis-orb-caption cockpit-jarvis-orb-caption-right">
        <small>Frank</small>
        <strong>{tradingReviews ? `${formatNumber(tradingReviews)} Trading-Entscheidungen warten` : documentationReviews ? `${formatNumber(documentationReviews)} Dokumentationsprüfungen können später erfolgen` : reviewOpen ? `${formatNumber(reviewOpen)} Prüfungen` : evidenceOpen ? 'Hermes sammelt weitere Evidenz' : 'nichts offen'}</strong>
      </div>
      <div className="cockpit-jarvis-orb-caption cockpit-jarvis-orb-caption-bottom">
        <small>Trading</small>
        <strong>{tradingReady ? 'bereit' : 'prüfen'}</strong>
      </div>
      <div className="cockpit-jarvis-orb-caption cockpit-jarvis-orb-caption-left">
        <small>Aufsicht</small>
        <strong>{operatorState.supervisor.running ? 'läuft' : 'gestoppt'}</strong>
      </div>
      <div className="cockpit-jarvis-orb-footer">
        <span>{truncateText(activeAssets.join(' · '), 34)}</span>
      </div>
    </section>
  );
}

function DashboardModuleOrbit({ modules, onOpen }) {
  const orbitPositions = [
    { '--orbit-angle': '-28deg', '--orbit-radius': 'clamp(255px, 31vw, 390px)' },
    { '--orbit-angle': '-62deg', '--orbit-radius': 'clamp(250px, 30vw, 380px)' },
    { '--orbit-angle': '-96deg', '--orbit-radius': 'clamp(246px, 29vw, 370px)' },
    { '--orbit-angle': '-130deg', '--orbit-radius': 'clamp(250px, 30vw, 380px)' },
    { '--orbit-angle': '-164deg', '--orbit-radius': 'clamp(255px, 31vw, 390px)' },
    { '--orbit-angle': '28deg', '--orbit-radius': 'clamp(245px, 29vw, 370px)' },
    { '--orbit-angle': '62deg', '--orbit-radius': 'clamp(238px, 28vw, 355px)' },
    { '--orbit-angle': '96deg', '--orbit-radius': 'clamp(234px, 27vw, 345px)' },
    { '--orbit-angle': '130deg', '--orbit-radius': 'clamp(238px, 28vw, 355px)' },
    { '--orbit-angle': '164deg', '--orbit-radius': 'clamp(245px, 29vw, 370px)' },
  ];

  return (
    <section className="cockpit-module-orbit" aria-label="Modulkreis">
      <div className="cockpit-module-orbit-half">
        {modules.map((module, index) => (
          <OrbitPanel
            className={`orbit-slot-${Math.min(index + 1, 10)}`}
            style={orbitPositions[index] || orbitPositions[orbitPositions.length - 1]}
            key={module.id}
            module={module}
            onOpen={onOpen}
          />
        ))}
      </div>
    </section>
  );
}

function HudPanel({ module, onOpen }) {
  if (!module) {
    return null;
  }

  return (
    <button
      className={`hud-panel ${toneClass(module.tone)}`}
      onClick={() => onOpen(module.id)}
      type="button"
    >
      <div className="hud-panel-head">
        <span>{module.title}</span>
        <StatusPill tone={module.tone}>{module.value}</StatusPill>
      </div>
      <p>{module.detail}</p>
      <small>{module.meta}</small>
    </button>
  );
}

function HudPanelStack({ title, modules, ids, onOpen }) {
  return (
    <section className="hud-panel-stack" aria-label={title}>
      <div className="hud-stack-label">{title}</div>
      {ids.map((id) => (
        <HudPanel key={id} module={moduleById(modules, id)} onOpen={onOpen} />
      ))}
    </section>
  );
}

function HudOperationsTimeline({ operatorState, modules, onOpen }) {
  const warnings = consolidateOperatorWarnings([
    ...operatorState.warnings,
    ...operatorState.storage.warnings,
    ...operatorState.storage.errors,
  ]);
  const criticalCount = warnings.filter((warning) => warning.tone === 'danger').length;
  const warningCount = warnings.filter((warning) => warning.tone === 'warn').length;
  const hintCount = warnings.filter((warning) => warning.tone === 'info').length;
  const packageModule = moduleById(modules, 'signal-package');
  const review = operatorState.humanReview || {};
  const events = [
    {
      label: 'Letzte Aktivität',
      value: shortDateTime(operatorState.masterStatus.last_meta_review || operatorState.lastUpdatedAt),
      tone: 'info',
    },
    {
      label: 'Warnungen',
      value: criticalCount
        ? `${formatNumber(criticalCount)} kritisch`
        : warningCount
          ? `${formatNumber(warningCount)} Warnungen`
          : hintCount
            ? `${formatNumber(hintCount)} Hinweise`
            : 'keine relevanten Hinweise',
      tone: criticalCount ? 'danger' : warningCount ? 'warn' : hintCount ? 'info' : 'good',
    },
    {
      label: 'Letzte Review-Aktion',
      value: review.last_decision || review.last_action || (review.pending_reviews ? 'Prüfung offen' : 'keine Aktion nötig'),
      tone: review.pending_reviews ? 'warn' : 'good',
    },
    {
      label: 'Letzter Export',
      value: packageModule?.meta || 'Übergabepaket bereit',
      tone: 'info',
    },
    {
      label: 'Research/Nightly',
      value: shortDateTime(operatorState.masterStatus.last_nightly_run || operatorState.nightly.last_run_utc),
      tone: toneFromStatus(operatorState.nightly.current_state),
    },
  ];

  return (
    <section className="hud-timeline" aria-label="Operations Timeline">
      <div className="hud-timeline-head">
        <div>
          <span>Operations Timeline</span>
          <strong>letzte Ereignisse und Warnungen</strong>
        </div>
        <button onClick={() => onOpen('logs')} type="button">Details öffnen</button>
      </div>
      <div className="hud-timeline-track">
        {events.map((event) => (
          <article className={`hud-timeline-event ${toneClass(event.tone)}`} key={event.label}>
            <span>{event.label}</span>
            <strong>{truncateText(event.value, 44)}</strong>
          </article>
        ))}
      </div>
    </section>
  );
}

function HudCommandGrid({ operatorState, modules, onOpen }) {
  return (
    <div className="hud-command-grid">
      <HudPanelStack
        title="Mensch & Lernen"
        modules={modules}
        ids={['review', 'learning', 'trust', 'roles']}
        onOpen={onOpen}
      />

      <div className="hud-core-zone">
        <DashboardJarvisCenter operatorState={operatorState} />
      </div>

      <HudPanelStack
        title="Trading & Betrieb"
        modules={modules}
        ids={['trading', 'signal-package', 'time-control', 'system', 'storage']}
        onOpen={onOpen}
      />

      <HudOperationsTimeline operatorState={operatorState} modules={modules} onOpen={onOpen} />
    </div>
  );
}

function FrankActionCenter({ operatorState, onOpen }) {
  const model = frankActionCenterModel(operatorState);

  return (
    <section className={`frank-action-center tone-${model.mode}`} aria-label="Aktionen für Frank">
      <div className="frank-action-head">
        <div>
          <span>Aktionen für Frank</span>
          <strong>{model.title}</strong>
        </div>
        <StatusPill tone={model.mode === 'red' ? 'danger' : model.mode === 'yellow' ? 'warn' : 'good'}>
          {model.mode === 'red' ? '🔴' : model.mode === 'yellow' ? '🟡' : '🟢'}
        </StatusPill>
      </div>

      <p className="frank-action-summary">{model.summary}</p>
      <p className="frank-action-headline">{model.headline}</p>

      <div className="frank-action-list" aria-label="Frank Themen">
        {model.items.slice(0, 3).map((item, index) => (
          <article className={`frank-action-item ${item.tone ? `tone-${item.tone}` : ''}`} key={`${item.topic}-${index}`}>
            <strong>{truncateText(item.topic, 42)}</strong>
            <span>{truncateText(`Hermes: ${item.recommendation}`, 56)}</span>
            <span>{`Risiko: ${item.risk}`}</span>
          </article>
        ))}
      </div>

      <div className="frank-action-footer">
        <StatusPill tone={model.mode === 'red' ? 'danger' : model.mode === 'yellow' ? 'warn' : 'good'}>
          Aktion für Frank: {model.action}
        </StatusPill>
        {model.buttonVisible ? (
          <button type="button" onClick={() => onOpen(model.buttonTarget)}>
            {model.buttonLabel}
          </button>
        ) : null}
      </div>
    </section>
  );
}

function CommandCenterStatusBar({ operatorState }) {
  return (
    <div className="command-center-status-bar" aria-label="Jarvis Statusleiste">
      <strong>Jarvis Control Center</strong>
      <StatusPill tone={sourceTone(operatorState.dataSource)}>
        {operatorState.dataSource === DATA_SOURCE.LIVE_FILE ? 'Live-Bridge' : 'Ersatzdaten'}
      </StatusPill>
      <span>Aktualisiert {shortTime(operatorState.lastUpdatedAt)}</span>
      <StatusPill tone="good">Auto-Trading deaktiviert</StatusPill>
      <StatusPill tone="warn">Menschliche Freigabe erforderlich</StatusPill>
      <StatusPill tone="good">Research-only aktiv</StatusPill>
      <StatusPill tone={operatorState.timeControl?.in_work_window ? 'good' : 'warn'}>
        {operatorState.timeControl?.status_label || 'Außerhalb des Arbeitsfensters'}
      </StatusPill>
    </div>
  );
}

function DashboardTradingIntelligence({ operatorState }) {
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  const validationReport = reportByKey(operatorState, 'validateEnsembleSignalPackage')?.raw || {};
  const handoffReport = reportByKey(operatorState, 'systemBHandoffBundle')?.raw || {};
  const actions = botSpecActions(operatorState);
  const [botSpecStatus, setBotSpecStatus] = useState('');
  const [busyCandidate, setBusyCandidate] = useState('');
  const assetQuality = (asset) => {
    const readiness = String(asset.readiness || '').toLowerCase();
    if (readiness.includes('bot_ready')) return 'good';
    if (readiness.includes('signal_ready')) return 'info';
    if (readiness.includes('need')) return 'warn';
    return 'info';
  };

  const assets = Array.isArray(portfolioReport.assets) && portfolioReport.assets.length
    ? portfolioReport.assets
    : Array.isArray(portfolioReport.entries) && portfolioReport.entries.length
      ? portfolioReport.entries
    : [
        { asset: 'GER40', readiness: 'bot_ready' },
        { asset: 'XAUUSD', readiness: 'bot_ready' },
        { asset: 'EURUSD', readiness: 'needs_more_validation' },
      ];

  const exportBotSpec = async (action) => {
    setBusyCandidate(action.candidate_id);
    setBotSpecStatus('');
    try {
      const response = await fetch(`${__HERMES_READONLY_BRIDGE_URL__}/bridge/bot-spec/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          candidate_id: action.candidate_id,
          asset: action.asset,
          setup_id: action.setup_id,
          source: 'jarvis-control-center',
        }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload?.warnings?.[0] || payload?.error || payload?.message || `${response.status} ${response.statusText}`.trim());
      }

      const result = payload?.data || payload;
      setBotSpecStatus(`Spezifikation erzeugt: ${result.json_path || result.markdown_path || action.candidate_id}`);
    } catch (error) {
      setBotSpecStatus(`Spezifikation nicht erzeugt: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setBusyCandidate('');
    }
  };

  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={portfolioReport.portfolio_readiness || operatorState.masterStatus.ensemble_portfolio_status || 'ready'} />
      {assets.map((asset) => (
        <div className="cockpit-asset-card" key={asset.asset}>
          <strong>{asset.asset}</strong>
          <span>{compactStatusLabel(asset.readiness)}</span>
          <small>Aktives Setup: {asset.primary_setup || '-'}</small>
          <small>Signalqualität: {formatNumber(asset.signal_quality || asset.quality_score || 0)}</small>
          <small>Letzte Zertifizierung: {shortDateTime(asset.last_certified_at || asset.last_certification)}</small>
          <StatusPill tone={assetQuality(asset)}>{compactStatusLabel(asset.readiness)}</StatusPill>
          <small>{translateOperatorCode(asset.readiness).meaning}</small>
          <small>Hermes arbeitet an: {translateOperatorCode(asset.readiness).whatHermesDoes}</small>
          <small>Aktion für Frank: {translateOperatorCode(asset.readiness).franksAction}</small>
        </div>
      ))}
      <Metric label="Portfolio" value={compactStatusLabel(portfolioReport.portfolio_readiness || portfolioReport.portfolio_status)} tone={toneFromStatus(portfolioReport.portfolio_readiness || portfolioReport.portfolio_status)} />
      <Metric label="Paketvalidierung" value={compactStatusLabel(validationReport.validation_status || validationReport.status)} tone={toneFromStatus(validationReport.validation_status || validationReport.status)} />
      <Metric label="Aktion für Frank" value={describeMustFrankAct(translateOperatorCode(portfolioReport.portfolio_readiness || portfolioReport.portfolio_status).action)} tone="info" />
      <section className="bot-spec-action-panel">
        <div className="bot-spec-action-head">
          <div>
            <span>Bot-Spezifikation</span>
            <strong>cTrader Bot-Spezifikation erzeugen</strong>
          </div>
          <StatusPill tone="warn">Human Review danach Pflicht</StatusPill>
        </div>
        <p>
          Erzeugt nur eine Spezifikation mit Asset, Setup, Entry-/Exit-Regeln, SL/TP, Risk Rules,
          Session Filter, Kill Switch und Safety Flags. Kein Bot-Code, keine Order API.
        </p>
        <div className="bot-spec-action-list">
          {actions.map((action) => (
            <button
              disabled={busyCandidate === action.candidate_id}
              key={action.candidate_id}
              onClick={() => exportBotSpec(action)}
              type="button"
            >
              {busyCandidate === action.candidate_id ? 'Erzeuge Spezifikation...' : `Spezifikation erzeugen: ${action.asset} / ${action.setup_id}`}
            </button>
          ))}
          {actions.length === 0 ? <span>Kein bot_ready Setup mit Primary Candidate gefunden.</span> : null}
        </div>
        {botSpecStatus ? <p className="control-view-note">{botSpecStatus}</p> : null}
        <div className="operator-safety-flags">
          <StatusPill tone="good">specification_only=true</StatusPill>
          <StatusPill tone="good">no_ctrader_order_api=true</StatusPill>
          <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
          <StatusPill tone="good">live_trading_enabled=false</StatusPill>
        </div>
      </section>
    </div>
  );
}

function DashboardSignalPackage({ operatorState }) {
  const validationReport = reportByKey(operatorState, 'validateEnsembleSignalPackage')?.raw || {};
  const handoffReport = reportByKey(operatorState, 'systemBHandoffBundle')?.raw || {};
  const specsReport = reportByKey(operatorState, 'signalAgentSpecs')?.raw || {};
  const portfolioReport = reportByKey(operatorState, 'ensemblePortfolioStatus')?.raw || {};
  const lastExport = validationReport.generated_at
    || validationReport.generated_at_utc
    || handoffReport.generated_at
    || handoffReport.generated_at_utc
    || portfolioReport.updated_at_utc
    || operatorState.lastUpdatedAt;

  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={validationReport.validation_status || validationReport.status || 'ready'} />
      <Metric
        label="Paketprüfung"
        value={compactStatusLabel(validationReport.validation_status || validationReport.status || 'bereit')}
        tone={toneFromStatus(validationReport.validation_status || validationReport.status || 'completed')}
      />
      <Metric
        label="Signal-Spezifikationen"
        value={formatNumber(specsReport.spec_count || operatorState.masterStatus.signal_agent_specs_ready || 0)}
        tone={(specsReport.spec_count || operatorState.masterStatus.signal_agent_specs_ready) ? 'good' : 'warn'}
      />
      <Metric
        label="Übergabepaket"
        value={handoffReport.bundle_status || handoffReport.status || handoffReport.portfolio_status || 'vorbereitet'}
        tone={toneFromStatus(handoffReport.bundle_status || handoffReport.status || 'completed')}
      />
      <Metric label="Aktion für Frank" value={describeMustFrankAct(translateOperatorCode(validationReport.validation_status || validationReport.status).action)} tone="info" />
      <Metric label="Letzter Export" value={shortDateTime(lastExport)} tone="info" />
      <Metric label="Auto-Trading" value="deaktiviert" tone="good" />
      <Metric label="Broker-Orders" value="aus" tone="good" />
    </div>
  );
}

function DashboardTimeControl({ operatorState, onRefresh }) {
  const timeControl = operatorState.timeControl || {};
  const evidenceAutoLoop = reportByKey(operatorState, 'evidenceAutoLoop')?.raw || {};
  const evidenceAutoLoopStatus = evidenceAutoLoopStatusText(evidenceAutoLoop, timeControl);
  const [draft, setDraft] = useState(() => createTimeControlDraft(timeControl));
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    setDraft(createTimeControlDraft(timeControl));
  }, [timeControl]);

  const toggleWeekday = (day) => {
    setDraft((current) => {
      const nextActive = current.activeWeekdays.includes(day)
        ? current.activeWeekdays.filter((item) => item !== day)
        : [...current.activeWeekdays, day];
      return { ...current, activeWeekdays: nextActive };
    });
  };

  const saveTimeControl = async () => {
    setSaving(true);
    setMessage('');
    setError('');
    try {
      const response = await fetch(`${__HERMES_READONLY_BRIDGE_URL__}/bridge/time-control/update`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(timeControlPayload(draft)),
      });
      const payload = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(payload?.warnings?.[0] || payload?.error || payload?.message || `${response.status} ${response.statusText}`.trim());
      }

      const result = payload?.data || payload;
      setMessage(result?.status_label || 'Zeitsteuerung gespeichert.');
      await onRefresh?.();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : String(saveError));
    } finally {
      setSaving(false);
    }
  };

  const workWindowWarning = timeControlWindowWarning(draft.workStart, draft.workEnd, draft.workEnabled);
  const nightlyWindowWarning = timeControlWindowWarning(draft.nightlyStart, draft.nightlyEnd, draft.nightlyEnabled);
  const learningWindowWarning = timeControlWindowWarning(draft.learningStart, draft.learningEnd, draft.learningEnabled);
  const reviewWindowWarning = timeControlWindowWarning(draft.reviewStart, draft.reviewEnd, draft.reviewEnabled);

  return (
    <div className="time-control-detail">
      <div className="time-control-summary">
        <Metric label="Status" value={timeControl.status_label || 'Außerhalb des Arbeitsfensters'} tone={timeControl.in_work_window ? 'good' : 'warn'} />
        <Metric label="Zeitzone" value={timeControl.time_zone || draft.timeZone} tone="info" />
        <Metric label="Arbeitszeit" value={`${draft.workStart} - ${draft.workEnd}`} tone="info" />
        <Metric label="Nightly" value={`${draft.nightlyStart} - ${draft.nightlyEnd}`} tone="info" />
        <Metric label="Lernfenster" value={`${draft.learningStart} - ${draft.learningEnd}`} tone="info" />
        <Metric label="Human-Review" value={`${draft.reviewStart} - ${draft.reviewEnd}`} tone="info" />
        <Metric label="Evidenz Auto-Loop" value={evidenceAutoLoopStatus.label} tone={evidenceAutoLoopStatus.active ? 'good' : evidenceAutoLoopStatus.enabled ? 'info' : 'warn'} />
        <Metric label="Auto-Loop Modus" value={evidenceAutoLoopStatus.label} tone={evidenceAutoLoopStatus.active ? 'good' : evidenceAutoLoopStatus.enabled ? 'info' : 'warn'} />
        <Metric label="Geplante Tasks" value={formatNumber(evidenceAutoLoop.planned_tasks || 0)} tone={evidenceAutoLoop.planned_tasks ? 'info' : 'good'} />
        <Metric label="Frank nötig" value={(evidenceAutoLoop.frank_required ?? evidenceAutoLoop.frankRequired ?? 0) > 0 ? 'ja' : 'nein'} tone={(evidenceAutoLoop.frank_required ?? evidenceAutoLoop.frankRequired ?? 0) > 0 ? 'warn' : 'good'} />
        <Metric label="Letzter Lauf" value={shortDateTime(evidenceAutoLoop.last_run_utc || evidenceAutoLoop.lastRunUtc)} tone="info" />
        <Metric label="Nächster Lauf" value={shortDateTime(evidenceAutoLoopStatus.nextRun) || evidenceAutoLoopStatus.nextRun} tone="info" />
      </div>

      <div className="time-control-help">
        <p>Arbeitszeit = normale Aufgaben</p>
        <p>Nightly = schwere Nachtläufe</p>
        <p>Lernfenster = autonomes Lernen</p>
        <p>Human-Review = Zeitfenster für Frank-Entscheidungen</p>
      </div>

      <div className="time-control-presets">
        <span>Presets</span>
        <div>
          {TIME_CONTROL_PRESETS.map((preset) => (
            <button
              key={preset.id}
              type="button"
              onClick={() => setDraft((current) => ({ ...current, ...preset.apply() }))}
            >
              <strong>{preset.label}</strong>
              <small>{preset.description}</small>
            </button>
          ))}
        </div>
      </div>

      <div className="time-control-form">
        <label>
          <span>Zeitzone</span>
          <input value={draft.timeZone} onChange={(event) => setDraft((current) => ({ ...current, timeZone: event.target.value }))} />
        </label>
        <label>
          <span>Arbeitszeit von</span>
          <input type="time" value={draft.workStart} onChange={(event) => setDraft((current) => ({ ...current, workStart: event.target.value }))} />
        </label>
        <label>
          <span>Arbeitszeit bis</span>
          <input type="time" value={draft.workEnd} onChange={(event) => setDraft((current) => ({ ...current, workEnd: event.target.value }))} />
        </label>
        <label className="time-control-toggle">
          <span>Arbeitszeit aktiv</span>
          <button type="button" className={draft.workEnabled ? 'is-active' : ''} onClick={() => setDraft((current) => ({ ...current, workEnabled: !current.workEnabled }))}>
            {draft.workEnabled ? 'aktiv' : 'inaktiv'}
          </button>
        </label>
        <label>
          <span>Nightly von</span>
          <input type="time" value={draft.nightlyStart} onChange={(event) => setDraft((current) => ({ ...current, nightlyStart: event.target.value }))} />
        </label>
        <label>
          <span>Nightly bis</span>
          <input type="time" value={draft.nightlyEnd} onChange={(event) => setDraft((current) => ({ ...current, nightlyEnd: event.target.value }))} />
        </label>
        <label className="time-control-toggle">
          <span>Nightly aktiv</span>
          <button type="button" className={draft.nightlyEnabled ? 'is-active' : ''} onClick={() => setDraft((current) => ({ ...current, nightlyEnabled: !current.nightlyEnabled }))}>
            {draft.nightlyEnabled ? 'aktiv' : 'inaktiv'}
          </button>
        </label>
        <label>
          <span>Lernfenster von</span>
          <input type="time" value={draft.learningStart} onChange={(event) => setDraft((current) => ({ ...current, learningStart: event.target.value }))} />
        </label>
        <label>
          <span>Lernfenster bis</span>
          <input type="time" value={draft.learningEnd} onChange={(event) => setDraft((current) => ({ ...current, learningEnd: event.target.value }))} />
        </label>
        <label className="time-control-toggle">
          <span>Lernen aktiv</span>
          <button type="button" className={draft.learningEnabled ? 'is-active' : ''} onClick={() => setDraft((current) => ({ ...current, learningEnabled: !current.learningEnabled }))}>
            {draft.learningEnabled ? 'aktiv' : 'inaktiv'}
          </button>
        </label>
        <label>
          <span>Human-Review von</span>
          <input type="time" value={draft.reviewStart} onChange={(event) => setDraft((current) => ({ ...current, reviewStart: event.target.value }))} />
        </label>
        <label>
          <span>Human-Review bis</span>
          <input type="time" value={draft.reviewEnd} onChange={(event) => setDraft((current) => ({ ...current, reviewEnd: event.target.value }))} />
        </label>
        <label className="time-control-toggle">
          <span>Human-Review aktiv</span>
          <button type="button" className={draft.reviewEnabled ? 'is-active' : ''} onClick={() => setDraft((current) => ({ ...current, reviewEnabled: !current.reviewEnabled }))}>
            {draft.reviewEnabled ? 'aktiv' : 'inaktiv'}
          </button>
        </label>
      </div>

      <div className="time-control-weekdays">
        <span>Wochentage</span>
        <div>
          {TIME_CONTROL_WEEKDAYS.map((day) => {
            const active = draft.activeWeekdays.includes(day);
            return (
              <button
                key={day}
                type="button"
                className={active ? 'is-active' : ''}
                onClick={() => toggleWeekday(day)}
              >
                {day}
              </button>
            );
          })}
        </div>
      </div>

      {(workWindowWarning || nightlyWindowWarning || learningWindowWarning || reviewWindowWarning) ? (
        <div className="time-control-warnings">
          {workWindowWarning ? <p>{workWindowWarning}</p> : null}
          {nightlyWindowWarning ? <p>{nightlyWindowWarning}</p> : null}
          {learningWindowWarning ? <p>{learningWindowWarning}</p> : null}
          {reviewWindowWarning ? <p>{reviewWindowWarning}</p> : null}
        </div>
      ) : null}

      <div className="time-control-actions">
        <button disabled={saving || !operatorState.bridgeAvailable} onClick={saveTimeControl} type="button">
          {saving ? 'Speichere...' : 'Zeitsteuerung speichern'}
        </button>
        <span>{operatorState.bridgeAvailable ? 'Änderungen werden zentral in `config/schedules.json` gespeichert.' : 'Bridge nicht erreichbar - nur Anzeige.'}</span>
      </div>

      {message ? <p className="time-control-message is-good">{message}</p> : null}
      {error ? <p className="time-control-message is-danger">Zeitsteuerung konnte nicht gespeichert werden: {error}</p> : null}
      <div className="operator-safety-flags">
        <StatusPill tone="good">no_auto_trading=true</StatusPill>
        <StatusPill tone="warn">human_review_required=true</StatusPill>
        <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
        <StatusPill tone="good">live_trading_enabled=false</StatusPill>
        <StatusPill tone="good">research_only=true</StatusPill>
      </div>
    </div>
  );
}

function DashboardReviewSummary({ operatorState }) {
  const reviewPrioritization = reportByKey(operatorState, 'reviewPrioritizationAudit')?.raw || {};
  const review = operatorState.humanReview || {};
  const openReviews = Number(review.pending_reviews || 0);
  const needsMoreEvidence = Number(review.needs_more_evidence_reviews || 0);
  const tradingReviews = Number(reviewPrioritization.trading_reviews || 0);
  const documentationReviews = Number(reviewPrioritization.documentation_reviews || 0);
  const knowledgeReviews = Number(reviewPrioritization.research_reviews || 0) + Number(reviewPrioritization.software_reviews || 0) + Number(reviewPrioritization.process_reviews || 0);
  const reviewTone = openReviews > 0 ? 'warn' : needsMoreEvidence > 0 ? 'warn' : 'good';
  const reviewSummary = tradingReviews > 0
    ? {
        title: 'Wichtige Entscheidungen warten',
        meaning: 'Hermes hat Prioritäten für Trading-Entscheidungen vorbereitet.',
        action: 'Ja, im Prüfzentrum',
        severity: 'warn',
        whatHermesDoes: 'ordnet Trading-Reviews nach Priorität',
        franksAction: 'Ja, im Prüfzentrum',
      }
    : documentationReviews > 0
      ? {
          title: 'Dokumentationsprüfungen können später erfolgen',
          meaning: 'Hermes kann Dokumentationsprüfungen warten lassen.',
          action: 'Nein',
          severity: 'info',
          whatHermesDoes: 'bearbeitet Dokumentationsprüfungen im Hintergrund',
          franksAction: 'Nein',
        }
      : openReviews > 0
        ? translateOperatorCode('review_required')
        : needsMoreEvidence > 0
          ? {
              title: 'Hermes sammelt weitere Evidenz',
              meaning: 'Hermes arbeitet an offenen Evidenzthemen.',
              action: 'Keine Aktion erforderlich.',
              severity: 'warn',
              whatHermesDoes: 'sammelt weitere Evidenz',
              franksAction: 'Nein',
            }
          : translateOperatorCode('no_action_required');
  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={openReviews ? 'human_review_required' : 'no_action_required'} />
      <Metric label="Bedeutung" value={reviewSummary.meaning} tone={reviewTone} />
      <Metric label="Hermes arbeitet an" value={reviewSummary.whatHermesDoes} tone="info" />
      <Metric label="Aktion für Frank" value={reviewSummary.franksAction} tone={reviewSummary.franksAction !== 'Nein' ? 'warn' : 'good'} />
      <Metric label="Wichtige Entscheidungen" value={formatNumber(tradingReviews)} tone={tradingReviews ? 'warn' : 'good'} />
      <Metric label="Wissensprüfungen" value={formatNumber(knowledgeReviews)} tone={knowledgeReviews ? 'warn' : 'good'} />
      <Metric label="Dokumentation" value={formatNumber(documentationReviews)} tone={documentationReviews ? 'good' : 'info'} />
      <Metric label="Offene Prüfungen" value={formatNumber(openReviews)} tone={openReviews ? 'warn' : 'good'} />
      <Metric label="Freigegeben" value={formatNumber(review.approved_reviews)} tone="good" />
      <Metric label="Zurückgestellt" value={formatNumber(review.deferred_reviews)} tone="info" />
      <Metric label="Evidenz angefordert" value={formatNumber(needsMoreEvidence)} tone={needsMoreEvidence ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardLearningSummary({ operatorState }) {
  const masterStatus = operatorState.masterStatus;
  const audit = reportByKey(operatorState, 'knowledgeValidationAudit')?.raw || {};
  const improvement = reportByKey(operatorState, 'autonomousImprovementQueue')?.raw || {};
  const improvementPolicy = reportByKey(operatorState, 'workAreaExecutorPolicy')?.raw || {};
  const reviewPrioritization = reportByKey(operatorState, 'reviewPrioritizationAudit')?.raw || {};
  const evidenceAutoLoop = reportByKey(operatorState, 'evidenceAutoLoop')?.raw || {};
  const evidenceTaskExecution = reportByKey(operatorState, 'evidenceTaskExecution')?.raw || {};
  const nightly = reportByKey(operatorState, 'nightlyWorkAreaStatus')?.raw || {};
  const execution = reportByKey(operatorState, 'autonomousImprovementExecution')?.raw || {};
  const trustPlan = reportByKey(operatorState, 'knowledgeTrustImprovementPlan')?.raw || {};
  const evidenceAutoLoopStatus = evidenceAutoLoopStatusText(evidenceAutoLoop, operatorState.timeControl || {});
  const openValidations = audit.open_validations ?? audit.openValidations ?? masterStatus.validation_plans_open;
  const openReviews = Number(operatorState.humanReview?.pending_reviews || 0);
  const needsMoreEvidence = Number(operatorState.humanReview?.needs_more_evidence_reviews || 0);
  const tradingReviews = Number(reviewPrioritization.trading_reviews || 0);
  const documentationReviews = Number(reviewPrioritization.documentation_reviews || 0);
  const knowledgeReviews = Number(reviewPrioritization.research_reviews || 0) + Number(reviewPrioritization.software_reviews || 0) + Number(reviewPrioritization.process_reviews || 0);
  const criticalGaps = audit.critical_knowledge_gaps ?? audit.criticalKnowledgeGaps ?? masterStatus.knowledge_items_needing_oos;
  const oldestOpenValidationAgeDays = audit.oldest_open_validation_age_days ?? audit.oldestOpenValidationAgeDays ?? 0;
  const validationCompletionPercent =
    audit.validation_completion_percent
    ?? audit.validationCompletionPercent
    ?? Math.max(
      0,
      Math.round(
        (1 - (openValidations / Math.max(1, openValidations + criticalGaps))) * 100,
      ),
    );
  const validationCompletion =
    audit.validation_completion_label
    || audit.validationCompletionLabel
    || `${validationCompletionPercent}% abgeschlossen`;
  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={criticalGaps ? 'oos_data_missing' : 'ok'} />
      <Metric label="Validierung" value={validationCompletion} tone={criticalGaps ? 'warn' : 'good'} />
      <Metric label="Offene Validierungen" value={formatNumber(openValidations)} tone={openValidations ? 'warn' : 'good'} />
      <Metric label="Kritische Wissenslücken" value={formatNumber(criticalGaps)} tone={criticalGaps ? 'warn' : 'good'} />
      <Metric label="Älteste offene Validierung" value={`${formatNumber(oldestOpenValidationAgeDays)} Tage`} tone={oldestOpenValidationAgeDays >= 14 ? 'warn' : 'info'} />
      <Metric label="Wissensqualität" value={statusDeutsch(masterStatus.knowledge_health)} tone={toneFromStatus(masterStatus.knowledge_health)} />
      <Metric label="Vertrauen" value={scorePercent(masterStatus.average_trust_score)} tone="info" />
      <Metric label="Offene Pläne" value={formatNumber(masterStatus.validation_plans_open)} tone={masterStatus.validation_plans_open ? 'warn' : 'good'} />
      <Metric label="OOS nötig" value={formatNumber(masterStatus.knowledge_items_needing_oos)} tone={masterStatus.knowledge_items_needing_oos ? 'warn' : 'good'} />
      <Metric label="Selbstverbesserung" value={`${formatNumber(improvementPolicy.active_areas || improvement.active_improvements || 0)} Bereiche`} tone={improvementPolicy.active_areas || improvement.active_improvements ? 'good' : 'info'} />
      <Metric
        label="Evidenz Auto-Loop"
        value={evidenceAutoLoopStatus.label}
        tone={evidenceAutoLoopStatus.active ? 'good' : evidenceAutoLoopStatus.enabled ? 'info' : 'warn'}
      />
      <Metric
        label="Auto-Loop Modus"
        value={evidenceAutoLoopStatus.label}
        tone={evidenceAutoLoopStatus.active ? 'good' : evidenceAutoLoopStatus.enabled ? 'info' : 'warn'}
      />
      <Metric
        label="Auto-Loop Lauf"
        value={evidenceAutoLoop.review_count ? `${formatNumber(evidenceAutoLoop.planned_tasks || 0)} Tasks geplant` : 'bereit'}
        tone={evidenceAutoLoop.planned_tasks ? 'info' : 'good'}
      />
      <Metric label="Evidenzaufgaben ausgeführt" value={formatNumber(evidenceTaskExecution.tasks_executed || 0)} tone={evidenceTaskExecution.tasks_executed ? 'good' : 'info'} />
      <Metric label="Verbleibende Aufgaben" value={formatNumber(Math.max(0, (evidenceTaskExecution.tasks_found || 0) - (evidenceTaskExecution.tasks_executed || 0) - (evidenceTaskExecution.tasks_skipped || 0)))} tone="info" />
      <Metric label="Letzte Ausführung" value={shortDateTime(evidenceTaskExecution.updated_at_utc || evidenceTaskExecution.updatedAtUtc)} tone="info" />
      <Metric label="Nächste Ausführung" value={shortDateTime(evidenceAutoLoopStatus.nextRun) || evidenceAutoLoopStatus.nextRun} tone="info" />
      <Metric
        label="Letzter Lauf"
        value={shortDateTime(evidenceAutoLoop.last_run_utc || evidenceAutoLoop.lastRunUtc)}
        tone="info"
      />
      <Metric
        label="Frank nötig"
        value={(evidenceAutoLoop.frank_required ?? evidenceAutoLoop.frankRequired ?? 0) > 0 ? 'ja' : 'nein'}
        tone={(evidenceAutoLoop.frank_required ?? evidenceAutoLoop.frankRequired ?? 0) > 0 ? 'warn' : 'good'}
      />
      <Metric label="Prüfzentrum" value={tradingReviews ? `${formatNumber(tradingReviews)} Trading-Entscheidungen warten` : documentationReviews ? `${formatNumber(documentationReviews)} Dokumentationsprüfungen können später erfolgen` : openReviews ? `${formatNumber(openReviews)} offen` : needsMoreEvidence ? 'Hermes sammelt weitere Evidenz' : 'Keine Aktion erforderlich'} tone={tradingReviews ? 'warn' : openReviews ? 'warn' : needsMoreEvidence ? 'warn' : 'good'} />
      <Metric label="Wichtige Entscheidungen" value={formatNumber(tradingReviews)} tone={tradingReviews ? 'warn' : 'good'} />
      <Metric label="Wissensprüfungen" value={formatNumber(knowledgeReviews)} tone={knowledgeReviews ? 'warn' : 'good'} />
      <Metric label="Dokumentationsprüfungen" value={formatNumber(documentationReviews)} tone={documentationReviews ? 'good' : 'info'} />
      <Metric
        label="Re-Validierung"
        value={
          nightly.revalidation?.status
          || nightly.Revalidation?.status
          || nightly.revalidation?.Status
          || nightly.Revalidation?.Status
          || 'bereit'
        }
        tone={nightly.in_nightly_window || nightly.InNightlyWindow ? 'good' : 'warn'}
      />
      <Metric label="Vertrauensverbesserungen" value={trustPlan.total_blocked_items ?? trustPlan.totalBlockedItems ? `Hermes arbeitet an ${formatNumber(trustPlan.total_blocked_items ?? trustPlan.totalBlockedItems)}` : 'bereit'} tone={trustPlan.total_blocked_items ?? trustPlan.totalBlockedItems ? 'warn' : 'info'} />
      <Metric label="Hauptgründe" value={trustPlan.blocker_counts ? `${formatNumber(trustPlan.blocker_counts.trust_score_too_low || trustPlan.blockerCounts?.trust_score_too_low || 0)} niedriges Vertrauen` : 'bereit'} tone="info" />
      <Metric label="Erledigt" value={execution.executed ?? execution.Executed ?? 0} tone="good" />
      <Metric label="Geplant" value={execution.planned ?? execution.Planned ?? 0} tone="info" />
      <Metric label="Übersprungen" value={execution.skipped ?? execution.Skipped ?? 0} tone="info" />
      <Metric label="Fehlgeschlagen" value={execution.failed ?? execution.Failed ?? 0} tone={execution.failed || execution.Failed ? 'warn' : 'good'} />
      <Metric label="Frank" value={openReviews ? `${formatNumber(openReviews)} prüfen` : 'nichts offen'} tone={openReviews ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardStorageResources({ operatorState }) {
  const storageStatus = operatorState.storage.status || 'ok';
  const storageSummary = translateOperatorCode(
    operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : storageStatus,
  );
  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : storageStatus} />
      <Metric label="Bedeutung" value={storageSummary.meaning} tone={storageSummary.severity === 'warn' ? 'warn' : 'info'} />
      <Metric label="Hermes arbeitet an" value={storageSummary.whatHermesDoes} tone="info" />
      <Metric label="Aktion für Frank" value={storageSummary.franksAction} tone={storageSummary.franksAction !== 'Nein' ? 'warn' : 'good'} />
      <Metric label="RAM" value={formatGb(operatorState.resource.free_memory_gb || operatorState.resource.memory_free_gb || 0)} tone="info" />
      <Metric label="CPU" value={`${Math.round(operatorState.resource.cpu_usage_percent)}%`} tone={operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good'} />
      <Metric label="Speicherplatz" value={formatGb(operatorState.storage.free_disk_gb)} tone={operatorState.storage.errors.length ? 'warn' : 'good'} />
      <Metric label="Cleanup" value={formatNumber(operatorState.storage.cleanup_candidate_count)} tone={operatorState.storage.cleanup_candidate_count ? 'warn' : 'good'} />
    </div>
  );
}

function DashboardSafety({ operatorState }) {
  const safetyState = operatorState.masterStatus.no_auto_trading
    && !operatorState.masterStatus.broker_orders_enabled
    && !operatorState.masterStatus.live_trading_enabled
    ? 'ok'
    : 'safe_stop_requested';
  const safetySummary = translateOperatorCode(safetyState);
  return (
    <div className="cockpit-accordion-grid">
      <OperatorSummary code={safetyState} />
      <Metric label="Bedeutung" value={safetySummary.meaning} tone={safetySummary.severity === 'warn' ? 'warn' : 'good'} />
      <Metric label="Hermes arbeitet an" value={safetySummary.whatHermesDoes} tone="info" />
      <Metric label="Aktion für Frank" value={safetySummary.franksAction} tone={safetySummary.franksAction !== 'Nein' ? 'warn' : 'good'} />
      <Metric label="Auto-Trading" value={operatorState.masterStatus.no_auto_trading ? 'aus' : 'an'} tone={operatorState.masterStatus.no_auto_trading ? 'good' : 'danger'} />
      <Metric label="Menschliche Prüfung" value={operatorState.masterStatus.human_review_required ? 'erforderlich' : 'frei'} tone={operatorState.masterStatus.human_review_required ? 'warn' : 'good'} />
      <Metric label="Broker-Orders" value={operatorState.masterStatus.broker_orders_enabled ? 'an' : 'aus'} tone={operatorState.masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
      <Metric label="Live-Trading" value={operatorState.masterStatus.live_trading_enabled ? 'an' : 'aus'} tone={operatorState.masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      <Metric label="Nur Forschung" value={operatorState.masterStatus.research_only ? 'aktiv' : 'inaktiv'} tone={operatorState.masterStatus.research_only ? 'good' : 'warn'} />
    </div>
  );
}

function DashboardLogs({ operatorState }) {
  const logs = operatorLogView(operatorState);

  return (
    <div className="operator-log-summary">
      <section>
        <h3>Systemereignisse</h3>
        <div className="operator-event-list">
          {logs.systemEvents.map((event) => (
            <article className={`operator-event-row ${toneClass(event.tone)}`} key={event.label}>
              <span aria-hidden="true">{event.tone === 'good' ? '🟢' : event.tone === 'danger' ? '🔴' : event.tone === 'warn' ? '🟡' : '🟢'}</span>
              <div>
                <strong>{event.label}</strong>
                <small>{event.detail}</small>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section>
        <h3>Warnungen</h3>
        <div className="operator-event-list">
          {logs.warnings.map((warning) => (
            <article className={`operator-event-row ${toneClass(warning.tone)}`} key={`${warning.label}:${warning.detail}`}>
              <span aria-hidden="true">{warningToneInfo(warning.tone).icon}</span>
              <div>
                <strong>
                  {warningToneInfo(warning.tone).label}: {translateOperatorCode(warning.label).title}
                  {warning.count > 1 ? ` (${formatNumber(warning.count)}x)` : ''}
                </strong>
                <small>{translateOperatorCode(warning.detail).meaning || warning.detail}</small>
                <small>Handlung: {warning.action}</small>
              </div>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function ReportsActionCards({ operatorState }) {
  const reports = [
    {
      title: 'Systemstatus-Berichte',
      detail: 'Aufsicht, Planer, Nachtlauf und Gesamtstatus',
    },
    {
      title: 'Handelsintelligenz-Berichte',
      detail: 'Portfolio, Setup-Registry und Signalpaket',
    },
    {
      title: 'Prüfzentrum-Berichte',
      detail: 'Offene Reviews und Human-Review-Feedback',
    },
  ];

  return (
    <div className="cockpit-report-card-grid">
      {reports.map((report) => (
        <article className="cockpit-report-card" key={report.title}>
          <h3>{report.title}</h3>
          <p>{report.detail}</p>
          <div className="review-action-row">
            <button type="button">Öffnen</button>
            <button type="button">Exportieren</button>
            <button type="button">Anzeigen</button>
          </div>
        </article>
      ))}
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
      <Metric label="Menschliche Prüfung" value={masterStatus.human_review_required ? 'erforderlich' : 'frei'} tone={masterStatus.human_review_required ? 'good' : 'danger'} />
        <Metric label="broker_orders" value={String(masterStatus.broker_orders_enabled)} tone={masterStatus.broker_orders_enabled ? 'danger' : 'good'} />
        <Metric label="live_trading" value={String(masterStatus.live_trading_enabled)} tone={masterStatus.live_trading_enabled ? 'danger' : 'good'} />
      </div>
      <p className="cockpit-master-source-warning">Read-only: uses master-status/report snapshots only. No runtime commands or trading actions.</p>
    </section>
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
  const reviewPrioritization = reportByKey(operatorState, 'reviewPrioritizationAudit')?.raw || {};
  const evidenceAutoLoop = reportByKey(operatorState, 'evidenceAutoLoop')?.raw || {};
  const review = operatorState.humanReview || {
    pending_reviews: 0,
    approved_reviews: 0,
    rejected_reviews: 0,
    items: [],
  };
  const items = Array.isArray(review.items) ? review.items : [];
  const openReviews = Number(review.pending_reviews || 0);
  const tradingPriorityCount = Number(reviewPrioritization.trading_reviews || 0);
  const knowledgePriorityCount = Number(reviewPrioritization.research_reviews || 0) + Number(reviewPrioritization.software_reviews || 0);
  const documentationPriorityCount = Number(reviewPrioritization.documentation_reviews || 0);
  const [actionBusyId, setActionBusyId] = useState('');
  const [pendingReviewAction, setPendingReviewAction] = useState(null);
  const [reviewNotesById, setReviewNotesById] = useState({});
  const [reviewFeedbackById, setReviewFeedbackById] = useState({});
  const [resolvedReviewIds, setResolvedReviewIds] = useState([]);
  const assistantEntries = items
    .filter((item) => item.status === 'pending' && !resolvedReviewIds.includes(item.review_id))
    .map((item) => ({ ...reviewDecisionAssistant(item), reviewId: item.review_id, item }))
    .sort((left, right) => reviewActionPriority(right) - reviewActionPriority(left));
  const assistantApprove = assistantEntries.filter((entry) => entry.recommendationKey === 'approve').length;
  const assistantMoreEvidence = assistantEntries.filter((entry) => entry.recommendationKey === 'more_evidence').length;
  const assistantReject = assistantEntries.filter((entry) => entry.recommendationKey === 'reject').length;
  const topDecisionCards = assistantEntries.slice(0, 3);
  const visibleItems = openReviews > 0
    ? items.filter((item) => item.status === 'pending' && !resolvedReviewIds.includes(item.review_id))
      .sort((left, right) => {
        const leftScore = Number(left.review_action_score || left.reviewActionScore || 0);
        const rightScore = Number(right.review_action_score || right.reviewActionScore || 0);
        if (rightScore !== leftScore) {
          return rightScore - leftScore;
        }
        const priorityDelta = reviewPriorityRank(right.priority) - reviewPriorityRank(left.priority);
        if (priorityDelta !== 0) {
          return priorityDelta;
        }

        const domainDelta = reviewDomainRank(left.domain) - reviewDomainRank(right.domain);
        if (domainDelta !== 0) {
          return domainDelta;
        }

        return Number(right.trust_before || 0) - Number(left.trust_before || 0);
      })
    : [];

  const openReviewAction = (actionKey, item) => {
    const action = REVIEW_ACTIONS[actionKey];
    if (!action || !item?.review_id) {
      return;
    }

    setPendingReviewAction({ reviewId: item.review_id, actionKey });
    setReviewNotesById((current) => ({
      ...current,
      [item.review_id]: current[item.review_id] || `${action.label}: ${item.title}`,
    }));
    setReviewFeedbackById((current) => {
      const next = { ...current };
      delete next[item.review_id];
      return next;
    });
  };

  const cancelReviewAction = (reviewId) => {
    setPendingReviewAction((current) => (current?.reviewId === reviewId ? null : current));
  };

  const runReviewAction = async (actionKey, item) => {
    const action = REVIEW_ACTIONS[actionKey];
    if (!action || !item?.review_id) {
      return;
    }

    if (pendingReviewAction?.reviewId !== item.review_id || pendingReviewAction.actionKey !== actionKey) {
      return;
    }

    const note = String(reviewNotesById[item.review_id] || '').trim();

    setActionBusyId(item.review_id);
    let timeoutId = 0;
    try {
      await assertReviewEndpointAvailable(action.endpoint);
      const controller = new AbortController();
      timeoutId = window.setTimeout(() => controller.abort(), 10000);
      const response = await fetch(`${__HERMES_READONLY_BRIDGE_URL__}/bridge/review/${action.endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        signal: controller.signal,
        body: JSON.stringify({
          review_id: item.review_id,
          note: note.trim(),
          reviewer: 'ui_operator',
          source: 'jarvis-control-center',
        }),
      });
      window.clearTimeout(timeoutId);

      const responseText = await response.text();
      let payload = {};
      try {
        payload = responseText ? JSON.parse(responseText) : {};
      } catch {
        payload = {};
      }
      if (!response.ok) {
        throw new Error(postErrorMessage(payload, responseText, response));
      }

      const decision = payload?.data?.decision || payload?.decision || action.decisionLabel;
      const feedbackPath = payload?.data?.learning_feedback_path || payload?.learning_feedback_path || 'Learning Feedback gespeichert';
      const successMessage = `Entscheidung gespeichert: ${statusDeutsch(decision)}. Learning Feedback bestätigt.`;
      setResolvedReviewIds((current) => [...current, item.review_id]);
      setReviewFeedbackById((current) => ({
        ...current,
        [item.review_id]: {
          tone: 'good',
          title: 'Entscheidung gespeichert',
          message: `${successMessage} ${feedbackPath}`,
        },
      }));
      setPendingReviewAction(null);
      if (typeof onRefresh === 'function') {
        window.setTimeout(() => {
          void onRefresh();
        }, 0);
      }
    } catch (error) {
      if (timeoutId) {
        window.clearTimeout(timeoutId);
      }
      const reason = error instanceof Error ? error.message : String(error);
      const userMessage = reason.toLowerCase().includes('aborted')
        ? 'Anfrage wurde abgebrochen. Bitte erneut versuchen.'
        : 'Prüfentscheidung konnte nicht gespeichert werden. Bitte erneut versuchen.';
      const message = `${userMessage} Ursache: ${reason}`;
      setReviewFeedbackById((current) => ({
        ...current,
        [item.review_id]: {
          tone: 'danger',
          title: 'Speichern fehlgeschlagen',
          message,
        },
      }));
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
          <StatusPill tone={openReviews ? 'warn' : 'good'}>
            {formatNumber(openReviews)} offen
          </StatusPill>
          <StatusPill tone={tradingPriorityCount ? 'danger' : 'good'}>
            🔴 {formatNumber(tradingPriorityCount)} wichtig
          </StatusPill>
          <StatusPill tone={knowledgePriorityCount ? 'warn' : 'good'}>
            🟡 {formatNumber(knowledgePriorityCount)} Wissensprüfungen
          </StatusPill>
          <StatusPill tone={documentationPriorityCount ? 'good' : 'info'}>
            🟢 {formatNumber(documentationPriorityCount)} Dokumentationsprüfungen
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
      <p className="control-view-note">
        {tradingPriorityCount
          ? `${formatNumber(tradingPriorityCount)} Trading-Entscheidungen warten.`
          : documentationPriorityCount
            ? `${formatNumber(documentationPriorityCount)} Dokumentationsprüfungen können später erfolgen.`
            : 'Keine offenen Reviews mit hoher Priorität.'}
      </p>
      <p className="control-view-note">
        {assistantEntries.length
          ? 'Frank sieht jetzt die drei wichtigsten Entscheidungen zuerst.'
          : 'Keine Entscheidungshilfe verfügbar.'}
      </p>
      <p className="control-view-note">
        {evidenceAutoLoop.review_count
          ? 'Hermes plant weitere Evidenzläufe. Frank muss aktuell nichts freigeben.'
          : 'Keine weiteren Evidenzläufe geplant.'}
      </p>
      <div className="operator-safety-flags">
        <StatusPill tone="warn">no_auto_trading=true</StatusPill>
        <StatusPill tone="warn">human_review_required=true</StatusPill>
        <StatusPill tone="good">broker_orders_enabled=false</StatusPill>
        <StatusPill tone="good">live_trading_enabled=false</StatusPill>
        <StatusPill tone="good">research_only=true</StatusPill>
      </div>
      <div className="review-grid">
        {topDecisionCards.map(({ item, ...assistant }) => {
          const trafficLight = assistant;
          const risk = reviewRisk(item);
          const evidenceQuality = reviewEvidenceQuality(item);
          const pendingAction = pendingReviewAction?.reviewId === item.review_id ? REVIEW_ACTIONS[pendingReviewAction.actionKey] : null;
          const feedback = reviewFeedbackById[item.review_id];
          const recommendedActionKey = assistant.recommendationKey === 'approve'
            ? 'approve'
            : assistant.recommendationKey === 'reject'
              ? 'reject'
              : 'more';

          return (
          <article className={`review-card review-operator-card ${assistant.tone === 'good' ? 'is-green' : assistant.tone === 'danger' ? 'is-red' : 'is-yellow'}`} key={item.review_id}>
            <div className="review-card-head">
              <div>
                <span>{domainLabel(item.domain)}</span>
                <h3>{item.title}</h3>
              </div>
              <div className="review-traffic-light" aria-label={trafficLight.label}>
                <i />
                <StatusPill tone={trafficLight.tone}>{trafficLight.label}</StatusPill>
              </div>
            </div>

            <div className="review-card-metrics">
              <Metric label="Score" value={String(Math.round(Number(assistant.reviewActionScore || 0)))} tone={assistant.reviewActionBand === 'A' ? 'danger' : assistant.reviewActionBand === 'B' ? 'warn' : 'info'} />
              <Metric label="Klasse" value={assistant.recommendationClass} tone={assistant.recommendationClass === 'Fast bereit' ? 'good' : assistant.recommendationClass === 'Unsicher' ? 'warn' : 'danger'} />
              <Metric label="Band" value={assistant.reviewActionBand || 'C'} tone={assistant.reviewActionBand === 'A' ? 'danger' : assistant.reviewActionBand === 'B' ? 'warn' : 'info'} />
            </div>

            <div className="review-card-metrics">
              <Metric label="Vertrauen" value={scorePercent(item.trust_before)} tone={item.trust_before >= 0.65 ? 'good' : 'warn'} />
              <Metric label="Evidenzqualität" value={scorePercent(evidenceQuality)} tone={evidenceQuality >= 0.66 ? 'good' : evidenceQuality >= 0.45 ? 'warn' : 'danger'} />
              <Metric label="Risiko" value={risk.label} tone={risk.tone} />
              <Metric label="Priorität" value={statusDeutsch(item.priority)} tone={priorityTone(item.priority)} />
            </div>

            <div className="review-cleartext">
              <p><strong>Empfehlung:</strong> {assistant.recommendation}</p>
              <p><strong>Warum jetzt?</strong> {assistant.whyNow}</p>
              <p><strong>Fehlt:</strong> {assistant.missingEvidence.join(' · ')}</p>
              <p><strong>Nächster Schritt:</strong> {assistant.nextStep}</p>
            </div>

            {feedback ? (
              <div className={`review-inline-feedback ${toneClass(feedback.tone)}`}>
                <strong>{feedback.title}</strong>
                <p>{feedback.message}</p>
              </div>
            ) : null}

            {pendingAction ? (
              <div className="review-inline-confirmation">
                <strong>{pendingAction.label} wirklich ausführen?</strong>
                <p><b>Thema:</b> {item.title}</p>
                <p><b>Hermes Empfehlung:</b> {assistant.recommendation}</p>
                <p><b>Safety-Hinweis:</b> no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false</p>
                <label className="review-inline-note">
                  <span>Notiz für Learning Feedback</span>
                  <textarea
                    rows={3}
                    value={reviewNotesById[item.review_id] || ''}
                    onChange={(event) => setReviewNotesById((current) => ({ ...current, [item.review_id]: event.target.value }))}
                    placeholder={`${pendingAction.label} via UI review`}
                  />
                </label>
                <div className="review-inline-actions">
                  <button
                    className={recommendedActionKey === pendingReviewAction.actionKey ? 'is-recommended' : ''}
                    disabled={actionBusyId === item.review_id}
                    onClick={() => runReviewAction(pendingReviewAction.actionKey, item)}
                    type="button"
                  >
                    {actionBusyId === item.review_id ? 'Speichere...' : 'Bestätigen'}
                  </button>
                  <button disabled={actionBusyId === item.review_id} onClick={() => cancelReviewAction(item.review_id)} type="button">
                    Abbrechen
                  </button>
                </div>
              </div>
            ) : (
              <div className="review-action-row" aria-label="Vorbereitete Prüfaktionen">
                <button className={recommendedActionKey === 'approve' ? 'is-recommended' : ''} disabled={actionBusyId === item.review_id} onClick={() => openReviewAction('approve', item)} type="button">Freigeben</button>
                <button className={recommendedActionKey === 'more' ? 'is-recommended' : ''} disabled={actionBusyId === item.review_id} onClick={() => openReviewAction('more', item)} type="button">Mehr Evidenz</button>
                <button className={recommendedActionKey === 'reject' ? 'is-recommended' : ''} disabled={actionBusyId === item.review_id} onClick={() => openReviewAction('reject', item)} type="button">Ablehnen</button>
                <button disabled={actionBusyId === item.review_id} onClick={() => openReviewAction('defer', item)} type="button">Zurückstellen</button>
              </div>
            )}
          </article>
          );
        })}

        {visibleItems.length === 0 ? (
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
                {step.warnings.length ? 'Hermes arbeitet daran' : 'Alles ok'}
              </StatusPill>
            </div>
            <OperatorSummary code={step.status} />
            <Metric label="Letzte Aktivität" value={shortDateTime(step.last_activity)} />
            <Metric label="Nächster Schritt" value={translateOperatorCode(step.next_step).title} />
            {step.warnings.length ? (
              <div className="operator-warning-list">
                {step.warnings.slice(0, 4).map((warning) => (
                  <span key={warning}>{translateOperatorCode(warning).title}</span>
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
        <Metric label="Offene Prüfungen" value={formatNumber(operatorState.humanReview?.pending_reviews || 0)} tone={operatorState.humanReview?.pending_reviews ? 'warn' : 'good'} />
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
              <span key={blocker}>{translateOperatorCode(blocker).title}</span>
            ))}
          </div>
        </article>
        <article className="control-mini-panel">
          <h3>Prüfprioritäten</h3>
          <div className="operator-warning-list">
            {masterStatus.top_review_priorities.slice(0, 8).map((priority) => (
              <span key={priority}>{translateOperatorCode(priority).title}</span>
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
            <Metric label="Nächste Aufgabe" value={translateOperatorCode(domain.next_recommended_task).title} tone="info" />
            <div className="operator-token-list">
              {domain.open_needs.slice(0, 6).map((need) => (
                <span key={need}>{translateOperatorCode(need).title}</span>
              ))}
              {domain.open_needs.length === 0 ? <span>Keine offenen Needs gemeldet</span> : null}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function SystemView({ operatorState }) {
  return (
    <section className="control-view-panel" aria-label="System">
      <div className="control-view-head">
        <div>
          <p className="eyebrow">Systemüberblick</p>
          <h2>System</h2>
        </div>
        <StatusPill tone={operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good'}>
          {operatorState.resource.should_stop ? 'Frank muss entscheiden' : operatorState.resource.should_pause ? 'Hermes arbeitet daran' : 'Alles ok'}
        </StatusPill>
      </div>

      <OperatorSummary code={operatorState.supervisor.status || operatorState.resource.action} />
      <div className="trust-summary-grid">
        <Metric label="CPU" value={`${Math.round(operatorState.resource.cpu_usage_percent)}%`} tone={operatorState.resource.should_stop ? 'danger' : operatorState.resource.should_pause ? 'warn' : 'good'} />
        <Metric label="RAM" value={`${Math.round(operatorState.resource.memory_usage_percent)}%`} tone={operatorState.resource.should_stop ? 'danger' : 'good'} />
        <Metric label="Speicherplatz" value={formatGb(operatorState.storage.free_disk_gb)} tone={operatorState.storage.errors.length ? 'warn' : 'good'} />
        <Metric label="Planer" value={`${formatNumber(operatorState.schedulerJobs.filter((job) => job.enabled).length)} aktiv`} tone="info" />
        <Metric label="Aufsicht" value={operatorState.supervisor.running ? 'läuft' : 'gestoppt'} tone={operatorState.supervisor.running ? 'good' : 'warn'} />
        <Metric label="Nachtlauf" value={translateOperatorCode(operatorState.nightly.current_state).title} tone={toneFromStatus(operatorState.nightly.current_state)} />
      </div>

      <div className="control-split-grid">
        <article className="control-mini-panel">
          <h3>Systemhinweise</h3>
          <div className="operator-warning-list">
            {(operatorState.resource.warnings || []).slice(0, 6).map((warning) => (
              <span key={warning}>{translateOperatorCode(warning).title}</span>
            ))}
            {(operatorState.resource.warnings || []).length === 0 ? <span>Keine aktuellen Systemhinweise.</span> : null}
          </div>
        </article>
        <article className="control-mini-panel">
          <h3>Nächste Aktion</h3>
          <div className="operator-warning-list">
            <span>{translateOperatorCode(operatorState.resource.next_action || operatorState.supervisor.next_action).meaning || 'Keine Aktion geplant.'}</span>
          </div>
        </article>
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
            <Metric label="Ergebnis" value={translateOperatorCode(role.result).title} />
            {role.warnings.length ? (
              <div className="operator-warning-list">
                {role.warnings.map((warning) => (
                  <span key={warning}>{translateOperatorCode(warning).title}</span>
                ))}
              </div>
            ) : null}
          </article>
        ))}
      </div>
    </section>
  );
}

function CommandModuleDetails({ moduleId, operatorState, onRefresh }) {
  if (moduleId === 'trading') {
    return <DashboardTradingIntelligence operatorState={operatorState} />;
  }

  if (moduleId === 'signal-package') {
    return <DashboardSignalPackage operatorState={operatorState} />;
  }

  if (moduleId === 'review') {
    return <HumanReviewCenter operatorState={operatorState} onRefresh={onRefresh} />;
  }

  if (moduleId === 'learning') {
    return <CognitiveCenter operatorState={operatorState} />;
  }

  if (moduleId === 'self-improvement') {
    return <DashboardLearningSummary operatorState={operatorState} />;
  }

  if (moduleId === 'trust') {
    return <KnowledgeTrustView operatorState={operatorState} />;
  }

  if (moduleId === 'system') {
    return <SystemView operatorState={operatorState} />;
  }

  if (moduleId === 'time-control') {
    return <DashboardTimeControl operatorState={operatorState} onRefresh={onRefresh} />;
  }

  if (moduleId === 'roles') {
    return <RoleView operatorState={operatorState} />;
  }

  if (moduleId === 'safety') {
    return <DashboardSafety operatorState={operatorState} />;
  }

  if (moduleId === 'storage') {
    return <DashboardStorageResources operatorState={operatorState} />;
  }

  if (moduleId === 'reports') {
    return <ReportsActionCards operatorState={operatorState} />;
  }

  if (moduleId === 'logs') {
    return <DashboardLogs operatorState={operatorState} />;
  }

  return null;
}

function DetailOverlay({ moduleId, modules, operatorState, onRefresh, onClose }) {
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
            <p className="eyebrow">Subsystem</p>
            <h2 id="cockpit-detail-title">{module.title}</h2>
            <p className="control-view-note">{module.detail}</p>
          </div>
          <button className="cockpit-close-button" onClick={onClose} type="button">Schließen</button>
        </div>

        <ViewErrorBoundary>
          <CommandModuleDetails moduleId={moduleId} operatorState={operatorState} onRefresh={onRefresh} />
        </ViewErrorBoundary>

        <div className="cockpit-detail-grid">
          {moduleId === 'open_supervisor' ? (
            <>
              <Metric label="Status" value={translateOperatorCode(operatorState.supervisor.status).title} tone={module.tone} />
              <Metric label="Bedeutung" value={translateOperatorCode(operatorState.supervisor.status).meaning} tone="info" />
              <Metric label="Hermes arbeitet an" value={translateOperatorCode(operatorState.supervisor.status).whatHermesDoes} tone="info" />
              <Metric label="Aktion für Frank" value={translateOperatorCode(operatorState.supervisor.status).franksAction} tone={translateOperatorCode(operatorState.supervisor.status).franksAction !== 'Nein' ? 'warn' : 'good'} />
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
              <Metric label="Status" value={translateOperatorCode(operatorState.nightly.current_state).title} tone={module.tone} />
              <Metric label="Bedeutung" value={translateOperatorCode(operatorState.nightly.current_state).meaning} tone="info" />
              <Metric label="Hermes arbeitet an" value={translateOperatorCode(operatorState.nightly.current_state).whatHermesDoes} tone="info" />
              <Metric label="Aktion für Frank" value={translateOperatorCode(operatorState.nightly.current_state).franksAction} tone={translateOperatorCode(operatorState.nightly.current_state).franksAction !== 'Nein' ? 'warn' : 'good'} />
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
              <Metric label="Status" value={translateOperatorCode(operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : operatorState.storage.status).title} tone={module.tone} />
              <Metric label="Bedeutung" value={translateOperatorCode(operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : operatorState.storage.status).meaning} tone="info" />
              <Metric label="Hermes arbeitet an" value={translateOperatorCode(operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : operatorState.storage.status).whatHermesDoes} tone="info" />
              <Metric label="Aktion für Frank" value={translateOperatorCode(operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : operatorState.storage.status).franksAction} tone={translateOperatorCode(operatorState.storage.cleanup_candidate_count ? 'storage_cleanup_candidates' : operatorState.storage.status).franksAction !== 'Nein' ? 'warn' : 'good'} />
              <Metric label="Freier Speicher" value={formatGb(operatorState.storage.free_disk_gb)} tone="good" />
              <Metric label="Cleanup-Kandidaten" value={formatNumber(operatorState.storage.cleanup_candidate_count)} tone="warn" />
            </>
          ) : null}

          {moduleId === 'open_resources' ? (
            <>
              <Metric label="CPU" value={`${Math.round(operatorState.resource.cpu_usage_percent)}%`} />
              <Metric label="RAM" value={`${Math.round(operatorState.resource.memory_usage_percent)}%`} />
              <Metric label="Disk frei" value={formatGb(operatorState.resource.free_disk_gb)} tone="good" />
              <Metric label="Aktion" value={translateOperatorCode(operatorState.resource.action || 'continue').title} tone={module.tone} />
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
            <>
              <Metric label="Status" value="Keine Warnungen gemeldet" tone="good" />
              <Metric label="Bedeutung" value="Das System läuft ohne neue Warnungen." tone="good" />
              <Metric label="Hermes arbeitet an" value="Keine aktive Störungsbearbeitung erforderlich." tone="good" />
              <Metric label="Aktion für Frank" value="Nein" tone="good" />
            </>
          ) : null}
        </div>

        <details className="cockpit-report-preview">
          <summary className="cockpit-report-head">
            <span>Technische Details anzeigen</span>
            <StatusPill tone={sourceTone(operatorState.dataSource)}>
              {sourceModeLabel(operatorState.dataSource)}
            </StatusPill>
          </summary>
          <pre>{detailPreview(moduleId, operatorState)}</pre>
        </details>
      </section>
    </div>
  );
}

function detailPreview(moduleId, operatorState) {
  const map = {
    trading: 'ensemblePortfolioStatus',
    'signal-package': 'validateEnsembleSignalPackage',
    learning: 'researchInsights',
    trust: 'knowledgeQuality',
    review: 'humanReviewState',
    system: 'supervisorState',
    'time-control': 'timeControl',
    roles: 'roleStatus',
    safety: 'masterStatus',
    reports: 'validateEnsembleSignalPackage',
    storage: 'cleanupPlan',
    logs: 'nightlyState',
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

  if (moduleId === 'open_safety' || moduleId === 'safety') {
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
  const commandModules = useMemo(() => buildCommandCenterModules(operatorState), [operatorState]);
  const fixtureActive = operatorState.dataSource !== DATA_SOURCE.LIVE_FILE;

  return (
    <section className="cockpit-shell" aria-label="Jarvis Cockpit Hauptansicht">
      <CommandCenterStatusBar operatorState={operatorState} />

      {fixtureActive ? (
        <p className="cockpit-warning">
          Bridge ist nicht aktiv. Starte: cd ~/jarvis/HermesRuntime && dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge
        </p>
      ) : null}

      <FrankActionCenter operatorState={operatorState} onOpen={setActiveModule} />

      <HudCommandGrid
        operatorState={operatorState}
        modules={commandModules}
        onOpen={setActiveModule}
      />

      <DetailOverlay
        moduleId={activeModule}
        modules={[...commandModules, ...modules]}
        operatorState={operatorState}
        onRefresh={refreshOperatorState}
        onClose={() => setActiveModule('')}
      />
    </section>
  );
}
