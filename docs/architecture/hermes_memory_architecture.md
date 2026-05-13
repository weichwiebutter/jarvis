# Hermes Memory Architecture Foundation
Version: 1.0
Status: FOUNDATION
System: Jarvis Hybrid Architecture

---

# 1. Ziel

Dieses Dokument beschreibt die geplante Memory-Architektur fuer Hermes.

Es ist eine Architektur- und Foundation-Dokumentation. Es baut keine echte
Memory-Engine, keine Embeddings, keine Datenbank und keine Runtime-Services.

Hermes Memory soll kuenftig drei getrennte Wissensebenen nutzen:

- Runtime Learning Layer
- Structured Memory Layer
- Obsidian Knowledge Layer

Die Trennung ist absichtlich. Jede Ebene hat einen anderen Zweck, andere
Sicherheitsanforderungen und eine andere Lebensdauer.

---

# 2. Nicht-Ziele

Diese Foundation definiert ausdruecklich NICHT:

- keine produktive Memory-Engine
- keine Embedding-Pipeline
- keine Vektordatenbank
- keine neue Datenbank
- keine automatische Langzeitspeicherung
- keine Runtime-Schreibprozesse
- keine Services oder Hintergrundprozesse
- keine Aenderung bestehender Runtime-Daten

---

# 3. Drei Wissensebenen

## A) Runtime Learning Layer

Pfad:

```text
.hermes/
```

Zweck:

- adaptive routing
- runtime learnings
- prediction feedback
- success/failure patterns
- agent metrics
- confidence history

Der Runtime Learning Layer ist fuer operative Lernsignale vorgesehen. Er
beschreibt, was Hermes aus laufenden Entscheidungen, Delegationen und
Ergebnissen ableitet.

Typische Inhalte:

- welche Agenten fuer bestimmte Aufgaben gut funktioniert haben
- wann Routing-Entscheidungen erfolgreich oder fehlerhaft waren
- welche Vorhersagen eingetroffen sind
- wie sicher Hermes bei bestimmten Entscheidungen war
- welche Fehlermuster wiederholt auftreten
- welche Agenten stabile oder instabile Resultate liefern

Charakter:

- maschinenorientiert
- laufzeitnah
- stark auditierbar
- nicht als menschliches Wissensarchiv gedacht
- nicht als allgemeine Projektdokumentation gedacht

Hermes darf diese Ebene kuenftig nur innerhalb klarer Approval- und
Safety-Regeln beschreiben. Runtime Learning ist kein Freibrief fuer stille,
dauerhafte Persoenlichkeits- oder Nutzerprofile.

---

## B) Structured Memory Layer

Pfad:

```text
memory/
```

Zweck:

- strukturierte Wissenseintraege
- persoenliche Praeferenzen
- bekannte Projekte
- bekannte Systeme
- Retrieval/semantic memory spaeter

Der Structured Memory Layer ist fuer dauerhaftes, strukturiertes Wissen
vorgesehen. Diese Ebene soll spaeter von Hermes gezielt abgefragt werden
koennen, ohne dass daraus automatisch eine Datenbankpflicht entsteht.

Typische Inhalte:

- stabile Nutzerpraeferenzen
- bekannte Projektkontexte
- bekannte Systeme und Rollen
- wiederverwendbare Arbeitsweisen
- freigegebene Fakten ueber Tools, Workflows oder Umgebungen
- spaetere Retrieval-Metadaten

Charakter:

- langlebiger als Runtime Learning
- strukturierter als Obsidian-Notizen
- freigabepflichtig bei kritischen Inhalten
- geeignet fuer spaeteres semantisches Retrieval
- nicht geeignet fuer ungefilterte Logs oder rohe Runtime-Daten

Structured Memory soll Wissen speichern, das bewusst wiederverwendet werden
darf. Es ist nicht der Ort fuer jede Beobachtung, jeden Fehler oder jede
temporare Session-Information.

---

## C) Obsidian Knowledge Layer

Pfad:

```text
obsidian/
```

Zweck:

- Architekturwissen
- Masterplaene
- Strategien
- Research
- UI-Ideen
- menschliche Dokumentation

Der Obsidian Knowledge Layer ist fuer menschlich lesbares Wissen vorgesehen.
Er dient als Denk-, Planungs- und Dokumentationsraum fuer Architektur,
Strategie und langfristige Konzepte.

Typische Inhalte:

- Masterplaene
- Architekturentscheidungen
- strategische Roadmaps
- Research-Zusammenfassungen
- Produkt- und UI-Ideen
- manuell gepflegte Systemdokumentation

Charakter:

- menschenorientiert
- erklaerend
- narrativ und kontextreich
- geeignet fuer Review und Planung
- nicht als Runtime-State gedacht
- nicht als automatische Wahrheitsschicht gedacht

Obsidian ist kein Ersatz fuer strukturierte Memory-Eintraege. Es ist ein
Arbeits- und Wissensraum fuer Menschen und fuer bewusst exportierte
Dokumentation.

---

# 4. Warum nicht eine einzige Wissensdatenbank?

Nicht alles gehoert in eine zentrale Wissensdatenbank.

Die drei Ebenen haben unterschiedliche Anforderungen:

- Runtime Learning braucht schnelle, operative Feedbacksignale.
- Structured Memory braucht klare Struktur, Freigabe und Wiederverwendbarkeit.
- Obsidian braucht Lesbarkeit, Kontext und menschliche Pflege.

Eine einzige Wissensdatenbank wuerde diese Unterschiede vermischen.

Risiken einer Einheitsdatenbank:

- operative Fehlerdaten wuerden wie dauerhaftes Wissen wirken
- menschliche Strategieplaene wuerden mit Runtime-Metriken vermischt
- persoenliche Praeferenzen koennten ohne klare Freigabe entstehen
- alte oder falsche Beobachtungen koennten zu stark gewichtet werden
- Audit, Review und Loeschung wuerden schwieriger
- Retrieval koennte irrelevante oder sensible Daten bevorzugen
- Hermes koennte kurzfristige Muster faelschlich als langfristige Wahrheit behandeln

Die Memory-Architektur folgt deshalb dem Prinzip:

```text
Different knowledge types need different storage, review, and aging rules.
```

Hermes soll Wissen nicht nur speichern, sondern wissen, welche Art von Wissen
es ist.

---

# 5. Geplante Zukunftskomponenten

## Hermes Memory Manager

Zentrale Komponente fuer geplante Memory-Operationen.

Aufgaben:

- Memory-Lesezugriffe koordinieren
- Memory-Schreibvorschlaege vorbereiten
- Approval-Regeln anwenden
- Ebenen sauber trennen
- Konflikte zwischen Memory-Quellen markieren
- Audit-Informationen bereitstellen

Der Memory Manager entscheidet nicht autonom ueber kritische dauerhafte
Speicherung. Er bereitet Entscheidungen strukturiert vor.

---

## Context Compression

Komponente fuer die Verdichtung langer Sessions, Plaene und Ergebnisverlaeufe.

Ziele:

- wichtige Fakten erhalten
- irrelevante Details entfernen
- Entscheidungen nachvollziehbar zusammenfassen
- Uebergaben zwischen Sessions stabilisieren
- Kontextfenster effizient nutzen

Context Compression darf keine stillen neuen Wahrheiten erzeugen. Verdichtete
Zusammenfassungen muessen nachvollziehbar bleiben.

---

## Knowledge Aging

Komponente fuer Alterung, Gewichtung und Verfall von Wissen.

Ziele:

- alte Beobachtungen schwacher gewichten
- veraltete Informationen markieren
- wiederholt bestaetigtes Wissen staerken
- widerspruechliche Informationen sichtbar machen
- Review-Zeitpunkte vorschlagen

Nicht jedes gespeicherte Wissen bleibt dauerhaft gleich wichtig.

---

## Obsidian Export

Komponente fuer bewusst erzeugte, menschlich lesbare Exporte.

Ziele:

- Architekturentscheidungen dokumentieren
- Lernberichte fuer Review bereitstellen
- Research oder Strategien in Markdown ueberfuehren
- Memory-Zusammenfassungen manuell pruefbar machen

Obsidian Export ist kein automatischer Dump aller Runtime-Daten.

---

## Semantic Retrieval

Spaetere Komponente fuer semantische Suche ueber freigegebene Memory-Inhalte.

Ziele:

- relevante strukturierte Erinnerungen finden
- bekannte Projekte und Systeme kontextuell abrufen
- Praeferenzen situationsbezogen beruecksichtigen
- Obsidian-Dokumentation bei Bedarf referenzieren

Semantic Retrieval ist eine spaetere Ausbaustufe. Diese Foundation legt nur
die Trennung der Wissensebenen fest.

---

## Memory Prioritization

Komponente fuer Gewichtung und Auswahl relevanter Memory-Inhalte.

Ziele:

- kritische Fakten vor weichen Hinweisen priorisieren
- aktuelle Projektkontexte bevorzugen
- Nutzerfreigaben hoeher gewichten als implizite Muster
- alte oder unsichere Informationen abwerten
- Konflikte sichtbar machen statt automatisch zu entscheiden

Hermes soll nicht einfach alles abrufen, sondern das passende Wissen mit
passender Gewichtung nutzen.

---

## Archive Strategy

Komponente fuer Archivierung, Review und Deaktivierung alter Informationen.

Ziele:

- alte Runtime-Learnings archivieren
- obsolete Memory-Eintraege markieren
- historische Entscheidungen nachvollziehbar halten
- aktive Memory-Basis klein und hochwertig halten
- sensible oder kritische Daten gezielt reviewen

Archivierung bedeutet nicht zwingend Loeschung. Sie bedeutet, dass Informationen
nicht mehr unkritisch als aktiver Kontext verwendet werden.

---

# 6. Trading Learning Foundation

Pfad:

```text
.hermes/trading/
```

Der Trading-Learning-Bereich ist als spezialisierter Teil des Runtime Learning
Layers vorgesehen.

Zweck:

- predictions
- outcomes
- confidence
- accuracy
- timeframe learning

Geplante Lernsignale:

- welche Marktprognosen erstellt wurden
- welcher Zeithorizont angenommen wurde
- welche Confidence Hermes oder ein Trading-Agent angegeben hat
- welches Ergebnis spaeter eingetreten ist
- wie genau die Prognose war
- welche Bedingungen die Prognose beeinflusst haben

Beispielhafte Kategorien:

- short-term predictions
- medium-term predictions
- long-term predictions
- market regime assumptions
- confidence calibration
- accuracy history
- recurring failure patterns

Safety-Grenzen:

- Trading Learning ist kein autonomes Handelssystem.
- Trading Learning darf keine Trades ausfuehren.
- Trading Learning ersetzt keine menschliche Entscheidung.
- Kritische oder finanzielle Schlussfolgerungen brauchen Review.
- Prediction-Historie darf nicht als sichere Zukunftsaussage behandelt werden.

Ziel ist bessere Kalibrierung, nicht automatische Aktion.

---

# 7. Safety-Prinzipien

## Approval-Based Persistence

Dauerhafte oder kritische Memory-Eintraege duerfen nicht still entstehen.

Hermes soll kuenftig zwischen folgenden Kategorien unterscheiden:

- rein temporarer Kontext
- runtime-nahes Lernsignal
- strukturierter Memory-Vorschlag
- freigegebener Memory-Eintrag
- kritisch reviewpflichtiger Memory-Eintrag

Je dauerhafter und sensibler ein Eintrag ist, desto klarer muss die Freigabe
sein.

---

## No Silent Long-Term Learning

Hermes darf keine langfristigen Nutzerprofile, Praeferenzen oder kritischen
Systemannahmen ohne sichtbare Freigabe speichern.

Stilles Lernen ist nur fuer begrenzte, operative Metriken denkbar und muss
auditierbar bleiben. Langfristige Bedeutung braucht menschliche Kontrolle.

---

## Human Review For Critical Memory

Kritische Memory-Inhalte brauchen menschlichen Review.

Dazu gehoeren insbesondere:

- Sicherheitsregeln
- finanzielle Annahmen
- persoenliche Praeferenzen mit hoher Wirkung
- Projektentscheidungen mit Langzeitwirkung
- Systemarchitektur
- externe Provider- oder Kostenentscheidungen
- Inhalte, die Verhalten dauerhaft veraendern

Hermes soll kritische Memory-Eintraege markieren, erklaeren und zur Freigabe
vorlegen.

---

# 8. Grundprinzip

Hermes Memory ist kein einzelner Speicherort, sondern eine kontrollierte
Wissensarchitektur.

```text
.hermes/   = runtime learning and adaptive feedback
memory/    = structured, approved, reusable knowledge
obsidian/  = human-readable architecture and strategy knowledge
```

Die Foundation trennt operative Lernsignale, strukturierte Erinnerung und
menschliche Dokumentation. Dadurch bleiben Review, Sicherheit, Retrieval und
langfristige Pflege kontrollierbar.
