import { Header } from './components/Header';
import { RuntimeHealthPanel } from './components/RuntimeHealthPanel';
import { HermesBrainPanel, LearningQueuePanel, ApprovalQueuePanel, ReflectiveLearningPanel } from './components/LearningApprovalPanel';
import { SetupWatchPanel } from './components/SetupWatchPanel';
import { ResearchCenterPanel } from './components/ResearchCenterPanel';
import { StorageRetentionPanel } from './components/StorageRetentionPanel';
import { EventTimelinePanel } from './components/EventTimelinePanel';
import { CostProviderPanel, SafetyPanel } from './components/SafetyPanel';

export default function App() {
  return (
    <main className="app-shell">
      <Header />
      <div className="dashboard-grid">
        <RuntimeHealthPanel />
        <HermesBrainPanel />
        <SetupWatchPanel />
        <ResearchCenterPanel />
        <StorageRetentionPanel />
        <LearningQueuePanel />
        <ApprovalQueuePanel />
        <ReflectiveLearningPanel />
        <EventTimelinePanel />
        <SafetyPanel />
        <CostProviderPanel />
      </div>
    </main>
  );
}
