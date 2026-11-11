-- Carcavelos Circuit: OPTIONAL rate limit for the leaderboard (task R10, Role 4). Run after 001_lap_times.sql.
-- Why: the public key is in every Web build, so anyone could post many valid-looking rows with a script.
-- This trigger refuses a new row when, in the last minute, the same name already posted 5 rows, or everybody
-- together posted 60. A real player finishes at most a few laps per minute, so the game never hits it; when it
-- does, the insert fails and the game simply says "offline" and keeps the time on the device.
-- Remove it again with:  drop trigger if exists lap_times_rate_limit on public.lap_times;

create index if not exists lap_times_created_at on public.lap_times (created_at);

create or replace function public.lap_times_check_rate()
returns trigger
language plpgsql
security definer          -- counts with the owner's rights (anon may not read the id column)
set search_path = public  -- fixed search path: a security definer function must not pick up other schemas
as $$
declare
    same_name_last_minute integer;
    all_last_minute integer;
begin
    select count(*) into same_name_last_minute
        from public.lap_times
        where lower(name) = lower(new.name) and created_at > now() - interval '1 minute';
    if same_name_last_minute >= 5 then
        raise exception 'too many lap times for this name, try again in a minute';
    end if;

    select count(*) into all_last_minute
        from public.lap_times
        where created_at > now() - interval '1 minute';
    if all_last_minute >= 60 then
        raise exception 'too many lap times right now, try again in a minute';
    end if;

    return new;
end;
$$;

-- Nobody calls the function directly through the API; only the trigger runs it.
revoke all on function public.lap_times_check_rate() from public, anon, authenticated;

drop trigger if exists lap_times_rate_limit on public.lap_times;
create trigger lap_times_rate_limit
    before insert on public.lap_times
    for each row
    execute function public.lap_times_check_rate();
