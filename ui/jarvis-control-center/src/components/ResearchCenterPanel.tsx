import {
  backtestRuns,
  clusterScores,
  regimeAnalyses,
  researchArtifacts,
  researchJobs,
} from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
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

export function ResearchCenterPanel() {
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
