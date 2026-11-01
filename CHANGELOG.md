# Changelog

## 1.0.1.0 — 2026-11-01

- The configuration page is now listed in the dashboard sidebar ("Plugins" section) with a
  paw icon, so the DoesTheDogDie API key is easy to find.

## 1.0.0.0 — 2026-11-01

Initial release.

- DoesTheDogDie API v3 client with `X-API-KEY`, rate limiting, retry with backoff and a
  20-error circuit breaker.
- Matching by IMDb, TMDb and scored title + year, with ambiguity rejection.
- Curated default topics, vote thresholds, spoiler filter and per-topic label overrides.
- Tags merged without touching unrelated tags, plus a cache-only reapply task.
- JSON cache with atomic writes for matches, media and the topic taxonomy.
- Scheduled tasks, item-added/updated automation with anti-loop protection.
- Admin API: refresh, manual match, unmatched list, topics and API key test.
- Configuration page.
- Unit tests for the pure core and the HTTP client.
