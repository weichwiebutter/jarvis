# cTrader In-Memory Timer Logging

## Summary

In the HermesPaperBot cTrader runtime, timer logging can fall back to an in-memory path when file IO is unavailable or restricted in the cTrader environment.

This is expected behavior and does not indicate a bot failure.

## What happens

- `PaperLogger.WriteTimer(...)` attempts to write timer entries to the configured log file path.
- If cTrader blocks file IO or the path is not writable, the logger falls back to in-memory storage.
- The cTrader OnTimer log still exposes:
  - `timer_log_written`
  - `timer_log_path`
  - `timer_log_fallback`
  - `timer_tick_count`
  - `session_started_at`
  - `last_timer_at`

## Interpretation

- `timer_log_fallback=in_memory` means the timer log was captured, but not persisted to file.
- `timer_tick_count > 0` in the OnTimer log confirms that timer cycles are being processed.
- `paper-forward-session-report` may still show `timer_ticks=0` if it only reads the file-backed log path and no file log entries were written.

## Important note

`paper-forward-session-report` showing `timer_ticks=0` under an in-memory fallback is not a bot malfunction.
It only means the session report could not recover timer ticks from a file log and did not use the cTrader in-memory timer counter.

## Operational guidance

- Use the cTrader OnTimer log for the authoritative runtime tick evidence when `timer_log_fallback=in_memory`.
- Use `paper-forward-session-report` for file-backed session analysis.
- If file logging is required, ensure the configured log path is writable in the current cTrader environment.

