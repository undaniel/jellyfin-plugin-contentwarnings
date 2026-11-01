# DoesTheDogDie API — verification notes

Date: 2026-11-01
Status: endpoints and JSON shapes verified against the official API v3 documentation.
No live API key was available in the implementation environment, and unauthenticated
requests to `www.doesthedogdie.com` are stopped by a Cloudflare challenge (HTTP 403).
Therefore there is **no captured live response** in `TestData/`; the samples there are
taken verbatim from the official documentation. Treat them as documentation fixtures,
not as evidence that the production API currently returns exactly that shape.

Source: <https://www.doesthedogdie.com/api/3.0>

## Auth

Every request needs a personal API key from the user's profile:

```
X-API-KEY: ddd_...
Accept: application/json
```

The plugin never ships a key. Each administrator uses their own. Missing or invalid
keys return 401 (`missing_api_key` / `invalid_api_key`). 403 can also mean
`upgrade_required`, an endpoint outside the account tier, or a Cloudflare block.

## Endpoints used

| Purpose | Request |
|---|---|
| Search by IMDb | `GET https://www.doesthedogdie.com/api/v3/items?imdb=tt0050798` |
| Search by TMDb | `GET https://www.doesthedogdie.com/api/v3/items?tmdb=22660` |
| Search by title + year | `GET https://www.doesthedogdie.com/api/v3/items?q=Old%20Yeller%201957` |
| Item detail + votes | `GET https://www.doesthedogdie.com/api/v3/items/10752` |
| Topic taxonomy | `GET https://www.doesthedogdie.com/api/v3/topics` |

Search returns an array. Item detail is one object. `items?name=<exact>&releaseYear=<year>`
also exists; the plugin uses `q` because it tolerates localized titles and minor spelling
differences.

### Search result

```json
[
  {
    "id": 10752,
    "name": "Old Yeller",
    "releaseYear": 1957,
    "itemTypeId": 15,
    "itemTypeName": "Movie",
    "tmdbId": 22660,
    "imdbId": "tt0050798"
  }
]
```

### Item detail

```json
{
  "id": 10752,
  "name": "Old Yeller",
  "releaseYear": 1957,
  "itemTypeName": "Movie",
  "topicItemStats": [
    {
      "topicItemId": 395866,
      "yesSum": 57,
      "noSum": 3,
      "numComments": 7,
      "topicId": 153,
      "topicName": "a dog dies",
      "itemId": 10752
    }
  ]
}
```

`numComments` is deliberately not stored: comments contain spoilers.

### Topics

```json
[
  {
    "id": 153,
    "name": "a dog dies",
    "notName": "no dogs die",
    "keywords": "Dog death, canine death",
    "doesName": "Does the dog die",
    "listName": "where the dog dies",
    "minimalName": "dogs dying",
    "topicCategoryId": 56
  }
]
```

## Differences from the original specification

1. **The old `/dddsearch` and `/media/<id>` endpoints are not used.** The current
   documented API is `/api/v3/items` and `/api/v3/items/{id}`. Item pages are still
   linked as `https://www.doesthedogdie.com/media/{id}` for humans.
2. **`isSpoiler` is not part of the v3 topic or stat shape.** The plugin keeps the
   field in its models and honours `IncludeSpoilerTopics`, but until DoesTheDogDie
   exposes a spoiler flag every topic arrives with `isSpoiler = false`.
3. **Default topic ids are not hardcoded.** Without a key it was impossible to fetch
   the real ids, and hardcoded ids can silently point at the wrong topic if the
   taxonomy changes. `Core/DefaultTopics.cs` therefore identifies the curated topics by
   normalized topic name/keywords and `WarningEvaluator` applies them by name. The ids
   shown on the configuration page come from the live `/topics` response. Explicit
   `EnabledTopicIds` still use ids.
4. **No comments or timestamps are fetched.** Only the rolled-up `topicItemStats`.

## Rate limits (verified docs)

| Tier | Requests / minute | Requests / month | Commercial use |
|---|---|---|---|
| Free | 30 | 5,000 | No |
| Commercial | 600 | 500,000 | Yes |

The plugin defaults to 1 request/second. That is **above** the Free tier (30/min), so
free-tier users should lower `RequestsPerSecond` to `0.4` in the configuration page.
429 responses include `Retry-After`; the plugin honours it and also backs off
1 s, 2 s, 4 s, 8 s, 16 s. After 20 consecutive errors the current cycle stops.

The Free tier is non-commercial only. Review the API Terms of Service before deploying
this plugin on a monetized Jellyfin instance.

## Attribution (required)

The exact phrase is **Powered by DoesTheDogDie.com**, linked to
<https://www.doesthedogdie.com> wherever the data appears. The plugin shows it on the
configuration page and in the `attribution` field of the item API response.

## What could not be verified

- Live response bodies, field nullability and exact error payloads.
- Whether a Cloudflare challenge can hit plugin traffic even with a valid API key.
  If it does, the client reports the 403 as an auth error and the task aborts.
- Real topic ids for the curated defaults.
- Any spoiler metadata, because it is absent from the documented v3 shape.
