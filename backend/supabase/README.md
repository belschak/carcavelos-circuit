# Online leaderboard: Supabase setup (task R10)

The game keeps every finished lap time with a name. Without this setup it does so **on the player's device only** (no error, the board says "THIS DEVICE"). With it, times go to one small free online database and every player sees the same top 10.

Nothing in this folder runs by itself. No account exists yet: David (Role 4) creates it once, in about 10 minutes.

## What David does (once)

1. **Create the free project.** Go to `https://supabase.com`, sign up (free plan, no card), then **New project**: name `carcavelos-circuit`, region **West EU (Ireland)** or **Central EU (Frankfurt)**, a strong database password (keep it in Bitwarden; the game never needs it).
2. **Create the table and its rules.** In the project: **SQL Editor > New query**, paste all of `001_lap_times.sql`, **Run**. It should say "Success. No rows returned".
   Optional: the same with `002_rate_limit.sql` (refuses floods of rows, see the comment at its top).
3. **Copy two values** from **Project Settings > API Keys** (older dashboards: **Project Settings > API**):
   - the **Project URL**, like `https://abcdefghijklmnop.supabase.co`
   - the **publishable** key (`sb_publishable_...`), or on older projects the **anon public** key (a long `eyJ...` text).
   Never copy the **secret** / **service_role** key: it can change and delete every row. The build refuses it anyway.
4. **Put them into GitHub as secrets** (never into a file): repo **Settings > Secrets and variables > Actions > New repository secret**
   - name `LEADERBOARD_URL`, value = the Project URL
   - name `LEADERBOARD_ANON_KEY`, value = the publishable / anon key
5. **Build.** **Actions > Web build > Run workflow** (or merge anything into `main`). In the build log, the line `LeaderboardBuildConfig: online leaderboard is ON for this build` means it worked. A wrong secret stops the build with a message that says what is wrong (http instead of https, not a `.supabase.co` address, a secret key ...).
6. **Check.** Play the build, finish a lap, type a name, SAVE. The board shows "ONLINE" and your row; in Supabase, **Table Editor > lap_times** shows the row.

To switch the online leaderboard off again: delete the two secrets and build again.

## How it fits together

| Part | File |
|---|---|
| Table, checks, who may do what (Row Level Security) | `001_lap_times.sql` |
| Optional flood protection | `002_rate_limit.sql` |
| Name and time rules in the game (the same as the SQL checks) | `Assets/_Project/RaceFlow/Leaderboard/PlayerNameRules.cs`, `LapTimeRules.cs` |
| Requests (REST, `UnityWebRequest`, 6 s timeout) | `.../Leaderboard/SupabaseLeaderboardService.cs`, `SupabaseRequests.cs` |
| Offline fallback (times always kept on the device too) | `.../Leaderboard/FallbackLeaderboardService.cs`, `LocalLeaderboardService.cs` |
| Address + key into the build, and out again | `Assets/_Project/RaceFlow/Editor/LeaderboardBuildConfig.cs`, `.github/workflows/web-build.yml` (`customParameters`) |
| The connection asset (always EMPTY in the repo) | `Assets/_Project/RaceFlow/Settings/LeaderboardConfig.asset` |

## Why the key is "public by design", and still not in the repo

The publishable / anon key is **meant** to be in the game: every Web build carries it, and anyone can read it from the downloaded files or the browser's network tab. It is safe there because it only gets what the database allows that role: **read** the top lists and **add** a row that passes the name and time checks. It cannot change or delete anything, and it cannot see any other table. The real protection is Row Level Security plus the CHECK rules in `001_lap_times.sql`, not the secrecy of the key.

It still never goes into a file in the repo, because:
- the course rules say no credentials in the repo (`docs/course-requirements.md`, Security), and secret scanners (gitleaks) flag these keys;
- a key in the repo stays in the Git history forever, while a GitHub secret can be changed in one minute (new key in Supabase, new secret, new build) without touching code;
- forks, test builds and teammates' laptops then never write test laps into the real leaderboard.

## Limits (honest)

- Someone with the public key can post valid-looking fake times with a script (inside 5 s to 30 min and with a clean name). The optional rate limit slows that down; the project owner can delete such rows in the Table Editor. The game also hides times that are impossible on the track (faster than 40 % of the Gold medal time).
- The rude-word list is short; it catches the obvious words in English, Portuguese, German and Dutch, not everything.
- Free Supabase projects pause after about a week without any request. Then the game says "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE" until David presses **Restore** in the dashboard. Open the game once before a demo.
