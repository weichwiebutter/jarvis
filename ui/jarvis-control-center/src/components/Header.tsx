import { de as t } from '../i18n/de';
import { StatusPill } from './StatusCard';

export function Header() {
  return (
    <header className="hero-shell">
      <div className="hero-copy">
        <p className="eyebrow">{t.header.eyebrow}</p>
        <h1>{t.header.title}</h1>
        <p>{t.header.copy}</p>
      </div>
      <div className="hero-status" aria-label="Systemstatus Zusammenfassung">
        <StatusPill tone="good">{t.header.hermesOnline}</StatusPill>
        <StatusPill tone="warn">{t.header.noAutoTrading}</StatusPill>
        <StatusPill tone="info">{t.header.approvalRequired}</StatusPill>
      </div>
    </header>
  );
}
