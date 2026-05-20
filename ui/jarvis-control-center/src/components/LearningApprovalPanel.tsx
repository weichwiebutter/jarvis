import { learningCandidates } from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { Panel, StatusPill, toneClass } from './StatusCard';

function learningStatusTone(status) {
  switch (status) {
    case 'approved':
      return 'good';
    case 'rejected':
      return 'danger';
    case 'review':
      return 'warn';
    default:
      return 'info';
  }
}

function learningStatusLabel(status) {
  switch (status) {
    case 'approved':
      return t.learningQueue.approved;
    case 'rejected':
      return t.learningQueue.rejected;
    case 'review':
      return t.learningQueue.review;
    default:
      return t.learningQueue.open;
  }
}

function riskTone(risk) {
  switch (risk) {
    case 'high':
      return 'danger';
    case 'medium':
      return 'warn';
    default:
      return 'good';
  }
}

function riskLabel(risk) {
  switch (risk) {
    case 'high':
      return t.learningQueue.highRisk;
    case 'medium':
      return t.learningQueue.mediumRisk;
    default:
      return t.learningQueue.lowRisk;
  }
}

export function HermesBrainPanel() {
  return (
    <Panel
      eyebrow={t.hermesBrain.eyebrow}
      title={t.hermesBrain.title}
      action={<StatusPill tone="info">{t.hermesBrain.status}</StatusPill>}
    >
      <div className="brain-grid">
        <div>
          <span className="node-label">{t.hermesBrain.planner}</span>
          <strong>{t.hermesBrain.ready}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.memory}</span>
          <strong>{t.hermesBrain.approvalGated}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.learning}</span>
          <strong>{t.hermesBrain.reviewOnly}</strong>
        </div>
        <div>
          <span className="node-label">{t.hermesBrain.delegation}</span>
          <strong>{t.hermesBrain.visibleChains}</strong>
        </div>
      </div>
      <p className="panel-copy">{t.hermesBrain.copy}</p>
    </Panel>
  );
}

function LearningGuardStrip() {
  return (
    <div className="learning-guard-strip">
      <strong>{t.learningQueue.noSilentLearning}</strong>
      <strong>{t.learningQueue.humanApprovalRequired}</strong>
      <strong>{t.learningQueue.noAutoSkillActivation}</strong>
      <strong>{t.learningQueue.noTradingRuleWithoutReview}</strong>
    </div>
  );
}

function LearningCandidateCard({ candidate }) {
  return (
    <article className={`learning-candidate risk-${candidate.risk}`}>
      <div className="learning-card-head">
        <div>
          <span>{t.learningQueue.type}: {candidate.type}</span>
          <strong>{candidate.title}</strong>
        </div>
        <StatusPill tone={learningStatusTone(candidate.status)}>
          {learningStatusLabel(candidate.status)}
        </StatusPill>
      </div>
      <p>{candidate.description}</p>
      <div className="learning-card-meta">
        <div>
          <span>{t.learningQueue.source}</span>
          <strong>{candidate.source}</strong>
        </div>
        <div>
          <span>{t.learningQueue.risk}</span>
          <strong className={toneClass(riskTone(candidate.risk))}>{riskLabel(candidate.risk)}</strong>
        </div>
      </div>
      <div className="learning-action">
        <span>{t.learningQueue.action}</span>
        <strong>{candidate.action}</strong>
      </div>
    </article>
  );
}

export function LearningQueuePanel() {
  return (
    <Panel
      eyebrow={t.learningQueue.eyebrow}
      title={t.learningQueue.title}
      action={<StatusPill tone="warn">{t.learningQueue.pending}</StatusPill>}
      className="learning-panel"
    >
      <LearningGuardStrip />
      <div className="learning-candidate-list">
        {learningCandidates.map((candidate) => (
          <LearningCandidateCard candidate={candidate} key={candidate.id} />
        ))}
      </div>
    </Panel>
  );
}

export function ApprovalQueuePanel() {
  const approvalItems = learningCandidates.filter((candidate) =>
    ['open', 'review'].includes(candidate.status),
  );

  return (
    <Panel
      eyebrow={t.approvalQueue.eyebrow}
      title={t.approvalQueue.title}
      action={<StatusPill tone="warn">{t.approvalQueue.waiting}</StatusPill>}
      className="approval-panel"
    >
      <div className="approval-list">
        {approvalItems.length === 0 ? (
          <p className="panel-copy">{t.approvalQueue.empty}</p>
        ) : (
          approvalItems.map((candidate) => (
            <article className="approval-item" key={candidate.id}>
              <div>
                <strong>{candidate.type}</strong>
                <span>{candidate.title}</span>
              </div>
              <StatusPill tone={riskTone(candidate.risk)}>{riskLabel(candidate.risk)}</StatusPill>
              <p>{candidate.action}</p>
            </article>
          ))
        )}
      </div>
    </Panel>
  );
}

export function ReflectiveLearningPanel() {
  return (
    <Panel
      eyebrow={t.reflectiveLearning.eyebrow}
      title={t.reflectiveLearning.title}
      action={<StatusPill tone="info">{t.reflectiveLearning.status}</StatusPill>}
      className="reflective-panel"
    >
      <div className="reflective-stack">
        <div>
          <span>Loop</span>
          <strong>{t.reflectiveLearning.reviewLoop}</strong>
        </div>
        <div>
          <span>Memory</span>
          <strong>{t.reflectiveLearning.memoryGate}</strong>
        </div>
        <div>
          <span>Skills</span>
          <strong>{t.reflectiveLearning.skillGate}</strong>
        </div>
        <div>
          <span>Trading</span>
          <strong>{t.reflectiveLearning.tradingGate}</strong>
        </div>
      </div>
    </Panel>
  );
}
