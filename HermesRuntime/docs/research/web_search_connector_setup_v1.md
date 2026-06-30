# Hermes Web Search Connector Setup V1

## Purpose

Hermes can optionally fetch external web sources for controlled research tasks. The connector is read-only and must never promote sources to trusted automatically.

## Supported Providers

- `none`
- `generic_http_json`

## Required Environment Variables

- `HERMES_WEB_SEARCH_PROVIDER`
- `HERMES_WEB_SEARCH_ENDPOINT`
- `HERMES_WEB_SEARCH_API_KEY`
- `HERMES_WEB_SEARCH_MAX_RESULTS`
- `HERMES_WEB_SEARCH_ALLOWED_DOMAINS`

## Recommended V1 Setup

- `HERMES_WEB_SEARCH_PROVIDER=generic_http_json`
- `HERMES_WEB_SEARCH_ENDPOINT=<controlled search endpoint>`
- `HERMES_WEB_SEARCH_API_KEY=<secret, never commit>`
- `HERMES_WEB_SEARCH_MAX_RESULTS=10`
- `HERMES_WEB_SEARCH_ALLOWED_DOMAINS=spotware.com,github.com,learn.microsoft.com,docs.microsoft.com`

## Behavior

- If provider is `none`, Hermes reports `blocked_no_web_connector`.
- If provider is `generic_http_json`, Hermes sends a controlled JSON request and defensively parses the response.
- Imported results become `web_research_import_candidates.json` entries only.
- Every imported candidate remains `human_review_status=pending`.
- No source is marked trusted automatically.

## Safety Rules

- `research_only=true`
- no trading execution
- no broker action
- no auto-trading
- human review required
- no fake sources

## Notes for Frank

Set the environment variables in your local shell/session manager, not in Git. Use the `web-search-connector-status` CLI command to verify the connector before running automated fetch.
