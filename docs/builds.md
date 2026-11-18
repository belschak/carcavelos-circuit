# Builds and deploys

**Every push to `main` is built and deployed by GitHub Actions.** The complete, always current log is the list of runs of the **Web build** workflow on `main` (repository > Actions > Web build, filter branch `main`): each run shows the date, the commit, the build log with every file size, and whether it was deployed to GitHub Pages (this repository's address and the `nova-kart.github.io` mirror, the same files). Nobody builds the public version on a laptop, so the live game is always a `main` commit.

| Where | What it tells you |
|---|---|
| Actions > Web build, branch `main` | Every deployed build: commit, date, result, build log |
| Actions > Tests | Edit Mode and Play Mode results for a commit ([verification.md](verification.md)) |
| `version.json` next to the live `index.html` | The build id the live page is running (the page checks it so nobody plays a cached old build) |
| [demo-plan.md](demo-plan.md) | Hand checks of the live build before the presentation |

The first local Web builds (8 Nov 2026) were tested by hand and logged in a table, before CI deployed to GitHub Pages (from 13 Nov, PR #79). That table is kept as a record: [history/builds-manual-tests-2026-11-08.md](history/builds-manual-tests-2026-11-08.md).

How to make a Web build yourself: [setup.md](setup.md#web-build-role-3-or-the-build-backup) and the README.

Key art and photo mode: the loading page's pictures are shot from a Web build in photo mode (`?photo=1` hides every menu and the HUD; `&dpr=2` renders the canvas at 2x the window for supersampled captures; normal play stays capped at 1.5x). For the 18 Nov 2026 set a throwaway branch built the game without the Web texture crunch; `tools/ui_art/make_keyart.py` turns the raw captures into the AVIF/WebP/JPEG sizes in `TemplateData/` (decision "Sharp key art on the loading page").
