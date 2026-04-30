import json
from datetime import datetime
from pathlib import Path

import yfinance as yf

# =========================
# PFAD
# =========================
DATA_DIR = Path.home() / "jarvis" / "data"
DATA_DIR.mkdir(parents=True, exist_ok=True)

OUTPUT_FILE = DATA_DIR / "market_data.json"

# =========================
# SYMBOLE
# =========================
SYMBOLS = {
    "Gold Futures": "GC=F",
    "EUR/USD": "EURUSD=X",
    "USD/CHF": "CHF=X",
    "EUR/CHF": "EURCHF=X",
    "GBP/USD": "GBPUSD=X",
    "USD/JPY": "JPY=X",
    "Dollar Index": "DX-Y.NYB",
    "US 10Y Yield": "^TNX",
}

# =========================
# HILFSFUNKTION
# =========================
def safe_float(value):
    try:
        if value is None:
            return None
        return round(float(value), 5)
    except Exception:
        return None

# =========================
# DATEN LADEN
# =========================
def get_symbol_data(name, ticker):
    try:
        obj = yf.Ticker(ticker)
        hist = obj.history(period="5d", interval="1d")

        if hist.empty:
            return {
                "name": name,
                "ticker": ticker,
                "status": "no_data",
                "error": "Keine Daten erhalten",
            }

        last = hist.iloc[-1]
        prev = hist.iloc[-2] if len(hist) > 1 else None

        last_close = safe_float(last.get("Close"))
        prev_close = safe_float(prev.get("Close")) if prev is not None else None

        change = None
        change_pct = None

        if last_close is not None and prev_close not in (None, 0):
            change = round(last_close - prev_close, 5)
            change_pct = round((change / prev_close) * 100, 3)

        return {
            "name": name,
            "ticker": ticker,
            "status": "ok",
            "last_close": last_close,
            "previous_close": prev_close,
            "change": change,
            "change_pct": change_pct,
            "last_date": str(hist.index[-1].date()),
        }

    except Exception as e:
        return {
            "name": name,
            "ticker": ticker,
            "status": "error",
            "error": str(e),
        }

# =========================
# MAIN
# =========================
def main():
    snapshot = {
        "created_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "source": "yfinance (Yahoo Finance)",
        "note": "Kostenloser Prototyp – Daten können ungenau oder verzögert sein.",
        "symbols": {},
    }

    for name, ticker in SYMBOLS.items():
        print(f"📡 Lade {name} ({ticker})...")
        snapshot["symbols"][name] = get_symbol_data(name, ticker)

    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(snapshot, f, ensure_ascii=False, indent=2)

    print("\n✅ Marktdaten gespeichert:")
    print(OUTPUT_FILE)

if __name__ == "__main__":
    main()
