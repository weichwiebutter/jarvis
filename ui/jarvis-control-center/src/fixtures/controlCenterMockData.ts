import { de as t } from '../i18n/de';

export const learningCandidates = [
  {
    id: 'learn-trading-xauusd-pullback',
    type: 'Trading-Setup',
    title: 'XAUUSD Pullback als Lernkandidat',
    description:
      'Setup-Beobachtung war konsistent mit Pullback-Rejection und soll fuer spaetere Bewertung markiert werden.',
    source: 'Setup-Beobachtung / Prediction-Feedback',
    risk: 'high',
    status: 'review',
    action: 'Nur nach Ergebnisvergleich und Freigabe als Regelkandidat speichern.',
  },
  {
    id: 'learn-routing-local-worker',
    type: 'Routing-Hinweis',
    title: 'Lokaler Worker fuer kleine UI-Aufgaben',
    description:
      'Wiederkehrende kleine React-Textanpassungen koennten spaeter bevorzugt lokal geroutet werden.',
    source: 'Codex Routing Beobachtung',
    risk: 'medium',
    status: 'open',
    action: 'Als Routing-Hypothese vormerken, nicht automatisch aktivieren.',
  },
  {
    id: 'learn-error-pattern-file-access',
    type: 'Fehlerpattern',
    title: 'Browser blockiert lokale Runtime-Dateien',
    description:
      'Read-only Bridge oder lokale Runtime-Reports koennen fehlen; Fixture-Fallback muss sichtbar bleiben.',
    source: 'Laufzeitstatus-Loader',
    risk: 'low',
    status: 'approved',
    action: 'Als UI-Hinweis behalten und bei echten Connectors erneut pruefen.',
  },
  {
    id: 'learn-skill-proposal-storage',
    type: 'Skill-Vorschlag',
    title: 'Storage-Retention Dry-Run Skill',
    description:
      'Spaeterer Skill koennte nur lesend Datenklassen scannen und Cleanup-Plaene als Vorschlag zeigen.',
    source: 'Speicher-Retention-Policy',
    risk: 'high',
    status: 'rejected',
    action: 'Nicht aktivieren; erst nach Approval-Flow und Dry-Run-UI erneut bewerten.',
  },
];

export const runtimeEvents = [
  {
    id: 'runtime-started',
    time: '08:00:03 UTC',
    eventType: 'RuntimeStarted',
    category: 'runtime',
    severity: 'info',
    source: 'RuntimeHost',
    description: 'Hermes Runtime v1 wurde lokal gestartet und initialisiert.',
  },
  {
    id: 'storage-initialized',
    time: '08:00:05 UTC',
    eventType: 'StorageInitialized',
    category: 'runtime',
    severity: 'info',
    source: 'StorageManager',
    description: 'Storage-Pfade, Event Store und Snapshot-Verzeichnisse wurden geprueft.',
  },
  {
    id: 'snapshot-created',
    time: '08:00:11 UTC',
    eventType: 'SnapshotCreated',
    category: 'runtime',
    severity: 'info',
    source: 'SnapshotManager',
    description: 'Ein Runtime-Snapshot wurde geschrieben und mit Hash im Manifest referenziert.',
  },
  {
    id: 'replay-manifest-created',
    time: '08:00:15 UTC',
    eventType: 'ReplayManifestCreated',
    category: 'runtime',
    severity: 'info',
    source: 'ReplayManifestService',
    description: 'Demo-Replay-Manifest fuer spaetere Backtest- und Research-Laeufe ist verfuegbar.',
  },
  {
    id: 'setup-watch-created',
    time: '08:01:20 UTC',
    eventType: 'SetupWatchCreated',
    category: 'trading',
    severity: 'warning',
    source: 'SetupWatchService',
    description: 'XAUUSD Long-Szenario wurde als Beobachtung markiert; keine Orderausfuehrung.',
  },
  {
    id: 'learning-candidate-created',
    time: '08:02:44 UTC',
    eventType: 'LearningCandidateCreated',
    category: 'learning',
    severity: 'warning',
    source: 'LearningQueue',
    description: 'Prediction-Feedback wurde als Lernkandidat vorgemerkt und wartet auf Review.',
  },
  {
    id: 'risk-guard-blocked',
    time: '08:03:08 UTC',
    eventType: 'RiskGuardBlocked',
    category: 'trading',
    severity: 'critical',
    source: 'RiskGuard',
    description:
      'Trading-Aktion blockiert: Auto-Trading ist deaktiviert und menschliche Freigabe ist Pflicht.',
  },
  {
    id: 'runtime-stopped',
    time: '08:04:02 UTC',
    eventType: 'RuntimeStopped',
    category: 'runtime',
    severity: 'info',
    source: 'RuntimeHost',
    description: 'Runtime wurde sauber beendet; Event Store bleibt nur lesend im UI sichtbar.',
  },
];

export const eventLegend = [
  { label: t.eventTimeline.info, tone: 'info' },
  { label: t.eventTimeline.warning, tone: 'warn' },
  { label: t.eventTimeline.critical, tone: 'danger' },
  { label: t.eventTimeline.trading, tone: 'warn' },
  { label: t.eventTimeline.learning, tone: 'good' },
  { label: t.eventTimeline.runtime, tone: 'info' },
];

export const researchJobs = [
  {
    id: 'research-xauusd-overnight',
    name: 'Overnight Research XAUUSD',
    type: 'Backtest Sweep',
    status: 'running',
    detail: 'Trend Pullback / London-New-York Session',
    progress: 68,
  },
  {
    id: 'research-eurusd-walk-forward',
    name: 'EURUSD Walk-Forward',
    type: 'Walk-Forward Run',
    status: 'planned',
    detail: 'Mean Reversion Cluster mit OOS-Check',
    progress: 0,
  },
  {
    id: 'research-ger40-breakout',
    name: 'GER40 Breakout Replay',
    type: 'Replay Manifest',
    status: 'paused',
    detail: 'Pausiert wegen Spread-/Liquiditaetsfilter',
    progress: 42,
  },
  {
    id: 'research-us500-cluster',
    name: 'US500 Cluster Review',
    type: 'Cluster-Bewertung',
    status: 'completed',
    detail: 'Volatilitaetscluster fuer Review vorbereitet',
    progress: 100,
  },
  {
    id: 'research-xauusd-news',
    name: 'XAUUSD News-Markt Hypothese',
    type: 'Regime-Analyse',
    status: 'quarantined',
    detail: 'Ausreisser erkannt; keine Lernfreigabe',
    progress: 18,
  },
];

export const backtestRuns = [
  {
    id: 'bt-xauusd-trend-pullback',
    name: 'Trend Pullback v0.3',
    symbol: 'XAUUSD',
    period: '2024-01 bis 2025-12',
    marketRegime: 'Trendmarkt / hohe Volatilitaet',
    profitFactor: '1.84',
    winrate: '57%',
    maxDrawdown: '8.6%',
    status: 'completed',
    confidenceStability: 'stabil 0.72',
    outOfSample: 'bestanden',
  },
  {
    id: 'bt-eurusd-mean-reversion',
    name: 'Mean Reversion Session Filter',
    symbol: 'EURUSD',
    period: '2023-06 bis 2025-12',
    marketRegime: 'Seitwaertsmarkt / London',
    profitFactor: '1.31',
    winrate: '53%',
    maxDrawdown: '5.2%',
    status: 'paused',
    confidenceStability: 'mittel 0.58',
    outOfSample: 'in Review',
  },
  {
    id: 'bt-ger40-breakout',
    name: 'Opening Breakout Guarded',
    symbol: 'GER40',
    period: '2024-03 bis 2025-11',
    marketRegime: 'News-Markt / hohe Spreads',
    profitFactor: '0.94',
    winrate: '46%',
    maxDrawdown: '13.9%',
    status: 'quarantined',
    confidenceStability: 'instabil 0.41',
    outOfSample: 'fehlgeschlagen',
  },
];

export const researchArtifacts = [
  {
    id: 'walk-forward',
    label: 'Walk-Forward-Runs',
    value: '6',
    detail: '2 bestanden / 3 Review / 1 pausiert',
    tone: 'info',
  },
  {
    id: 'replay-manifests',
    label: 'Replay-Manifeste',
    value: '14',
    detail: 'nur Manifestdaten, kein Live-Replay',
    tone: 'good',
  },
  {
    id: 'feature-exports',
    label: 'Feature-Exporte',
    value: '9',
    detail: 'Feature Store Kandidaten fuer Review',
    tone: 'info',
  },
  {
    id: 'storage',
    label: t.backtestResearch.storageLoad,
    value: '38%',
    detail: 'aktive Runs auf SSD, Archiv spaeter auslagern',
    tone: 'warn',
  },
];

export const clusterScores = [
  { id: 'trend-pullback', label: 'Trend Pullback', score: '82', className: 'cluster-strong' },
  { id: 'breakout', label: 'Breakout', score: '61', className: 'cluster-medium' },
  { id: 'mean-reversion', label: 'Mean Reversion', score: '55', className: 'cluster-medium' },
  { id: 'news-market', label: 'News-Markt', score: '28', className: 'cluster-risk' },
];

export const regimeAnalyses = [
  {
    id: 'regime-trend',
    name: 'Trendmarkt',
    symbols: 'XAUUSD / US500',
    detail: 'Pullback-Setups zeigen die stabilste Confidence.',
    tone: 'good',
  },
  {
    id: 'regime-range',
    name: 'Seitwaertsmarkt',
    symbols: 'EURUSD',
    detail: 'Mean-Reversion nur mit Session- und Spread-Filter.',
    tone: 'info',
  },
  {
    id: 'regime-news',
    name: 'News-Markt',
    symbols: 'GER40 / XAUUSD',
    detail: 'Keine automatische Freigabe; RiskGuard bleibt aktiv.',
    tone: 'danger',
  },
];

export const storageSummary = {
  root: 'D:/HermesData',
  freeDiskGb: 888,
  totalDiskGb: 1200,
  usedPercent: 26,
  warningThreshold: '75%',
  criticalThreshold: '90%',
};

export const storageBuckets = [
  {
    id: 'cache',
    label: t.storageRetention.cache,
    path: 'HermesData/cache',
    used: '18 GB',
    percent: 8,
    tone: 'good',
    detail: t.storageRetention.tempCacheShort,
  },
  {
    id: 'events',
    label: t.storageRetention.events,
    path: 'HermesData/events',
    used: '42 GB',
    percent: 19,
    tone: 'info',
    detail: t.storageRetention.eventsCompress,
  },
  {
    id: 'snapshots',
    label: t.storageRetention.snapshots,
    path: 'HermesData/snapshots',
    used: '64 GB',
    percent: 29,
    tone: 'info',
    detail: t.storageRetention.snapshotsRotate,
  },
  {
    id: 'replays',
    label: t.storageRetention.replays,
    path: 'HermesData/replays',
    used: '180 GB',
    percent: 54,
    tone: 'warn',
    detail: 'Replay-Daten spaeter ins Archiv verschieben.',
  },
  {
    id: 'features',
    label: t.storageRetention.featureStore,
    path: 'HermesData/features',
    used: '96 GB',
    percent: 40,
    tone: 'info',
    detail: 'Approved Feature-Sets nur nach Review behalten.',
  },
  {
    id: 'backtests',
    label: t.storageRetention.backtestRuns,
    path: 'HermesData/backtests',
    used: '220 GB',
    percent: 62,
    tone: 'warn',
    detail: t.storageRetention.failedResearchLimit,
  },
  {
    id: 'archive',
    label: t.storageRetention.archive,
    path: 'HermesData/archive',
    used: '310 GB',
    percent: 68,
    tone: 'muted',
    detail: 'Langzeitdaten auf HDD/NAS auslagern.',
  },
];

export const retentionRules = [
  t.storageRetention.tempCacheShort,
  t.storageRetention.eventsCompress,
  t.storageRetention.snapshotsRotate,
  t.storageRetention.failedResearchLimit,
  t.storageRetention.approvedNeverDelete,
];

export const storageSafetyRules = [
  { label: t.storageRetention.diskGuardActive, tone: 'good' },
  { label: t.storageRetention.stopResearchCritical, tone: 'warn' },
  { label: t.storageRetention.safeModeStorage, tone: 'danger' },
  { label: t.storageRetention.emergencyTempOnly, tone: 'warn' },
];

export const hermesCliCommands = [
  {
    id: 'cli-health',
    command: 'hermes health',
    description: 'Runtime Health, Safety Flags und Queue-Zahlen anzeigen.',
    tone: 'good',
  },
  {
    id: 'cli-setup-watch',
    command: 'hermes setup-watch',
    description: 'Setup-Beobachtungen mit Entry, Stop, Ziel und Status lesen.',
    tone: 'warn',
  },
  {
    id: 'cli-events',
    command: 'hermes events recent',
    description: 'Letzte Runtime-Events aus lokalen JSONL-Dateien anzeigen.',
    tone: 'info',
  },
  {
    id: 'cli-jobs',
    command: 'hermes jobs',
    description: 'Queue-Status und Job-Manifeste read-only zusammenfassen.',
    tone: 'info',
  },
  {
    id: 'cli-storage',
    command: 'hermes storage',
    description: 'Lokale Storage-Verzeichnisse und freien Speicher anzeigen.',
    tone: 'warn',
  },
  {
    id: 'cli-version',
    command: 'hermes version',
    description: 'CLI- und Runtime-Projektversion anzeigen.',
    tone: 'muted',
  },
];

export const hermesCliOutputs = [
  {
    id: 'output-health',
    title: 'Runtime Health',
    command: 'hermes health',
    tone: 'good',
    lines: [
      'Runtime State: stopped',
      'Safe Mode: false',
      'no_auto_trading: true',
      'human_review_required: true',
      'Pending Jobs: 1',
      'Active Setup Watches: 2',
    ],
  },
  {
    id: 'output-setup-watch',
    title: 'Setup-Watch Status',
    command: 'hermes setup-watch',
    tone: 'warn',
    lines: [
      'XAUUSD long: watching, Confidence 68%',
      'EURUSD neutral: expired, Confidence 42%',
      'GER40 possible_breakout: armed, Confidence 57%',
      'Keine Orderausfuehrung',
    ],
  },
  {
    id: 'output-events',
    title: 'Letzte Events',
    command: 'hermes events recent',
    tone: 'info',
    lines: [
      'SetupWatchCreated / Info',
      'SetupWatchUpdated / Info',
      'SnapshotCreated / Info',
      'RuntimeStopped / Info',
    ],
  },
  {
    id: 'output-jobs',
    title: 'Job-Status',
    command: 'hermes jobs',
    tone: 'info',
    lines: [
      'Pending: 1',
      'Running: 0',
      'Completed: 8',
      'Failed: 0',
      'Quarantined: 0',
    ],
  },
  {
    id: 'output-storage',
    title: 'Storage-Status',
    command: 'hermes storage',
    tone: 'warn',
    lines: [
      'Data Root: HermesRuntime/data',
      'Free Disk: 887.88 GB',
      'Events: 121.83 KB',
      'Jobs: 7.94 KB',
    ],
  },
];

export const hermesCliSafetyFlags = [
  { label: t.hermesCli.readOnly, tone: 'good' },
  { label: t.hermesCli.noRuntimeControl, tone: 'warn' },
  { label: t.hermesCli.noTradingExecution, tone: 'danger' },
  { label: t.hermesCli.noAutoTradingActive, tone: 'warn' },
  { label: t.hermesCli.humanReviewRequiredActive, tone: 'info' },
];

export const providers = [
  { name: 'GPT-5.5', role: t.providers.seniorArchitect, status: t.providers.manualRoute },
  { name: 'Ollama / Qwen', role: t.providers.localWorker, status: t.providers.ready },
  { name: 'OpenRouter', role: t.providers.fallback, status: t.providers.disabled },
];


export const tradingIntelligenceMock = {
  bundle_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle',
  package_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/scalping_portfolio/ensemble_portfolio/ensemble_signal_agent_package.json',
  package_validation_status: 'ok',
  portfolio_status: 'needs_validation',
  assets: [
    { asset: 'GER40', readiness: 'bot_ready', primary_setup: 'ger40_range_breakout_m5', backup_setups: ['ger40_ema_pullback_m5'], candidate_count: 5, signal_spec_count: 5 },
    { asset: 'XAUUSD', readiness: 'bot_ready', primary_setup: 'xauusd_micro_trend_continuation_m5', backup_setups: ['xauusd_liquidity_rejection_m5', 'xauusd_ema_pullback_m5', 'xauusd_range_breakout_m5'], candidate_count: 8, signal_spec_count: 8 },
    { asset: 'EURUSD', readiness: 'needs_more_validation', primary_setup: '-', backup_setups: [], candidate_count: 0, signal_spec_count: 0 },
  ],
  safety_flags: [
    'no_auto_trading=true',
    'human_review_required=true',
    'broker_orders_enabled=false',
    'live_trading_enabled=false',
    'research_only=true',
  ],
  no_auto_trading: true,
  human_review_required: true,
  broker_orders_enabled: false,
  live_trading_enabled: false,
  research_only: true,
};

export const systemBHandoffBundleMock = {
  bundle_path: '/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle',
  files: ['README.md','ensemble_signal_agent_package.json','ensemble_signal_agent_package.schema.json','system_b_signal_agent_export_contract.md','portfolio_summary.json','portfolio_summary.md','bundle-manifest.json'],
  asset_count: 3,
  portfolio_status: 'needs_validation',
  safety_flags: ['no_auto_trading=true','human_review_required=true','broker_orders_enabled=false','live_trading_enabled=false','research_only=true'],
  no_auto_trading: true,
  human_review_required: true,
  broker_orders_enabled: false,
  live_trading_enabled: false,
  research_only: true,
};
