# Security policy

Carcavelos Circuit is a student project (Nova SBE, Introduction to Programming, 2026). It has no user accounts and
stores no personal data beyond the nickname and lap time a player chooses to put on the public leaderboard.

## Reporting a problem

Please do not open a public issue for a security problem. Use GitHub's private report instead:
**Security > Report a vulnerability** on <https://github.com/belschak/carcavelos-circuit/security>. We answer within a
few days while the course runs (until December 2026). After that the project is not maintained.

## Supported version

Only the newest commit on `main`, which is the version deployed at <https://nova-kart.github.io/> (and, unchanged, at
the older address <https://belschak.github.io/carcavelos-circuit/>).

## What is public on purpose

The Supabase project URL with its **public (anon / publishable) key** and the **Photon App ID** are inside every Web
build, because every browser needs them. They only allow what the database's Row Level Security and Photon's room
rules allow: read the leaderboard, add a checked lap time, join a race room. No secret key, password or token is in
this repository or its history; CI scans every commit of every branch with gitleaks.

## Details

- [docs/security.md](docs/security.md): keys, access control, input validation, personal data, dependencies, limits.
- [docs/verification.md](docs/verification.md): which tests check these rules.
- [backend/supabase/](backend/supabase/): the database rules themselves.
