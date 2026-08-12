# Elementals-Fight — Claude Code project guide

## What this project is
Elementals Fight is a mobile-first, side-scrolling 2-player fighting game built in Unity (editor version 6000.5.0f1). Playable via WebGL build; target platforms are mobile + browser.

## Working relationship with Claude AI (Cowork)
This repo is worked on in two halves:

- **Claude AI (Cowork, cloud)** owns architecture, design decisions, and task breakdown. It writes/updates `TASKS.md` in this repo with scoped, ready-to-execute tasks.
- **Claude Code (you, running here)** executes: edits scripts, runs build/test steps where possible, and makes git commits. When you finish a task, mark it done in `TASKS.md` with a short note of what changed, so it can be reviewed on the Cowork side.

Read `TASKS.md` at the start of a session for the current queue. If a task turns out to need a design decision rather than an implementation decision, stop and flag it instead of guessing — that call belongs to Claude AI / Stephanie, not to a mid-implementation judgment call.

## Repo / git conventions
- Remote: `https://github.com/Takeme-Steph/Elementals-Fight` (origin), default branch `main`.
- Git LFS is enabled (`.gitattributes` tracks `.fbx`/`.FBX`/`.png`/`.tga`/`.anim`/`.controller`/etc.) — don't bypass it for new or changed binaries.
- **History was rewritten on 2026-07-27** to retroactively migrate existing binaries into LFS and purge a stale `connectwebgl.zip` build artifact (~213 MiB across 9 recommits) from history entirely. `main` was force-pushed; every commit hash changed. A fresh clone's packed size dropped from ~460 MiB to ~214 MiB (WebGL Builds/ binary content and scene/prefab YAML weren't in scope for this pass, so there's still bulk left if that's ever worth revisiting). A pre-rewrite snapshot is preserved on the `backup/pre-lfs-history-rewrite` branch if anything needs to be recovered from it.
- **History was rewritten again on 2026-08-12**, message-only this time: stripped the `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` trailer that earlier Claude Code sessions had been appending to commits, since it was surfacing as a "claude" entry on the repo's GitHub Contributors page (display-only — it never granted any actual collaborator access). Scoped to `main` only via `git filter-repo --refs main --message-callback ...`; file contents are byte-identical (`git diff` between the old and new tip is empty), only commit messages/hashes changed. `main` was force-pushed again; a pre-rewrite snapshot is preserved on the `backup/pre-coauthor-strip-2026-08-12` branch. **Going forward, do not add a `Co-Authored-By` trailer to commits in this repo.**
- **If you're picking up local work from before 2026-08-12, don't try to reconcile old history against the new `main` — re-clone or rebase onto the new tip instead** (see the two backup branches above if anything needs recovering from either pre-rewrite state).
- **Do not commit directly to `main`.** Create a feature branch per task (e.g. `task/fix-knockback-state`), commit there. Do not push or open a PR unless explicitly told to — leave the branch ready for review.
- Keep commits scoped to one task. Write commit messages that explain *why*, not just *what* — matches the existing comment style in this codebase (see below). Do not append a `Co-Authored-By` trailer.

## Engine / build
- Unity 6000.5.0f1. Don't attempt command-line Unity builds without the Editor; play-mode verification happens inside the Editor. You're primarily making source-level changes here, not driving the Editor itself.
- WebGL build output lives in `WebGL Builds/` — never hand-edit build artifacts.

## Code architecture
- `Assets/Scripts/Input/` — Unity's new Input System. `PlayerInput.cs` is **generated** from `Assets/Settings/Input/*.inputactions` — never hand-edit it. `InputReader.cs` converts input actions into C# events (MoveEvent, JumpEvent, AttackEvent, etc.) — subscribe to these instead of touching `PlayerInput` directly.
- `Assets/Scripts/PlayerCTRLs/` — per-character gameplay logic:
  - `PlayerStateMachine.cs` is the FSM controller. **Note:** this replaced an older `PlayerStateManager.cs` — that file no longer exists in the codebase; don't recreate it or reference it. States live in `PlayerCTRLs/States/`: `IdleState`, `WalkingState`, `JumpingState`, `AttackingState`, `BlockingState`, `HitstunState`, `KnockbackState`, all deriving from `PlayerState`. Other scripts drive the state machine through its public methods (`Move`, `RequestJump`, `RequestAttack`, `RequestBlock`, `EnterHitstun`, `EnterKnockback`, `ForceIdle`) rather than touching state flags directly.
  - `PlayerController.cs`, `PlayerManager.cs`, `AttackCTRL.cs`, `CharacterPhysics.cs`, `PlayerAutoPilot.cs` round out per-character behavior.
- `Assets/Scripts/GameManager/` — scene/match flow: `SceneHandler.cs` (round/match state, safe zones, health bar wiring — lives on a GameObject literally named `"GameManager"` in each scene; multiple scripts look it up by that exact name via `GameObject.Find`, so don't rename it without updating every lookup), `LoadCharacter.cs`, `MatchUIManager.cs`, `PauseManager.cs`, `CameraCTRL.cs`.
- `Assets/Scripts/PlayerSelection.cs` — character-select screen; stores picks via `PlayerPrefs` (`selectedCharacter`, `selectedOpponent`) before loading the fight scene.

## Conventions already established in this codebase
- Favor `TryGetComponent<T>()` over `GetComponent<T>()` + null check.
- Log missing-required-component errors via `Debug.LogError(...)` rather than throwing.
- Event-driven where possible: subscribe/unsubscribe in `OnEnable`/`OnDisable`.
- Bug-fix comments in this codebase explain *why* the bug happened and what would break without the fix (see `PlayerStateMachine.cs` and `SceneHandler.cs` for the pattern) — keep writing comments that way; it's what makes this repo legible to both AI agents and the human maintainer later.

## Things not to touch without explicit instruction
- Don't hand-edit `.unity` scene files, `.prefab`, or `.asset` files as raw YAML — these are normally edited through the Unity Editor; manual text edits risk GUID mismatches or broken references. If a task needs a scene/prefab change, flag it as an in-editor task rather than editing blind.
- Don't edit generated Input System code (`PlayerInput.cs`).
- Don't rename the `"GameManager"` GameObject or the `"Player"` tag without grepping for and updating every `GameObject.Find(...)` / tag lookup that depends on it.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` are Unity-generated/local state — never commit changes there.
