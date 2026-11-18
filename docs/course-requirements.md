# Course requirements (official, the checklist we are graded against)

Sources: the course wiki https://2477-introduction-programming.gitlab.io/wiki/ (pages "Team project" and "Exam", linked from Moodle 2477 since 10 Nov 2026) and Moodle > Announcements > "Group project instructions" (Cláudio Vieira, 1 Nov 2026). Checked 2026-11-11 and again 2026-11-18 (rubric unchanged). If the teaching team posts more, update this file first.

## Hard requirements (all must be true at submission)
| # | Requirement | How we meet it | Status |
|---|---|---|---|
| C1 | 3 to 4 members | 4: see README | done |
| C2 | New application of the team's own choice (allowed by the wiki: "start a new application") | From-scratch Unity game. The stack guide says a language other than Python is agreed with the staff case by case: asked via the Moodle question form on 7 Nov (G1); reasons and current status in `docs/architecture.md` (Stack approval) | ok |
| C3 | **Submission: Mon 23 Nov 2026, 23:59, on Moodle.** One member submits the GitHub repo link on the team project assignment. **Commits after the deadline are not graded** | Task G5. The last commit before the deadline is the graded state | due Mon 23 Nov |
| C4 | **Every member contributes code to the repository** | Each member commits their own code from their own GitHub account (commit email linked to it; your avatar must show on your commits). AI-written is fine, but you commit it yourself. All four have merged pull requests on `main` (`docs/walkthrough-qa.md`, "Who built what") | done |
| C5 | **Repository is public** (staff open it without an invitation); keys and passwords stay out of it | Public (checked 18 Nov 2026); only David changes the visibility | done |
| C6 | **The deployed app's code is on `main`** | GitHub Actions builds and deploys every push to `main`; the `github-pages` environment accepts only `main` | done |
| C7 | **README.md** with the deployed URL, **all members' names and student numbers**, and the commands to run the app and its tests locally | README sections "Team", "Run the app locally", "Run the tests" (G2) | done |
| C8 | Deployed at a public URL | GitHub Pages, link at the top of the README (V6) | done |

## Presentation (weeks 11 and 12, in class, TXC 15:30, half the teams each week: Tue 24 Nov or Fri 4 Dec; our day is not published yet)
1. **Live demo** at the deployed URL. The format is up to us, but the app must really run in front of the class. Slides or recorded videos are not accepted as a replacement.
2. **Repository walkthrough**: the staff open the repo and ask high-level questions about how it is built. **Any member can be asked about any part**, so everyone must understand the whole project, not only their own files. Example questions from the wiki: *Where is the data stored? What do the tests cover, and what do they not cover? What happens if the external API the app depends on is unavailable?*

**All team members receive the same grade** (team project = 40 % of the course grade).

## Grading criteria (official rubric from the wiki "Team project" page, checked again 2026-11-11)
Each criterion is scored Low (30 % of its weight), Mid (70 %) or High (100 %). **Our target: High on all six.**

| # | Criterion (weight) | Low (30 %) | Mid (70 %) | High (100 %) |
|---|---|---|---|---|
| 1 | It works (20 %) | Deployed app is down or crashes in main workflows | App runs, but some workflows fail or need workarounds | All workflows run without errors, **including with invalid input** |
| 2 | User interface and experience (20 %) | Interface does not suit the problem; users need help to complete tasks | Interface suits the problem, but some workflows have extra steps or unclear feedback | Interface suits the problem; workflows are direct, and **results and errors are clear** |
| 3 | Scope and ambition (20 %) | Little more than a solo project in size and features | Several features that integrate into one product | Substantial features **well beyond a solo project, implemented consistently** |
| 4 | Verification (10 %) | No tests, or tests that do not check the required behaviour | Tests cover main workflows but miss rejected actions or edge cases | Tests cover **accepted and rejected actions and edge cases**, and all pass on `main` |
| 5 | Security (10 %) | Credentials in the repo, or sensitive data exposed or stored insecurely | No credentials in the repo, but gaps in access control, input validation, or dependencies | **Access control and input validation enforced; dependencies minimal, pinned, and free of known vulnerabilities** |
| 6 | Repository walkthrough (20 %) | Members cannot explain how the project is built or where its parts are | Members explain what the main parts do, but not why they were built that way or their limits | Members explain **how it is built, why, its limits, and how it could change** |

## How we meet each criterion, and where a reader can check it
| # | Criterion | How the project meets it | Evidence |
|---|---|---|---|
| 1 | It works | Every mode runs on the deployed build; invalid input (names, page-address flags, lost connection, full room, falling off the map, driving backwards) has a defined, visible result; Supabase and Photon failures fall back or explain themselves | `docs/walkthrough-qa.md` (When something fails), `docs/demo-plan.md` |
| 2 | UI and experience | One click or key per step; every state says what to do next; errors in plain words ("LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE"); touch buttons on phones | `Assets/_Project/RaceFlow/UI/`, `Assets/WebGLTemplates/CarcavelosCircuit/` |
| 3 | Scope | Kart physics with drift and jumps, a campus generated from map data, five rule-based bots, items, ghost and medals, four drivers, a global online leaderboard with a device fallback, a 15-player online race, touch controls | README feature table, `docs/architecture.md` |
| 4 | Verification | Edit Mode tests for every rule with accepted, rejected and edge cases; Play Mode tests that drive the real game scene; run on GitHub Actions | `docs/verification.md` |
| 5 | Security | No keys in the repo (only GitHub secrets); the database lets the public key only read and add rows that pass its CHECK rules; the game validates input and every row it gets back; packages pinned | `docs/security.md`, `backend/supabase/` |
| 6 | Walkthrough | How + why, limits + change, short answers, one walkthrough per role | `docs/architecture.md`, `docs/limits-and-next-steps.md`, `docs/walkthrough-qa.md`, `docs/explain/` |

## What this means for every AI working here
- The repo is **public**: never commit API keys, tokens, `.env` files, the ElevenLabs key, passwords, phone numbers, emails, or photos or videos of people.
- Keep code explainable: any member can be asked about any file (same grade for all).
- Never commit for someone else. Your human commits their own work from their own account (C4).
- Every new rule gets tests for accepted **and rejected** input and edge cases.
