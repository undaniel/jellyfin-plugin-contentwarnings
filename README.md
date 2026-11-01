# Content Warnings for Jellyfin

A Jellyfin plugin that turns the community content warnings from
[DoesTheDogDie.com](https://www.doesthedogdie.com) into native Jellyfin tags such as
`CW: Dog Death`, `CW: Flashing Lights` or `CW: Sexual Assault`.

Because the output is a normal tag, it works in every Jellyfin client without client
changes: tags appear on the item page, can be filtered, and can be used by each user's
**Parental Control > Block items with tags** setting.

> Data: **Powered by DoesTheDogDie.com**. This plugin is not affiliated with
> DoesTheDogDie.com. Each administrator needs their own API key.

## Features

- Matches **movies and series** by IMDb id, TMDb id, then title + year with a scored,
  ambiguity-rejecting matcher.
- Applies only tags that start with the configured prefix (`CW: ` by default) and never
  touches other tags.
- Caches matches and fetched warnings as JSON with atomic writes; network calls respect a
  configurable rate limit, `Retry-After`, exponential backoff and a 20-error circuit
  breaker.
- Scheduled task **Update content warnings** (daily) plus a fast **Reapply content warning
  tags** task that only reads the cache.
- New items are processed automatically after a 60 s debounce; metadata refreshes are
  repaired after a short debounce, with anti-loop protection.
- Admin API for manual matches, unmatched items and the topic list.
- Configuration page with API key test, thresholds, topic selection/translation, library
  selection and a manual-match table.

## Requirements

- Jellyfin **12.0 or newer** (.NET 10). Built against `Jellyfin.Controller` 12.1.0.
- A personal DoesTheDogDie API key. The plugin never includes one.
- The DoesTheDogDie **Free tier allows 30 requests/minute and 5,000/month and is
  non-commercial only**. Set `RequestsPerSecond` to about `0.4` on the Free tier, or use a
  Commercial key. See [docs/dtdd-api-notes.md](docs/dtdd-api-notes.md).

A Jellyfin 10.11 (.NET 9) build is **not** included in this repository.

## Install

### From a plugin repository

Add this URL in **Dashboard > Plugins > Repositories**, then install **Content Warnings**
from the catalog:

```
https://github.com/undaniel/jellyfin-plugin-contentwarnings/releases/latest/download/manifest.json
```

Updates are offered automatically with each new release.

### Manually from a release

1. Download the release zip.
2. Extract it into a folder under your Jellyfin `plugins` directory, for example
   `.../jellyfin/plugins/ContentWarnings/`.
3. Restart Jellyfin.
4. Open **Dashboard > Plugins > Content Warnings**.

### From source

```bash
dotnet publish Jellyfin.Plugin.ContentWarnings/Jellyfin.Plugin.ContentWarnings.csproj \
  -c Release -o artifacts

cp artifacts/Jellyfin.Plugin.ContentWarnings.dll \
   /path/to/jellyfin/plugins/ContentWarnings/
```

Restart Jellyfin after copying the DLL.

## Configuration

1. Paste your DoesTheDogDie API key and click **Test connection**.
2. Review the vote thresholds:
   - `MinYesVotes` (default 3): minimum community "yes" votes.
   - `MinYesRatio` (default 0.6): `yes / (yes + no)` must reach this value.
3. Pick topics. If none is checked, a curated set of high-value topics is used
   (animal death, spiders, flashing lights, gore, sexual assault, suicide, drowning, and
   more). Use the text field next to a topic to translate or rename its tag.
4. Pick libraries, or leave everything unchecked for all movie and series libraries.
5. Save, then run **Dashboard > Scheduled Tasks > Update content warnings** once.

Cache files live under `{Jellyfin data directory}/contentwarnings/`:
`matches.json`, `media/<dtddId>.json` and `topics.json`.

## Hiding content per user

1. Open **Dashboard > Users**, edit a user.
2. Go to **Parental Control**.
3. Under **Block items with tags**, add the tags that user should not see, for example
   `CW: Dog Death` or `CW: Sexual Assault`.
4. Save.

Jellyfin 12 resolves blocked tags through `BaseItem.GetInheritedTags()`, which includes
the tags of parent items. A tag blocked on a **Series** therefore also hides its seasons
and episodes. The plugin does not tag episodes itself.

## HTTP API

| Method | Route | Access | Purpose |
|---|---|---|---|
| `GET` | `/ContentWarnings/Items/{itemId}` | authenticated user | Cached warnings for one item |
| `POST` | `/ContentWarnings/Items/{itemId}/Refresh` | admin | Force matching and fetching |
| `PUT` | `/ContentWarnings/Items/{itemId}/Match` | admin | Manual `{ "dtddId": 123 }`; `null` means "no match, do not retry" |
| `GET` | `/ContentWarnings/Topics` | admin | Topic taxonomy for the config page |
| `GET` | `/ContentWarnings/Unmatched` | admin | Items without a match |
| `POST` | `/ContentWarnings/TestKey` | admin | Test the configured API key |

Example item response:

```json
{
  "itemId": "…",
  "dtddId": 12345,
  "dtddUrl": "https://www.doesthedogdie.com/media/12345",
  "matchMethod": "Imdb",
  "fetchedAt": "2026-10-31T12:00:00+00:00",
  "warnings": [
    { "topicId": 153, "label": "Dog Death", "yes": 41, "no": 2, "ratio": 0.9535, "isSpoiler": false }
  ],
  "attribution": "Powered by DoesTheDogDie.com"
}
```

## Matching and evaluation

1. Manual override (never replaced automatically).
2. IMDb id through `items?imdb=`.
3. TMDb id through `items?tmdb=`.
4. Title + year through `items?q=` (also tries `OriginalTitle`). Candidates are scored:
   exact normalized title +60, similarity >= 0.9 +40, same year +30 / ±1 year +15,
   same media type +10. The best candidate is accepted only at score >= 80 **and** with a
   lead of >= 15 over the runner-up.

A topic becomes a tag when it passes the vote thresholds, the topic filter and the
spoiler filter. Tags are stable: label overrides and the tag prefix are the only things
that change them, and old plugin tags are removed during the next application.

## Limitations

- The current DoesTheDogDie v3 API does not expose an `isSpoiler` flag, so
  `IncludeSpoilerTopics` has no effect until they add one.
- No scene timestamps, skipping or silencing.
- No separate episodes/seasons tags; Jellyfin's inherited tags cover them.
- The plugin has unit tests for the pure logic and the HTTP client, but the live API
  could not be exercised from the build environment (Cloudflare returned 403 without a
  key). See [docs/dtdd-api-notes.md](docs/dtdd-api-notes.md).
- The optional enriched web UI (phase 5 of the specification) and the 10.11 build
  (phase 6) are not implemented.

## Development

```bash
dotnet build
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

- Solution: `Jellyfin.Plugin.ContentWarnings.slnx`
- Tests: `Jellyfin.Plugin.ContentWarnings.Tests`
- `Core/` has no Jellyfin dependency and ~98% line coverage.
- GitHub Actions: `.github/workflows/build.yaml`, `test.yaml`, `publish.yaml`.

## License

GPL-3.0. See [LICENSE](LICENSE).

---

## Español

Plugin de Jellyfin que convierte las advertencias de contenido de DoesTheDogDie.com en
tags nativos, por ejemplo `CW: Dog Death` o `CW: Flashing Lights`. Funciona en todos los
clientes sin modificarlos y permite ocultar contenido por perfil con
**Usuarios > Control parental > Bloquear items con tags**.

### Puesta en marcha

1. Consigue una API key personal en DoesTheDogDie.com.
2. En **Dashboard > Plugins > Content Warnings**, pega la key y pulsa **Test connection**.
3. Ajusta los umbrales: mínimo de votos "sí" (3 por defecto) y ratio mínima (0,6).
4. Elige los topics. Si no marcas ninguno se usa una lista curada de advertencias
   importantes. Puedes traducir cada etiqueta, por ejemplo `Dog Death` → `Muerte de perro`.
5. Elige bibliotecas (vacío = todas las de películas y series) y guarda.
6. Ejecuta una vez **Tareas programadas > Update content warnings**.

### Ocultar contenido a un usuario

**Usuarios > (usuario) > Control parental > Bloquear items con tags** y añade, por
ejemplo, `CW: Dog Death`. En Jellyfin 12 los episodios heredan los tags de su serie, así
que bloquear el tag en la serie oculta también sus episodios.

### Atribución y límites

Los datos son de **Powered by DoesTheDogDie.com**. Cada administrador usa su propia API
key: el plugin no incluye ninguna. La capa gratuita permite 30 peticiones/minuto y 5.000
al mes, y no admite uso comercial; baja `RequestsPerSecond` a ~0,4 o usa una key
comercial. La API v3 actual no expone `isSpoiler`, así que esa opción queda inactiva
hasta que DoesTheDogDie la añada.
