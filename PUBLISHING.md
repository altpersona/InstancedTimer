# Publishing a release

How to ship a new version of InstancedTimer to GitHub (source + Release) and
Thunderstore (the mod-manager catalog). Run everything from the repo root.

> History: the mod was called **SunkenCryptTimer** through v1.1.8 (GitHub repo and
> Thunderstore package `StandardVibeware-SunkenCryptTimer`, now deprecated). v1.2.0
> renamed it to InstancedTimer — on Thunderstore a rename means a NEW package, which
> is why the old listing still exists. Old tags/releases keep their old names.

## One-time setup (already done 2026-10-06)

- GitHub CLI authenticated (`gh auth login`, account `altpersona`, SSH remote).
- Thunderstore account: log in at thunderstore.io with the GitHub account.
- Thunderstore team: **`StandardVibeware`** — the team name is the permanent author
  namespace, so the package is listed as `StandardVibeware-InstancedTimer`
  (not `altpersona-...`; namespaces can never be renamed).
- Service-account API token (`tss_...`, created under the team's Service Accounts
  page) stored at `~/.config/thunderstore/api_token` (chmod 600, outside the repo).

## Every release

1. **Bump the version in all three places** (they must agree):

   | File | Field |
   |---|---|
   | `InstancedTimer/InstancedTimer.csproj` | `<Version>` |
   | `InstancedTimer/Plugin.cs` | `PluginVersion` |
   | `InstancedTimer/manifest.json` | `version_number` |

   Semantic `Major.Minor.Patch`. Thunderstore **rejects re-uploading a version
   number that already exists** — every upload needs a higher number.

2. **Build + package:**

   ```
   ./build.sh            # -> InstancedTimer/bin/Release/InstancedTimer.dll
   ./package.sh          # -> InstancedTimer-v<version>.zip (Thunderstore format)
   ```

   `package.sh` reads the version from `manifest.json`, so the zip is named after it.

3. **Commit, push, GitHub Release:**

   ```
   git add -A && git commit -m "<version>: <what changed and why>"
   git push
   gh release create v<version> --title "v<version> — <summary>" --notes "<notes>" \
     InstancedTimer-v<version>.zip InstancedTimer/bin/Release/InstancedTimer.dll
   ```

   (`gh release create` also creates the tag from HEAD.)

4. **Publish to Thunderstore:**

   ```
   ./publish_thunderstore.sh
   ```

   Reads the token from `~/.config/thunderstore/api_token`, the version from
   `manifest.json`, and publishes the zip in one call (team `StandardVibeware`,
   community `valheim`, category `client-side`). Auth is `Authorization: Bearer`
   with the service-account token — the Swagger docs' "Basic" scheme is wrong for
   `tss_` tokens. Two HTTP gotchas are handled inside the script: Cloudflare 502s
   the POST with curl's default User-Agent, and `Expect: 100-continue` must be
   suppressed. A failed publish prints the API's error body; a duplicate version
   is rejected server-side (bump first).

5. Update `CHANGELOG.md` with what shipped (if not done in step 3's commit).

## Thunderstore package rules (what package.sh satisfies)

Reference so future changes don't break uploads (source: Thunderstore wiki,
"Creating a Package" / "Your First Upload"):

- Zip root must contain, with exact case-sensitive names:
  - `manifest.json` — UTF-8 JSON with `name` (≤128 chars, only `A-Za-z0-9_`;
    underscores render as spaces), `version_number` (`Major.Minor.Patch`),
    `description` (**≤250 chars**), `website_url` (empty string if unused),
    `dependencies` (`team-package-version` strings, auto-installed by mod managers).
  - `icon.png` — **exactly 256×256** PNG (transparency OK).
  - `README.md` — rendered on the package page; close to but not exactly GitHub
    markdown — use the site's Markdown Preview tool when in doubt.
  - Optional: `CHANGELOG.md`. Mod files go anywhere (ours: `Plugins/InstancedTimer.dll`).
- Max package size ~5 GB (not a concern here).
- Validation tools before uploading if something's off: the manifest validator and
  markdown preview linked from the upload page.

## Gotchas

- Manifest `description` over 250 chars is the easiest rule to break when editing
  it casually — `package.sh` doesn't validate it; check before upload.
- The README shown on Thunderstore is the repo README copied into the zip at package
  time. Published versions are **immutable** — even a README fix requires uploading a
  new (higher) version; you cannot edit a listing in place.
- Never change the `name` field in `manifest.json` for an update — a different name
  (or a different team) uploads a *new package* instead of updating the existing one.
- Semver gotcha the docs call out: `1.0.20 > 1.0.3` (numeric compare of each part,
  not string compare).
- `denikson-BepInExPack_Valheim-5.4.2351` in `dependencies` should be refreshed when
  the pack updates — mod managers install exactly that version alongside the mod.
- Don't `git add` the zip: it's build output (already in `.gitignore`); GitHub
  Releases host the artifacts.
