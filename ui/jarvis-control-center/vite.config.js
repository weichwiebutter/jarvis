import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(projectDir, '../..');
const runtimeHealthPath = resolve(repoRoot, 'HermesRuntime/data/reports/runtime_health.json');
const runtimeEventsPath = resolve(repoRoot, 'HermesRuntime/data/events/runtime');
const runtimeJobsPath = resolve(repoRoot, 'HermesRuntime/data/jobs/jobs.index.json');
const replayManifestPath = resolve(repoRoot, 'HermesRuntime/data/replays/manifests');
const setupWatchPath = resolve(repoRoot, 'HermesRuntime/data/setup_watch/setup_watch.json');
const featureExportsPath = resolve(repoRoot, 'HermesRuntime/data/exports/features');
const signalExportsPath = resolve(repoRoot, 'HermesRuntime/data/exports/signals');
const backtestReportsPath = resolve(repoRoot, 'HermesRuntime/data/reports/backtests');

function findLatestReplayManifest() {
  try {
    const manifest = readdirSync(replayManifestPath)
      .filter((fileName) => fileName.endsWith('.manifest.json'))
      .sort()
      .at(-1);

    return manifest ? resolve(replayManifestPath, manifest) : '';
  } catch {
    return '';
  }
}

function findLatestExportFile(directory, suffix) {
  try {
    const fileName = readdirSync(directory)
      .filter((file) => file.endsWith(suffix))
      .sort()
      .at(-1);

    return fileName ? resolve(directory, fileName) : '';
  } catch {
    return '';
  }
}

const latestReplayManifestPath = findLatestReplayManifest();
const latestFeatureExportPath = findLatestExportFile(featureExportsPath, '.features.jsonl');
const latestSignalExportPath = findLatestExportFile(signalExportsPath, '.signals.jsonl');
const latestBacktestReportPath = findLatestExportFile(backtestReportsPath, '.backtest.json');

export default defineConfig({
  plugins: [react()],
  define: {
    __HERMES_RUNTIME_HEALTH_URL__: JSON.stringify(`/@fs/${runtimeHealthPath}`),
    __HERMES_RUNTIME_EVENTS_BASE_URL__: JSON.stringify(`/@fs/${runtimeEventsPath}`),
    __HERMES_RUNTIME_JOBS_URL__: JSON.stringify(`/@fs/${runtimeJobsPath}`),
    __HERMES_REPLAY_MANIFEST_URL__: JSON.stringify(
      latestReplayManifestPath ? `/@fs/${latestReplayManifestPath}` : '',
    ),
    __HERMES_FEATURE_EXPORT_URL__: JSON.stringify(
      latestFeatureExportPath ? `/@fs/${latestFeatureExportPath}` : '',
    ),
    __HERMES_SIGNAL_EXPORT_URL__: JSON.stringify(
      latestSignalExportPath ? `/@fs/${latestSignalExportPath}` : '',
    ),
    __HERMES_BACKTEST_REPORT_URL__: JSON.stringify(
      latestBacktestReportPath ? `/@fs/${latestBacktestReportPath}` : '',
    ),
    __HERMES_SETUP_WATCH_URL__: JSON.stringify(`/@fs/${setupWatchPath}`),
    __HERMES_RUNTIME_HEALTH_PATH__: JSON.stringify(
      'HermesRuntime/data/reports/runtime_health.json',
    ),
    __HERMES_RUNTIME_JOBS_PATH__: JSON.stringify('HermesRuntime/data/jobs/jobs.index.json'),
    __HERMES_FEATURE_EXPORT_PATH__: JSON.stringify(
      latestFeatureExportPath
        ? `HermesRuntime/data/exports/features/${latestFeatureExportPath.split('/').at(-1)}`
        : '',
    ),
    __HERMES_SIGNAL_EXPORT_PATH__: JSON.stringify(
      latestSignalExportPath
        ? `HermesRuntime/data/exports/signals/${latestSignalExportPath.split('/').at(-1)}`
        : '',
    ),
    __HERMES_BACKTEST_REPORT_PATH__: JSON.stringify(
      latestBacktestReportPath
        ? `HermesRuntime/data/reports/backtests/${latestBacktestReportPath.split('/').at(-1)}`
        : '',
    ),
    __HERMES_SETUP_WATCH_PATH__: JSON.stringify('HermesRuntime/data/setup_watch/setup_watch.json'),
  },
  server: {
    fs: {
      allow: [projectDir, repoRoot],
    },
  },
});
