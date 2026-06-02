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
    label: 'Hermes Master Status',
    url: bridgeUrl('/reports/master-status'),
    path: 'read-only bridge:/reports/master-status',
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
