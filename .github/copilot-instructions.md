## Elementals-Fight — Copilot instructions

Quick, focused guidance to help AI code agents be productive in this Unity project.

- Project type: Unity (Editor version recorded in `ProjectSettings/ProjectVersion.txt`: 6000.0.61f1 — Unity 2023.x). Use the Unity Editor to build and run scenes; do not attempt to run Unity-specific build steps from scratch in the workspace without the Editor.
- Entry points & scenes: main gameplay scenes are `Assets/Scenes/CharacterSelect.unity` (character selection) and `Assets/Scenes/FightScene.unity` (match). `GameManager` logic is mounted on a GameObject named "GameManager" in scenes and the `SceneHandler` script (Assets/Scripts/GameManager/SceneHandler.cs) exposes scene-wide state (safe zones, players, health bars).

- Code organization:
  - `Assets/Scripts/Input/` contains generated Input System code (`PlayerInput.cs`) and `InputReader.cs` which convert input actions into events. Prefer subscribing to `InputReader` events (MoveEvent, JumpEvent, AttackEvent) rather than touching the auto-generated `PlayerInput` file.
  - `Assets/Scripts/PlayerCTRLs/` contains per-character controllers and logic (`PlayerController.cs`, `PlayerManager.cs`, `PlayerStateManager.cs`, `AttackCTRL.cs`). Use these when changing movement, attacks, damage, or states.
  - `Assets/Scripts/GameManager/` contains scene and flow management (`SceneHandler.cs`, `LoadCharacter.cs`, `CameraCTRL.cs`). Game flow (match start/over) is handled here.
  - `Assets/Scripts/` top-level helper scripts like `PlayerSelection.cs` manage the Character Select UI and store choices via `PlayerPrefs` keys `selectedCharacter` and `selectedOpponent` before loading the fight scene.

- Conventions and important patterns:
  - Event-driven input: gameplay reacts to `InputReader` events — add/remove delegates in `OnEnable`/`OnDisable` to manage subscriptions (see `PlayerController.cs`).
  - Use `TryGetComponent<T>` where present and prefer logging via `Debug.LogError(...)` when required components are missing (consistent pattern across scripts).
  - Scene-wide references: `SceneHandler` is located on a GameObject named `GameManager`. Scripts frequently call `GameObject.Find("GameManager").TryGetComponent<SceneHandler>(out sceneHandler)` — preserve that GameObject name when editing scenes or prefabs.
  - Physics and ground checks: `PlayerController` uses raycasts against `sceneHandler.groundLayerMask` and small distances (0.1f). If changing ground colliders or layers, update `SceneHandler` and masks accordingly.
  - UI binding: health bars use `SceneHandler.UpdateHealthBars()` which fetches `PlayerManager` data from `mainPlayer` and `oppPlayer` references — ensure Player prefabs have `PlayerManager` attached and the "Player" tag.

- Build & run notes (developer workflows):
  - Open the project in Unity Editor matching ProjectVersion when possible. The repository includes a WebGL build link in README; WebGL builds require Unity's WebGL module.
  - For quick local testing, run scenes inside the Editor; Play-mode flow expects a `GameManager` GameObject in the active scene.
  - The Input System used is Unity's new Input System (package in `Packages/manifest.json`). Changes to `Assets/Settings/Input/PlayerInput.inputactions` will regenerate `PlayerInput.cs` — do not manually edit the generated file.

- Cross-cutting integrations and external deps:
  - Unity packages are declared in `Packages/manifest.json` (Cinemachine, Input System, Universal RP, Visual Scripting, Test Framework, etc.). Use the Package Manager to manage versions.
  - No external network services or CI configurations are present in the repo — changes should be Unity-local.

- Quick examples to reference while coding:
  - Subscribe to input events (see `PlayerController.OnEnable`):
    - inputReader.MoveEvent += HandleMove;
  - Store chosen character from character select (see `PlayerSelection.StartGame`):
    - PlayerPrefs.SetInt("selectedCharacter", _selectedPlayer);
    - SceneManager.LoadScene(1, LoadSceneMode.Single);

- Safety and do-not-edit rules for agents:
  - Do NOT edit generated files under `Assets/Settings/Input/` (e.g., `PlayerInput.cs`).
  - Avoid changing the `GameManager` GameObject name or the `Player` tag without updating all scene lookup usages.
  - When altering physics or layer masks, update related values in `SceneHandler` and search for usages of `groundLayerMask`, `safeZoneLeftX`, `safeZoneRightX`.

- Where to look for tests and further documentation:
  - `Packages/manifest.json` shows included test framework; no tests shipped in repo root — search `Assets/Tests` if adding tests.
  - High-level game design and WebGL link in `README.md`.

If anything here is unclear or you'd like more detail (scene setup, prefab lists, or example edits), tell me which area to expand and I will iterate.
