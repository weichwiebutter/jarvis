import { Header } from './components/Header';
import { CockpitShell } from './components/CockpitShell';

export default function App() {
  return (
    <main className="app-shell control-center-shell">
      <Header />
      <CockpitShell />
    </main>
  );
}
