import { useEffect, useState } from 'react';
import {
  createFeatureSignalExportsFallback,
  loadFeatureSignalExports,
} from '../data/runtimeDataAdapter';
import {
  backtestRuns,
  clusterScores,
  regimeAnalyses,
  researchArtifacts,
  researchJobs,
} from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { confidencePercent, sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
import { Panel, StatusPill, toneClass } from './StatusCard';

function researchStatusTone(status) {
  switch (status) {
    case 'running':
      return 'info';
    case 'planned':
      return 'warn';
    case 'paused':
      return 'muted';
    case 'completed':
      return 'good';
    case 'quarantined':
      return 'danger';
    default:
      return 'info';
  }
}

function researchStatusLabel(status) {
  switch (status) {
    case 'running':
      return t.backtestResearch.running;
    case 'planned':
      return t.backtestResearch.planned;
    case 'paused':
      return t.backtestResearch.paused;
    case 'completed':
      return t.backtestResearch.completed;
    case 'quarantined':
      return t.backtestResearch.quarantined;
    default:
      return status;
  }
}

function outOfSampleTone(status) {
  if (status === 'bestanden') {
    return 'good';
  }

  if (status === 'fehlgeschlagen') {
    return 'danger';
  }

  return 'warn';
}

function exportStatusLabel(state) {
  if (state.dataSource === 'live_file') {
    return t.backtestResearch.ready;
  }

  return t.backtestResearch.fixture;
}

function FeatureSignalExportSection({ exportState }) {
  const fixtureActive = exportState.dataSource === 'fixture';
  const latestFeatureRows = exportState.features.slice(0, 3);
  const latestSignalRows = exportState.signals.slice(0, 3);

  return (
    <section className="research-section feature-export-section">
      <div className="research-section-head">
        <h3>{t.backtestResearch.featureSignalTitle}</h3>
        <StatusPill tone={sourceTone(exportState.dataSource)}>
          {exportStatusLabel(exportState)}
        </StatusPill>
      </div>
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
      <div className="feature-export-guard">
        <strong>{t.backtestResearch.analysisOnly}</strong>
        <strong>{t.backtestResearch.noAutoTrading}</strong>
        <strong>{t.backtestResearch.learningDataBasis}</strong>
      </div>
      <div className="feature-export-summary-grid">
        <article className="feature-export-summary-card tone-info">
          <span>{t.backtestResearch.featureExports}</span>
          <strong>{exportState.counts.features}</strong>
          <p>{t.backtestResearch.featureRows}</p>
        </article>
        <article className="feature-export-summary-card tone-good">
          <span>{t.backtestResearch.signalExports}</span>
          <strong>{exportState.counts.signals}</strong>
          <p>{t.backtestResearch.signalRows}</p>
        </article>
        <article className="feature-export-summary-card tone-warn">
          <span>{t.backtestResearch.symbols}</span>
          <div className="feature-symbol-strip">
            {exportState.symbols.map((symbol) => (
              <b key={symbol}>{symbol}</b>
            ))}
          </div>
        </article>
        <article className="feature-export-summary-card tone-muted">
          <span>{t.backtestResearch.latestExport}</span>
          <strong>{exportState.latestExportTimestamp || t.common.notReported}</strong>
          <p>{sourceModeLabel(exportState.dataSource)}</p>
        </article>
      </div>
      <div className="feature-export-file-grid">
        <article>
          <span>{t.backtestResearch.featureExports}</span>
          <code>{exportState.exportFiles.features || t.common.notReported}</code>
        </article>
        <article>
          <span>{t.backtestResearch.signalExports}</span>
          <code>{exportState.exportFiles.signals || t.common.notReported}</code>
        </article>
      </div>
      <div className="feature-export-preview-grid">
        <section>
          <div className="research-section-head">
            <h3>{t.backtestResearch.featureExports}</h3>
            <StatusPill tone="info">{latestFeatureRows.length}</StatusPill>
          </div>
          <div className="feature-row-list">
            {latestFeatureRows.map((row) => (
              <article className="feature-row-card tone-info" key={row.id}>
                <div>
                  <span>{row.timeframe}</span>
                  <strong>{row.symbol}</strong>
                </div>
                <p>{row.pattern_candidate} / {row.h4_regime}</p>
                <div className="feature-row-metrics">
                  <span>{t.backtestResearch.signalScore}: <b>{row.signal_score.toFixed(2)}</b></span>
                  <span>ADX: <b>{row.adx.toFixed(1)}</b></span>
                  <span>RSI: <b>{row.rsi.toFixed(1)}</b></span>
                </div>
              </article>
            ))}
          </div>
        </section>
        <section>
          <div className="research-section-head">
            <h3>{t.backtestResearch.signalExports}</h3>
            <StatusPill tone="good">{latestSignalRows.length}</StatusPill>
          </div>
          <div className="feature-row-list">
            {latestSignalRows.map((row) => (
              <article className="feature-row-card tone-good" key={row.id}>
                <div>
                  <span>{t.backtestResearch.direction}</span>
                  <strong>{row.symbol} / {row.direction}</strong>
                </div>
                <p>{row.signal_type}</p>
                <div className="feature-row-metrics">
                  <span>{t.backtestResearch.signalScore}: <b>{row.score.toFixed(2)}</b></span>
                  <span>{t.backtestResearch.confidence}: <b>{confidencePercent(row.confidence)}</b></span>
                </div>
                <p>{t.backtestResearch.reasonCodes}: {row.reason_codes.join(', ')}</p>
              </article>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
}

export function ResearchCenterPanel() {
  const [exportState, setExportState] = useState(() => createFeatureSignalExportsFallback());

  useEffect(() => {
    let active = true;

    loadFeatureSignalExports().then((nextState) => {
      if (active) {
        setExportState(nextState);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return (
    <Panel
      eyebrow={t.backtestResearch.eyebrow}
      title={t.backtestResearch.title}
      action={<StatusPill tone="info">{t.backtestResearch.status}</StatusPill>}
      className="backtest-panel"
    >
      <div className="research-safety-strip">
        <strong>{t.backtestResearch.noLiveTrades}</strong>
        <strong>{t.backtestResearch.noAutoApproval}</strong>
        <strong>{t.backtestResearch.humanReviewRequired}</strong>
      </div>

      <FeatureSignalExportSection exportState={exportState} />

      <div className="research-overview-grid">
        <section className="research-block">
          <div className="research-section-head">
            <h3>{t.backtestResearch.jobsTitle}</h3>
            <StatusPill tone="warn">{researchJobs.length} Mock-Jobs</StatusPill>
          </div>
          <div className="research-job-list">
            {researchJobs.map((job) => (
              <article className={`research-job ${toneClass(researchStatusTone(job.status))}`} key={job.id}>
                <div className="research-job-top">
                  <div>
                    <span>{job.type}</span>
                    <strong>{job.name}</strong>
                  </div>
                  <StatusPill tone={researchStatusTone(job.status)}>
                    {researchStatusLabel(job.status)}
                  </StatusPill>
                </div>
                <p>{job.detail}</p>
                <div className="research-progress">
                  <span>{t.backtestResearch.progress}: {job.progress}%</span>
                  <i style={{ width: `${job.progress}%` }} />
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="research-block">
          <div className="research-section-head">
            <h3>{t.backtestResearch.assetsTitle}</h3>
            <StatusPill tone="good">{t.common.readOnly}</StatusPill>
          </div>
          <div className="research-asset-grid">
            {researchArtifacts.map((artifact) => (
              <article className={`research-asset ${toneClass(artifact.tone)}`} key={artifact.id}>
                <span>{artifact.label}</span>
                <strong>{artifact.value}</strong>
                <p>{artifact.detail}</p>
              </article>
            ))}
          </div>
          <div className="research-replay-note">
            <span>{t.backtestResearch.replayNote}</span>
            <strong>Replay-Manifeste zeigen nur gespeicherte Referenzen; kein Replay wird im UI gestartet.</strong>
          </div>
        </section>
      </div>

      <section className="research-section">
        <div className="research-section-head">
          <h3>{t.backtestResearch.backtestsTitle}</h3>
          <StatusPill tone="info">3 Runs</StatusPill>
        </div>
        <div className="backtest-card-list">
          {backtestRuns.map((run) => (
            <article
              className={`backtest-card ${toneClass(researchStatusTone(run.status))}`}
              key={run.id}
            >
              <div className="backtest-card-head">
                <div>
                  <span>{t.backtestResearch.name}</span>
                  <strong>{run.name}</strong>
                </div>
                <StatusPill tone={researchStatusTone(run.status)}>
                  {researchStatusLabel(run.status)}
                </StatusPill>
              </div>
              <div className="backtest-meta">
                <div>
                  <span>{t.backtestResearch.symbol}</span>
                  <strong>{run.symbol}</strong>
                </div>
                <div>
                  <span>{t.backtestResearch.period}</span>
                  <strong>{run.period}</strong>
                </div>
                <div>
                  <span>{t.backtestResearch.marketRegime}</span>
                  <strong>{run.marketRegime}</strong>
                </div>
              </div>
              <div className="backtest-metrics">
                <div>
                  <span>{t.backtestResearch.profitFactor}</span>
                  <strong>{run.profitFactor}</strong>
                </div>
                <div>
                  <span>{t.backtestResearch.winrate}</span>
                  <strong>{run.winrate}</strong>
                </div>
                <div>
                  <span>{t.backtestResearch.maxDrawdown}</span>
                  <strong>{run.maxDrawdown}</strong>
                </div>
                <div>
                  <span>{t.backtestResearch.confidenceStability}</span>
                  <strong>{run.confidenceStability}</strong>
                </div>
              </div>
              <div className="backtest-oos">
                <span>{t.backtestResearch.outOfSample}</span>
                <strong className={toneClass(outOfSampleTone(run.outOfSample))}>
                  {run.outOfSample}
                </strong>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className="research-analysis-grid">
        <section className="research-block">
          <div className="research-section-head">
            <h3>{t.backtestResearch.clusterTitle}</h3>
            <StatusPill tone="info">Heatmap</StatusPill>
          </div>
          <div className="cluster-heatmap">
            {clusterScores.map((cluster) => (
              <div className={`cluster-tile ${cluster.className}`} key={cluster.id}>
                <span>{cluster.label}</span>
                <strong>{cluster.score}</strong>
              </div>
            ))}
          </div>
        </section>

        <section className="research-block">
          <div className="research-section-head">
            <h3>{t.backtestResearch.regimeTitle}</h3>
            <StatusPill tone="warn">Review</StatusPill>
          </div>
          <div className="regime-card-list">
            {regimeAnalyses.map((regime) => (
              <article className={`regime-card ${toneClass(regime.tone)}`} key={regime.id}>
                <div>
                  <span>{regime.symbols}</span>
                  <strong>{regime.name}</strong>
                </div>
                <p>{regime.detail}</p>
              </article>
            ))}
          </div>
        </section>
      </div>
    </Panel>
  );
}
