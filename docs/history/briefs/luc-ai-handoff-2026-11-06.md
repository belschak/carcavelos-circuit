# Carcavelos Circuit AI handoff

## Project decisions

- Course: Nova SBE Introduction to Programming. The TA approved a new game in Unity and C#. Final live presentation: 23 November 2026.
- Stack: Unity 6.3 LTS as the recommended baseline, C#, Blender for meshes, GitHub for source, and a hosted Unity Web build. Confirm the exact Editor patch with the team and use the version recorded in the Unity project.
- Scope: one kart, one Carcavelos campus loop, one timed lap, desktop keyboard controls, drift with a short boost, and a start-to-retry flow.
- Visual direction: cheerful kart racing inspired by the feel of Mario Kart Wii. Use original low-poly models, bright colours, a simple seated driver, large route markers and two or three recognisable campus landmarks.
- Target: a 35–60 second lap. The route, exact controls, steering values, recovery rule and named owners are open until the kickoff. Treat W/A/S/D and arrow keys, Space for drift and R for recovery as proposals.
- Out of scope for the presentation: kart selection, items, AI opponents, multiplayer, touch input and alternate routes. Local personal best is optional after release checks pass.
- Milestones: browser smoke build by 8 November, end-to-end race by 12 November, feature freeze on 17 November, release candidate on 20 November, final presentation on 23 November. Project setup and builds are pending.

## Asset choices

Start at €0. Use the [Kenney Racing Kit](https://kenney.nl/assets/racing-kit) for CC0 placeholders if useful. Evaluate [Unity's Karting Microgame](https://assetstore.unity.com/packages/templates/packs/karting-microgame-3d-sample-project-150956) as a reference or [UniKart](https://github.com/eviltwo/UniKart) as a controller candidate in a separate project. Verify the exact Editor patch, Web export, licence and team understanding before adopting an external controller. Preserve a licence notice and record any code or assets brought into the project.

## Workspace and ownership

- Use one GitHub repository and one Unity project. Write the exact Unity Editor patch in the README and commit ProjectSettings/ProjectVersion.txt. Install Web Build Support in Unity Hub.
- Track Assets and its .meta files, Packages, and ProjectSettings. Use GitHub’s Unity .gitignore. Keep Library, local preferences, generated builds, raw photo collections, and Blender backups outside tracked assets. Import exported FBX assets into Unity and keep editable Blender sources separately.
- Use short-lived branches and review before merging. One owner at a time for each Unity scene, prefab, or project settings file. The integration owner updates the build and records the commit tested. Record third-party assets and licenses in the README or THIRD_PARTY.md.

## Module boundaries

- Driving owns keyboard input, motion, recovery and camera. Keep the Kart root, Rigidbody, and colliders separate from its replaceable visual child. Expose only the speed and drift/boost state that UI and effects need.
- Track owns the closed loop, stable spawn pose, boundaries and ordered checkpoints. Race progress decides whether a finish crossing is valid. Start counting only after the countdown, require checkpoints in order, and ignore reverse or duplicate finish crossings.
- Visuals owns kart mesh, driver silhouette, campus landmarks and a small effects component driven by kart state. Use a one-meter test object to verify Blender scale. Record the FBX export settings after the first successful import.
- Integration owns Ready → Countdown → Racing → Results. Retry resets the kart pose and velocity, race state, checkpoints, timer, and temporary effects. Use a simple respawn that keeps the timer running and returns the kart to a safe point without awarding a checkpoint. Agree on the exact recovery rule before implementing it.

## Verification

| Check | Expected result |
|---|---|
| Countdown | The kart cannot move and the lap timer has not started. |
| Drift and boost | Holding drift while steering through a turn charges the boost. Releasing a charged drift triggers one brief boost and a visible cue. An early release gives no boost. |
| Ordered checkpoints | A valid forward finish produces one time and freezes it. |
| Missed, duplicated or wrong-way crossing | Progress cannot be exploited and the race does not finish early. |
| Retry | The next race starts with a clean timer, checkpoint state and kart velocity. |
| Recovery | The kart returns safely without advancing progress or resetting the race timer. |
| Deployed build | A teammate on a different computer opens the link and completes two consecutive attempts. |
| Fresh clone | A teammate opens the project in the documented Editor version and sees the intended scenes and prefabs. |

Keep the timing and lap rules independent of scene objects where practical. Use Unity Edit Mode tests for the race rules and a manual playtest for the driving feel and deployed build. Keep a compact record of build URL, commit, browser and outcomes.

## First tasks in order

1. Record the approved course scope and presentation date, and confirm repository and role owners. Create the Unity project from the Editor and verify it opens from a fresh clone.
2. Export a minimal Web build. Upload the ZIP with index.html at its root to itch.io and test outside the uploader’s session. If compression prevents loading, try an uncompressed smoke build.
3. Build one placeholder kart and one gray closed loop in parallel. Import one FBX sample and agree on scale and forward orientation.
4. Integrate the race rules and a minimal HUD, then deploy a complete start-to-retry run. Add art, drift tuning and sound after this works.

## Instructions for Claude Code and Codex

Start by reading this brief, README, ProjectSettings/ProjectVersion.txt, Packages/manifest.json and the assigned issue. Inspect the existing code before proposing changes. Work on the current task only. Keep C# readable for a beginner and reuse existing code when it fits. Ask the team to confirm a scope decision only when it affects the current task.

Preserve .meta files and prefab references. Prefer short C# scripts and explicit Inspector steps over bulk changes to Unity scene YAML. Coordinate any edits to shared scenes or project settings with the owner. Obtain team agreement before introducing a new package. Keep generated artifacts out of commits. Do not purchase assets, publish builds, or change repository settings as part of a coding task unless the team explicitly asks.

For each change, give the team the affected files, what the code does, any Inspector setup, a short manual check, and the tests actually run. Report what still needs a human playtest or browser check.

**First prompt after creating the project**

> Read this handoff and inspect the Unity project. Plan only the first browser smoke test. Identify the Editor version and Web module prerequisites, propose the smallest scene and acceptance check, and give the exact Unity steps we need to perform. After we confirm the scene runs, we will give you a separate driving task.
