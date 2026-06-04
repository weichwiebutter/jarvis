# Architecture Decisions

## AD-001

Trading ist nur Domäne 1.

Hermes entwickelt sich zu einem domänenübergreifenden kognitiven System.

Status: beschlossen

---

## AD-002

Trusted Knowledge benötigt immer Human Review.

Automatische Vertrauensvergabe ist nicht erlaubt.

Status: beschlossen

---

## AD-003

Control Center bleibt read-only.

Keine Runtime-Kommandos.
Keine Broker-Aktionen.
Keine Schreibzugriffe.

Status: beschlossen

---

## AD-004

Promotion Pipeline:

weak
→ promising
→ robust
→ trusted

Trusted nur nach Human Review.

Status: beschlossen

---

## AD-005

Lokale LLMs werden als Ergänzung zu Sonnet evaluiert.

Aktuell:
- Sonnet Referenzmodell
- GPT-OSS lokal
- Qwen lokal
- Kimi offen
- Groq offen

Status: aktiv
