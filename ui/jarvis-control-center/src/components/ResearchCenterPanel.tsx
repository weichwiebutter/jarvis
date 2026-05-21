import { useEffect, useState } from 'react';
import {
  createBacktestReportsFallback,
  createFeatureSignalExportsFallback,
  createOutcomeReportsFallback,
  loadBacktestReports,
  loadFeatureSignalExports,
  loadOutcomeReports,
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

function backtestReportStatusTone(status) {
  if (String(status).includes('completed')) {
    return 'good';
  }

  if (String(status).includes('failed')) {
    return 'danger';
  }

  return 'warn';
}

function backtestReportStatusLabel(status) {
  if (status === 'completed_demo') {
    return t.backtestResearch.completedDemo;
  }

  return researchStatusLabel(status);
}

function outcomeStatusTone(status) {
  switch (status) {
    case 'tp_hit':
      return 'good';
    case 'sl_hit':
    case 'invalidated':
      return 'danger';
    case 'expired':
      return 'muted';
    case 'partial':
      return 'warn';
    default:
      return 'info';
  }
}

function outcomeStatusLabel(status) {
  switch (status) {
    case 'tp_hit':
      return t.backtestResearch.tpHit;
    case 'sl_hit':
      return t.backtestResearch.slHit;
    case 'expired':
      return t.backtestResearch.expiredOutcome;
    case 'invalidated':
      return t.backtestResearch.invalidatedOutcome;
    case 'partial':
      return t.backtestResearch.partialOutcome;
    default:
      return status;
  }
}

function formatDecimal(value, digits = 2) {
  return Number(value || 0).toFixed(digits);
}

function formatR(value) {
  return `${formatDecimal(value)} R`;
}

function boolLabel(value) {
  return value ? t.common.yes : t.common.no;
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

function BacktestReportsSection({ backtestReportState }) {
  const fixtureActive = backtestReportState.dataSource === 'fixture';
  const reports = backtestReportState.reports.slice(0, 3);

  return (
    <section className="research-section backtest-report-section">
      <div className="research-section-head">
        <h3>{t.backtestResearch.backtestReportsTitle}</h3>
        <StatusPill tone={sourceTone(backtestReportState.dataSource)}>
          {exportStatusLabel(backtestReportState)}
        </StatusPill>
      </div>
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
      <div className="backtest-report-safety">
        <strong>{t.backtestResearch.demoNoRealTrading}</strong>
        <strong>{t.backtestResearch.noLiveTrades}</strong>
        <strong>{t.backtestResearch.noAutoTrading}</strong>
      </div>
      <div className="backtest-report-grid">
        {reports.map((report) => (
          <article
            className={`backtest-report-card ${toneClass(backtestReportStatusTone(report.status))}`}
            key={report.run_id}
          >
            <div className="backtest-card-head">
              <div>
                <span>{t.backtestResearch.backtestRunId}</span>
                <strong>{report.run_id}</strong>
              </div>
              <StatusPill tone={backtestReportStatusTone(report.status)}>
                {backtestReportStatusLabel(report.status)}
              </StatusPill>
            </div>
            <div className="backtest-report-identity">
              <div>
                <span>{t.backtestResearch.symbol}</span>
                <strong>{report.symbol}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.timeframe}</span>
                <strong>{report.timeframe}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.strategy}</span>
                <strong>{report.strategy_name}</strong>
              </div>
            </div>
            <div className="backtest-report-metrics">
              <div>
                <span>{t.backtestResearch.tradeCount}</span>
                <strong>{report.trade_count}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.winrate}</span>
                <strong>{confidencePercent(report.winrate)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.profitFactor}</span>
                <strong>{formatDecimal(report.profit_factor)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.maxDrawdown}</span>
                <strong>{confidencePercent(report.max_drawdown)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.expectancy}</span>
                <strong>{formatDecimal(report.expectancy)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.noAutoTrading}</span>
                <strong>{report.no_auto_trading ? t.common.active : t.common.inactive}</strong>
              </div>
            </div>
            {report.notes ? <p className="backtest-report-note">{report.notes}</p> : null}
          </article>
        ))}
      </div>
      <div className="feature-export-file-grid">
        <article>
          <span>{t.backtestResearch.reportFiles}</span>
          <code>{backtestReportState.sourcePath || t.common.notReported}</code>
        </article>
      </div>
    </section>
  );
}

function OutcomeReportsSection({ outcomeReportState }) {
  const fixtureActive = outcomeReportState.dataSource === 'fixture';
  const outcomes = outcomeReportState.outcomes.slice(0, 4);

  return (
    <section className="research-section outcome-report-section">
      <div className="research-section-head">
        <h3>{t.backtestResearch.outcomeReportsTitle}</h3>
        <StatusPill tone={sourceTone(outcomeReportState.dataSource)}>
          {exportStatusLabel(outcomeReportState)}
        </StatusPill>
      </div>
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
      <div className="outcome-summary-grid">
        <article className="outcome-summary-card tone-good">
          <span>{t.backtestResearch.tpHit}</span>
          <strong>{outcomeReportState.counts.targetHits}</strong>
        </article>
        <article className="outcome-summary-card tone-danger">
          <span>{t.backtestResearch.slHit}</span>
          <strong>{outcomeReportState.counts.stopHits}</strong>
        </article>
        <article className="outcome-summary-card tone-muted">
          <span>{t.backtestResearch.expiredOutcome}</span>
          <strong>{outcomeReportState.counts.expired}</strong>
        </article>
        <article className="outcome-summary-card tone-warn">
          <span>{t.backtestResearch.invalidatedOutcome}</span>
          <strong>{outcomeReportState.counts.invalidated}</strong>
        </article>
      </div>
      <div className="outcome-safety-strip">
        <strong>{t.backtestResearch.theoreticalOutcome}</strong>
        <strong>{t.backtestResearch.noOrderExecution}</strong>
        <strong>{t.backtestResearch.confidenceCalibrationBasis}</strong>
      </div>
      <div className="outcome-card-grid">
        {outcomes.map((outcome) => (
          <article
            className={`outcome-card ${toneClass(outcomeStatusTone(outcome.outcome_status))}`}
            key={outcome.outcome_id}
          >
            <div className="backtest-card-head">
              <div>
                <span>{t.backtestResearch.outcomeId}</span>
                <strong>{outcome.outcome_id}</strong>
              </div>
              <StatusPill tone={outcomeStatusTone(outcome.outcome_status)}>
                {outcomeStatusLabel(outcome.outcome_status)}
              </StatusPill>
            </div>
            <div className="outcome-identity">
              <div>
                <span>{t.backtestResearch.symbol}</span>
                <strong>{outcome.symbol}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.timeframe}</span>
                <strong>{outcome.timeframe}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.direction}</span>
                <strong>{outcome.direction}</strong>
              </div>
            </div>
            <div className="outcome-flags">
              <span className={toneClass(outcome.hit_target ? 'good' : 'muted')}>
                {t.backtestResearch.targetHit}: <b>{boolLabel(outcome.hit_target)}</b>
              </span>
              <span className={toneClass(outcome.hit_stop ? 'danger' : 'muted')}>
                {t.backtestResearch.stopHit}: <b>{boolLabel(outcome.hit_stop)}</b>
              </span>
              <span className={toneClass(outcome.expired ? 'warn' : 'muted')}>
                {t.backtestResearch.expiredOutcome}: <b>{boolLabel(outcome.expired)}</b>
              </span>
              <span className={toneClass(outcome.invalidated ? 'danger' : 'muted')}>
                {t.backtestResearch.invalidatedOutcome}: <b>{boolLabel(outcome.invalidated)}</b>
              </span>
            </div>
            <div className="outcome-metrics">
              <div>
                <span>{t.backtestResearch.mfe}</span>
                <strong>{formatR(outcome.mfe)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.mae}</span>
                <strong>{formatR(outcome.mae)}</strong>
              </div>
              <div>
                <span>{t.backtestResearch.finalR}</span>
                <strong>{formatR(outcome.final_r)}</strong>
              </div>
            </div>
            <div className="outcome-evaluated">
              <span>{t.backtestResearch.evaluatedAt}</span>
              <strong>{outcome.evaluated_at_utc || t.common.notReported}</strong>
            </div>
          </article>
        ))}
      </div>
      <div className="feature-export-file-grid">
        <article>
          <span>{t.backtestResearch.outcomeReportFiles}</span>
          <code>{outcomeReportState.sourcePath || t.common.notReported}</code>
        </article>
      </div>
    </section>
  );
}

export function ResearchCenterPanel() {
  const [exportState, setExportState] = useState(() => createFeatureSignalExportsFallback());
  const [backtestReportState, setBacktestReportState] = useState(() =>
    createBacktestReportsFallback(),
  );
  const [outcomeReportState, setOutcomeReportState] = useState(() =>
    createOutcomeReportsFallback(),
  );

  useEffect(() => {
    let active = true;

    loadFeatureSignalExports().then((nextState) => {
      if (active) {
        setExportState(nextState);
      }
    });

    loadBacktestReports().then((nextState) => {
      if (active) {
        setBacktestReportState(nextState);
      }
    });

    loadOutcomeReports().then((nextState) => {
      if (active) {
        setOutcomeReportState(nextState);
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
      <BacktestReportsSection backtestReportState={backtestReportState} />
      <OutcomeReportsSection outcomeReportState={outcomeReportState} />

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
