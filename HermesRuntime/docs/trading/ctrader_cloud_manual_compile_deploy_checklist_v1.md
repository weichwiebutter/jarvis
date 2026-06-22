# cTrader Cloud Manual Compile & Deploy Checklist V1

Diese Checkliste beschreibt den manuellen Weg, wie HermesPaperBot in der echten cTrader-Umgebung kompiliert, geprüft und als Cloud-Instance gestartet wird.

## 1. Voraussetzungen

- cTrader Desktop mit Algo/cBot-Umgebung
- Zugriff auf den HermesPaperBot-Source
- `Generated/EmbeddedReleasePackage.g.cs` vorhanden
- `HermesPaperBotCTraderWrapper.cs` vorhanden
- `AccessRights.None` erwartet
- Cloud-Features im cTrader-Account verfügbar

## 2. Vorbereitende Repo-Checks

Führe im HermesRuntime-Repo aus:

```bash
bash scripts/check_ctrader_paper_bot_forbidden_refs.sh
bash scripts/preflight_ctrader_paper_bot.sh
dotnet build ./cli/Hermes.Cli.csproj
dotnet run --project ./cli/Hermes.Cli.csproj -- cloud-embedded-release-package
```

Erwarte dabei jeweils einen erfolgreichen Lauf, bevor der manuelle cTrader-Compile beginnt.

## 3. Dateien für cTrader

Für den manuellen Import bzw. die manuelle Projekterstellung in cTrader werden typischerweise diese Dateien benötigt:

- `HermesPaperBotCTraderWrapper.cs`
- `Generated/EmbeddedReleasePackage.g.cs`
- `Models/*.cs`
- `Services/*.cs`

Die restlichen Dateien bleiben außerhalb der cTrader-Umgebung und werden nur übernommen, wenn sie für den Compile erforderlich sind.

## 4. cTrader Compile Check

1. In cTrader Algo ein neues cBot-Projekt anlegen oder ein bestehendes Testprojekt öffnen.
2. Die benötigten HermesPaperBot-Dateien importieren oder kopieren.
3. Sicherstellen, dass der Build mit `AccessRights.None` erfolgt.
4. Kompilieren.
5. Fehler dokumentieren.

Besonders prüfen:

- `Bars.TimeFrame` SDK-Kompatibilität
- `Timer`-API
- `AccessRights.None`
- keine Order-, Account- oder Positions-Referenzen

## 5. Cloud Start Check

1. Den cBot lokal im Paper-Modus starten.
2. Prüfen, dass Print-Ausgaben `broker_action=none` zeigen.
3. Sicherstellen, dass keine Orders erzeugt werden.
4. Danach die Cloud-Instance starten.
5. Prüfen, ob die Cloud-Instance läuft.
6. Runtime Summary und Print-Ausgaben prüfen.

## 6. Akzeptanzkriterien

- Compile PASS
- Guard PASS
- Preflight PASS
- Cloud Instance startet
- `no_auto_trading=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`
- keine Positionen oder Orders erzeugt

## 7. Blocker

Ein Deployment ist blockiert bei:

- Compile-Fehler
- `AccessRights` ist nicht `None`
- verbotene API-Referenz
- Safety-Flag-Verletzung
- Embedded Package fehlt oder ist ungültig
- Cloud Instance startet nicht
- `broker_action != none`

## 8. Rollback

Wenn ein neuer Stand Probleme verursacht:

1. Vorherige Generated-Datei bzw. Bot-Version behalten.
2. Cloud-Instance stoppen.
3. Vorherige Version deployen.
4. Erneut Akzeptanz prüfen.

## 9. Offene Punkte

- Export von Runtime Summary aus cTrader Cloud
- exakte SDK-Versionen
- Signierung
- spätere Demo-Execution getrennt spezifizieren

