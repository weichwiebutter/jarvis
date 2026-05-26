import { useState } from 'react';
import { Header } from './components/Header';
import { RuntimeHealthPanel } from './components/RuntimeHealthPanel';
import { HermesBrainPanel, LearningQueuePanel, ApprovalQueuePanel, ReflectiveLearningPanel } from './components/LearningApprovalPanel';
import { SetupWatchPanel } from './components/SetupWatchPanel';
import { ResearchCenterPanel } from './components/ResearchCenterPanel';
import { StorageRetentionPanel } from './components/StorageRetentionPanel';
import { HermesCliPanel } from './components/HermesCliPanel';
import { JobsQueuePanel } from './components/JobsQueuePanel';
import { EventTimelinePanel } from './components/EventTimelinePanel';
import { OperatorDashboardPanel } from './components/OperatorDashboardPanel';
import { CostProviderPanel, SafetyPanel } from './components/SafetyPanel';
import { StatusPill } from './components/StatusCard';

const bottomTabs = [
  { id: 'operator', label: 'Operator Dashboard' },
  { id: 'research', label: 'Forschung / Backtests / Outcomes' },
  { id: 'storage', label: 'Speicher & Datenhaltung' },
  { id: 'cli', label: 'CLI / Dev-Konsole' },
  { id: 'details', label: 'Details' },
];

function StatusSummaryCard({ title, value, detail, tone = 'info', badges = [] }) {
  return (
    <article className={`status-summary-card tone-${tone}`}>
      <div>
        <span>{title}</span>
        <strong>{value}</strong>
      </div>
      <p>{detail}</p>
      <div className="status-summary-badges">
        {badges.map((badge) => (
          <StatusPill tone={badge.tone || tone} key={badge.label}>
            {badge.label}
          </StatusPill>
        ))}
      </div>
    </article>
  );
}

export default function App() {
  const [activeTab, setActiveTab] = useState('operator');

  const renderActiveTab = () => {
    switch (activeTab) {
      case 'operator':
        return <OperatorDashboardPanel />;
      case 'storage':
        return <StorageRetentionPanel />;
      case 'cli':
        return <HermesCliPanel />;
      case 'details':
        return (
          <div className="details-grid">
            <RuntimeHealthPanel />
            <SafetyPanel />
            <HermesBrainPanel />
            <JobsQueuePanel />
            <ReflectiveLearningPanel />
            <CostProviderPanel />
          </div>
        );
      case 'research':
      default:
        return <ResearchCenterPanel />;
    }
  };

  return (
    <main className="app-shell control-center-shell">
      <Header />

      <section className="control-status-grid" aria-label="Kompakter Systemstatus">
        <StatusSummaryCard
          title="Laufzeitstatus"
          value="Hermes Runtime v1"
          detail="Lokale Health-JSON nur lesend, keine Runtime-Steuerung aus der UI."
          tone="good"
          badges={[
            { label: 'nur lesend', tone: 'good' },
            { label: 'gestoppt / bereit', tone: 'info' },
          ]}
        />
        <StatusSummaryCard
          title="Sicherheitsstatus"
          value="Auto-Trading aus"
          detail="Menschliche Freigabe bleibt Pflicht; keine Orders, keine Brokeraktion."
          tone="warn"
          badges={[
            { label: 'no_auto_trading', tone: 'warn' },
            { label: 'menschliche Freigabe', tone: 'info' },
          ]}
        />
        <StatusSummaryCard
          title="Trading-Status"
          value="Setup-Beobachtung"
          detail="Nur Warnungen und theoretische Signale fuer XAUUSD, EURUSD und GER40."
          tone="info"
          badges={[
            { label: 'watching', tone: 'info' },
            { label: 'nur Hinweise', tone: 'warn' },
          ]}
        />
        <StatusSummaryCard
          title="Speicherstatus"
          value="Data Lake lokal"
          detail="Retention- und Storage-Safety bleiben sichtbar, keine Cleanup-Aktion."
          tone="good"
          badges={[
            { label: 'DiskSpaceGuard', tone: 'good' },
            { label: 'keine Loeschung', tone: 'warn' },
          ]}
        />
      </section>

      <section className="control-main-grid" aria-label="Jarvis Hauptbereiche">
        <div className="control-column trading-column">
          <SetupWatchPanel />
        </div>
        <div className="control-column activity-column">
          <EventTimelinePanel />
        </div>
        <div className="control-column learning-column">
          <LearningQueuePanel />
          <ApprovalQueuePanel />
        </div>
      </section>

      <section className="control-tabs" aria-label="Weitere Kontrollbereiche">
        <div className="tab-nav" role="tablist" aria-label="Detailbereiche">
          {bottomTabs.map((tab) => (
            <button
              aria-selected={activeTab === tab.id}
              className={`tab-button ${activeTab === tab.id ? 'is-active' : ''}`}
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              role="tab"
              type="button"
            >
              {tab.label}
            </button>
          ))}
        </div>
        <div className="tab-panel" role="tabpanel">
          {renderActiveTab()}
        </div>
      </section>
    </main>
  );
}
