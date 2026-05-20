import { providers } from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { Panel, StatusPill } from './StatusCard';

export function SafetyPanel() {
  return (
    <Panel
      eyebrow={t.safety.eyebrow}
      title={t.safety.title}
      action={<StatusPill tone="danger">{t.safety.locked}</StatusPill>}
    >
      <div className="safety-stack">
        <div className="safety-row">
          <span>{t.safety.autoTrading}</span>
          <strong className="tone-danger">{t.safety.blocked}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.humanApproval}</span>
          <strong className="tone-warn">{t.safety.required}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.silentLearning}</span>
          <strong className="tone-danger">{t.safety.disabled}</strong>
        </div>
        <div className="safety-row">
          <span>{t.safety.martingaleGrid}</span>
          <strong className="tone-danger">{t.safety.notAllowed}</strong>
        </div>
      </div>
    </Panel>
  );
}

export function CostProviderPanel() {
  return (
    <Panel
      eyebrow={t.providers.eyebrow}
      title={t.providers.title}
      action={<StatusPill tone="good">{t.providers.costVisible}</StatusPill>}
    >
      <div className="provider-list">
        {providers.map((provider) => (
          <div className="provider-row" key={provider.name}>
            <strong>{provider.name}</strong>
            <span>{provider.role}</span>
            <StatusPill
              tone={
                provider.status === t.providers.ready
                  ? 'good'
                  : provider.status === t.providers.disabled
                    ? 'muted'
                    : 'info'
              }
            >
              {provider.status}
            </StatusPill>
          </div>
        ))}
      </div>
    </Panel>
  );
}
