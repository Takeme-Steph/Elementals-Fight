# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

## Format for new tasks
Each task gets: a scope-limited title, the *why* (architectural reasoning, not just the ask), acceptance criteria, and any files known to be relevant. Claude AI keeps entries in this shape going forward.

## Queue

### [x] CharacterSelect EventSystem input module - fixed live, needs review + scene sweep + commit
**Why:** Project Settings > Player > Active Input Handling is set to "Input System Package (New)" only, which disables `UnityEngine.Input` at the API level (throws `InvalidOperationException` instead of returning stale values). The `EventSystem` GameObject in `CharacterSelect.unity` still had the default `Standalone Input Module` component, which Unity auto-attaches to any EventSystem whenever a Canvas is created, regardless of the project's Active Input Handling setting - it was never updated when the project standardized on the new Input System. Every frame, `EventSystem.Update()` ticked that module, which called into the disabled `UnityEngine.Input.mousePosition` and threw - flooding the console on every Play, and, more importantly, silently breaking all UI pointer input (clicking character portraits) since the module could never successfully read a pointer position.

Claude AI (Cowork) already applied the fix directly through a live Unity Editor connection - with Stephanie's explicit go-ahead for this one case, since it touches scene YAML - rather than routing it through this queue first: removed `StandaloneInputModule` from the `EventSystem` GameObject in `CharacterSelect.unity` and added `InputSystemUIInputModule` (from the already-installed `com.unity.inputsystem@1.19.0` package) in its place, then saved the scene. That part is done. What's left is Claude Code's normal review-and-commit pass, plus checking whether the same stale-component issue exists in other UI scenes that were created before the project switched to the new Input System.

**Acceptance criteria:**
- Confirm the diff in `Assets/Scenes/CharacterSelect.unity` is exactly the EventSystem component swap (StandaloneInputModule removed, InputSystemUIInputModule added) with no unrelated changes.
- Playtest CharacterSelect in the Editor: console should stay clean of the `InvalidOperationException`, and clicking/selecting a character portrait should work.
- Check other scenes (main menu, pause, results, any other Canvas-bearing scene) for `EventSystem` GameObjects still using `StandaloneInputModule` instead of `InputSystemUIInputModule`, and apply the same swap wherever found.
- Commit with a clear message noting this is an Input System migration cleanup, not a gameplay change.

**Relevant files:** `Assets/Scenes/CharacterSelect.unity` (already changed), and any other `.unity` scene with a Canvas/EventSystem.

**Done:** Reviewed the diff - it's not a pure component swap, it also carries incidental Unity resave churn (RenderSettings/LightmapSettings serializedVersion bumps, TMP field upgrades on ~6 text objects, Light/URP light-data field renames, one Canvas flag) from opening+saving the scene with the current package versions. Called this out and committed anyway per go-ahead, noted in the commit message. Scene sweep: this project only has two scenes with a Canvas/EventSystem (`CharacterSelect.unity`, `FightScene.unity`) - `FightScene.unity`'s EventSystem was already on `InputSystemUIInputModule`, so no other scene needed the swap. Playtest step still needs manual in-Editor verification (console clean, portrait click-select works) - not something I can drive from here.

---
### [x] Jump/Attack/HeavyAttack input queues across the match-inactive gate
**Why:** Same root cause as the Block fix (see below), different shape. `Jump()`/`Attack()` in `Update()` are correctly gated on `!sceneHandler.isGameOver && sceneHandler.activeMatch` before *acting* on the `jump`/`isAttacking`/`isHeavyAttacking` flags - but `HandleJump()`, `HandleAttack()`, and `HandleHeavyAttack()` set those flags *unconditionally* the instant the input fires, with no gate at all. If a press happens while `activeMatch` is false (the post-round countdown, or right as Game Over hits), the flag gets set but is never consumed/reset that frame, since the gated `Jump()`/`Attack()` calls don't run at all. It sits queued until `activeMatch` flips back to true at the start of the next round, at which point it fires immediately on the first frame of the new round - even though the player isn't pressing anything anymore. Block doesn't have this problem anymore since `HandleBlock` now gates before setting anything; these three still do.

**Acceptance criteria:** `HandleJump()`, `HandleAttack()`, and `HandleHeavyAttack()` each check the same condition already used in `HandleBlock` (`sceneHandler == null || sceneHandler.isGameOver || !sceneHandler.activeMatch`) and return early *before* setting their respective flag, rather than setting it and relying on `Update()` to gate the consumption. Tapping Attack, Heavy Attack, or Jump during the round-transition countdown or after Game Over should have zero effect - not fire immediately, and not fire later when the next round starts. Normal input during active gameplay must be unaffected.

**Relevant files:** `Assets/Scripts/PlayerCTRLs/PlayerController.cs`

**Done:** Added the same `sceneHandler == null || sceneHandler.isGameOver || !sceneHandler.activeMatch` guard to the top of `HandleJump`, `HandleAttack`, and `HandleHeavyAttack`, before their flags get set.

---
### [x] Block input bypasses match-active gating
**Why:** `PlayerController.Jump()` and `Attack()` are both polled inside `Update()`, which already guards on `!sceneHandler.isGameOver && sceneHandler.activeMatch` before doing anything. `Block` is different - `HandleBlock(bool isHeld)` is wired directly to `InputReader.BlockEvent` and fires immediately on press/release, so it never passes through that same gate. Result: holding Block still works during the post-round countdown (`activeMatch == false`) and after Game Over (`isGameOver == true`), even though every other action is correctly locked out in both cases. `PlayerAutoPilot` doesn't have this bug - its `Update()` already gates *before* calling `RequestBlock`, so this is human-input-only.

**Acceptance criteria:** `HandleBlock` in `PlayerController` checks the same condition (`sceneHandler == null || sceneHandler.isGameOver || !sceneHandler.activeMatch`) before calling `stateMachine.RequestBlock(isHeld)`, mirroring how `Jump()`/`Attack()` already guard. Pressing/holding Block during the round-transition countdown or after Game Over should do nothing. Normal blocking during active gameplay must still work exactly as before.

**Relevant files:** `Assets/Scripts/PlayerCTRLs/PlayerController.cs`

**Done:** Added the same `sceneHandler == null || sceneHandler.isGameOver || !sceneHandler.activeMatch` guard to the top of `HandleBlock`, mirroring `Jump()`/`Attack()`.

---
*Nothing else queued — bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
