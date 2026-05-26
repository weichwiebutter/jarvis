# Hermes Beta 3 Operational Readiness Check

Stand: 2026-05-26

Ziel: pruefen, ob Hermes Beta 3 fuer kontrollierten automatischen Nachtbetrieb
geeignet ist. Dieser Check hat keine Trading-Ausfuehrung, keine Broker-Orders,
keinen Nightly-Start und keine UI-Aenderung ausgefuehrt.

## Gepruefte Grundlagen

- `AGENTS.md`: Codex-Regeln, keine Parallel-Systeme, Masterplan/TODO zuerst.
- `README.md`: Rollen, Schnellstart, Beta-3-Architekturregeln.
- `docs/Masterplan/Jarvis_Masterplan_V6_Hermes_AI_OS.md`: Supervisor/Scheduler
  als zentrale Dauerbetriebsarchitektur.
- `docs/architecture/future_hermes_todo.md`: Beta-3-Regeln, Trading-Safety,
  Future Trading Control Layer.
- `HermesRuntime/config/schedules.json`: interne Zeitplaene.
- HermesRuntime Supervisor/Scheduler/Nightly-Beta3 CLI und Services.

## Kurzfazit

Beta 3 ist fuer einen kontrollierten Nachtlauf vorbereitet, wenn der Windows
Supervisor-Task installiert und einmal per Smoke-Test geprueft wurde.

Bereit:
- Supervisor Background Mode mit PID, Log und Heartbeat.
- Interner Scheduler aus `HermesRuntime/config/schedules.json`.
- ResourceGuard mit CPU/RAM/Disk-Schutz.
- StorageHygiene mit sicherem Cleanup-Plan ohne automatische Loeschung.
- Stop-Request-Pfad fuer Supervisor und Nightly Beta 3.
- D-Laufwerk/Data-Lake ueber `/mnt/d/HermesData`.
- Secrets/Tokens sind lokal ignoriert und nicht getrackt.

Watchpoints:
- Supervisor lief beim Check nicht aktiv. Das ist ok fuer den Readiness-Check,
  aber der Windows-Task sollte vor dem ersten echten Nachtbetrieb einmal
  manuell verifiziert werden.
- Cleanup-Plan findet viele sichere Kandidaten, aktuell ca. 15.872 Dateien /
  235 MB. Kein `cleanup-apply --safe` wurde ausgefuehrt.
- Nightly-State enthaelt noch den letzten Stop-Request-Status, aber keinen
  laufenden Prozess. Der Supervisor/Nightly-Start loescht Stop-Requests beim
  Start kontrolliert.

## Betriebscheck

| Bereich | Status | Ergebnis |
| --- | --- | --- |
| Start nach Windows-Reboot | bereit | `scripts/windows/install_supervisor_task.ps1` installiert einen AtStartup-Trigger und einen taeglichen Start fuer `start_supervisor.sh`. |
| Supervisor Background Mode | bereit | `supervisor-start --background` nutzt PID-Datei, Log, stale-PID-Erkennung und Duplicate-Schutz. |
| Scheduler Status | bereit | `schedules.json` enthaelt 5 Jobs, davon 4 aktiv. `market_data_refresh` ist bewusst deaktiviert. |
| ResourceGuard | bereit | Letzter Check: CPU 3.75%, RAM 6.83%, freier Speicher 887.77 GB / 88.17%, Action `continue`. |
| StorageHygiene | bereit | Cleanup-Plan wird erzeugt; protected paths verhindern Loeschung von Candles, Research Memory, Auth und wichtigen Strategy-Artefakten. |
| Cleanup-Plan | bereit, Review empfohlen | 15.872 sichere Kandidaten, ca. 235 MB. Nur Plan erzeugt, nichts geloescht. |
| Logs | bereit | Supervisor-Log: `/mnt/d/HermesData/logs/supervisor.log`; Rotation ab 50 MB vorhanden. Nightly-Scripts schreiben separate Logs. |
| Heartbeat | bereit | Heartbeat-Datei vorhanden; beim Check stale, weil Supervisor nicht lief. Status erkennt `running=false`. |
| Stop Request | bereit | `supervisor-stop-request` setzt Supervisor- und Nightly-Stop-Flags; laufende Jobs sollen kontrolliert auslaufen. |
| Recovery nach Absturz | ausreichend fuer Beta 3 | Stale PID, Heartbeat, Scheduler-State und Checkpoints sind vorhanden. Nach Absturz startet der Windows Task/Supervisor erneut und plant intern weiter. |
| Keine doppelten Supervisor-Prozesse | bereit | CLI und WSL-Launcher pruefen laufende Supervisor-Prozesse vor Start. |
| D:/HermesData Nutzung | bereit | `storage.profile.json` zeigt auf `/mnt/d/HermesData`; WSL normalisiert Windows-Pfade wie `D:/HermesData`. |
| Keine Secrets im Repo | ok | `config/ctrader.openapi.local.json` und `data/auth/ctrader_tokens.json` sind ignoriert und nicht getrackt. |

## Gemessene CLI-Ergebnisse

Builds:
- `dotnet build ./Hermes.Runtime.csproj`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build ./cli/Hermes.Cli.csproj`: erfolgreich, 0 Warnungen, 0 Fehler.

Status:
- `supervisor-status`: `running=false`, `stale_pid=false`, letzter Status
  `stopped_by_stop_request`, Safety Flags sichtbar.
- `scheduler-status`: 4 aktivierte Jobs, naechster Job `health_snapshot`,
  `nightly_beta3_research` fuer 23:00 geplant.
- `resource-status`: `Action=continue`, `Should Pause=false`,
  `Should Stop=false`, Storage Root `/mnt/d/HermesData`.
- `storage-status`: Storage Root `/mnt/d/HermesData`, freier Speicher
  887.77 GB, Resource Action `continue`.
- `cleanup-plan`: Plan erzeugt unter
  `/mnt/d/HermesData/reports/storage/cleanup_plan.json`,
  `SafeToApply=true`, keine Dateien geloescht.
- `nightly-status`: kein laufender Nightly-Prozess, naechster Start
  `2026-05-26 23:00:00 +02:00`.

## Kleine Stabilitaetsverbesserungen

Behoben:
- `nightly-status` berechnet `Next Scheduled Start` jetzt frisch aus
  `config/nightly.research.json` und aktueller lokaler Zeit. Dadurch bleibt
  die Anzeige nach alten Stop-States nicht mehr auf einem vergangenen Datum.
- CLI-Safety-Text korrigiert: Supervisor-Befehle sind kontrollierte lokale
  Betriebssteuerung, aber weiterhin keine Trading-Ausfuehrung und keine
  Broker-Orders.
- README-Safety-Text an die aktuelle Supervisor-CLI angepasst.

Nicht geaendert:
- Keine Nightly-Pipeline gestartet.
- Kein Supervisor im Hintergrund gestartet.
- Kein `cleanup-apply --safe` ausgefuehrt.
- Keine Trading-, Order- oder Broker-Aktion ausgefuehrt.

## Empfehlung fuer den ersten Nachtlauf

1. Windows Task installieren oder pruefen:
   `powershell -ExecutionPolicy Bypass -File ./scripts/windows/install_supervisor_task.ps1`
2. In WSL einmal Smoke-Test ohne Nightly-Zeitfenster erzwingen:
   `bash ./scripts/nightly/start_supervisor.sh`
3. Danach Status pruefen:
   `dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-status`
4. Vor 23:00 sicherstellen:
   `dotnet run --project ./cli/Hermes.Cli.csproj -- scheduler-status`
5. Nach dem ersten Nachtlauf pruefen:
   `dotnet run --project ./cli/Hermes.Cli.csproj -- nightly-status`

## Readiness-Einschaetzung

Beta 3 ist operativ ausreichend vorbereitet fuer einen ueberwachten ersten
Nachtbetrieb. Kritische Sicherheitsregeln sind sichtbar:
`no_auto_trading=true`, `human_review_required=true`, keine Trading-Ausfuehrung,
keine Broker-Orders.

Vor unbeaufsichtigtem mehrtaegigem Betrieb sollten der Windows-Task, der
Supervisor-Background-Start und ein sauberer Stop-Request einmal praktisch
verifiziert werden.
