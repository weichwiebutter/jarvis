import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(projectDir, '../..');
const runtimeHealthPath = resolve(repoRoot, 'HermesRuntime/data/reports/runtime_health.json');
const runtimeEventsPath = resolve(repoRoot, 'HermesRuntime/data/events/runtime');
const replayManifestPath = resolve(repoRoot, 'HermesRuntime/data/replays/manifests');

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

const latestReplayManifestPath = findLatestReplayManifest();

export default defineConfig({
  plugins: [react()],
  define: {
    __HERMES_RUNTIME_HEALTH_URL__: JSON.stringify(`/@fs/${runtimeHealthPath}`),
    __HERMES_RUNTIME_EVENTS_BASE_URL__: JSON.stringify(`/@fs/${runtimeEventsPath}`),
    __HERMES_REPLAY_MANIFEST_URL__: JSON.stringify(
      latestReplayManifestPath ? `/@fs/${latestReplayManifestPath}` : '',
    ),
    __HERMES_RUNTIME_HEALTH_PATH__: JSON.stringify(
      'HermesRuntime/data/reports/runtime_health.json',
    ),
  },
  server: {
    fs: {
      allow: [projectDir, repoRoot],
    },
  },
});
