# GER40 Chart Annotation Data Path Recommendation

## Ziel

GER40 soll `entry_price`, `stop_loss_price`, `take_profit_1`, `take_profit_2`, `invalidation_level` und `risk_reward` bekommen, ohne Trading-Logik zu ändern.

## Ausgangslage

Aktuell ist GER40:

- im Source Package vorhanden
- `bot_ready`
- aber ohne passende Chart Annotation
- daher im Exportpfad ohne Entry-/SL-/TP-Felder

## Optionen

### 1. GER40 Annotation aus bestehendem Signal/Spec ableiten

**Beschreibung**
- Eine interne Abbildungslogik nutzt vorhandene GER40-Signal-/Spec-Daten als Quelle für eine Chart Annotation.
- Die Annotation wird aus bereits vorhandenen nicht-tradenden Daten erzeugt.

**Vorteile**
- Kein manueller Pflegeaufwand pro Release
- Konsistent, wenn die Spec schon valide ist
- Passt zur bestehenden Embedded-Release-Pipeline

**Nachteile**
- Nur sinnvoll, wenn die zugrunde liegende GER40-Quelle ausreichend vollständig ist
- Kann bei unvollständigen Spec-Daten wieder leer bleiben

**Empfehlung**
- **Beste Minimal-Option**, falls eine valide interne GER40-Spec bereits existiert

---

### 2. GER40 Annotation manuell als Review-Artefakt erzeugen

**Beschreibung**
- GER40 bekommt ein separates Review-Artefakt mit Entry/SL/TP/Invalidation/RR.
- Dieses Artefakt wird als Freigabequelle genutzt, nicht als Trading-Ausführung.

**Vorteile**
- Schnell
- Explizit reviewbar
- Trennung von Datenfreigabe und Runtime

**Nachteile**
- Manuell
- Zusätzliche Pflege
- Risiko von Drift gegenüber zukünftigen Signal-/Spec-Änderungen

**Empfehlung**
- **Beste Fallback-Option**, wenn die interne Ableitung noch nicht zuverlässig möglich ist

---

### 3. GER40 weiter blockiert lassen bis echtes Signal erzeugt wird

**Beschreibung**
- Keine Annotation wird ergänzt.
- GER40 bleibt im Export `paper_entry_enabled=false` bzw. ohne Entry-Felder, bis später ein echtes Signal die Daten liefert.

**Vorteile**
- Am konservativsten
- Keine zusätzliche Datenpflege

**Nachteile**
- GER40 bleibt blockiert
- Keine Fortschritte beim cTrader-Paperbot
- Reduziert Nutzbarkeit der vorhandenen GER40-bot_ready-Daten

**Empfehlung**
- Nur als **sicherster Notfallpfad**

## Empfehlung

**Minimale sinnvolle Lösung:**

1. Zuerst **Option 1** prüfen:
   - GER40-Annotation aus bestehender interner Signal-/Spec-Quelle ableiten
2. Wenn das nicht robust genug ist:
   - **Option 2** als manuelles Review-Artefakt
3. **Option 3** nur wählen, wenn weder interne Ableitung noch Review-Artefakt aktuell freigegeben werden sollen

## Ergebnis für den aktuellen Stand

Für GER40 fehlt derzeit eine Chart Annotation, daher sind Entry-/SL-/TP-Felder im Export nicht vorhanden.
Die schnellste nachhaltige Lösung ist ein **GER40 Review-/Annotation-Artefakt**, idealerweise abgeleitet aus vorhandenen GER40-Spec-Daten.

