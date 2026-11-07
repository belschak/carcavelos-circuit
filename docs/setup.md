# Setup (each teammate, once, ~30 min)

1. **Unity Hub**, only from the official site: https://unity.com/download . Sign in with a free Unity account (Personal licence). Never install Unity or add-ons from other sites.
2. In Hub > Installs > Install Editor > Archive / "Install other versions": **exactly `6000.3.28f1` (Unity 6.3 LTS)**. Tick **Web Build Support** in the modules list. Already installed? Installs > Manage > Add modules > Web Build Support.
3. **Git** (Windows: git-scm.com, Mac: `xcode-select --install`) and **GitHub Desktop** or the `gh` CLI. Accept the repo invite in your email.
4. Clone the repo: `gh repo clone belschak/carcavelos-circuit` (or GitHub Desktop > Clone).
5. Unity Hub > Projects > Add > the cloned folder. It must say 6000.3.28f1. **If Hub offers to open with another version: stop and ask in the group.**
6. Smart Merge for Unity files (once, global). Windows:
   ```
   git config --global merge.unityyamlmerge.name "Unity SmartMerge"
   git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
   git config --global merge.unityyamlmerge.recursive binary
   ```
   Mac: same, with path `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/Helpers/UnityYAMLMerge`.
7. Block accidental pushes to `main` (once per clone): `git config core.hooksPath .githooks`
8. Blender (blender.org, only Role 3 + whoever models landmarks).
9. Give your AI assistant this repo folder as its workspace. It reads `AGENTS.md` automatically (Claude Code via `CLAUDE.md`, Codex and Cursor directly).

Unity settings that must stay as they are (Edit > Project Settings): Editor > Asset Serialization = Force Text, "Reduce version control noise" ON; Version Control > Mode = Visible Meta Files.

## Daily loop
```
git switch main && git pull
git switch -c role1/drift-boost      # <role>/<task>
# ...work, commit small...
git push -u origin role1/drift-boost
gh pr create --fill                   # then ask a teammate to review
```
Before opening Unity: pull. Before a PR: merge `main` into your branch, open Unity, check the Console is clean, play once.

## Running Edit Mode tests from the command line (Unity must be closed)
Windows:
```
& "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults/editmode.xml -logFile TestResults/editmode.log
```
Mac: `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity` with the same arguments. Or in the Editor: Window > General > Test Runner > EditMode > Run All.

## Web build (Role 3, or the build backup)
File > Build Profiles > Web > Switch Platform. Player Settings > Web > Publishing Settings: Compression **Gzip**, Decompression Fallback **off** (turn on only if itch shows a decompression error). Build into `Build-Web/` (ignored by Git). Zip the folder contents so `index.html` is at the zip root, upload to itch.io as HTML, "This file will be played in the browser", viewport 1280x720, click-to-play on, SharedArrayBuffer off. Log it in `docs/builds.md`.
