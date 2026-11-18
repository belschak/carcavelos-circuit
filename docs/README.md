# Documentation index

Start with the first three if you have never seen the project.

## Understand the project

| Document | Answers |
|---|---|
| [architecture.md](architecture.md) | What the parts are, how data flows, where data is stored, why each choice was made |
| [walkthrough-qa.md](walkthrough-qa.md) | Short answers to the repository walkthrough questions, including who built what |
| [limits-and-next-steps.md](limits-and-next-steps.md) | What the game cannot do, where it can break, how it could change |
| [interfaces.md](interfaces.md) | The contracts between modules (`IKartState`, `TrackInfo`, `RaceRules`, `IRacer`, `ITrackProgress`, `IKartDrivable`, online hooks) |
| [online.md](online.md) | The online race: how to play it, how it is built, how it was tested, its limits |
| [route.md](route.md) | The campus route and its real distances |
| [decisions.md](decisions.md) | Every decision with its date and reason, newest first |
| [explain/](explain/) | One walkthrough per role, all in the same shape (files, how it works, why, limits, how it could change, tests): [Role 1](explain/role1-walkthrough.md) driving + camera, [Role 2](explain/role2-walkthrough.md) track + race rules, [Role 3](explain/role3-walkthrough.md) visuals, bots + web release, [Role 4](explain/role4-walkthrough.md) race flow, UI, sound + online; plus [the round 2 contracts](explain/round2-contracts.md) |

## Quality

| Document | Answers |
|---|---|
| [verification.md](verification.md) | What the tests cover, what they do not, how to run them |
| [security.md](security.md) | Keys, access control, input validation, dependencies |
| [demo-plan.md](demo-plan.md) | The live demo: order, who does what, fallback for each service |
| [builds.md](builds.md) | Where every deployed build is logged (GitHub Actions) and how to check what is live |
| [course-requirements.md](course-requirements.md) | The official course rules and grading rubric, and how we meet each one |

## Working on the project

| Document | Answers |
|---|---|
| [setup.md](setup.md) | Installing Unity, cloning, running tests and Web builds |
| [team.md](team.md) | Roles, owners, folders, scenes |
| [art-style.md](art-style.md) | Colours, fonts, the look every part follows |
| [../AGENTS.md](../AGENTS.md) | The one rules file for every teammate and every AI assistant (`CLAUDE.md`, `GEMINI.md` and `.github/copilot-instructions.md` only point to it) |
| [../TASKS.md](../TASKS.md) | The task board |
| [../THIRD_PARTY.md](../THIRD_PARTY.md) | Every external asset and its licence |
| [../backend/supabase/README.md](../backend/supabase/README.md) | Setting up the online leaderboard, and why its key is safe in the game |

## History

[history/](history/): the plans, briefs and milestone list from the three weeks of building, kept as a record. They describe what we planned at the time, not necessarily what the code does now.
