import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(projectDir, '../..');
const hermesDataRoot = process.env.HERMES_DATA_ROOT || '/mnt/d/HermesData';
const bridgeBaseUrl =
  process.env.HERMES_READONLY_BRIDGE_URL ||
  process.env.VITE_HERMES_READONLY_BRIDGE_URL ||
  'http://127.0.0.1:8787';
const normalizedBridgeBaseUrl = bridgeBaseUrl.replace(/\/+$/, '');

function bridgeUrl(endpoint) {
  return `${normalizedBridgeBaseUrl}${endpoint}`;
}

const operatorReports = {
  masterStatus: {
    label: 'Hermes Gesamtstatus',
    url: bridgeUrl('/reports/master-status'),
    path: 'read-only bridge:/reports/master-status',
  },
  humanReviewQueue: {
    label: 'Prüfwarteschlange',
    url: bridgeUrl('/reports/human-review-queue'),
    path: 'read-only bridge:/reports/human-review-queue',
  },
  knowledgeValidationAudit: {
    label: 'Knowledge Validation Audit',
    url: bridgeUrl('/reports/knowledge-validation-audit'),
    path: 'read-only bridge:/reports/knowledge-validation-audit',
  },
  validationBacklogAnalyzer: {
    label: 'Validation Backlog Analyzer',
    url: bridgeUrl('/reports/validation-backlog-analyzer'),
    path: 'read-only bridge:/reports/validation-backlog-analyzer',
  },
  knowledgeConsolidationAnalyzer: {
    label: 'Knowledge Consolidation Analyzer',
    url: bridgeUrl('/reports/knowledge-consolidation-analyzer'),
    path: 'read-only bridge:/reports/knowledge-consolidation-analyzer',
  },
  knowledgeConsolidationExecutor: {
    label: 'Knowledge Consolidation Executor',
    url: bridgeUrl('/reports/knowledge-consolidation-executor'),
    path: 'read-only bridge:/reports/knowledge-consolidation-executor',
  },
  strategyMutationAnalyzer: {
    label: 'Strategy Mutation Analyzer',
    url: bridgeUrl('/reports/strategy-mutation-analyzer'),
    path: 'read-only bridge:/reports/strategy-mutation-analyzer',
  },
  strategyParameterResearchPlanner: {
    label: 'Strategy Parameter Research Planner',
    url: bridgeUrl('/reports/strategy-parameter-research-planner'),
    path: 'read-only bridge:/reports/strategy-parameter-research-planner',
  },
  tradingResearchSynthesizer: {
    label: 'Trading Research Synthesizer',
    url: bridgeUrl('/reports/trading-research-synthesizer'),
    path: 'read-only bridge:/reports/trading-research-synthesizer',
  },
  strategyMutationValidationPlanner: {
    label: 'Strategy Mutation Validation Planner',
    url: bridgeUrl('/reports/strategy-mutation-validation-planner'),
    path: 'read-only bridge:/reports/strategy-mutation-validation-planner',
  },
  validationBacklogExecutor: {
    label: 'Validation Backlog Executor',
    url: bridgeUrl('/reports/validation-backlog-executor'),
    path: 'read-only bridge:/reports/validation-backlog-executor',
  },
  reviewStatusConsistencyAudit: {
    label: 'Review Status Consistency Audit',
    url: bridgeUrl('/reports/review-status-consistency-audit'),
    path: 'read-only bridge:/reports/review-status-consistency-audit',
  },
  cognitiveStatus: {
    label: 'Hermes Gehirn Status',
    url: bridgeUrl('/reports/cognitive-status'),
    path: 'read-only bridge:/reports/cognitive-status',
  },
  planningStatus: {
    label: 'Planungsstatus',
    url: bridgeUrl('/reports/planning-status'),
    path: 'read-only bridge:/reports/planning-status',
  },
  taskExecutionState: {
    label: 'Aufgabenausfuehrung',
    url: bridgeUrl('/reports/task-execution-state'),
    path: 'read-only bridge:/reports/task-execution-state',
  },
  autonomousLoopState: {
    label: 'Autonomer Lernzyklus',
    url: bridgeUrl('/reports/autonomous-loop-state'),
    path: 'read-only bridge:/reports/autonomous-loop-state',
  },
  metaReview: {
    label: 'Lernanalyse',
    url: bridgeUrl('/reports/meta-review'),
    path: 'read-only bridge:/reports/meta-review',
  },
  domainStatus: {
    label: 'Domänenstatus',
    url: bridgeUrl('/reports/domain-status'),
    path: 'read-only bridge:/reports/domain-status',
  },
  researchInsights: {
    label: 'Research Insights',
    url: bridgeUrl('/reports/research-insights'),
    path: 'read-only bridge:/reports/research-insights',
  },
  robustStrategies: {
    label: 'Robuste Strategien',
    url: bridgeUrl('/reports/robust-strategies'),
    path: 'read-only bridge:/reports/robust-strategies',
  },
  overfitReport: {
    label: 'Overfit Report',
    url: bridgeUrl('/reports/overfit-report'),
    path: 'read-only bridge:/reports/overfit-report',
  },
  regimeSummary: {
    label: 'Regime Summary',
    url: bridgeUrl('/reports/regime-summary'),
    path: 'read-only bridge:/reports/regime-summary',
  },
  strategyRegimePerformance: {
    label: 'Strategy Regime Performance',
    url: bridgeUrl('/reports/strategy-regime-performance'),
    path: 'read-only bridge:/reports/strategy-regime-performance',
  },
  regimeDistribution: {
    label: 'Regime Distribution',
    url: bridgeUrl('/reports/regime-distribution'),
    path: 'read-only bridge:/reports/regime-distribution',
  },
  supervisorState: {
    label: 'Supervisor State',
    url: bridgeUrl('/runtime/supervisor'),
    path: 'read-only bridge:/runtime/supervisor',
  },
  schedulerState: {
    label: 'Scheduler State',
    url: bridgeUrl('/runtime/scheduler'),
    path: 'read-only bridge:/runtime/scheduler',
  },
  timeControl: {
    label: 'Zeitsteuerung',
    url: bridgeUrl('/reports/time-control'),
    path: 'read-only bridge:/reports/time-control',
  },
  resourceStatus: {
    label: 'Resource Status',
    url: bridgeUrl('/runtime/resource'),
    path: 'read-only bridge:/runtime/resource',
  },
  storageStatus: {
    label: 'Storage Status',
    url: bridgeUrl('/runtime/storage'),
    path: 'read-only bridge:/runtime/storage',
  },
  cleanupPlan: {
    label: 'Cleanup Plan',
    url: bridgeUrl('/runtime/cleanup-plan'),
    path: 'read-only bridge:/runtime/cleanup-plan',
  },
  nightlyState: {
    label: 'Nightly State',
    url: bridgeUrl('/runtime/nightly'),
    path: 'read-only bridge:/runtime/nightly',
  },
  demoSignalFeedStatus: {
    label: 'Demo Signal Feed Status',
    url: bridgeUrl('/reports/demo-signal-feed-status'),
    path: 'read-only bridge:/reports/demo-signal-feed-status',
  },
  latestDemoSignals: {
    label: 'Latest Demo Signals',
    url: bridgeUrl('/reports/latest-demo-signals'),
    path: 'read-only bridge:/reports/latest-demo-signals',
  },
  forwardTestStatus: {
    label: 'Forward Test Status',
    url: bridgeUrl('/reports/forward-test-status'),
    path: 'read-only bridge:/reports/forward-test-status',
  },
  trustedKnowledgeReviewGate: {
    label: 'Trusted Knowledge Review Gate',
    url: bridgeUrl('/reports/trusted-knowledge-review-gate'),
    path: 'read-only bridge:/reports/trusted-knowledge-review-gate',
  },
  knowledgeTrustImprovementPlan: {
    label: 'Knowledge Trust Improvement Plan',
    url: bridgeUrl('/reports/knowledge-trust-improvement-plan'),
    path: 'read-only bridge:/reports/knowledge-trust-improvement-plan',
  },
  autonomousImprovementQueue: {
    label: 'Autonomous Improvement Queue',
    url: bridgeUrl('/reports/autonomous-improvement-queue'),
    path: 'read-only bridge:/reports/autonomous-improvement-queue',
  },
  autonomousImprovementQueueSummary: {
    label: 'Autonomous Improvement Queue Summary',
    url: bridgeUrl('/reports/autonomous-improvement-queue-summary'),
    path: 'read-only bridge:/reports/autonomous-improvement-queue-summary',
  },
  autonomousImprovementWorkAreas: {
    label: 'Autonomous Improvement Work Areas',
    url: bridgeUrl('/reports/autonomous-improvement-work-areas'),
    path: 'read-only bridge:/reports/autonomous-improvement-work-areas',
  },
  workAreaExecutorPolicy: {
    label: 'Work Area Executor Policy',
    url: bridgeUrl('/reports/work-area-executor-policy'),
    path: 'read-only bridge:/reports/work-area-executor-policy',
  },
  nightlyWorkAreaStatus: {
    label: 'Nightly Work Area Status',
    url: bridgeUrl('/reports/nightly-work-area-status'),
    path: 'read-only bridge:/reports/nightly-work-area-status',
  },
  autonomousImprovementExecution: {
    label: 'Autonomous Improvement Execution',
    url: bridgeUrl('/reports/autonomous-improvement-execution'),
    path: 'read-only bridge:/reports/autonomous-improvement-execution',
  },
  ensemblePortfolioStatus: {
    label: 'Ensemble Portfolio Status',
    url: bridgeUrl('/reports/ensemble-portfolio-status'),
    path: 'read-only bridge:/reports/ensemble-portfolio-status',
  },
  systemBHandoffBundle: {
    label: 'System B Handoff Bundle',
    url: bridgeUrl('/reports/system-b-handoff-bundle'),
    path: 'read-only bridge:/reports/system-b-handoff-bundle',
  },
  validateEnsembleSignalPackage: {
    label: 'Validate Ensemble Signal Package',
    url: bridgeUrl('/reports/validate-ensemble-signal-package'),
    path: 'read-only bridge:/reports/validate-ensemble-signal-package',
  },
  setupRegistry: {
    label: 'Setup Registry',
    url: bridgeUrl('/reports/setup-registry'),
    path: 'read-only bridge:/reports/setup-registry',
  },
  signalAgentSpecs: {
    label: 'Signal Agent Specs',
    url: bridgeUrl('/reports/signal-agent-specs'),
    path: 'read-only bridge:/reports/signal-agent-specs',
  },
  multiAssetResearchStatus: {
    label: 'Multi-Asset Research Status',
    url: bridgeUrl('/reports/multi-asset-research-status'),
    path: 'read-only bridge:/reports/multi-asset-research-status',
  },
};

export default defineConfig({
  plugins: [react()],
  define: {
    __HERMES_READONLY_BRIDGE_URL__: JSON.stringify(normalizedBridgeBaseUrl),
    __HERMES_OPERATOR_DASHBOARD_URL__: JSON.stringify(bridgeUrl('/operator/dashboard')),
    __HERMES_RUNTIME_HEALTH_URL__: JSON.stringify(bridgeUrl('/runtime/health')),
    __HERMES_RUNTIME_EVENTS_BASE_URL__: JSON.stringify(''),
    __HERMES_RUNTIME_JOBS_URL__: JSON.stringify(''),
    __HERMES_REPLAY_MANIFEST_URL__: JSON.stringify(''),
    __HERMES_FEATURE_EXPORT_URL__: JSON.stringify(''),
    __HERMES_SIGNAL_EXPORT_URL__: JSON.stringify(''),
    __HERMES_BACKTEST_REPORT_URL__: JSON.stringify(''),
    __HERMES_OUTCOME_REPORT_URL__: JSON.stringify(''),
    __HERMES_BETA_REPORT_URL__: JSON.stringify(''),
    __HERMES_SETUP_WATCH_URL__: JSON.stringify(bridgeUrl('/runtime/setup-watch')),
    __HERMES_RUNTIME_HEALTH_PATH__: JSON.stringify('read-only bridge:/runtime/health'),
    __HERMES_RUNTIME_JOBS_PATH__: JSON.stringify('read-only bridge:/runtime/jobs'),
    __HERMES_FEATURE_EXPORT_PATH__: JSON.stringify(''),
    __HERMES_SIGNAL_EXPORT_PATH__: JSON.stringify(''),
    __HERMES_BACKTEST_REPORT_PATH__: JSON.stringify(''),
    __HERMES_OUTCOME_REPORT_PATH__: JSON.stringify(''),
    __HERMES_BETA_REPORT_PATH__: JSON.stringify(''),
    __HERMES_SETUP_WATCH_PATH__: JSON.stringify('read-only bridge:/runtime/setup-watch'),
    __HERMES_DATA_ROOT__: JSON.stringify(hermesDataRoot),
    __HERMES_OPERATOR_REPORTS__: JSON.stringify(operatorReports),
    __HERMES_SUPERVISOR_LOG_URL__: JSON.stringify(''),
    __HERMES_SUPERVISOR_LOG_PATH__: JSON.stringify('read-only bridge:logs unavailable in v1'),
  },
  server: {
    fs: {
      allow: [projectDir, repoRoot],
    },
  },
});
