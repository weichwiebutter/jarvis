import { useEffect, useState } from 'react';
import {
  createRuntimeStorageFallback,
  loadRuntimeStorage,
} from '../data/runtimeDataAdapter';
import { de as t } from '../i18n/de';
import { sourceModeLabel, sourceTone } from '../utils/controlCenterFormatters';
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
  const [storageState, setStorageState] = useState(() => createRuntimeStorageFallback());
  const { summary, buckets, retentionRules, storageSafetyRules } = storageState;
  const fixtureActive = storageState.dataSource === 'fixture';

  useEffect(() => {
    let active = true;

    loadRuntimeStorage().then((nextState) => {
      if (active) {
        setStorageState(nextState);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return (
    <Panel
      eyebrow={t.storageRetention.eyebrow}
      title={t.storageRetention.title}
      action={
        <StatusPill tone={sourceTone(storageState.dataSource)}>
          {sourceModeLabel(storageState.dataSource)}
        </StatusPill>
      }
      className="storage-retention-panel"
    >
      {fixtureActive ? <p className="runtime-warning">{t.common.demoFixtureActive}</p> : null}
      <div className="storage-summary-grid">
        <article className="storage-root-card">
          <span>{t.storageRetention.root}</span>
          <strong>{summary.root}</strong>
          <div className="storage-main-meter">
            <div>
              <span>{t.storageRetention.used}: {summary.usedPercent}%</span>
              <strong>{summary.freeDiskGb} GB {t.storageRetention.freeDisk}</strong>
            </div>
            <i style={{ width: `${summary.usedPercent}%` }} />
          </div>
        </article>
        <article className="storage-threshold-card tone-warn">
          <span>{t.storageRetention.warningThreshold}</span>
          <strong>{summary.warningThreshold}</strong>
          <p>Neue Research- und Replay-Jobs werden spaeter gedrosselt.</p>
        </article>
        <article className="storage-threshold-card tone-danger">
          <span>{t.storageRetention.criticalThreshold}</span>
          <strong>{summary.criticalThreshold}</strong>
          <p>Safe Mode und Stop neuer Research-Jobs werden spaeter Pflicht.</p>
        </article>
      </div>

      <section className="storage-section">
        <div className="research-section-head">
          <h3>{t.storageRetention.dataLake}</h3>
          <StatusPill tone={storagePressureTone(summary.usedPercent)}>
            {summary.usedPercent}% {t.storageRetention.used}
          </StatusPill>
        </div>
        <div className="storage-bucket-grid">
          {buckets.map((bucket) => (
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
