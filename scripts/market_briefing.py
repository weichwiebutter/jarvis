import argparse
import json
import requests
from datetime import datetime
from pathlib import Path

CONFIG_PATH = Path.home() / "jarvis" / "config" / "settings.env"
DATA_FILE = Path.home() / "jarvis" / "data" / "market_data.json"

config = {}
if CONFIG_PATH.exists():
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                config[key.strip()] = value.strip()

MODEL_NAME = config.get("OLLAMA_MODEL", "llama3.2:3b")
OPENJARVIS_API = config.get(
    "OPENJARVIS_API",
    "http://127.0.0.1:8000/v1/chat/completions"
)

OBSIDIAN_VAULT_PATH = Path(
    config.get("OBSIDIAN_VAULT_PATH", str(Path.home() / "jarvis" / "obsidian"))
)

REPORT_DIR = OBSIDIAN_VAULT_PATH / "MarketBriefings"
SYSTEM_DIR = OBSIDIAN_VAULT_PATH / "System"

REPORT_DIR.mkdir(parents=True, exist_ok=True)
SYSTEM_DIR.mkdir(parents=True, exist_ok=True)

FEEDBACK_RULES_PATH = SYSTEM_DIR / "feedback_rules.md"


def load_feedback_rules():
    if FEEDBACK_RULES_PATH.exists():
        return FEEDBACK_RULES_PATH.read_text(encoding="utf-8").strip()

    return """
# Jarvis Feedback Rules
- Deutsch.
- Kurz, direkt, professionell.
- Keine erfundenen Daten.
- Kein JSON.
- Markdown-Ausgabe.
"""


def load_market_data():
    if not DATA_FILE.exists():
        return None

    try:
        return json.loads(DATA_FILE.read_text(encoding="utf-8"))
    except Exception:
        return None


def symbol_line(data, name):
    item = data.get("symbols", {}).get(name)

    if not item or item.get("status") != "ok":
        return f"| {name} | n/a | n/a | n/a | n/a | keine verwertbaren Daten |"

    return (
        f"| {name} | {item.get('ticker')} | {item.get('last_close')} | "
        f"{item.get('change')} | {item.get('change_pct')}% | "
        f"{item.get('last_date')} |"
    )


def market_table(data):
    if not data:
        return "Keine Marktdaten verfügbar."

    names = [
        "Gold Futures",
        "EUR/USD",
        "USD/CHF",
        "EUR/CHF",
        "GBP/USD",
        "USD/JPY",
        "Dollar Index",
        "US 10Y Yield",
    ]

    lines = []
    lines.append(f"Quelle: {data.get('source', 'unbekannt')}")
    lines.append(f"Snapshot erstellt: {data.get('created_at', 'unbekannt')}")
    lines.append(f"Hinweis: {data.get('note', '')}")
    lines.append("")
    lines.append("| Instrument | Ticker | Letzter Schluss | Veränderung | Veränderung % | Datum |")
    lines.append("|---|---:|---:|---:|---:|---|")

    for name in names:
        lines.append(symbol_line(data, name))

    return "\n".join(lines)


parser = argparse.ArgumentParser()
parser.add_argument(
    "--mode",
    choices=["morning", "ny_preopen"],
    default="morning"
)
args = parser.parse_args()

mode = args.mode
now = datetime.now()
today = now.strftime("%Y-%m-%d")
timestamp = now.strftime("%Y-%m-%d %H:%M")

if mode == "morning":
    title = f"Morning Market Briefing – {today}"
    filename = f"{today}_Morning.md"
    mode_label = "Morning Briefing Europa"
else:
    title = f"NY Pre-Open Market Briefing – {today}"
    filename = f"{today}_NY.md"
    mode_label = "New York Pre-Open"

report_path = REPORT_DIR / filename

feedback_rules = load_feedback_rules()
market_data = load_market_data()
market_data_text = market_table(market_data)

SYSTEM_PROMPT = f"""
Du bist Jarvis Market Analyst.

WICHTIGSTE REGEL:
Du antwortest AUSSCHLIESSLICH in sauberem Markdown.
Kein JSON.
Keine Funktionsausgabe.
Keine Parameter-Ausgabe.
Keine Codeblöcke.
Keine erfundenen Kurse.
Keine erfundenen Nachrichten.
Keine erfundenen Makrodaten.
Keine erfundenen Inflations-, Arbeitsmarkt- oder Zentralbankdaten.

Rolle:
Institutioneller Macro- und FX-Analyst.
Stil: Trading-Desk-Briefing. Kurz, direkt, nüchtern.

Du darfst nur diese Daten verwenden:
- die Marktdaten aus der Tabelle
- die Information, dass die Quelle ein kostenloser Prototyp ist
- allgemeine Marktlogik

Wenn Informationen fehlen:
klar sagen: "nicht angebunden" oder "aus aktueller Datenlage nicht ableitbar".

Dauerhafte Feedback-Regeln:
{feedback_rules}
"""

USER_PROMPT = f"""
Erstelle ein {mode_label} auf Deutsch.

Datum/Zeit: {timestamp}

VERFÜGBARE MARKTDATEN:
{market_data_text}

AUSGABEFORMAT EXAKT EINHALTEN:

## Executive Summary
- 
- 
- 

## Datenlage
- Quelle:
- Aktualität:
- Einschränkung:

## Gold / XAUUSD
Bias: bullisch / bärisch / neutral

- Datenlage:
- Begründung:
- USD-Faktor:
- Rendite-Faktor:
- Was würde den Bias ändern:

## Forex

### EUR/USD
Bias:
- Datenlage:
- Grund:
- Beobachten:

### USD/CHF
Bias:
- Datenlage:
- Grund:
- Beobachten:

### EUR/CHF
Bias:
- Datenlage:
- Grund:
- Beobachten:

### GBP/USD
Bias:
- Datenlage:
- Grund:
- Beobachten:

### USD/JPY
Bias:
- Datenlage:
- Grund:
- Beobachten:

## Dollar / Renditen
- Dollar Index:
- US 10Y Yield:
- Interpretation:

## Risiken
- 
- 
- 

## Desk Note
Maximal 3 Sätze.

## Disclaimer
Keine Anlageberatung. Keine automatische Handelsentscheidung.

NOCHMAL:
Nur Markdown.
Kein JSON.
Keine erfundenen aktuellen Makrodaten.
"""

print("📡 Starte Jarvis Market Briefing")
print(f"Modus: {mode}")
print(f"Modell: {MODEL_NAME}")
print(f"Market Data: {DATA_FILE}")

response = requests.post(
    OPENJARVIS_API,
    json={
        "model": MODEL_NAME,
        "temperature": 0.2,
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": USER_PROMPT},
        ],
    },
    timeout=240,
)

response.raise_for_status()
data = response.json()
briefing = data["choices"][0]["message"]["content"].strip()

if briefing.startswith("{"):
    repair_prompt = f"""
Wandle die folgende fehlerhafte Ausgabe in sauberes deutsches Markdown um.

Regeln:
- Kein JSON.
- Keine erfundenen Daten.
- Keine neuen Informationen hinzufügen.
- Nur vorhandene Aussagen bereinigen.
- Trading-Desk-Stil.

Fehlerhafte Ausgabe:
{briefing}
"""

    repair_response = requests.post(
        OPENJARVIS_API,
        json={
            "model": MODEL_NAME,
            "temperature": 0.1,
            "messages": [
                {"role": "system", "content": "Du wandelst fehlerhafte Ausgaben in sauberes Markdown um. Keine neuen Fakten hinzufügen."},
                {"role": "user", "content": repair_prompt},
            ],
        },
        timeout=240,
    )

    repair_response.raise_for_status()
    briefing = repair_response.json()["choices"][0]["message"]["content"].strip()

with open(report_path, "w", encoding="utf-8") as f:
    f.write(f"# {title}\n\n")
    f.write(f"**Erstellt:** {timestamp}\n\n")
    f.write(f"**Modus:** `{mode}`\n\n")
    f.write(f"**Market Data:** `{DATA_FILE}`\n\n")
    f.write("---\n\n")
    f.write(briefing)
    f.write("\n\n---\n")
    f.write("Generated by local Jarvis Market Agent.\n")

print("✅ Briefing gespeichert:")
print(report_path)
