# Interfaces between modules (the contract)

Each module only talks to another module through what is listed here. Build against this contract with placeholders, so nobody waits for anyone. Changing this file needs the whole team's OK (see `docs/team.md`).

Namespace per module: `CarcavelosCircuit.Shared`, `.Driving`, `.Track`, `.Visuals`, `.RaceFlow`, `.Audio`. One class per file, file name = class name.

Each module folder has its own assembly definition (`.asmdef`) with the same name as the namespace. Allowed references (nothing else, no cycles):
`Driving -> Shared`, `Visuals -> Shared`, `Audio -> Shared`, `RaceFlow -> Shared, Track, Audio` (Audio added 2026-11-08, `docs/decisions.md`), `Tests.EditMode -> Shared, Track, RaceFlow, Audio`, `Tests.PlayMode -> Shared, RaceFlow`. Plus Unity packages (Input System, URP); Editor-only builder assemblies may reference their own module. Track and Shared reference no other module. Nobody references `Driving` directly: everyone talks to the kart through `IKartState`.

## Shared (Role 4): `IKartState` in `Assets/_Project/Shared/IKartState.cs`

```csharp
using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    public interface IKartState
    {
        float Speed { get; }            // current forward speed in m/s
        float MaxSpeed { get; }         // for HUD, engine pitch, FOV kick
        bool IsDrifting { get; }
        float DriftCharge01 { get; }    // 0..1 how charged the drift boost is
        int DriftStage { get; }         // 0 none, 1 blue sparks, 2 orange sparks
        bool IsBoosting { get; }
        bool IsGrounded { get; }        // false while in the air (ramp jump)
        float Steer { get; }            // -1 full left .. +1 full right, what the player steers now (0 when input is off)
        bool InputEnabled { get; set; } // RaceFlow turns input off during countdown/results
        void ResetTo(Vector3 position, Quaternion rotation); // teleport + zero velocity + clear drift/boost
    }
}
```

`Steer` added 2026-11-10 (Role 3's request Q11, OK from Role 3 and Role 4, `docs/decisions.md`): read only, for the steering wheel and front wheels of the kart model. Every class that implements `IKartState` must have it.

## Shared (Role 4): `IKartBoost` in `Assets/_Project/Shared/IKartBoost.cs` (optional, item boxes, 2026-11-10)

```csharp
public interface IKartBoost
{
    void AddBoost(float seconds, float speedMultiplier); // adds boost time (boosts chain); top speed = MaxSpeed * the higher of the kart's own boost multiplier and this one
}
```

Optional second interface, so `IKartState` stays as agreed: a kart without it still races, the items then give no speed. RaceFlow's `ItemSystem` calls it when the player uses an item (E). The kart decides how it accelerates; while the boost runs `IsBoosting` must be true (Role 3's boost flame and Role 4's boost sound read that). `ResetTo` ends an item boost like any boost. Implemented by `PlaceholderKart`; `KartController` via help PR (request Q15 in `TASKS.md`).

## Driving (Role 1) provides: `KartController : MonoBehaviour, IKartState` on the `Kart` prefab root

Approach (decided, see `docs/decisions.md`): our own simple **sphere-based** arcade controller. An invisible Rigidbody sphere rolls and carries the physics, the kart model follows it and is aligned to the ground with one raycast. It cannot flip, has few tuning values and every line can be explained. Read [Mix and Jam MarioKart-Drift](https://github.com/mixandjam/MarioKart-Drift) (MIT, 2019, old APIs) and [UniKart](https://github.com/eviltwo/UniKart) (MIT, Unity 6) as references only; credit them in `THIRD_PARTY.md` if you copy ideas closely.

Drift proposal (tune in `KartTuning`, everything time based with `Time.deltaTime`): start = drift key + steer > 0.5 + speed > 40 % + grounded; charge faster when steering into the turn; stage 1 at 0.8 s, stage 2 at 1.6 s; release = boost of +35 % max speed for 0.4 s (stage 1) or 1.2 s (stage 2); cancel if too slow, airborne or a wall is hit. In the air: 25 % steering, extra gravity after the apex.

Unity 6 names: `Rigidbody.linearVelocity` (not `velocity`), `linearDamping` (not `drag`). Camera = Cinemachine 3 (`using Unity.Cinemachine;`, `CinemachineCamera` + Follow, binding "Lock To Target With World Up"). Old tutorials use the old names and break.

Prefab layout: `Kart` (root: Rigidbody, colliders, `KartController`) with a child `Visual` that Role 3 can swap. Kart forward = +Z, 1 Unity unit = 1 metre, pivot at ground level centre.

How RaceFlow recognises the kart at a checkpoint (clarified 2026-11-08): every Rigidbody and collider of the kart must be on the `Kart` prefab root or one of its children when the prefab is spawned. RaceFlow remembers them right after spawning, so the kart may unparent its physics sphere later in its own `Start`. A collider counts as "the kart" if it is one of those colliders, if its `attachedRigidbody` is one of those Rigidbodies, or if it has `IKartState` on itself or a parent.

`InputEnabled` belongs to RaceFlow: the kart never sets it to `true` itself. RaceFlow writes it every frame (`true` only while racing), so a kart that switches itself on in `Start` is switched off again before it can drive.

## Track (Role 2) provides

- `Checkpoint` component with `int Index`. The indices are exactly 0..N-1, each used once: **index 0 = the start/finish line**, then 1, 2, 3 ... in driving order. The lap is finished when the kart crosses index 0 after passing 1..N-1 in order. Every checkpoint needs at least one collider with Is Trigger, on the `Checkpoint` object itself or on a child (RaceFlow adds its listener to whichever object owns the trigger collider). RaceFlow checks all of this when the race is set up and logs a red error if something is wrong.
- `SpawnPoint` transform (start grid) and one `RespawnPoint` child per checkpoint (safe pose for recovery).
- `TrackInfo` component on the scene root: `Checkpoint[] Checkpoints`, `Transform SpawnPoint`, `Transform GetRespawnPoint(int lastCheckpointIndex)`.
- `RaceRules` = plain C# class (no MonoBehaviour, no scene objects, unit tested):

```csharp
public RaceRules(int checkpointCount);
public void StartLap();                    // called when the countdown ends
public bool PassCheckpoint(int index);     // true only if it is the next expected one
public bool TryFinish();                   // true only if all checkpoints were passed in order
public int LastValidCheckpoint { get; }    // for respawn
public void Reset();
```

## Visuals (Role 3) provides

- `KartVisual` prefab (fits under `Kart/Visual`), same scale, pivot and forward axis as above.
- `KartEffects` component: reads `IKartState` (read only) and shows drift sparks / boost flame. Never changes driving values.
- Landmark prefabs with their own colliders set up by Role 2 in the track scene.

## RaceFlow + Audio (Role 4) provides

- `RaceManager` state machine: `Ready -> Countdown -> Racing -> Results`, plus `Retry()`.
- Retry resets: kart pose + velocity (`ResetTo`), `RaceRules.Reset()`, timer, temporary effects.
- Recovery (key R): kart goes to `GetRespawnPoint(LastValidCheckpoint)`, timer keeps running, no checkpoint is awarded.
- HUD: countdown, lap timer, final time, boost cue, retry button.
- Audio: engine pitch from `IKartState.Speed / MaxSpeed`, drift/boost SFX from the state flags, music. Sound only starts after the first click or key press (browser rule).

## Controls (proposal, confirm at kickoff)
W/A/S/D and arrow keys to drive, Space = drift, R = recover, Enter = start / retry, E = use the item in the HUD slot (item boxes, 2026-11-10; the phone's touch item button sends E).
