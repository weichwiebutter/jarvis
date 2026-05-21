import {
  hermesCliCommands,
  hermesCliOutputs,
  hermesCliSafetyFlags,
} from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { Panel, StatusPill, toneClass } from './StatusCard';

export function HermesCliPanel() {
  return (
    <Panel
      eyebrow={t.hermesCli.eyebrow}
      title={t.hermesCli.title}
      action={<StatusPill tone="warn">{t.hermesCli.status}</StatusPill>}
      className="cli-panel"
    >
      <div className="cli-safety-strip">
        {hermesCliSafetyFlags.map((flag) => (
          <strong className={toneClass(flag.tone)} key={flag.label}>
            {flag.label}
          </strong>
        ))}
      </div>

      <div className="cli-console-grid">
        <section className="cli-command-section">
          <div className="research-section-head">
            <h3>{t.hermesCli.commandsTitle}</h3>
            <StatusPill tone="good">{t.common.readOnly}</StatusPill>
          </div>
          <div className="cli-command-list">
            {hermesCliCommands.map((item) => (
              <article className={`cli-command-card ${toneClass(item.tone)}`} key={item.id}>
                <span>{t.hermesCli.commandLabel}</span>
                <code>{item.command}</code>
                <p>{item.description}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="cli-output-section">
          <div className="research-section-head">
            <h3>{t.hermesCli.outputTitle}</h3>
            <StatusPill tone="info">{t.hermesCli.mockOutput}</StatusPill>
          </div>
          <div className="cli-output-list">
            {hermesCliOutputs.map((output) => (
              <article className={`cli-output-card ${toneClass(output.tone)}`} key={output.id}>
                <div className="cli-output-head">
                  <div>
                    <span>{output.command}</span>
                    <strong>{output.title}</strong>
                  </div>
                  <StatusPill tone={output.tone}>{t.hermesCli.mockOutput}</StatusPill>
                </div>
                <pre>{output.lines.join('\n')}</pre>
              </article>
            ))}
          </div>
        </section>
      </div>

      <div className="inline-note">{t.hermesCli.note}</div>
    </Panel>
  );
}
