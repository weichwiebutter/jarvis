import { useEffect, useMemo, useState } from 'react';
import { createRuntimeJobsFallback, loadRuntimeJobs } from '../data/runtimeDataAdapter';
import { de as t } from '../i18n/de';
import { sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
import { Panel, StatusPill, toneClass } from './StatusCard';

const queueSections = [
  { key: 'pending', label: t.jobsQueue.pending, tone: 'warn' },
  { key: 'running', label: t.jobsQueue.running, tone: 'info' },
  { key: 'failed', label: t.jobsQueue.failed, tone: 'danger' },
  { key: 'quarantined', label: t.jobsQueue.quarantined, tone: 'danger' },
  { key: 'completed', label: t.jobsQueue.completedLatest, tone: 'good' },
];

function jobStatusTone(status) {
  switch (status) {
    case 'pending':
      return 'warn';
    case 'running':
      return 'info';
    case 'completed':
      return 'good';
    case 'failed':
    case 'quarantined':
      return 'danger';
    default:
      return 'muted';
  }
}

function jobStatusLabel(status) {
  switch (status) {
    case 'pending':
      return t.jobsQueue.pending;
    case 'running':
      return t.jobsQueue.running;
    case 'completed':
      return t.jobsQueue.completed;
    case 'failed':
      return t.jobsQueue.failed;
    case 'quarantined':
      return t.jobsQueue.quarantined;
    default:
      return status;
  }
}

function jobTime(job) {
  return job.completed_at_utc || job.started_at_utc || job.created_at_utc || t.common.notReported;
}

function JobCard({ job }) {
  const tone = jobStatusTone(job.status);
  const parameterPreview = Object.entries(job.parameters || {})
    .slice(0, 2)
    .map(([key, value]) => `${key}: ${value}`)
    .join(' / ');

  return (
    <article className={`job-card ${toneClass(tone)}`}>
      <div className="job-card-head">
        <div>
          <span>{job.job_type}</span>
          <strong>{job.job_id}</strong>
        </div>
        <StatusPill tone={tone}>{jobStatusLabel(job.status)}</StatusPill>
      </div>
      <p>{job.summary}</p>
      <div className="job-meta-grid">
        <span>
          {t.jobsQueue.priority}: <b>{job.priority}</b>
        </span>
        <span>
          {t.jobsQueue.requestedBy}: <b>{job.requested_by}</b>
        </span>
        <span>
          {t.jobsQueue.resourceProfile}: <b>{job.resource_profile}</b>
        </span>
        <span>
          {t.jobsQueue.retries}: <b>{job.retry_count}/{job.max_retries}</b>
        </span>
      </div>
      <div className="job-foot">
        <span>{jobTime(job)}</span>
        <span>{parameterPreview || t.common.none}</span>
      </div>
      {job.output_path ? <p className="job-path">{job.output_path}</p> : null}
      {job.error_message ? <p className="job-error">{job.error_message}</p> : null}
    </article>
  );
}

function JobLane({ section, jobs }) {
  const visibleJobs = section.key === 'completed' ? jobs.slice(0, 3) : jobs;

  return (
    <section className="job-lane">
      <div className="job-lane-head">
        <span>{section.label}</span>
        <StatusPill tone={section.tone}>{jobs.length}</StatusPill>
      </div>
      <div className="job-card-list">
        {visibleJobs.length ? (
          visibleJobs.map((job) => <JobCard job={job} key={job.job_id} />)
        ) : (
          <p className="job-empty">{t.jobsQueue.empty}</p>
        )}
      </div>
    </section>
  );
}

function buildQueueMetrics(jobs) {
  return queueSections.map((section) => ({
    ...section,
    value: jobs[section.key]?.length || 0,
  }));
}

export function JobsQueuePanel() {
  const [queueState, setQueueState] = useState(() => createRuntimeJobsFallback());
  const jobs = queueState.jobs;
  const metrics = useMemo(() => buildQueueMetrics(jobs), [jobs]);
  const fixtureActive = queueState.dataSource === 'fixture';

  useEffect(() => {
    let active = true;

    loadRuntimeJobs().then((nextState) => {
      if (active) {
        setQueueState(nextState);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return (
    <Panel
      eyebrow={t.jobsQueue.eyebrow}
      title={t.jobsQueue.title}
      action={
        <StatusPill tone={sourceTone(queueState.dataSource)}>
          {sourceModeLabel(queueState.dataSource)}
        </StatusPill>
      }
      className="jobs-queue-panel"
    >
      <div className="jobs-safety-strip">
        <strong>{t.jobsQueue.readOnly}</strong>
        <strong>{t.jobsQueue.noCommands}</strong>
        <strong>{t.jobsQueue.noRuntimeControl}</strong>
      </div>
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
      <div className="jobs-summary-grid">
        {metrics.map((metric) => (
          <article className={`job-count-card ${toneClass(metric.tone)}`} key={metric.key}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
          </article>
        ))}
      </div>
      <div className="jobs-board">
        {queueSections.map((section) => (
          <JobLane jobs={jobs[section.key] || []} key={section.key} section={section} />
        ))}
      </div>
      <div className="inline-note">{t.jobsQueue.note}</div>
    </Panel>
  );
}
