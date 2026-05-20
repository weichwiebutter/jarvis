import {
  retentionRules,
  storageBuckets,
  storageSafetyRules,
  storageSummary,
} from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { Panel, StatusPill, toneClass } from './StatusCard';

function storagePressureTone(percent) {
  if (percent >= 90) {
    return 'danger';
  }

  if (percent >= 60) {
    return 'warn';
  }

  return 'good';
}

export function StorageRetentionPanel() {
  return (
    <Panel
      eyebrow={t.storageRetention.eyebrow}
      title={t.storageRetention.title}
      action={<StatusPill tone="warn">{t.storageRetention.status}</StatusPill>}
      className="storage-retention-panel"
    >
      <div className="storage-summary-grid">
        <article className="storage-root-card">
          <span>{t.storageRetention.root}</span>
          <strong>{storageSummary.root}</strong>
          <div className="storage-main-meter">
            <div>
              <span>{t.storageRetention.used}: {storageSummary.usedPercent}%</span>
              <strong>{storageSummary.freeDiskGb} GB {t.storageRetention.freeDisk}</strong>
            </div>
            <i style={{ width: `${storageSummary.usedPercent}%` }} />
          </div>
        </article>
        <article className="storage-threshold-card tone-warn">
          <span>{t.storageRetention.warningThreshold}</span>
          <strong>{storageSummary.warningThreshold}</strong>
          <p>Neue Research- und Replay-Jobs werden spaeter gedrosselt.</p>
        </article>
        <article className="storage-threshold-card tone-danger">
          <span>{t.storageRetention.criticalThreshold}</span>
          <strong>{storageSummary.criticalThreshold}</strong>
          <p>Safe Mode und Stop neuer Research-Jobs werden spaeter Pflicht.</p>
        </article>
      </div>

      <section className="storage-section">
        <div className="research-section-head">
          <h3>{t.storageRetention.dataLake}</h3>
          <StatusPill tone={storagePressureTone(storageSummary.usedPercent)}>
            {storageSummary.usedPercent}% {t.storageRetention.used}
          </StatusPill>
        </div>
        <div className="storage-bucket-grid">
          {storageBuckets.map((bucket) => (
            <article className={`storage-bucket ${toneClass(bucket.tone)}`} key={bucket.id}>
              <div className="storage-bucket-head">
                <div>
                  <span>{t.storageRetention.path}</span>
                  <strong>{bucket.label}</strong>
                </div>
                <StatusPill tone={bucket.tone}>{bucket.used}</StatusPill>
              </div>
              <code>{bucket.path}</code>
              <div className="storage-bucket-meter">
                <span>{bucket.percent}% {t.storageRetention.used}</span>
                <i style={{ width: `${bucket.percent}%` }} />
              </div>
              <p>{bucket.detail}</p>
            </article>
          ))}
        </div>
      </section>

      <div className="storage-policy-grid">
        <section className="storage-policy-block">
          <div className="research-section-head">
            <h3>{t.storageRetention.retentionRules}</h3>
            <StatusPill tone="info">Policy</StatusPill>
          </div>
          <div className="storage-rule-list">
            {retentionRules.map((rule) => (
              <div className="storage-rule" key={rule}>
                <span>{rule}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="storage-policy-block">
          <div className="research-section-head">
            <h3>{t.storageRetention.storageSafety}</h3>
            <StatusPill tone="warn">Safety</StatusPill>
          </div>
          <div className="storage-safety-list">
            {storageSafetyRules.map((rule) => (
              <article className={`storage-safety-rule ${toneClass(rule.tone)}`} key={rule.label}>
                <strong>{rule.label}</strong>
              </article>
            ))}
          </div>
        </section>
      </div>
    </Panel>
  );
}
