-- Carcavelos Circuit: online lap-time leaderboard (task R10, Role 4).
-- Run once in the Supabase dashboard: SQL Editor > New query > paste this file > Run. Safe to run again.
-- Steps around it: backend/supabase/README.md.
--
-- What the public (anon / publishable) key may do after this file, and nothing else:
--   * read the four columns track, name, seconds, created_at
--   * add a row with track, name and seconds, only if they pass the same checks as the game
--     (Assets/_Project/RaceFlow/Leaderboard/PlayerNameRules.cs and LapTimeRules.cs)
--   * no update, no delete, no other table
-- The game validates everything it sends and everything it gets back; these checks are the second wall, for
-- anyone who calls the API without the game.

create table if not exists public.lap_times (
    id         bigint generated always as identity primary key,
    track      text        not null,
    name       text        not null,
    seconds    real        not null,
    created_at timestamptz not null default now(),

    -- Track = the Unity scene name, e.g. Track_CampusWorld.
    constraint lap_times_track_ok check (track ~ '^[A-Za-z0-9_-]{1,40}$'),

    -- Name: 2 to 16 characters; letters A-Z, accented Latin letters (U+00C0-U+017F without the signs x and /),
    -- digits, - and _; single spaces between words only (no space at the start or end, never two in a row).
    constraint lap_times_name_length check (char_length(name) between 2 and 16),
    constraint lap_times_name_chars check (
        name ~ '^[A-Za-z0-9À-ÖØ-öø-ſ_-]+( [A-Za-z0-9À-ÖØ-öø-ſ_-]+)*$'
    ),

    -- Lap time: 5 s to 30 min. NaN and Infinity count as bigger than every number in Postgres, so they fail too.
    constraint lap_times_seconds_range check (seconds >= 5 and seconds <= 1800)
);

-- The game asks "best 10 of one track": this index answers that without reading the whole table.
create index if not exists lap_times_track_seconds on public.lap_times (track, seconds);

-- ---------- Who may do what ----------

-- Row Level Security: with it on, a role sees and changes only what a policy allows. No policy = no access.
alter table public.lap_times enable row level security;

-- Supabase gives the API roles every right on new tables by default. Take them all away first, then give back
-- exactly the columns the game needs. (The id and created_at are always filled in by the database itself.)
revoke all on table public.lap_times from anon, authenticated;
grant select (track, name, seconds, created_at) on table public.lap_times to anon;
grant insert (track, name, seconds) on table public.lap_times to anon;

drop policy if exists "anon reads lap times" on public.lap_times;
create policy "anon reads lap times"
    on public.lap_times
    for select
    to anon
    using (true);

drop policy if exists "anon adds a checked lap time" on public.lap_times;
create policy "anon adds a checked lap time"
    on public.lap_times
    for insert
    to anon
    with check (
        track ~ '^[A-Za-z0-9_-]{1,40}$'
        and char_length(name) between 2 and 16
        and name ~ '^[A-Za-z0-9À-ÖØ-öø-ſ_-]+( [A-Za-z0-9À-ÖØ-öø-ſ_-]+)*$'
        and seconds >= 5
        and seconds <= 1800
    );

-- No UPDATE and no DELETE policy, on purpose: a saved time can never be changed or removed with the public key.
-- The project owner can still delete a rude or fake row in the dashboard (Table Editor), which bypasses RLS.
