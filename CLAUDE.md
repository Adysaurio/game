# Trash Pandas — notes for Claude

Online co-op Steam game ($5–8): a crew of raccoons robs a garden wedding. Unity **6000.6.4f1** (URP, Input System,
Cinemachine 3, Netcode for GameObjects 2.13 + Unity Relay/Lobby). The Unity project is in `TrashPandas/`.

## Working with the owner

- The owner is a product designer, not a programmer. **Answer in Mexican Spanish**; code, identifiers, commit
  messages and in-game UI text stay in English.
- **Test live before handing off.** Verify with the dev bots/telemetry and look at screenshots — never claim
  something works from reading code. Orientation/animation bugs only show in game (see "Lessons" below).
- **Ask before spending Higgsfield credits** (images, 3D, rigging, animation): give the cost (`get_cost: true`) and
  show results before the next paid step. Prefer local work (Blender) when possible.
- **Never push or merge to `main` without approval.** Merges are local, after the owner playtests.
- Builds go to `Builds/mac/TrashPandas.app` only. `Builds/share/*.zip` (for friends) only when asked.

## Design (current)

- Concept v2: `docs/superpowers/specs/2026-10-07-trash-pandas-v2-design.md`. Research behind the choices:
  `docs/research/2026-10-08-world-class-design-research.md`.
- Each player drives one raccoon. Debug mode (menu → "Debug mode") = one person, 3 raccoons, Tab switches.
- Round: 8-min clock (HUD top center). Bring the 3 random objectives to the den → **GETAWAY!** exits open. Clock out
  → "the party's over": a RUN you can't hide from, exits open.
- One **nemesis per round** at random (wedding planner — radio; pest control guy — never tires, flashlight sees into
  bushes; granny — hears everything, throws a slipper). Only the nemesis (+ helper: waiter / cat) hunts; other guests
  are witnesses who point and report. The nemesis learns tricks (same hideout twice, pebbles, pipes).
- **RUN**: no exits unless unlocked; hide (bush / trash can / hot-dog mascot / smoke) until the alert drains → PHEW,
  back to normal. Caught → pet carrier, friends free you.
- Tools: pebble (bonk / lure), smoke bomb, banana peel (anyone slips). **Hold Q (or RT) to aim**: over-the-shoulder
  camera, crosshair, lands where you aim, effect ring + distance. Carried loot: hold click to aim the same way.
- Moves: walk / Shift run (bounding gallop) / C sneak / jump / ledge grab + climb / push / towers (jump on a friend)
  / pipes / hideouts (E) / emotes G dance, H cheer. Xbox pad supported (`Runtime/Input/Pad.cs`).
- Pending: food powers (beans fart-jump, salsa fire burp; rules done in `Core/Raccoons/FoodPowers.cs`, not wired),
  2nd-floor ring shelf + frozen champagne locks, dance-floor trenchcoat event, Vivox voice.

## Code map

- `Assets/Scripts/Core` — pure rules, TDD'd (NUnit EditMode tests in `Assets/Tests/EditMode`).
- `Assets/Scripts/Runtime` — MonoBehaviours. Host-authoritative simulation (`SimulationAuthority`), raccoons
  owner-authoritative (`NetworkTransform`). Key directors: `Panic/PanicDirector`, `Panic/NemesisDirector`,
  `Npc/SuspicionDirector`, `Loot/LootDirector`, `Squad/GadgetDirector`, `Squad/CarryDirector`.
- `Assets/Scripts/Editor/GreyboxSceneBuilder*.cs` builds the whole scene + raccoon prefab from code
  (`BuildAll`). Change the level there, then rebuild — don't hand-edit the scene.
- One MonoBehaviour per file named after the class (otherwise the built scene comes out "corrupted").

## Commands (close the Unity editor first)

```bash
tools/unity-test.sh                                                     # EditMode tests (expect all green)
tools/unity-run.sh TrashPandas.EditorTools.GreyboxSceneBuilder.BuildAll # rebuild scene + prefabs
tools/unity-run.sh TrashPandas.EditorTools.BuildScripts.BuildMac        # → Builds/mac/TrashPandas.app
tools/unity-run.sh TrashPandas.EditorTools.BuildScripts.BuildWindows    # → Builds/windows
```

On **Windows** (PowerShell): `.\tools\unity-test.ps1` and `.\tools\unity-run.ps1 <Method>` (same methods; Unity is
looked up in `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`). The built Windows app is
`Builds\windows\TrashPandas.exe` and takes the same dev flags. The Blender scripts run with
`"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python ...`.

Dev flags for the built app (`Builds/mac/TrashPandas.app/Contents/MacOS/TrashPandas`):
`-debugmode` (straight into debug mode), `-batchmode -nographics -telemetry -quitafter N -logFile <path>` (headless
with a status line every 0.5 s), `-shot <seconds> <png>` (windowed screenshot), `-bot <name>`, `-nointro`,
`-nemesis WeddingPlanner|PestControl|Granny`, `-unlockexits`, `-sidecam` / `-chasecam`, `-aimtest`.
Useful bots: `keyw` / `keyd` / `keywd` (hold W / D like a player, camera-relative), `dash`, `stroll`, `sneak`,
`flee`, `hideflee`, `canhide`, `tunnel`, `push`, `climb`, `tower`, `gadgets`, `lureflee`.
Online: `-autohost-local -autostart 2` + `-autojoin-local` (same machine), or `-autohost-relay <codefile>` +
`-autojoin-relay <codefile>` (real internet test through Unity Relay; the project is linked to Unity Cloud).
**Don't combine `-debugmode` with the online flags** (debug mode is offline).

## Art pipeline

- Raccoon: Tripo3D T-pose mesh (`art/raccoon2/tripo.glb`, not in git) → `tools/rig_raccoon.py` (Blender, headless:
  measures orientation and landmarks, builds the skeleton with the bone names the gait code uses, skins through a
  watertight proxy) → `tools/raccoon_anims.py` (keyframed clips + FBX) → `Assets/Art/Raccoon/`. Bandana recolored per
  player (`RaccoonSkin_P1..P4.png`). Walk / sneak / gallop are procedural (`Runtime/Raccoon/RaccoonLook.cs`).
- Nemeses: Meshy auto-rigged models (`Assets/Art/Characters/*`, merged by `tools/raccoon_to_fbx.py`), turned −90°
  (`MeshyYaw`) because Meshy models face +X.
- Blender 5.2: `/Applications/Blender.app/Contents/MacOS/Blender -b --python <script> -- <args>`.

## Lessons (don't repeat)

- Verify facing **as a player would**: bots `keyw`/`keyd` + `-chasecam` (should see the back) and `-sidecam` (profile).
  Meshy's `headfront` bone is NOT the face — trusting it caused the crab walk.
- A bot that runs into a wall reports "not moving": check positions in telemetry before trusting a measurement.
- Take screenshots after the level has loaded (≥ 3.5–4 s with `-nointro`), or you capture the menu.
- `LootDirector`'s clock reads 0 before the round is set up — anything keyed on time-left must check the round started.
