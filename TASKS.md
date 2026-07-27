# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

## Format for new tasks
Each task gets: a scope-limited title, the *why* (architectural reasoning, not just the ask), acceptance criteria, and any files known to be relevant. Claude AI keeps entries in this shape going forward.

## Queue

### [x] Build Yemoja's playable character prefab and add her to the roster
**Why:** Yemoja is now imported, rigged (Mixamo Humanoid, valid avatar) and materialled at `Assets/CharacterModels/Yemoja/`, but the prefab there (`prefabs/Yemoja.prefab`) is just the raw model — Animator + avatar and nothing else. It is not a playable character. The three playable characters live somewhere different: `Assets/Prefabs/Characters/PlayerPrefabs/` (`EarthMage`, `Ninja`, `WarriorPrincess`), and each carries a specific component set on the root — `Animator, Rigidbody, BoxCollider, PlayerController, PlayerAutoPilot, PlayerManager, PlayerStateMachine, CharacterPhysics, AttackCTRL` (+ Transform). `PlayerStateMachine.Awake()` hard-`LogError`s if there is no Animator, and `LoadCharacter` `LogError`s if the spawned prefab lacks `PlayerController` (player slot) or `PlayerAutoPilot` (opponent slot), so a partially-wired prefab fails loudly rather than silently.

The important thing that makes this cheap: **all three existing characters already share the same `EarthMageAnimCTRL`**, retargeted through Unity's Humanoid system rather than each having a bespoke controller. Yemoja's avatar is a valid Humanoid with every core bone mapped, so she can reuse that same controller directly — no new AnimatorController, no re-authoring the state graph, and the `PerformAttack` / `StopAttacking` / `EndHit` animation events already baked into those clips will fire on her too. Do not author a new controller for her unless retargeting visibly breaks.

Two scale details that are easy to get wrong. Her source FBX is deliberately set to **1.800 units** (metres) to match how the other source models are authored (Ninja 1.89, WarriorPrincess ~1.77 at source). The in-game characters are roughly **twice** that — Ninja 3.425, WarriorPrincess 3.501, EarthMage 3.528 — because `Ninja` and `WarriorPrincess` apply a **1.813 root scale on the prefab**. So Yemoja needs the same treatment at the prefab root; do not "fix" it by changing her FBX import scale, which would desync her from the source-model convention and rescale her skeleton.

Also note `PlayerSelection` (CharacterSelect scene) and `LoadCharacter` (FightScene) hold two **separate** arrays that are index-aligned only by convention — `PlayerSelection.characters[]` / `opponents[]` are the display models, `LoadCharacter.charPrefabs[]` are the spawnable prefabs, and the chosen index is passed between scenes through `PlayerPrefs`. Appending her to one array but not the other, or at different positions, will spawn the wrong fighter rather than erroring.

**Acceptance criteria:**
- New prefab `Assets/Prefabs/Characters/PlayerPrefabs/Yemoja.prefab`, built to match `EarthMage.prefab`'s component set exactly.
- Animator: controller `EarthMageAnimCTRL`, avatar `YemojaAvatar`, `applyRootMotion = false` (matches the others).
- Rigidbody: mass 2, `useGravity` on, constraints freezing all rotation plus Z position (value 120, the 2.5D-fighter setup the others use).
- BoxCollider sized and centred to *her* proportions — do not copy EarthMage's box verbatim, her silhouette differs.
- Root scale set so her visual height lands in the 3.4–3.5 range, consistent with the existing roster.
- Added to **both** `LoadCharacter.charPrefabs` and `PlayerSelection.characters` / `opponents`, at the same index in each.
- Playtest: select her in CharacterSelect, confirm she spawns, idles, walks, jumps, attacks, blocks and takes hitstun without console errors, and that retargeted animation looks correct (no collapsed joints or floating).

**Relevant files:** `Assets/Prefabs/Characters/PlayerPrefabs/EarthMage.prefab` (template), `Assets/CharacterModels/Yemoja/`, `Assets/Scripts/GameManager/LoadCharacter.cs`, `Assets/Scripts/PlayerSelection.cs`, `Assets/Animations/AnimCTRLs/EarthMageAnimCTRL.controller`, and the `CharacterSelect.unity` / `FightScene.unity` scenes for the array wiring.

**Done:** Reviewed Cowork's live-Editor build of `Yemoja.prefab` and committed it. Verified via the prefab YAML: component set matches `EarthMage.prefab` exactly (Animator, Rigidbody, BoxCollider, PlayerController, PlayerAutoPilot, PlayerManager, PlayerStateMachine, CharacterPhysics, AttackCTRL); Animator controller overridden to `EarthMageAnimCTRL` (guid-matched) with `applyRootMotion = false`, avatar inherited correctly from her Humanoid FBX import (no override needed since it's already correct at the source); Rigidbody mass 2 / `useGravity` on / constraints 120; BoxCollider sized independently to her own proportions (not a copy of EarthMage's). Root scale is 1.91667 - not the 1.8132 Ninja/WarriorPrincess use, which is specific to their own source heights - correctly recalculated for her 1.8m source FBX to land at 1.8 x 1.91667 = 3.45m, inside the 3.4-3.5 band. Added to `LoadCharacter.charPrefabs` (FightScene) and both `PlayerSelection.characters`/`opponents` (CharacterSelect) at matching indices, verified by GUID cross-reference, not just visually. Playtest step still needs manual in-Editor verification - not something I can drive from here.

---
### [x] Attach Yemoja's trident to her hand
**Why:** Her trident shipped as a separate mesh (`Assets/CharacterModels/Yemoja/models/Yemoja_Trident.fbx`, static, `animationType = None`) with no attachment point, so right now it is an unparented prop — it will not move with her. Every other armed character in the roster solves this by parenting the weapon under a hand bone so it inherits the animated transform (`WarriorPrincess` has `Longsword`, `Ninja` has `NinjaSword`). Her skeleton is Mixamo-named, so the bone to look for is `mixamorig:RightHand` (or Left, if her attack animations read better mirrored).

Scale is already handled and should be left alone: the trident is set to **1.941 m**, recovered from the artist's authored ratio of 1.0785 x body height, so it reads as a polearm slightly taller than she is. Because it becomes a child of a bone inside a prefab whose root is scaled ~1.813, its *world* size will follow her — that is correct and intended, do not compensate for it.

Depends on the Yemoja playable-prefab task above; do that one first and attach the trident inside that prefab.

**Acceptance criteria:**
- Trident parented under the appropriate hand bone in `Yemoja.prefab`, with local position/rotation adjusted so the shaft sits naturally in her closed palm rather than intersecting it.
- Trident tracks her hand through idle, walk, jump and both attack animations with no detaching, sliding or visible clipping through her arm or body.
- Trident's `Yemoja_Trident_mat` still renders correctly (it uses a DirectX-convention normal map that is already flipped on import — do not re-import it with different settings).
- No change to the trident's 1.941 m local size.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_Trident.fbx`, `Assets/Prefabs/Characters/PlayerPrefabs/Yemoja.prefab`, and `WarriorPrincess.prefab` as a reference for how an existing weapon is attached.

**Done:** Verified in the prefab YAML: the trident is parented under a real skeleton bone (a stripped reference into her FBX hierarchy, not one of the `A_`/`H_` hand hitbox markers used by `AttackCTRL`), with a deliberate, non-zero local position (~2-8cm offset) and rotation - not left at the bone's origin. Local scale untouched at 1.941m. Didn't touch the trident material's import settings. Visual fit (no clipping through idle/walk/jump/attacks) still needs manual in-Editor playtest verification.

---
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
*Everything below this point is done. Open work is at the top of the queue — bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
