# Course requirements (official, the checklist we are graded against)

Sources: the course wiki https://2477-introduction-programming.gitlab.io/wiki/ (pages "Team project" and "Exam", linked from Moodle 2477 since 10 Nov 2026) and Moodle > Announcements > "Group project instructions" (Cláudio Vieira, 1 Nov 2026). Checked 2026-11-11. If the teaching team posts more, update this file first.

## Hard requirements (all must be true at submission)
| # | Requirement | How we meet it | Status |
|---|---|---|---|
| C1 | 3 to 4 members | 4: see README | done |
| C2 | New application of the team's own choice (allowed by the wiki: "start a new application") | From-scratch Unity game; Noah's email to the teaching team (G1) is a courtesy, the wiki allows it | ok |
| C3 | **Submission: Mon 23 Nov 2026, 23:59, on Moodle.** One member submits the GitHub repo link on the team project assignment. **Commits after the deadline are not graded** | Task G5 | open |
| C4 | **Every member contributes code to the repository** | Each member commits their own code from their own GitHub account (commit email linked to it; your avatar must show on your commits). AI-written is fine, but you commit it yourself | open |
| C5 | **Repository is public** (staff open it without an invitation); keys and passwords stay out of it | David switches visibility himself after the final check G4 | open |
| C6 | **The deployed app's code is on `main`** | Only build from `main`; `docs/builds.md` records the commit of every deployed build | open |
| C7 | **README.md** with the deployed URL, **all members' names and student numbers**, and the commands to run the app and its tests locally | README sections "Team", "Run the app", "Run the tests" (G2) | open |
| C8 | Deployed at a public URL | CI Web build; itch.io / GitHub Pages (V6, G5) | open |

## Presentation (weeks 11 and 12, in class, TXC 15:30, half the teams each week: Tue 24 Nov or Fri 4 Dec; our day is not published yet)
1. **Live demo** at the deployed URL. The format is up to us, but the app must really run in front of the class. Slides or recorded videos are not accepted as a replacement.
2. **Repository walkthrough**: the staff open the repo and ask high-level questions about how it is built. **Any member can be asked about any part**, so everyone must understand the whole project, not only their own files. Example questions from the wiki: *Where is the data stored? What do the tests cover, and what do they not cover? What happens if the external API the app depends on is unavailable?*

**All team members receive the same grade** (team project = 40 % of the course grade).

## Grading criteria (Low 30 % / Mid 70 % / High 100 % of each weight)
| # | Criterion | Weight | High (100 %) means |
|---|---|---|---|
| 1 | It works | 20 % | The deployed app runs all workflows without errors, **including invalid input** |
| 2 | User interface and experience | 20 % | Workflows are direct; results **and errors** are clearly shown to the user |
| 3 | Scope and ambition | 20 % | Substantial features that go well beyond a solo project, implemented consistently |
| 4 | Verification | 10 % | Tests cover **accepted and rejected actions and edge cases**, and all pass on `main` |
| 5 | Security | 10 % | **No credentials in the repo; access control and input validation enforced; dependencies minimal, pinned, without known vulnerabilities** |
| 6 | Repository walkthrough | 20 % | Members explain how it is built, **why**, what its **limits** are, and how it could change |

## How we score High on each criterion (checklist, tick before Sun 22)
1. **It works**: the deployed build is a `main` commit; the whole loop (start screen, countdown, race with bots, finish, results, leaderboard, retry) runs in Chrome, Firefox, Safari and on a phone without a console error; bad input (empty or rude name, unplugged internet, tab switch, falling off the map, driving backwards) has a defined, visible result.
2. **UI/UX**: one key or tap to start; every state says what to do next; errors in plain words ("Leaderboard offline, your time is saved on this device"); readable HUD on a phone.
3. **Scope**: kart physics with drift + jumps, items, rule-based bots, positions + minimap, online leaderboard, ghost, touch controls, a recognisable campus: far beyond a solo project. Consistent look (`docs/art-style.md`).
4. **Verification**: Edit Mode tests for every rule (accepted + rejected + edge cases: e.g. a name with 1 or 17 characters, a time below the possible minimum, a racer registered twice, a lap driven backwards), Play Mode smoke tests for the race, all green on `main` (CI). `docs/verification.md` says what is NOT tested (feel, visuals, browser quirks) and how we checked it by hand.
5. **Security**: no keys in the repo (gitleaks clean, keys only as CI secrets); the leaderboard database only allows *insert* and *read* for the public key (no update or delete), checks name length/characters and time range itself (not only the game), and the game validates everything it gets back; `Packages/manifest.json` pinned, no extra packages; repo public only after the G4 check.
6. **Walkthrough**: `docs/explain/` has one walkthrough per role (how, why, limits, what we would change); every member reads all four and can answer the three wiki example questions for our game (data: personal bests in the browser's PlayerPrefs, times in the online database; tests: see 4; leaderboard down: the game keeps the time locally and shows "offline").

## Exam (60 % of the course grade, individual)
**Sat 19 Dec 2026, 17:30**, on Wiseflow in a locked-down browser, own laptop (room: NOVA SharePoint). **30 multiple-choice questions about the process taught in the course: planning a task, briefing an agent, judging what comes back. It does not ask you to read code.** Make-up exam: 13 Jan 2027, 16:00, same rules. Working on this project the way `AGENTS.md` describes (plan, brief your AI, check what it returns) is good practice for it.

## What this means for every AI working here
- The repo will be **public**: never commit API keys, tokens, `.env` files, the ElevenLabs key, passwords, phone numbers, emails, or photos/videos of people.
- Keep code explainable: any member can be asked about any file (same grade for all).
- Never commit for someone else. Your human commits their own work from their own account (C4).
- Every new rule gets tests for accepted **and rejected** input and edge cases.
