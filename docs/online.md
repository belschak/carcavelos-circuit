# Online race (task M2)

Everyone in the class joins the live game from the one link, picks a room and races the others. Decided in the group chat on 16 Nov (Luc proposed Photon Fusion 2), details in `docs/decisions.md` (2026-11-16).

## For players
1. Open https://belschak.github.io/carcavelos-circuit/ and pick **ONLINE RACE** on the start screen.
2. Type a name, then pick **NOON ROOM** or **SUNSET ROOM** (the room sets the light). Each room takes 15 players, first come first served; a full room says so, take the other one.
3. The host (the first player in the room) presses **START RACE**. Everyone counts down to the same GO. One lap. Your own kart drives exactly as offline; the others are shown as they drive.
4. The finish order is the same on every screen (lap times, leavers count as DNF). After the first player crosses the line the others have 60 s to finish (180 s after GO if nobody finishes); then everyone still driving is DNF and the results are final. The host can start the next round only once the race is over for everyone; **Q** leaves the room.
5. Keep the tab in front: a browser tab in the background gets no frames from the browser (the kart stops, nothing is sent) and drops out of the room after a few seconds.

Online mode has no bots, no items, no ghost, no leaderboard saving and no pause when the window loses focus.

## How it is built
- `Assets/_Project/Online/` (assembly `CarcavelosCircuit.Online`, always compiled): lobby UI, room rules (`OnlineRooms`), URL flags (`OnlineUrlFlags`), pose codec + buffer, proxies (`ProxyKart`), race control (`OnlineRaceDirector`), all against the `IOnlineSession` interface. No Photon types here, so the public repo compiles and plays single player without the SDK.
- `Assets/_Project/Online/Fusion/` (assembly `CarcavelosCircuit.Online.Fusion`, define constraint `CARCAVELOS_FUSION`): the Photon Fusion 2 Shared Mode implementation of `IOnlineSession` (region eu, session names `carcavelos-noon` / `carcavelos-sunset`, PlayerCount 15).
- RaceFlow only knows `RaceFlow/OnlineRaceHooks.cs` (`Available`, `UnavailableMessage`, `OpenLobby`) plus the small external-start API on `RaceManager` / `RaceRoster` (`docs/interfaces.md`).
- Test transport without Photon: `?net=local` uses a `BroadcastChannel`, so several pages of the same browser on one machine form a room. Other URL flags for tests: `?room=noon|sunset`, `?name=X` (auto-join), `?autopilot=1` (the own kart drives itself), `?autostart=N` (the host starts 5 s after N players joined). Example: `index.html?net=local&room=noon&name=A&autopilot=1&autostart=2` in two windows (second one with another name).
- Local tests need separate, visible browser WINDOWS side by side (one page each, none minimised), not several tabs in one window. A hidden tab gets no frames (`Application.runInBackground` only helps a visible page without focus), so it stops sending, drops out of the room after about 6 s (DNF on the other screens), never answers a newcomer and its kart freezes; it re-joins when it is shown again.

## The SDK is not in the repo
Photon's licence terms do not allow redistributing the SDK (https://www.photonengine.com/terms/licenseterms, sections 10.2 and 17), so `Assets/Photon/` is gitignored. The Web build on GitHub Actions checks the SDK out of the private repo `belschak/carcavelos-photon-sdk` with a read-only deploy key (secret `PHOTON_SDK_DEPLOY_KEY`) into `Assets/Photon/`, and `OnlineBuildConfig` sets the define `CARCAVELOS_FUSION` plus the App ID from the secret `PHOTON_APP_ID` (checked to be a GUID, written to `Online/Resources/CarcavelosPhotonAppId.txt` only during the build, like the leaderboard keys). Without the two secrets the build is single player only; nothing fails.

The App ID is not a secret in the strict sense (every Photon client build contains it), it just stays out of commits so it cannot be scraped from the repo.

## For developers: the online race in your local editor
1. Free Photon account at https://www.photonengine.com, download **Photon Fusion 2.1.4** (`.unitypackage`) from https://www.photonengine.com/sdks#fusion and import it into the project (it lands in `Assets/Photon/`, which Git ignores).
2. The first import adds the package `com.unity.nuget.mono-cecil` and the `FUSION_*` defines by itself and creates `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` + `PhotonAppSettings.asset`.
3. Add `CARCAVELOS_FUSION` to Project Settings > Player > Scripting Define Symbols (WebGL and Standalone); without it the assembly `CarcavelosCircuit.Online.Fusion` is skipped and Online says it needs the online build. Weaving needs no setting: the asmdef carries `FUSION_FORCE_WEAVING` (and the private SDK copy used by CI also lists the assembly in "Assemblies To Weave").
4. App ID: ask David for the Fusion App ID of the "Carcavelos Circuit" app (or create your own free Fusion 2 app in the Photon dashboard) and put it into `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (App Id Fusion) or into `Assets/_Project/Online/Resources/CarcavelosPhotonAppId.txt` (ignored by Git).
5. Play: pick ONLINE RACE. For a two-player test on one machine, build to WebGL and open the link in two visible browser windows side by side (not two tabs of one window, a hidden tab drops out), or use `?net=local` the same way in the Web build without Photon.

## How it is tested
- Edit Mode tests cover the rules under the transport: pose codec + buffer, race control codec, rooms, URL flags, start rules (late-control countdown, host start only when the race is over), race time limit, proxy visibility, finish order (ties, DNF), loopback session (join, full room, master hand-over, leave).
- No test can reference Fusion (the public CI has no SDK), so the Photon adapter is tested by the CI Web build plus two or more browser tabs on the live link.

## Limits
- Players: 100 concurrent users (CCU) on the one App ID across both rooms (checked in the Photon dashboard on 16 Nov 2026: Fusion app "Carcavelos Circuit", Public Cloud, 100 CCU, 300 GB traffic; Photon's free 100 CCU offer, no paid subscription). Every open game page in a room counts, also a forgotten tab. Above 100 Photon refuses the connection (`MaxCcuReached`), the game shows "server full". Each room holds 15 players, so two full rooms use 30.
- Remote karts are drawn 0.22 s in the past (`PoseBuffer.RenderDelaySeconds`) and blended between poses; if poses stop, a kart keeps going straight for at most 0.3 s, then holds. 0.12 s was too tight for campus Wi-Fi (Luc's review): a pose needs the send gap (67 ms) plus the trip through Photon (Amsterdam) plus jitter. Our two-window test on one PC could not show this, the trip there is near zero.
- The host (first player in the room) starts every race. A host who walks away blocks the room until they leave (Q or closing the page hands the host role to the next player). For the class demo one of us hosts each room.
- Campus Wi-Fi: WebGL talks WebSocket (TCP) on 443, so packet loss shows as short freezes of other karts, never of your own. Poses are sent 15 times a second, about 30 bytes each.
- Joining a room takes about 8 s on a fast PC (Photon name server, master, game server, relay) and longer on a slow laptop or busy Wi-Fi; the game waits up to 35 s before it says the server did not answer (scale test, 17 Nov 2026: the old 15 s cut off slow clients).
- Test lines: every client prints `CCNET ...` lines to the browser console (`OnlineTestLog`: JOIN, HOST, RACE, GO with its skew from the shared start, FINISH, RESULT with the full finish order, STAT every 10 s with fps and how often a remote kart ran out of poses). The scale test compares them across all clients.
- Karts drive through each other online (no physics between players), on purpose: it keeps every client's own drive honest and simple.
