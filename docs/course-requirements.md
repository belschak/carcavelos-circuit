# Course requirements (official, the checklist we are graded against)

Source: Moodle 2477 > Announcements > "Group project instructions" (Cláudio Vieira, Sun 1 Nov 2026) + `content/evaluation.md` on the course site. Checked 2026-11-07. If the teaching team posts more (deploy instructions are announced), update this file first.

## Hard requirements (all must be true at submission)
| # | Requirement | How we meet it | Status |
|---|---|---|---|
| C1 | 3 to 4 members | 4: see README | done |
| C2 | From-scratch idea "agreed with the teaching team" | **Not approved yet.** Noah emails the teaching team (Prof. Ricardo Pereira, Cláudio Vieira) asking for confirmation; save the reply as a screenshot in the team Drive | open |
| C3 | **Submission: Mon 23 Nov 2026, 23:59, on Moodle.** One member submits the GitHub repo link on the Moodle team project assignment | Task G5 | open |
| C4 | **Every member contributes code to the repository** | Every role has C# tasks. Each member commits their own code from **their own GitHub account** (commit email linked to that account; check that your avatar shows on your commits). AI-written is fine, but you commit it yourself. `help(...)` commits do not replace your own | open |
| C5 | **Repository is public** | Repo owner (David) switches visibility himself after a final check (no secrets, no personal data, no raw photos). Never an AI, never a teammate | open |
| C6 | **The deployed app's code is on the `main` branch** | Only build from `main`; `docs/builds.md` records the commit of every deployed build | open |
| C7 | **README.md** with: deployed URL, **all members' names and student numbers**, the commands to run the app and its tests | README "Team", "Run the app", "Run the tests" sections, task G2 | open |
| C8 | Deploy as the teaching team instructs | "We will provide further instructions on how to deploy": until then itch.io / GitHub Pages is provisional | waiting |

## How we are graded (40% of the course grade)
Presentations in weeks 11-12 (W11 Tue 24 Nov, W12 Fri 4 Dec per the timetable, slot not yet known), half the groups each week.
1. **Live demo** at the deployed URL. "Not slides, not a video: the real thing."
2. **Code walkthrough**: they open the repo and **ask every member about code they did not write.**

Rubric: it works · design · scope and ambition · verification · walkthrough. Group grade, adjusted per person from the walkthrough.

## What this means for every AI working here
- The repo will be **public**: never commit API keys, tokens, `.env` files, the ElevenLabs key, passwords, phone numbers, emails, or photos/videos of people.
- Keep code explainable: every teammate must be able to explain any file (that is graded).
- Never commit for someone else. Your human commits their own work from their own account (C4).
