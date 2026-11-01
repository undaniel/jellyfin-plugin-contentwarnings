# Changelog

## 1.0.0.0 — 2026-11-01

First release.

- DoesTheDogDie API v3 client with `X-API-KEY`, rate limiting, retry with backoff and a
  20-error circuit breaker.
- Matching by IMDb, TMDb and scored title + year, with ambiguity rejection.
- Curated default topics, vote thresholds, spoiler filter and per-topic label overrides.
- Tags merged without touching unrelated tags, plus a cache-only reapply task.
- Optional warning badge baked into movie and series posters and episode stills, so it
  shows in every client. Episodes inherit the badge of their series. The original image
  path is restored when the warnings disappear or the option is turned off.
- JSON cache with atomic writes for matches, media, the topic taxonomy and the poster badge
  state.
- Scheduled tasks, item-added/updated automation with anti-loop protection.
- Admin API: refresh, manual match, unmatched list, topics and API key test.
- Configuration page, listed in the dashboard sidebar, with API key test, thresholds,
  topic selection/translation, library selection and a manual-match table.
- Unit tests for the pure core, the HTTP client and the badge renderer.
