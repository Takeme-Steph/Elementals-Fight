# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked — needs Stephanie's explicit go-ahead before executing

## Format for new tasks
Each task gets: a scope-limited title, the *why* (architectural reasoning, not just the ask), acceptance criteria, and any files known to be relevant. Claude AI keeps entries in this shape going forward.

## Queue

### [x] Dual AI+player control on every spawned fighter, and Yemoja sinking into the floor - both fixed live, need review + commit
**Why:** Stephanie played a real match (mirror Yemoja vs Yemoja) and hit two bugs at once. Cowork inspected both live via the Unity connection while paused/playing rather than guessing from source alone.

**Bug A - both PlayerController and PlayerAutoPilot end up enabled on both fighters, every match, not just mirror match-ups.** In `LoadCharacter.cs`, `SpawnPlayer()` sets `playerController.enabled = true` but never disables `PlayerAutoPilot`; `SpawnOpponent()` sets `autoPilot.enabled = true` but never disables `PlayerController`. Every character prefab (confirmed by checking `Yemoja.prefab`'s serialized defaults) ships with both components enabled by default, so neither spawn function ever actually turns the *other* one off - both fighters end up simultaneously driven by human input and the fight AI. This is unrelated to picking the same character twice; it would reproduce with any pairing.

Fixed by adding the missing negative case to each function: `SpawnPlayer()` now also grabs `PlayerAutoPilot` and sets `.enabled = false`; `SpawnOpponent()` now also grabs `PlayerController` and sets `.enabled = false`. Verified live post-fix across two separate spawn cycles (4 fighter instances total): every instance has exactly one of the two components enabled, matching its `isCTRLPlayer` flag, never both.

**Bug B - Yemoja's rig sinks badly below the floor while any of her new animations play (not while idle in the editor/bind pose - only once an AnimatorController clip drives her).** Measured directly: her prefab's own bind pose has `Hips` at world Y ~1.99 and feet at ~0.18 (correct, standing on the ground) - so the base rig/avatar import is fine. But with any of her 8 new Mixamo-sourced clips (`Assets/Animations/YemojaAnimations/*.fbx`) playing, `Hips` dropped to ~Y 0.02 and feet to ~Y -1.3, i.e. more than a metre of her lower body rendering underground.

Root cause traced to the animation clips' `ModelImporter.avatarSetup = CopyFromOther` (pointed at her own `YemojaAvatar`). That setting is meant for animation-only files that share the *exact same skeleton/proportions* as the target (e.g. multiple mocap takes of one already-rigged character) - it skips proportional retargeting and applies the clip's raw bone data as if it already matched. These clips don't share her skeleton at all; they're generic Mixamo animations at Mixamo's own proportions, being forced onto a custom rig that's roughly double Mixamo's default scale (per her documented scale history - 1.91667 prefab root scale, deliberately far from a 'natural' auto-computed value). Applied as same-skeleton data with no scale correction, the baked root height came out roughly half what it should be.

First attempted fix (`Root Transform Position (Y): Based Upon = Feet` instead of `Original`, i.e. `heightFromFeet = true`) only moved Hips from Y 0.02 to Y 0.21 - barely any improvement, confirming the problem wasn't the per-frame height basis. Second fix, which actually worked: changed `avatarSetup` from `CopyFromOther` to `CreateFromThisModel` on all 8 clips, so Unity builds each clip's own avatar from Mixamo's real proportions and performs genuine cross-skeleton humanoid retargeting (with correct human-scale ratio) onto `YemojaAvatar`, instead of blindly reusing hers. Re-measured post-fix: Hips ~Y 1.81-1.87 (vs bind-pose-correct ~1.99), feet ~Y 0.23-0.27 (vs bind-pose ~0.18), combined mesh bounds bottom at Y ~-0.46 to -0.54 (vs ~-2.24 before the fix). Dramatically better - no longer reads as 'half her body underground' - but not a perfect zero: there's a residual few-tenths-of-a-unit dip, most likely a genuine slight forward-weighted stance in the Sword & Shield idle pose itself rather than a leftover scale bug, but that's a guess, not verified.

Both `heightFromFeet = true` and the `avatarSetup` change were kept together on all 8 clips (`HitReaction`, `StumbleBackwards`, `SwordAndShieldAttack`, `SwordAndShieldBlockIdle`, `SwordAndShieldIdle`, `SwordAndShieldJump`, `SwordAndShieldSlash`, `SwordAndShieldWalk`).

**Acceptance criteria:**
- Review the diff: `Assets/Scripts/GameManager/LoadCharacter.cs` (two method bodies) and the `.meta`/import-settings changes on all 8 files under `Assets/Animations/YemojaAnimations/` (`avatarSetup` + `heightFromFeet`/`keepOriginalPositionY`).
- Playtest a mirror Yemoja match end to end: confirm exactly one of PlayerController/PlayerAutoPilot is enabled per fighter, and that only the human-controlled one responds to input.
- Playtest Yemoja specifically through Idle, Walk, Jump, Attack, HeavyAttack (Slash), Block, Hit, and KnockBack - visually confirm no part of her sinks noticeably into the floor across any of them, not just Idle (only Idle was measured directly this session).
- If the residual ~0.3-0.5 unit dip is visually noticeable rather than a subtle stance lean, treat it as unresolved and dig further (possible next step: check whether `Root Transform Position (Y): Offset` needs a small manual nudge, or re-run the trident grip/palm-pass-through re-sweep noted as still-open in `yemoja-character-setup.md` at the same time, since both are downstream of the same idle pose).
- If Ninja/WarriorPrincess ever get replaced with new Mixamo-sourced models/animations (see the BLOCKED task below), default new non-matching-skeleton animation imports to `avatarSetup = CreateFromThisModel`, not `CopyFromOther` - this bug class will recur otherwise.
- Commit with a message that separates the two fixes conceptually even if done in one commit (dual-control gating bug vs. animation retargeting bug) so `git blame`/history stays legible.

**Relevant files:** `Assets/Scripts/GameManager/LoadCharacter.cs`, `Assets/Animations/YemojaAnimations/*.fbx` (all 8), `claude/yemoja-character-setup.md` (project doc, updated alongside this).

**Done:** Reviewed both diffs against the description and confirmed they match: `SpawnPlayer()`/`SpawnOpponent()` now each disable the opposing component exactly as described, and re-verified independently against `PlayerStateMachine.PerformAttack()`/`StopAttacking()` call sites that the two are the only components gating input vs. AI (no third path). Found and fixed one incidental regression not part of the intended change: both method signatures (`void SpawnPlayer()` / `void SpawnOpponent()`) had lost their 4-space indent, presumably a live-edit artifact - restored to match the file's existing style. Confirmed all 8 clips carry `avatarSetup: 1` (`CreateFromThisModel`) and `heightFromFeet: 1` as described, and that `Yemoja.prefab`'s `m_Controller` override now points at the new dedicated `YemojaAnimCTRL.controller` (guid-matched) rather than the shared `EarthMageAnimCTRL` - confirmed by guid that all 8 clips are wired into it as states, one reference each.

**New finding, not part of this task's original scope - flagging rather than guessing at a fix:** all 8 new clips import with `events: []` - zero animation events, on any of them. `AttackingState.PerformAttack()` and the `OnAnimationComplete()` handlers on `AttackingState`/`HitstunState` are only ever invoked via animation events calling `PlayerStateMachine.PerformAttack()`/`StopAttacking()`/`EndHit()` at specific frames (see comments in `AttackingState.cs`/`HitstunState.cs`) - the same mechanism the shared `EarthMageAnimCTRL` clips already have baked in for the other three characters. Without those events, Yemoja's `SwordAndShieldAttack`/`SwordAndShieldSlash` will play visually but never call `PerformAttack()` (no damage, ever), and `HitReaction`/`StumbleBackwards` will never call `EndHit()` to exit hitstun/knockback via animation completion. Didn't attempt a fix myself - picking the correct impact/end frame per clip needs visual scrubbing in the Editor, not something to guess at blind. See new flagged entry below.

Left untouched as out of scope and clearly someone else's in-progress work (not described anywhere in this task): modified `Yemoja_*_mat.mat` materials and new `*_Graded.png`/`Detail_*.png` textures under `Assets/CharacterModels/Yemoja/` (looks like a separate color-grading/detail-texture pass, with matching before/after preview screenshots sitting in an untracked `_yemoja_preview/` folder), `Assets/Editor/BlenderBridge.cs` + `YemojaUVFixPostprocessor.cs`, and `BlenderTools/`. Playtest steps (mirror match control verification, full Yemoja move-set floor-sink check) still need manual in-Editor verification - not something I can drive from here. Committed on `task/fix-dual-control-and-yemoja-floor-sink`, not pushed.

---
### [!] Yemoja's new animation clips have no animation events - Attack/Slash won't deal damage, Hit/Stumble won't exit via animation completion
**Why:** Found while reviewing the dual-control/floor-sink fix above, not something that task described or asked for. Every clip under `Assets/Animations/YemojaAnimations/` imports with `events: []` in its `.fbx.meta` - confirmed on all 8, not just the attack ones. The other three characters' shared `EarthMageAnimCTRL` clips have `PerformAttack`/`StopAttacking`/`EndHit` events baked in at specific frames, which is how `AttackingState.PerformAttack()` (deals the actual hit) and the `OnAnimationComplete()` handlers on `AttackingState`/`HitstunState` (exit those states) get called at all - see the comments in `Assets/Scripts/PlayerCTRLs/States/AttackingState.cs` and `HitstunState.cs`. Without equivalent events on Yemoja's clips, her light/heavy attacks will animate but never register a hit, and her hit-reaction/knockback clips will never signal completion through this path.

This needs a design/authoring call, not a blind code fix: someone has to scrub each clip in the Editor to find the correct impact frame (for `SwordAndShieldAttack`/`SwordAndShieldSlash`) and end frame (for `HitReaction`/`StumbleBackwards`) and add the matching animation events, the same way the existing shared clips already do it. Flagging rather than guessing at frame numbers blind.

**Acceptance criteria:**
- `SwordAndShieldAttack.fbx` and `SwordAndShieldSlash.fbx` each get a `PerformAttack` event at their visual impact frame and a `StopAttacking` event at the clip's end.
- `HitReaction.fbx` and `StumbleBackwards.fbx` each get an `EndHit` event at the clip's end (confirm which of the two maps to hitstun vs. knockback in `YemojaAnimCTRL.controller` first - don't assume from name alone).
- Playtest: Yemoja's attacks land and deal damage; getting hit/knocked back correctly returns her to Idle rather than getting stuck.

**Relevant files:** `Assets/Animations/YemojaAnimations/SwordAndShieldAttack.fbx`, `SwordAndShieldSlash.fbx`, `HitReaction.fbx`, `StumbleBackwards.fbx`, `Assets/Animations/AnimCTRLs/YemojaAnimCTRL.controller`, `Assets/Scripts/PlayerCTRLs/States/AttackingState.cs`, `Assets/Scripts/PlayerCTRLs/States/HitstunState.cs`.

---
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
### [x] Delete `connectwebgl.zip` and gitignore it
**Why:** Confirmed dead weight, not a decision call — verified independently (not just taking the hypothesis on faith): no `.github/workflows` exist in this repo at all, so it can't be feeding a CI/deploy pipeline; `git grep` across every tracked file at HEAD for "connectwebgl" or "webgl_sharing" returns zero references anywhere; the actual documented WebGL build output already lives correctly under `WebGL Builds/` (a proper, complete Unity export — index.html, TemplateData/, Build/*.data.br); and the zip's last modifying commit ("Moved take damage function to late update") is completely unrelated to any build/deploy work, which is the fingerprint of a file getting swept up incidentally rather than maintained on purpose. This alone is a normal, reversible commit — no history rewrite involved, so it doesn't need to wait for the LFS decision below. Note: deleting it here does NOT shrink the repo — the ~213 MiB already spent across its 9 historical revisions stays in every clone until the history rewrite task below actually runs. This task only stops it from getting worse.

**Acceptance criteria:** `connectwebgl.zip` removed from the working tree and committed as a deletion. `.gitignore` updated with a `connectwebgl.zip` entry so it can't silently get re-added by an old build script or manual export. No other files reference it (already confirmed via repo-wide grep, but re-check if anything looks off).

**Relevant files:** `connectwebgl.zip` (root), `.gitignore`

**Done:** Independently re-verified Cowork's findings before deleting (no `.github/workflows`, `WebGL Builds/` is a complete real export, zip's last touching commit is unrelated to build/deploy). Removed `connectwebgl.zip` via `git rm` and added it to `.gitignore`. As Cowork noted, this doesn't shrink the repo - the ~213 MiB across its 9 historical revisions stays until the (blocked) history-rewrite task runs.

---
### [x] Extend `.gitattributes` LFS patterns to cover `.anim` (and consider `.controller`) before `task/enable-git-lfs` merges
**Why:** Cowork verified the pushed `.gitattributes` (branch `task/enable-git-lfs`, commit `84b63d6`) against the actual repo history and found two gaps. `.anim` isn't covered at all — 28 files, 150 MiB, averaging over 5 MiB each — genuinely large for Unity clips, likely dense imported/baked curve data. These are plain ASCII YAML text (`%YAML 1.1`, force-text serialization), not binary, confirmed by inspecting a sample directly - so this is a real tradeoff, not a pure bug: LFS-tracking them reclaims 150 MiB but turns them into opaque blobs for git (no line diffs, no partial merges). In practice a 5+ MiB text diff is already unreviewable in any PR view, so the diffability being traded away is mostly theoretical - leaning toward including it, but flag the tradeoff to Stephanie rather than deciding silently. `.controller` is also missing (22 files, 4.9 MiB) - real but small, lower priority either way.

Separately, Cowork tested (not assumed) whether the existing `*.fbx` pattern actually catches the uppercase `.FBX` files that make up 126 of the 147 MiB of FBX weight in history (`Assets/Animations/**/*.FBX` from imported animation packs). It does, on this repo, because `core.ignorecase = true` is set (standard for a Windows checkout) - verified by testing `git check-attr` with that config explicitly. This is correct as-is and doesn't need a code change, but it's a config-dependent behavior: if this repo were ever cloned somewhere with case-sensitive git defaults (e.g. a Linux CI runner), the uppercase `.FBX` files would silently stop going through LFS. Worth a one-line comment in `.gitattributes` noting this, so a future reader isn't confused about why there's no separate `*.FBX` pattern.

**Acceptance criteria:** `.gitattributes` on `task/enable-git-lfs` (or a follow-up branch, Claude Code's call) adds `*.anim filter=lfs diff=lfs merge=lfs -text` unless Stephanie decides the diffability tradeoff isn't worth it for a currently-small roster - flag it rather than deciding unilaterally. `*.controller` optional, lower priority. Add a short comment near the `*.fbx` line noting it also covers `.FBX` via `core.ignorecase`, and that this is Windows-checkout-dependent.

**Relevant files:** `.gitattributes`

**Done:** Independently re-verified before acting: on `task/enable-git-lfs`'s HEAD alone (single snapshot, not full history) there are 14 `.anim` files / 67.5 MiB and 8 `.controller` files / 4.7 MiB - smaller than Cowork's full-history-across-all-branches figures, as expected given the different scope, but the core finding held up: individual clips run up to 14.6 MiB (`MageIdle.anim`, `WPIdle.anim`), confirmed as plain-text YAML, genuinely large. Also independently confirmed `core.ignorecase=true` and that `*.fbx` does catch a real `.FBX` file via `git check-attr`. Added `*.anim` and `*.controller` LFS patterns plus the `.FBX`/`core.ignorecase` comment near `*.fbx`, on `task/enable-git-lfs` directly. Stephanie confirmed keeping `.anim`/`.controller` in LFS as-is.

---
### [x] Rewrite repo history: migrate binary assets to LFS + purge `connectwebgl.zip` history
**Do not run any part of this without Stephanie's explicit go-ahead first**, same as the CharacterSelect EventSystem fix. This rewrites every commit hash in the repo and requires a force-push — not a normal revertible commit.

**Why:** Claude AI (Cowork) independently measured the actual repo history (full clone, `git rev-list --objects --all` + `git cat-file --batch-check`, deduplicated by content hash) rather than estimating: 892.8 MiB of unique binary content exists across all history; 448.6 MiB of that is Unity asset types covered by the current LFS pattern list, plus the `.anim`/`.controller` gap above once resolved; 212.9 MiB is `connectwebgl.zip`'s 9 recommits (deletion already done above, but history purge is separate - see that task's note). Current packed `.git` is 460 MiB. GitHub's actual current LFS free tier (corrected from an earlier 1 GiB/1 GiB assumption — GitHub moved to a metered model) is 10 GiB storage + 10 GiB bandwidth/month on Free/Pro accounts, so migrating the assets uses under 5% of the monthly quota — comfortably covered, not a tight fit. Doing the LFS migration and the zip's history purge as one combined rewrite (rather than two separate disruptive events) should drop the packed repo from ~460 MiB to roughly 40-60 MiB.

**Preconditions before this can run:** the `.anim`/`.controller` gap task above resolved, `task/enable-git-lfs` and `task/remove-stale-webgl-zip` merged, and both older open branches (`task/input-system-eventsystem-migration`, `task/yemoja-roster-addition`) merged and deleted too, so `main` is the only branch left and there are no open PRs. No in-progress local worktrees or uncommitted changes at rewrite time.

**What the task involves once unblocked:**
1. `git lfs migrate import --everything --include="<final confirmed extension list from .gitattributes>"` to convert historical blobs to LFS pointers.
2. `git filter-repo --path connectwebgl.zip --invert-paths` to fully purge the zip's history (not converting it to LFS — it was deleted, not kept, so full removal is strictly better).
3. Force-push the rewritten `main` to origin.
4. Stephanie re-clones fresh locally rather than reconciling GitHub Desktop against rewritten history (Desktop doesn't handle force-pushed rewrites gracefully).
5. Note the rewrite in `CLAUDE.md` (date + resulting size) so a future session isn't confused by a `main` that looks like it diverged wildly from an old local copy.

**Relevant files:** whole-repo operation; `.gitattributes`, `CLAUDE.md` need updates as part of it.

**Done:** Executed with Stephanie's explicit go-ahead. Took a safety backup first (local bundle + `backup/pre-lfs-history-rewrite` pushed to origin, deliberately left untouched - scoped the migration to `main` only, not `--everything`'s default full ref set, so it wouldn't get rewritten too). `git lfs migrate import` needed `--everything` to actually walk commits in this git-lfs version (a bare ref positional arg resolved to 0 commits, seemingly a version quirk - `--everything` correctly processed all 54). Converted `.fbx`/`.FBX`/`.png`/`.tga`/`.jpg`/`.jpeg`/`.psd`/`.tiff`/`.exr`/`.hdr`/`.ttf`/`.otf`/`.wav`/`.mp3`/`.ogg`/`.mp4`/`.mov`/`.pdf`/`.anim`/`.controller` (confirmed no other case-variant extensions existed in history via a full scan). `git filter-repo` needed installing via pip (not present in this environment) and required re-adding the `origin` remote afterward (it removes it as a safety default). Verified thoroughly before force-pushing: identical file path sets, `git lfs fsck` clean, and a full content diff (CRLF-normalized) between the pre-rewrite backup and rewritten HEAD showed only the expected pointer conversions plus the `.gitattributes` update - nothing else touched. Force-pushed `main` with `--force-with-lease`. Fresh-clone packed size: ~460 MiB -> ~214 MiB (short of the 40-60 MiB projection because `WebGL Builds/` binary content and scene/prefab YAML weren't in scope for this pass - worth a future task if that's ever worth chasing further). `CLAUDE.md` updated with the rewrite date, resulting size, and a pointer to the backup branch.

---
### [x] Delete unreferenced files inside `Assets/CharacterModels/AllStarCharacterLibrary/`
**Why:** Cowork cross-referenced every GUID in this 251-file pack against the rest of the repo (prefabs, scenes, controllers, materials, scripts) and found only 4 files are actually load-bearing: `Ninjas.FBX` (Ninja's model), `A03.FBX` (WarriorPrincess's model), and `BaseFemale.fbx` (the root-motion animation source behind the 5 `MaterialLocation.External is obsolete` console errors — Ninja and WarriorPrincess are the only two characters that touch this pack at all; EarthMage, Yemoja, and Frank_Mage are untouched). The other 247 files — `CollisionHelpers/`, `StatusBars/`, most of `Models/Materials/`, unused character variants under `Models/Characters/` — are pure dead weight from what looks like a wholesale asset-pack import where only a couple of pieces ever got used.

This is independent of the Ninja/WarriorPrincess Mixamo replacement (see the blocked task below) — it's safe and worth doing regardless of that timeline, since it doesn't touch anything currently in use.

**Do not delete `Ninjas.FBX`, `A03.FBX`, or `BaseFemale.fbx` (or their `.meta` files) in this task** — they're still load-bearing until the replacement task below actually lands. Before deleting anything else, compute the full transitive closure: some of the 247 "unused" files may themselves be referenced *by* one of these 3 kept files (a material or texture the FBX importer pulls in) even though nothing outside the pack points to them directly. Trace GUIDs one more level in before finalizing the delete list, and confirm zero new console errors after a full reimport before committing.

**Acceptance criteria:** Everything under `AllStarCharacterLibrary/` deleted except `Ninjas.FBX`, `A03.FBX`, `BaseFemale.fbx`, and anything those three genuinely depend on (verified by GUID trace, not assumption). Fresh reimport in the Editor shows no new missing-reference errors for Ninja or WarriorPrincess. Committed as a normal reversible commit (no history rewrite needed — this content is already properly LFS-tracked going forward).

**Relevant files:** `Assets/CharacterModels/AllStarCharacterLibrary/`

**Done:** Independently traced the dependency closure by GUID rather than trusting file names: pulled the `guid:` out of every `.meta` in the pack (135 files), then `git grep`'d each one against the rest of the repo. Confirmed exactly 3 GUIDs are referenced from outside the pack — `Ninjas.FBX` and `A03.FBX` (from `Ninja.prefab`/`WarriorPrincess.prefab` and their `DisplayModels` counterparts) and `BaseFemale.fbx` (from `WarriorPrincess.prefab` only) — matching Cowork's finding exactly. For the "one level in" transitive closure: since these are old-format FBX imports using `materialSearch: 1` (recursive-up) + `materialName: 0` (by base texture name) rather than explicit external-object remaps, name-matching alone wasn't reliable, so I extracted the actual embedded texture-path strings from each FBX binary (`Ninja.png`/`SuperLowNinja.tga` for Ninjas.FBX; `A03.tga`/`A03N.tga`/`A03_LOD.tga` for A03.FBX) and cross-checked each candidate `.mat`'s `_MainTex`/`_BumpMap` GUID against the matching texture's own GUID to confirm the link, not just assume it from filename. `BaseFemale.fbx` has no embedded texture reference at all — it's the bare skeleton/root-motion source, consistent with it being the one throwing the `MaterialLocation.External is obsolete` warning (`materialLocation: 0` in its `.meta`, the legacy mode Unity flags).

Deleted everything outside that closure: `CollisionHelpers/`, `StatusBars/`, `Materials/` (Effects, Marketing, SexySidekicks, Weapons) in full, `RootAnimsMale/` in full, the orphaned `A06.mat` (blank material, `fileID: 0` texture, not touched by A03.FBX), and everything in `RootAnimsFemale/` except `BaseFemale.fbx` itself (the other `BaseFemale@*.fbx` animation clips, both avatar masks, `FemalePlayer.controller`, and `Marina@SpinningAttackLunge.FBX` were all part of the 132 GUIDs with zero outside references). 220 files deleted, 23 kept (the 3 load-bearing FBX + their true material/texture dependencies). Re-verified after deleting, using `git show HEAD:<meta>` to recover the deleted GUIDs, that none of them still appear in `Ninja.prefab`, `WarriorPrincess.prefab`, or either `DisplayModels` prefab — zero dangling references. Fresh reimport + playtest for missing-reference errors still needs manual in-Editor verification. Committed on `task/cleanup-allstar-library`, not pushed.

---
### [!] BLOCKED — Replace Ninja and WarriorPrincess's source models with new Mixamo-sourced characters
**Waiting on Stephanie to source the new models from Mixamo herself** (visual/creative selection, not something to automate) before this can start. Once she has Humanoid-rigged FBX files downloaded and staged, this follows essentially the same recipe as the Yemoja playable-prefab task above, which already proved the pattern works in this codebase.

**Why:** Stephanie decided this is a full visual replacement, not just a new animation source on the current look — both characters get new models entirely. The scope is smaller than a from-scratch rig job, though: every character in the current roster (including Yemoja, added via this exact recipe) shares one Animator Controller, `EarthMageAnimCTRL`, retargeted through Unity's Humanoid avatar system. As long as the new models import with a valid Humanoid avatar and the core bones map cleanly, they should retarget onto that same controller directly — no new controller, no re-authoring state transitions, and the existing `PerformAttack`/`StopAttacking`/`EndHit` animation events carry over for free, same as they did for Yemoja. Only build a bespoke controller if retargeting visibly breaks on one of these specific models.

Values to preserve from the current prefabs, captured before anything changes: both `Ninja.prefab` and `WarriorPrincess.prefab` currently carry `playerMaxHealth/playerHealth: 100`, `knockbackForce: 20`, `blockDamageMultiplier: 0.2`, `playerJumpForce: 35`, `moveSpeed: 6`, `attackRange: 2.5`. These live on `PlayerManager`/`CharacterPhysics` component fields, not on the model — carry them into the rebuilt prefab rather than starting from a fresh default.

**Acceptance criteria:**
- Both new models imported with a valid Humanoid avatar (all core bones mapped), matching Yemoja's approach.
- Retarget onto `EarthMageAnimCTRL` first; only author a new controller if that visibly fails for one of these models.
- Prefabs rebuilt with the same component set as the existing roster (`Animator, Rigidbody, BoxCollider, PlayerController, PlayerAutoPilot, PlayerManager, PlayerStateMachine, CharacterPhysics, AttackCTRL`), same Rigidbody constraints (mass 2, gravity on, rotation + Z-position frozen at 120), individually-sized BoxCollider (not copied from another character).
- Tuned gameplay values above carried over, then re-tuned by playtest for the new models' actual proportions (attack range and hitbox feel are geometry-dependent and will likely need adjustment even if the base numbers carry over as a starting point).
- Root scale computed the same way as the rest of the roster: source FBX height × prefab root scale should land in the existing ~3.4-3.5m in-game height band.
- `LoadCharacter.charPrefabs` and both `PlayerSelection.characters`/`opponents` arrays updated at the correct indices (these are index-aligned only by convention across two separate files — verify by GUID, not just visually, per the note on the Yemoja task).
- Playtest: select each character, confirm idle/walk/jump/attack/block/hitstun look correct with no clipping or collapsed joints, and the console stays clean.
- Once both are confirmed working end-to-end, delete `Ninjas.FBX`/`A03.FBX`/`BaseFemale.fbx` and whatever else survived the cleanup task above — `AllStarCharacterLibrary/` should end up fully empty and removable.

**Relevant files:** `Assets/Prefabs/Characters/PlayerPrefabs/Ninja.prefab`, `Assets/Prefabs/Characters/PlayerPrefabs/WarriorPrincess.prefab`, their `DisplayModels/` counterparts, `Assets/Scripts/GameManager/LoadCharacter.cs`, `Assets/Scripts/PlayerSelection.cs`, `Assets/Animations/AnimCTRLs/EarthMageAnimCTRL.controller`, `CharacterSelect.unity` / `FightScene.unity`.

---
### [x] Remove now-redundant Yemoja UV import-time patch + relocate stray pre-fix backup FBX out of Assets/
**Why:** The Blender-connector chat (Stephanie's separate modelling session, working directly in a live Blender via the official Anthropic connector) merged Yemoja's two UV layers into a single complete UV0 at the source - fixing the flat-clothes-colour bug in the model itself rather than patching it at import. Verified this after a forced reimport of `Assets/CharacterModels/Yemoja/models/Yemoja.fbx`: both meshes (`Yemoja`, `Hair`) now show 0% zero-area UV0 across every vertex (was ~81% before, per the original diagnosis), and neither carries a UV2 channel anymore. That means `Assets/Editor/YemojaUVFixPostprocessor.cs`'s repair condition (UV0 degenerate AND UV2 usable) can never fire again on the live asset - confirmed empirically, not just by reading the code: the only `[YemojaUVFix]` console log produced during this verification pass was against `Yemoja_preUVfix_backup.fbx` (the pre-fix backup, which still carries the old broken data), never against the live `Yemoja.fbx`.

Separately: that backup file was delivered inside `Assets/CharacterModels/Yemoja/models/` (same folder as the live FBX), which means Unity is auto-importing it as its own live, duplicate asset - generating a separate avatar/materials and getting reimported/repaired by the postprocessor on every domain reload, which is exactly what produced the stray console log above. It should not live inside `Assets/`.

**Acceptance criteria:**
- Delete `Assets/Editor/YemojaUVFixPostprocessor.cs` (confirmed redundant, see above). A fresh reimport of `Yemoja.fbx` afterward should be clean - no `[YemojaUVFix]` log, no visual regression on the clothes.
- Move `Yemoja_preUVfix_backup.fbx` out of `Assets/` entirely into a repo-root `Backups/` folder (new convention, same idea as the existing `BlenderTools/` - stays outside Unity's import scope). Stephanie confirmed (2026-07-28): keep pre-fix backups like this in `Backups/` going forward as standing practice, prune it by hand occasionally once it grows large rather than deleting on a schedule.
- Confirm `Yemoja.prefab`'s material/renderer references still resolve after both changes (`Yemoja_Body_mat`, `Yemoja_Clothes_mat`, `Yemoja_Hair_mat`, `Yemoja_Trident_mat`, `Animator.avatar = YemojaAvatar`) - already spot-checked live this session (all resolved, GUID of `Yemoja.fbx` unchanged across the reimport), re-verify after the actual commit.
- Playtest: Yemoja's clothes should show real surface detail (base colour/normal/etc.) instead of the flat single-texel colour, with the postprocessor gone.

**Suggested commit message** (from the modeller chat that fixed the source data): "Remove/simplify UV0 import patch now that Yemoja's UV layers are fixed at the source" - adapt if it doesn't cover the backup-file relocation too, since that's a second, unrelated change bundled into this same task.

**Relevant files:** `Assets/Editor/YemojaUVFixPostprocessor.cs`, `Assets/CharacterModels/Yemoja/models/Yemoja.fbx`, `Assets/CharacterModels/Yemoja/models/Yemoja_preUVfix_backup.fbx`, `claude/workflow-setup.md` (project doc - the Blender-connector pipeline is documented there).

**Done:** Deleted `Assets/Editor/YemojaUVFixPostprocessor.cs` (+`.meta`) outright rather than leaving it as an inert safeguard - re-checked its guard logic directly: it only ever rewrites a vertex when `uv2.Length == uv0.Length` and that vertex's UV0 is degenerate/UV2 isn't, so once UV2 is genuinely gone (as Cowork verified via forced reimport - 0% zero-area UV0, no UV2 channel on either mesh) the length check alone makes it a permanent no-op. Moved `Yemoja_preUVfix_backup.fbx` to a new repo-root `Backups/` folder and dropped its `.meta` (meaningless outside `Assets/`/`Packages/` - Unity never reads it there), rather than keeping it alongside the file. Committed only the geometry fix (`Yemoja.fbx`) plus the postprocessor deletion and backup relocation - the four `Yemoja_*_mat.mat` changes and new `*_Graded`/`Detail_*` textures sitting in the same working tree are a separate color-grading/detail-texture pass this task's acceptance criteria never mentioned (the "real surface detail" playtest criterion is about the UV geometry fix unlocking her *existing* texture assignments, not about new textures), so left those uncommitted, along with `Assets/Editor/BlenderBridge.cs` and `BlenderTools/` (unrelated tooling, no task covers them yet). Confirmed the relocated FBX still resolves through Git LFS (`git check-attr filter` -> `lfs`) since `.gitattributes` matches by extension, not path. Prefab material/renderer reference re-verification and the clothes-detail playtest still need manual in-Editor confirmation. Committed on `task/yemoja-uv-fix-cleanup`, not pushed.

---
### [x] SUPERSEDED — Yemoja rig-naming convention change + mesh rebuild, pending FBX export
**Superseded 2026-08-08** by the Yemoja_v2 full-replacement import below - the FBX landed and Cowork did the whole re-import live. Kept for context only; do not action this entry, action the one below.

**ORIGINAL ENTRY FOLLOWS**

**Waiting on the Blender-connector chat to actually export the new FBX** - as of this task being filed, `Assets/CharacterModels/Yemoja/models/Yemoja.fbx` is still the session-one version (UV fix, original bust sculpt, Mixamo-style bone names). Nothing below is actionable yet; this is a heads-up so the re-import work is planned before the file lands, not a live bug.

**Why:** Two changes landed in the same Blender session, reported by Stephanie's modeller chat (2026-07-29):

1. **Bone naming convention changed project-wide**, not just for Yemoja: Mixamo-style `mixamorig:LeftArm` / `mixamorig:RightHand` becomes Blender-style `mixamorig:Arm.L` / `mixamorig:Hand.R` (70 bones renamed; centre bones - Hips, Spine, Neck, Head - unchanged). Reason: Mixamo naming breaks Blender's mirror-aware tools - running Mesh > Symmetrize on Mixamo-named vertex groups copies them verbatim instead of flipping .L/.R, which put Yemoja's entire left half (10,158 verts) onto the wrong-side bones. Looked fine at rest, tore apart the moment she was posed. Verified on the Blender side before export: every sided bone has a counterpart, bpy.utils.flip_name resolves all pairs, no orphan vertex groups, weights sum to 1.0. This is now a standing rule for every future rigged import, enforced via `BlenderTools/normalize_rig_naming.py` (rationale in `BlenderTools/README_rig_conventions.md`) - the modeller asked for this rule to be added to `CLAUDE.md` so Claude Code picks it up automatically each session; see acceptance criteria below.
2. **Mesh rebuilt**: vertex count 29,198 -> 27,695 (Symmetrize replaced one half with the other's true mirror), body hand-resculpted (bust, collarbone, shoulders), now perfectly mirror-symmetric (topology parity 0 at every height). UV layout changed too - left/right halves now share UV space rather than separate regions, so the body texture will read mirrored. This is an accepted trade-off from the modeller, not a bug - do not fix it on the Unity side. Confirmed clean: 0 degenerate UVs, 0 unweighted vertices, 2 non-manifold edges (noted by the modeller, not flagged as blocking).

**What to do once the new FBX actually lands:**
- Reimport `Yemoja.fbx`. Unity's Humanoid mapper should handle the .L/.R suffix convention automatically, but open Rig > Configure (or the equivalent inspection) and confirm every bone actually mapped before trusting it - don't just check avatar.isHuman == true and stop there. This project already hit a case this session where an avatar reported isValid = true with a fully-populated humanDescription.human[] list yet still failed real humanoid mapping underneath (root cause was stale/corrupted importer metadata, not a naming problem, fixed by resetting animation type to None and back to Human) - so verify from a clean Editor-mode reimport, never while a Play Mode clone is loaded, and don't trust isValid or a populated bone list alone as proof it's genuinely working.
- The 8 clips under `Assets/Animations/YemojaAnimations/` use avatarSetup = CreateFromThisModel and retarget through Humanoid avatar slots (Hips, LeftUpperArm, etc.) rather than raw bone names, so they should carry over onto the renamed rig without changes - confirm this rather than assuming it.
- Re-parent the trident from `mixamorig:RightHand` to `mixamorig:Hand.R` in `Yemoja.prefab`. Per the modeller: this is a straight re-parent, not a re-solve - grip position (18% up the shaft) and clearance (0.315 m) are unchanged, don't re-run that sweep.
- Re-parent all 11 H_*/A_* hitbox colliders to their renamed bone equivalents. A_Trident is highest priority - AttackCTRL.Attack() reads attackColliders[0] with no null guard, so if it's left pointing at a bone that no longer exists, expect a hard error the moment she attacks, not a silent miss.
- Re-verify UV0 is still clean post-reimport the same way it was checked for the previous UV fix (0% zero-area across every vertex, no stray UV2) - the modeller's own numbers say it should be, but this is a materially different export (different vertex count, rebuilt mesh), worth confirming rather than assuming the old verification still applies.
- Add the modeller's rig-naming rule to `CLAUDE.md` (Claude Code's job, per the modeller's own request - quote her reasoning about Symmetrize/bpy.utils.flip_name rather than paraphrasing, so a future reader understands why the convention exists, not just that it does).

**Acceptance criteria:**
- New `Yemoja.fbx` imported, Humanoid avatar confirmed genuinely valid via Rig > Configure inspection (not just isValid), from a clean Editor-mode reimport.
- All 8 animation clips still play correctly retargeted (spot-check at least Idle and one attack).
- Trident re-parented to `mixamorig:Hand.R`, same local position/rotation/scale as before (no re-solve).
- All 11 hitbox colliders re-parented to their renamed bone equivalents; A_Trident specifically confirmed non-null on attackColliders[0].
- `CLAUDE.md` updated with the rig-naming-convention rule, in Stephanie's/the modeller's own reasoning.
- Playtest: idle/walk/jump/attack/heavy attack/block/hit/knockback all still look correct on the renamed rig, no missing-bone errors in console.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja.fbx`, `Assets/Prefabs/Characters/PlayerPrefabs/Yemoja.prefab`, `Assets/Animations/YemojaAnimations/*.fbx` (all 8), `Assets/Animations/AnimCTRLs/YemojaAnimCTRL.controller`, `CLAUDE.md`, `claude/yemoja-character-setup.md` (project doc).

---

## [FUTURE / NOT NOW] Character detail screen with orbit-able high-detail models (LOD pipeline)

**Status:** Deferred direction, not a current task. Captured now so it isn't lost — do NOT start this until the core game is done and Stephanie explicitly greenlights a polish/cleanup phase. Filed from the Blender (modeller) chat on 2026-08-01.

**Why:**
Stephanie asked how mobile fighting/gacha games (Genshin, Honkai, Diablo Immortal, etc.) show those high-fidelity, orbit-able character-detail screens when gameplay models have to be cut down so hard for mobile performance. The answer — and the direction we'd take here eventually:

- It is NOT a pre-rendered video. Video can't be orbited/zoomed/rotated by the player, which defeats the point of a detail screen. It's real-time 3D.
- The trick is the *budget*, not a different technique: in a fight the phone renders TWO characters + stage + FX + UI + logic at 60fps, so each character must be lean. On a character-detail screen there's ONE character and almost nothing else, so the entire freed-up rendering budget pours into that single model — allowing a higher-poly mesh, higher-res textures, nicer lighting/shader, and the expensive extras we skip mid-fight (subsurface-scattering skin, cloth physics, hair jiggle).
- Implementation is a standard **LOD (level-of-detail) chain**, and it ties directly into the existing "AAA base" plan (`C:\Users\steph\Documents\3D Model Backup (AAA quality base)\README_AAA_BASE.txt`): keep a high-poly master (the future AAA copy) → bake its detail into normal maps → generate LOD0 (highest, for the detail screen / close-ups), LOD1/LOD2 (leaner, for gameplay where the character is smaller on screen). The engine swaps LODs by screen-space size automatically.
- So the mobile Yemoja we just finished is effectively LOD1/LOD2; the future AAA copy becomes LOD0. They are the same character at different fidelities, not unrelated assets.
- Player orbit/zoom/rotate on the detail screen is cheap and standard on mobile when it's the only thing rendering — fully doable, no video trickery.

**Scope reality:** this is genuine extra work (author the high-poly master, set up the LOD import chain in Unity, build the detail-screen UI with an orbit camera + turntable). Achievable but deliberately scheduled, not free. Belongs in a later polish phase.

**Acceptance criteria (when eventually taken up):**
- A high-detail LOD0 exists for at least one character (Yemoja first, as the reference).
- Unity LODGroup configured on the character prefab(s) with LOD0/LOD1/LOD2 swapping by screen size; gameplay still uses the lean LODs and holds 60fps with two fighters on screen.
- A character-detail screen renders LOD0 alone with an interactive orbit camera (drag to rotate, pinch to zoom), staying within mobile frame budget.
- The mobile-vs-AAA relationship documented so LOD0 is understood as the re-optimized-upward AAA copy, not a separate lineage.

**Relevant files (future):** character prefabs under `Assets/Prefabs/Characters/`, a new detail-screen scene/UI (TBD), the AAA base README noted above, `claude/` project docs. No files to touch now.

---
### [x] Review + commit the Yemoja_v2 full replacement (done live by Cowork), then remove the superseded old model
**Why:** The modeller shipped `BlenderTools\_export\Yemoja_v2.fbx` (manifest `BlenderTools\yemoja_manifest.json`, schema v14) covering five sessions of work Unity had never seen: eye socket rebuild, rebuilt lash cards, upper + lower eyeliner, cornea/gaze correction, project-wide rig rename to Blender convention, mirror-symmetric body rebuild, new skin base colour, rebuilt hair material. Stephanie authorised replacing the existing Yemoja entirely rather than reconciling. Cowork did the import and rebuild live through the Unity connection; this entry is the review-and-commit pass plus the leftover deletions.

**What Cowork already did (verify, do not redo):**
- Imported to `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx`. Scale set from MEASURED bounds, not an assumed factor: at `useFileScale=true, globalScale=1` the model measured 7.50438 units (matching the manifest's stated 7.5044), so `globalScale = 1.8 / 7.50438 = 0.23985990`. Re-measured after reimport: exactly 1.80000 source height, 3.4500 at the prefab's 1.91667 root scale (roster band 3.4-3.5). `useFileScale` deliberately left TRUE - flipping it also drops the cm/m factor (the documented 100x trap).
- Rig set to Humanoid / CreateFromThisModel. `isHuman = true`, no Rig Errors.
- **Fixed two real faults in Unity's humanoid auto-mapper** (exactly what the modeller's "open Rig > Configure and confirm every bone maps" warning was for): (a) all 15 LEFT-hand finger bones were left unmapped while the right hand mapped fine - the `.L` suffix defeated the auto-mapper's heuristics; (b) the `LeftEye`, `RightEye` and `Jaw` humanoid slots were mapped to HAIR bones (`hair_loc16_0`, `hair_loc11_0`, `hair_loc10_0`), which is nonsense - Yemoja has no eye or jaw bones. Removed the three bogus mappings and added the 15 left-finger mappings explicitly. Mapped slot count now 52, matching the previous rig exactly. Verified functionally, not just by count: sampling the idle clip rotates `mixamorig:HandIndex2.L` by 63.8 degrees, which it could not do before the fix.
- All 6 materials wired per the manifest `material_map`, and the FBX's material slots resolve to them by name (`materialName = BasedOnMaterialName`, `materialSearch = Everywhere`). 8 submesh draw calls, matching the manifest. Hair is flat base colour + normal map ONLY - `Yemoja_Hair_BaseColor.png`, `Yemoja_Hair_Roughness.png` and `YemojaHair_MetallicSmoothness.png` are deliberately UNASSIGNED (manifest rules 67-69: they are flat-fill blotch maps and are the cause of the grey patches). Do not "helpfully" reassign them.
- Fixed `Yemoja_Hair_Normal.png`, which was importing as a Default sRGB texture rather than a Normal Map.
- Rebuilt both `Assets/Prefabs/Characters/PlayerPrefabs/Yemoja.prefab` and `Assets/Prefabs/Characters/DisplayModels/YemojaDisplay.prefab` over their existing paths, so both GUIDs are preserved and `LoadCharacter.charPrefabs` / `PlayerSelection.characters[]`/`opponents[]` stayed wired untouched at index 3 (verified by GUID against both scene files). No scene edits were needed at all.
- Trident and all 11 `H_*`/`A_*` hitbox colliders re-parented to the renamed bones, with collider sizes and local transforms copied verbatim from the pre-replacement prefab (balance-relevant, deliberately not re-derived). `attackColliders[0]` = `A_Trident`, non-null.

**FOUND WHILE DOING THIS - a real pre-existing bug, now fixed as a side effect:** on the OLD prefab every hitbox and the trident had silently become detached from the skeleton and were sitting on the prefab ROOT, not on bones. Confirmed from the prefab YAML (their `m_Father` entries pointed at six distinct stripped Transform references into the old FBX) and by control test - `WarriorPrincess.prefab` and `EarthMage.prefab` resolve their markers to real bones (`L Calf`, `Spine`, `bn_Head` etc.) under the identical inspection, Yemoja resolved all of hers to root. Cause is almost certainly the earlier in-place FBX re-export plus rig reset regenerating the model's internal transform fileIDs, which broke the stripped parent references; Unity fell back to root-parenting silently rather than erroring. Practical effect while it was broken: the trident did not follow her hand and no hitbox tracked its limb. The rebuild fixes it (12/12 markers now resolve to real bones), but it is worth knowing this failure mode is silent - **if a character's FBX is ever re-exported in place again, re-verify marker parenting afterwards.**

**Two judgement calls Cowork made that are easy to reverse if you disagree:**
1. Kept the uncommitted colour-grading pass's detail normal maps (`Detail_SkinPores_Normal` on skin, `Detail_LeatherGrain_Normal` on clothes) - purely additive and not mentioned either way in the manifest.
2. Followed the manifest's `_Smoothness` spec (1.0 on both skin and clothes) rather than the 0.72 / 0.62 values from that same grading pass, since those were tuned by eye against the OLD base colour maps and the base colours have both changed. Previous values recorded here so they can be restored in one edit if the new look reads worse.

**Acceptance criteria:**
- Review the diff. New files: `Yemoja_v2.fbx` (+meta), `Yemoja_Eye_mat.mat`, `Yemoja_Lashes_mat.mat`, `Yemoja_Eyeliner_mat.mat`. Modified: the other three Yemoja materials, both prefabs, `Yemoja_Hair_Normal.png.meta`.
- Delete the superseded `Assets/CharacterModels/Yemoja/models/Yemoja.fbx` (+meta) and the now-orphaned raw model prefab `Assets/CharacterModels/Yemoja/prefabs/Yemoja.prefab` (+meta) - verified by GUID scan that this raw prefab is the ONLY remaining referrer of the old FBX, and nothing at all references that raw prefab. Optionally move the old FBX to `Backups/` instead of deleting, per the convention set on 2026-07-28.
- Consider renaming `Yemoja_v2.fbx` to `Yemoja.fbx` once the old one is gone. A Unity rename preserves the GUID so both prefabs survive it - but do it as its own commit, and re-verify avatar `isHuman` afterwards.
- Playtest: select Yemoja, confirm she animates (idle/walk/jump/attack/heavy/block/hit/knockback), the trident stays in her right hand, hitboxes register, and the console stays clean.

**Not fixed, flagged for a judgement call:**
- Root `BoxCollider` (size 0.511877 x 1.686132 x 0.335375, centre Y 0.833587) and all hitbox sizes were carried over verbatim from the old prefab, but they were authored against the OLD body proportions and the body has since been resculpted and made mirror-symmetric. They are consistent, not necessarily correct. Worth an eyeball pass in the Scene view.
- 6 polygons of the `Hair` mesh were discarded by Unity's importer as self-intersecting (console warnings on import). The manifest already lists "1 non-manifold hair edge" as a known open item on the Blender side; worth telling the modeller the Unity-side count is 6 discarded polys, in case it leaves visible holes.
- `CharacterPhysics.groundLayerMask` is 0 (nothing) on Yemoja, carried over verbatim. That looks wrong but it is pre-existing and matches what was there before, so it was not silently "fixed" - check against the other characters.

**Relevant files:** `Assets/CharacterModels/Yemoja/`, `Assets/Prefabs/Characters/PlayerPrefabs/Yemoja.prefab`, `Assets/Prefabs/Characters/DisplayModels/YemojaDisplay.prefab`, `BlenderTools/yemoja_manifest.json`, `claude/yemoja-character-setup.md`.

**Done:** Independently re-verified Cowork's claims by GUID rather than trusting the write-up: `Yemoja.prefab`'s `m_SourcePrefab` resolves to `Yemoja_v2.fbx`'s guid; all 6 material GUIDs in the FBX importer's `externalObjects` remap match the 6 `.mat` files exactly (Body/Clothes/Eye/Eyeliner/Hair/Lashes); `humanDescription.human` has exactly 52 `boneName` entries with `mixamorig:HandIndex2.L` present and no Eye/Jaw/hair-bone mappings, matching the claimed auto-mapper fix; `AttackCTRL.attackColliders[0]` resolves to `A_Trident`, whose parent chain (through the nested `Yemoja_Trident.prefab`) roots at a transform whose `m_CorrespondingSourceObject` guid is `Yemoja_v2.fbx`'s - i.e. a real bone on the new skeleton, not the prefab root; `Yemoja_Hair_mat` has only `_BumpMap` assigned (BaseMap/MetallicGlossMap both `fileID: 0`), matching the "flat colour + normal map only" claim; `Yemoja_Hair_Normal.png` imports with `textureType: 1` (Normal Map), `sRGBTexture: 0`. `LoadCharacter.cs`/`PlayerSelection.cs` confirmed genuinely untouched (empty diff) - the GUID-preservation claim holds.

Scoped the commit by tracing which texture files are actually load-bearing dependencies of the modified/new materials (via GUID, not filename guessing) rather than committing the entire messy working tree: committed `Yemoja_v2.fbx`(+meta), the 3 new materials, the 4 modified materials, both prefabs, and the 9 textures those materials actually reference (including `Detail_SkinPores_Normal`/`Detail_LeatherGrain_Normal`/`Clothes_BaseColor_Ocean2`/`lash_strand_white4`, which are new and load-bearing, not incidental). Left uncommitted, confirmed unreferenced by any current material: `Clothes_BaseColor_Graded.png`, `Clothes_BaseColor_Ocean.png` (the non-"2" version), `Yemoja_Color_Graded.png`, `Yemoja_Color_Makeup_CatEyeBlack.png`, `Yemoja_Color_SkinGrade_v1.png`, `Yemoja_Makeup_CatEye_Black.png` - superseded iteration files, same as the out-of-scope grading-pass files flagged in the earlier UV-fix task. Also left uncommitted, unrelated to this task: `BlenderTools/` (scripts, previews, manifests), `Backups/*.blend`(+previews), `Assets/Editor/BlenderBridge.cs`(+`Assets/Editor.meta`), `YEMOJA_PRODUCTION_STATUS.md`, `HAIR_TEXTURE_STATUS.txt`, and the `Elementals-Fight.slnx` addition of `Assembly-CSharp-Editor.csproj` (tied to `BlenderBridge.cs` existing on disk, not to this task).

**New finding, flagging rather than folding in blind:** `Assets/Animations/YemojaAnimations/SwordAndShieldIdle.fbx.meta` and `SwordAndShieldWalk.fbx.meta` are sitting modified in the working tree (`loopTime: 0` -> `1`, plus a previously-empty `humanDescription.human`/`skeleton` now populated) - looks like a real, sensible fix (only the two clips that should actually loop, Idle and Walk, got `loopTime` enabled; the one-shot clips are untouched), but it's not mentioned anywhere in this task's scope or Cowork's "already did" list, and I can't tell from source alone whether it's finished or mid-edit. Left uncommitted rather than guessing.

Deleted the superseded old model in a second commit after independently GUID-scanning for referrers (not trusting the write-up): the old `Yemoja.fbx`'s guid was referenced only by the raw model prefab at `Assets/CharacterModels/Yemoja/prefabs/Yemoja.prefab`, and that raw prefab's own guid had zero referrers anywhere in the repo - confirmed dead weight even before the replacement. Also picked up `Backups/Yemoja_preUVfix_backup.fbx`, already manually pruned from disk (per the standing "prune by hand" convention) but not yet committed.

Did not do the optional `Yemoja_v2.fbx` -> `Yemoja.fbx` rename - its own acceptance criteria calls for re-verifying avatar `isHuman` afterward, which needs an Editor reimport I can't drive headless; leaving it for a follow-up in-Editor pass rather than doing it blind. The "Not fixed, flagged for a judgement call" items (collider sizing, 6 discarded hair polys, `groundLayerMask`) are Cowork's own open items, not re-verified further here since they're explicitly flagged as needing an eyeball/Editor pass, not a source-level check. Playtest step (idle/walk/jump/attack/heavy/block/hit/knockback, trident+hitbox tracking, console clean) still needs manual in-Editor verification - not something I can drive from here. Committed on `task/yemoja-v2-replacement` (two commits: asset rebuild, then the deletion), not pushed.

---
### [x] Ninja has no attack colliders at all - `AttackCTRL.Attack()` will throw on his first attack
**Why:** Found incidentally while auditing Yemoja's hitbox rig. `Ninja.prefab` has `attackColliders` **size 0** and exactly ONE collider in its whole hierarchy (the root body collider) - no `H_*`/`A_*` markers whatsoever. `AttackCTRL.Attack()` reads `attackColliders[0]` with no null or length guard, so this is an `IndexOutOfRangeException` the moment Ninja throws a punch, not a silent miss. By contrast EarthMage, WarriorPrincess and now Yemoja all carry a full marker set.

Not fixed here because it needs a design call, not a blind patch: Ninja is also the subject of the BLOCKED full-model-replacement task further down, so building him a hitbox rig now may be throwaway work. Two sensible options: (a) add a defensive length check in `AttackCTRL.Attack()` so a missing rig degrades to "deals no damage" instead of throwing - cheap and worth doing regardless; (b) build Ninja a marker set mirroring WarriorPrincess's, which is wasted if his model is replaced soon. Recommend (a) now, (b) as part of the replacement.

**Acceptance criteria:** Ninja can be selected and can attack without throwing. If the guard route is taken, the log should make it obvious the character is missing its hitbox rig rather than failing silently.

**Relevant files:** `Assets/Scripts/PlayerCTRLs/AttackCTRL.cs`, `Assets/Prefabs/Characters/PlayerPrefabs/Ninja.prefab`.

**Done:** Took the recommended option (a): `Attack()` now returns early with a `Debug.LogError` naming the offending GameObject when `attackColliders` is null or empty, instead of indexing into it unguarded. Left Ninja's prefab itself untouched - building him a real `H_*`/`A_*` marker set is still explicitly deferred to the blocked full-model-replacement task, since it'd be throwaway work otherwise. This only stops the crash; Ninja's attacks still deal no damage until that replacement lands, which is the intended interim behavior per the task's own recommendation.

---
### [x] Shared EarthMageAnimCTRL Walk state was accidentally repointed at Yemoja's clip
**Why:** Found by Claude Code while reviewing the accumulated Yemoja v3-v6 import work below, not part of any task's described scope. `EarthMageAnimCTRL.controller` is the Animator Controller shared by EarthMage, Ninja, and WarriorPrincess (Yemoja has her own dedicated `YemojaAnimCTRL`, added back on the original playable-prefab task). Diffing the controller's actual LFS-smudged content against HEAD (not just the LFS pointer, which is all `git diff` shows by default) turned up a real, substantive change buried inside what looked like routine resave noise: the Walk state's `m_Motion` had been repointed from `MageWalk.anim` to Yemoja's `SwordAndShieldWalk.fbx` clip. Almost certainly an accidental drag in the Animator window during a Yemoja editing session - nothing in this session's task history describes touching the shared controller, and every other line in the file matched the pre-edit content exactly.

Left as committed, every character sharing that controller (not just Yemoja) would have played Yemoja's sword-and-shield walk animation instead of their own the instant they walked.

**Acceptance criteria:** `EarthMageAnimCTRL.controller`'s Walk state Motion points back at `MageWalk.anim`; nothing else in the controller changes.

**Relevant files:** `Assets/Animations/AnimCTRLs/EarthMageAnimCTRL.controller`.

**Done (2026-09-03):** Reverted the Motion reference. Verified against the pre-edit content by fetching the historical LFS object directly (`git show HEAD:<path> | git lfs smudge`, since the local LFS cache didn't have that historical blob checked out) and diffing it against the corrected working copy - identical except for a handful of trailing-whitespace-only bytes on unrelated empty YAML fields, which don't affect parsing. Branch `task/fix-earthmage-walk-anim-miswire`, committed on its own ahead of the Yemoja work since it's a correctness bug with a much bigger blast radius than the task it was hiding inside.

---
### [x] Commit the Yemoja_v2 material-location fix (one .meta change, already applied live)
**Why:** While wiring Yemoja_v2's six materials, Cowork set the FBX importer's `materialLocation = External` to bind them to the authored `.mat` assets rather than embedding copies. That worked, but this Unity version has **deprecated** that setting and logs it as a repeating *exception* (not a warning) on every import touch - `MaterialLocation.External is obsolete. External Material Location is no longer supported.` This is the console error Stephanie spotted after the branch work; it was Cowork's doing, not Claude Code's, and it was cosmetic - nothing rendered or behaved wrongly because of it.

**Fixed live (2026-08-12, post-merge):** re-asserted all six entries in the importer's external-object remap table, then set `materialLocation = InPrefab`. The remap table is the modern supported mechanism for binding external material assets; `materialLocation` is the obsolete one. Verified after a full reimport: all 8 material slots still resolve to `Assets/CharacterModels/Yemoja/materials/*.mat` (NOT to copies embedded in the FBX - that distinction matters, since embedded copies would silently stop tracking edits to the `.mat` files), console completely clean, avatar still `isHuman = true` with 52 mapped slots, `globalScale` still 0.23985990.

The only file changed is `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx.meta`.

**Also re-verified on the merged `main` after the cherry-picks, all passing:**
- Playable prefab is based on `Yemoja_v2.fbx`; 12/12 hitbox + trident markers resolve to real bones, 0 stranded at root.
- `attackColliders[0]` = `A_Trident`, non-null. 9 material slots, 0 missing.
- Retargeting live: sampling the idle clip moves hips 0.221, rotates `mixamorig:HandIndex2.L` by 63.8 degrees (proving the left-finger mapping fix survived the merge), and the trident-to-hand distance holds at 0.1574 (it rigidly follows the hand).
- `FightScene` and `CharacterSelect` still reference the playable and display prefabs by GUID - roster arrays untouched.
- Claude Code's `AttackCTRL` length guard is present, so Ninja's missing hitbox rig no longer throws.

**Acceptance criteria:** commit the single `.meta` change. Nothing else to do.

**Deliberately NOT done - flagging rather than deciding:** the model file is still named `Yemoja_v2.fbx`. The old `Yemoja.fbx` has been deleted so the plain name is free, and a Unity rename preserves the GUID, so the rename is safe - but it would invalidate the verification pass above and needs its own reimport + `isHuman` re-check afterwards. Low benefit, non-zero risk; left as Stephanie's call rather than bundled in silently.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx.meta`.

**Done (2026-09-03):** By the time this session picked the queue back up, `Yemoja_v2.fbx.meta` had already been overwritten several more times live (v3 through v6 plus the 2026-09-02 material correction all landed on top of it before anything got committed) - so this task's original single-.meta scope no longer exists as an isolated diff. Committed as part of the combined Yemoja v2->v6 rebuild commit instead; see the v3/v4/v5/v6 entries below for the full record and the shared "Done" note. Branch `task/yemoja-v3-v6-import-review`.

### [x] Reference the new project-wide eye standard from `CLAUDE.md`
**Why:** `BlenderTools/README_eye_standard.md` was created 2026-08-12 as a character-agnostic
standard for how eyes are built across the whole roster - geometry, pivot, UV handedness,
texture, bones, gaze, and a 7-point verification checklist. It is a peer of
`README_rig_conventions.md`, which `CLAUDE.md` already points at.

The Unity-side rules in it are the ones most likely to be missed, because they fail *silently*:

- Unity's Humanoid auto-mapper does **not** leave `LeftEye`/`RightEye` empty when a character
  has no eye bones - it binds them to the nearest plausible transforms. On Yemoja v2 it bound
  both eyes and the jaw to **hair bones**, and reported `isHuman = true` regardless.
- Verifying an avatar therefore means opening `Rig > Configure` and reading *what each slot
  points at*, not merely that the slot is populated and the bone count is right.

Without a pointer in `CLAUDE.md`, Claude Code has no way to know the standard exists, and the
next character through the pipeline repeats the same faults.

Deliberately not done from the Blender side: `CLAUDE.md` is the Unity session's context file,
and edits to it should go through the normal review/commit path rather than being written
directly by the modelling session.

**Acceptance criteria:**
- `CLAUDE.md` gains a short reference to `BlenderTools/README_eye_standard.md`, alongside the
  existing `README_rig_conventions.md` reference, noting it applies to every character.
- The avatar-mapping caveat above is stated in one line, or explicitly linked.
- No content from the standard is copied into `CLAUDE.md` - reference only. The standard is
  single-source; restating it is how standards drift.

**Relevant files:** `CLAUDE.md`, `BlenderTools/README_eye_standard.md`,
`BlenderTools/README_rig_conventions.md`.

**Done (2026-09-03):** Also found while doing this: `CLAUDE.md` didn't actually reference `README_rig_conventions.md` either, despite this task's own "Why" saying it did - the earlier rig-naming task that was meant to add that pointer got superseded before it was ever executed, so the gap was real, not just this task's. Added both references together in a new "Character rig/eye standards" section rather than compounding the gap further. `README_eye_standard.md` itself was untracked (only `README_rig_conventions.md` was already in git), so tracked it too - a reference to a file nobody's clone has isn't a reference. Stated the Humanoid-auto-mapper-binds-to-hair-bones caveat in one line per the acceptance criteria, no other content copied from the standard. Branch `task/claude-md-eye-standard-reference`.

---
### [x] Review + commit the Yemoja_v3 import (done live by Cowork), and rename the now-misleading model file
**Why:** The modeller shipped `BlenderTools\_export\Yemoja_v3.fbx` (2,220,940 bytes; manifest now schema 18). Headline change is a **skin weight fix**: in v2 `Yemoja_Body` was skinned almost entirely to the `.R` bone set on BOTH halves (`.L` total 13.25 vs `.R` 4098). The bind pose looked correct so nothing caught it, but it would have torn body away from clothes on the first asymmetric attack. Also included: a proportion sculpt (thighs thickened, forearms slimmed, eye sockets widened - 775 verts moved, topology unchanged), and eye work (iris 10.98 -> 12.90 mm, gaze convergence corrected to 3.78 deg, both painted catchlights moved onto the mirror axis).

**Imported by copying v3 over the existing `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` path**, per the modeller's own recommendation, so the GUID is preserved and both prefabs plus the roster arrays stayed wired with zero scene edits (verified: GUID `13d3e5395146e914f89ba56696257511` unchanged; `FightScene` and `CharacterSelect` still reference both prefabs).

**Verified independently rather than taken on trust:**
- **The skin weight fix is real, measured from Unity's own imported bone weights** (not the Blender-side report): `.L` total 2472.34 vs `.R` 2468.39, a ratio of **1.0016** where v2 was roughly 300:1 skewed. 23 groups per side carrying weight, exactly as reported, and 0 vertices with no bone influence. Mirror-paired bones now carry identical weight (`Arm.L`/`Arm.R` both 367.35, `ToeBase` both 351.26, `Hand` both 225.34, `Leg` both 193.46) - that symmetry is the signature of a correctly mirrored skin. Note the absolute totals differ from the modeller's Blender figures (2472 vs 2056) purely because Unity splits vertices at UV/normal seams (8017 in Unity vs 6475 in Blender) and clamps to 4 influences; the **ratio** is the meaningful number, not the magnitude.
- Scale re-measured rather than assumed to carry: still exactly **1.80000** source height, **3.4500** in-game at the 1.91667 root scale.
- Avatar `isHuman = true`, 52 mapped slots, `Armature` present in `skeleton[]`, 0 bones referenced that don't exist in the model.
- **The two v2 auto-mapper faults did NOT recur** - 0 humanoid slots bound to hair bones (v2 had `LeftEye`/`RightEye`/`Jaw` pointing at `hair_loc*`), and 15/15 left-hand plus 15/15 right-hand finger bones mapped. See the finding below for why.
- **All 12 hitbox/trident markers still resolve to real bones, 0 stranded** on both the playable and display prefabs. See the second finding below.
- Retargeting live-checked across all 8 clips: trident-to-hand distance holds at exactly 0.1574 at every sample of every clip (it rigidly follows the hand), toe clearance ranges -0.03 to -0.39, and at idle the left index finger rotates 63.8 deg / right 56.0 deg (proving both hands' finger mappings are live).
- Console completely clean. The 6 self-intersecting `Hair` polygon warnings seen on the v2 import did **not** recur this time - worth mentioning to the modeller, though I'd treat that as an observation rather than proof they were fixed.

**TWO FINDINGS THAT REFINE THE v2 RECORD'S WARNINGS - both good news, both worth internalising:**
1. **Overwriting the FBX in place preserved the hand-corrected humanoid map.** Because the `.meta` was untouched, Unity did not re-run its auto-mapper, so the 52-slot map Cowork hand-fixed on v2 (removing the three bogus hair-bone bindings, adding the 15 missing left-finger bindings) carried straight over. Had the file been imported at a *new* path, the auto-mapper would have run fresh and almost certainly reintroduced both faults. **This is a strong, concrete argument for always overwriting in place on model updates.**
2. **Marker parenting survived, which narrows the earlier warning.** The v2 record warns that an in-place re-export regenerates internal transform fileIDs and silently strands bone-attached objects on the root. That did not happen here. The distinction appears to be that v3's bone hierarchy and names are **identical** to v2's - the fileIDs are derived from hierarchy, so an export that changes only vertex positions is safe, whereas the earlier breakage followed a re-export that *did* change the hierarchy (the project-wide bone rename). So the rule is better stated as: **re-verify marker parenting whenever the rig's hierarchy or bone names change** - not on every re-export. It is still cheap to check either way.

**MANUAL STEP THAT WAS EASY TO MISS, and was done:** `Yemoja_Body_mat` and `Yemoja_Eye_mat` both had their BaseColor repointed from `Yemoja_Color_SkinGrade_v2_eyeShade.png` to `Yemoja_Color_SkinGrade_v3_iris.png`. Nothing errors if this is skipped - you silently get the old, smaller iris. Confirmed both now read `Yemoja_Color_SkinGrade_v3_iris`, sRGB on. The other four materials are unchanged, and all 8 model slots + 9 prefab slots still bind to the authored `.mat` assets via the remap table with `materialLocation = InPrefab` (never `External`, which is deprecated and logs a repeating exception).

**Acceptance criteria:**
- Review the diff: the FBX binary, its `.meta`, and the two `.mat` files. Nothing else should have changed.
- **Rename `Yemoja_v2.fbx` to `Yemoja.fbx`.** This was optional before; it is now actively misleading, because the file is named v2 but contains v3 and will contain v4 next time. A Unity rename preserves the GUID so both prefabs survive it - but do it as its own commit and re-verify `isHuman`, the 52-slot map and marker parenting afterwards, since a rename regenerates the avatar sub-asset name.
- Playtest: select Yemoja, confirm the new proportions and iris read correctly, and specifically watch for body-vs-clothes tearing during the asymmetric attacks (`SwordAndShieldAttack`, `SwordAndShieldSlash`) - that is the exact failure the weight fix prevents, so it is the meaningful regression test.

**New standard doc to wire in:** `BlenderTools/README_eye_standard.md` - project-wide, character-agnostic eye spec (geometry, pivot, UV handedness, texture, bones, gaze, 7-point verification), peer of `README_rig_conventions.md`. There is already a `TASKS.md` item to reference it from `CLAUDE.md`; the manifest at schema 18 points at both standards.

**Still open, unchanged by this import:** animation events (`PerformAttack` / `StopAttacking` / `EndHit`) are still absent from all 8 clips, so **her attacks still deal no damage** - biggest remaining gap. Eye pitch ~1.1 deg down with the manifest contradicting itself (8 deg in one place, 0.29 in another), left alone deliberately. Eyebrows still read as plastic slabs (Blender-side). `CharacterPhysics.groundLayerMask` is still 0 on Yemoja, carried over verbatim and still looks wrong. Root `BoxCollider` and hitbox sizes still date from the pre-resculpt proportions - and v3 thickened the thighs and slimmed the forearms, so the leg and hand boxes are now a little further from the silhouette than they were.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` (+meta), `Assets/CharacterModels/Yemoja/materials/Yemoja_Body_mat.mat`, `Yemoja_Eye_mat.mat`, `BlenderTools/README_eye_standard.md`, `claude/yemoja-v2-import-record.md` (project doc).

**Done (2026-09-03):** Committed together with v4/v5/v6 and the 2026-09-02 material correction as one combined commit - see the shared note on the v6 entry below for why (only the final on-disk state ever existed to commit; there was no way to split it back into per-version commits). The rename to `Yemoja.fbx` still was not done, for the same reason given here originally plus the fact it's now four further imports stale; still Stephanie's/Cowork's call. Branch `task/yemoja-v3-v6-import-review`.

---
### [x] Review + commit the Yemoja_v4 import (done live by Cowork)
**Why:** Modeller shipped `BlenderTools\_export\Yemoja_v4.fbx` (2,360,076 bytes; manifest schema 21). New since v3: real **eye bones** (`mixamorig:Eye.L` / `Eye.R` under Head, rig 92 -> 94, 191 verts each at weight 1.0), **rebuilt eyebrows** (the two solid brow shells inside `Yemoja_Body` deleted, replaced by `Yemoja_BrowCards_A_R` alpha hair cards), and a painted brow base plus rougher under-brow skin in the atlas.

Imported by copying over `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` (filename still misleading). GUID preserved, so both prefabs and the roster arrays stayed wired with zero scene edits.

**THE ONE THING THAT WENT WRONG, AND THE RULE IT PRODUCES.** Overwriting in place preserves the `.meta`, which on v3 was purely a benefit - it kept Cowork's hand-corrected humanoid map and stopped Unity re-running its auto-mapper. On v4 the same mechanism **bit us**, because the preserved `.meta` also pins `humanDescription.skeleton[]`, and that list was the stale 101-entry v3 skeleton with no `Eye.L`/`Eye.R` in it. Mapping the two new eye slots produced a `human[]` entry referencing a bone absent from `skeleton[]`, and the avatar silently flipped to **`isHuman = false`** - the exact signature of the original frozen-animation bug (`Rig Error: Avatar creation failed`, which only appears as a *Log*, not an error, so it is easy to miss).

Fixed with the standard reset: `animationType = None` -> reimport -> `Human` + `CreateFromThisModel` -> reimport, which rebuilt `skeleton[]` from the current hierarchy (101 -> 104 entries, eye bones present).

**Rule for next time, worth adding to the standing notes: overwrite in place as usual, but if the BONE COUNT changed, do the None -> Human reset immediately afterwards.** Vertex-only updates (v3) keep the preserved map safely; skeleton changes (v4) need the rebuild. Then re-audit the fresh auto-map, because the reset discards the hand corrections.

**What the fresh auto-map got right and wrong this time:** eyes correctly bound to the real `Eye.L`/`Eye.R` (as predicted - once real eye bones exist the mapper can no longer reach for hair bones), and **both** hands' 15 finger bones mapped correctly, unlike v2 where the left hand was entirely missed. It still bound the **Jaw** slot to a hair bone (`hair_loc16_0`); Yemoja has no jaw bone, so that entry was removed. Final state: **54 mapped slots**, `isHuman = true`, 0 slots bound to hair bones, 0 bones referenced that don't exist, `Armature` present in `skeleton[]`.

**Marker parenting survived, which refines the rule again.** The handoff flagged the 92 -> 94 bone change as likely to regenerate transform fileIDs and silently strand the hitboxes on the prefab root. It did not happen - all 12 markers on the playable prefab and the trident on the display prefab still resolve to real bones, 0 stranded. The reason appears to be that the new bones are *leaves added under Head*, which does not perturb the fileIDs of the bones the markers actually attach to (`Head`, `Hand.L/R`, `Spine`, `Leg.L/R`). So the sharper statement is: **a bone-count change does not automatically break marker parenting - what breaks it is renaming or restructuring the bones the markers hang off.** Still worth checking every time; it is cheap and it fails silently.

**Texture repoints - four changes, two maps on each of two materials, all applied:**
- `Yemoja_Body_mat` and `Yemoja_Eye_mat`: BaseColor -> `Yemoja_Color_SkinGrade_v4_brow.png`, MetallicSmoothness -> `Yemoja_MetallicSmoothness_v2_brow.png`.
- **Caught an import-setting fault while doing it:** `Yemoja_MetallicSmoothness_v2_brow.png` was importing with **sRGB ON**. It is data, not colour - left that way it would have gamma-shifted both the metallic and the smoothness channels, giving subtly wrong skin specular with nothing in the console to indicate why. Corrected to sRGB OFF.

**Brow cards verified:** 1,360 triangles (exactly as reported), **100.00% rigid to `mixamorig:Head`**, bound to the **existing** `Yemoja_Lashes_mat` with no new material created. Distinct materials on the model still **6**; submeshes **9**; no stray auto-generated Materials folder. Confirmed the brow cards track the head by comparing their posed bounds against the lash cards' across five clips - the two move together to within a few thousandths, and the lashes are the control since they were already correct.

**Everything else re-verified:** source height exactly 1.80000 / 3.4500 in-game; skin weight balance from Unity's own imported weights still `.L` 2671.34 vs `.R` 2667.39, **ratio 1.0015**, 0 unweighted verts (now 24 weighted groups per side rather than 23, consistent with the new per-side eye bone); trident-to-hand distance holds at exactly 0.1574 across every sample of all 8 clips; toe clearance -0.03 to -0.39; both hands' fingers animating at idle; all 9 submesh slots bound to the authored materials with 0 nulls; `FightScene` and `CharacterSelect` still referencing both prefabs; console clean.

**Acceptance criteria:**
- Review the diff: FBX binary, its `.meta`, the two `.mat` files, and the `.meta` for `Yemoja_MetallicSmoothness_v2_brow.png` (the sRGB fix).
- **Rename `Yemoja_v2.fbx` to `Yemoja.fbx`.** Now three versions stale. GUID-safe, but give it its own commit and re-check `isHuman`, the 54-slot map and marker parenting afterwards.
- Playtest: brows and eyes read correctly at CharacterSelect distance; and the regression test that matters - **watch for body-vs-clothes tearing during `SwordAndShieldAttack` and `SwordAndShieldSlash`**, since a static pose looks fine even when the skin weights are wrong.

**Still open, unchanged:** animation events (`PerformAttack` / `StopAttacking` / `EndHit`) absent from all 8 clips, so **her attacks still deal no damage** - biggest remaining gap by far. Root `BoxCollider` and hitbox sizes still date from pre-resculpt proportions. `CharacterPhysics.groundLayerMask` still 0 on Yemoja.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` (+meta), `materials/Yemoja_Body_mat.mat`, `materials/Yemoja_Eye_mat.mat`, `textures/Yemoja_MetallicSmoothness_v2_brow.png.meta`, `claude/yemoja-v4-import-record.md`.

**Done (2026-09-03):** Committed together with v3/v5/v6 and the 2026-09-02 material correction as one combined commit - see the shared note on the v6 entry below. Branch `task/yemoja-v3-v6-import-review`.

---
### [x] Review + commit the Yemoja_v5 lips import (done live by Cowork)
**Why:** Modeller shipped `BlenderTools\_export\Yemoja_v5.fbx` (2,415,996 bytes; manifest schema 24). A **lips pass** and nothing else: the lip region was subdivided two levels and hand-sculpted, plus three new skin-atlas textures. Body 6,493 -> 7,385 verts / 12,896 -> 14,680 tris in Blender terms. No new materials, textures slots, draw calls or renderers - still 6 materials across 9 submeshes.

Imported by overwriting `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` in place. GUID preserved, so both prefabs and the roster arrays stayed wired with zero scene edits.

**BOTH PREDICTED IMPORT-SETTING FAULTS WERE REAL AND ARE FIXED.** The three new PNGs had no `.meta`, so Unity generated defaults, and two of the three defaults were wrong - each one a fault this project has already shipped once:
- `Yemoja_Normal_v7_seam.png` imported as a plain **Default / sRGB** texture. Corrected to **Normal Map** type. (Same fault as `Yemoja_Hair_Normal.png` on the v2 import.)
- `Yemoja_MetallicSmoothness_v8_lipbreakup.png` imported with **sRGB ON**. Corrected to **OFF**. (Same fault as `Yemoja_MetallicSmoothness_v2_brow.png` on v4.) Left wrong this gamma-shifts both the metallic and smoothness channels and gives subtly wrong skin specular with nothing in the console.
- `Yemoja_Color_SkinGrade_v7_lipsFinal.png` defaulted correctly (Default / sRGB ON).

These were fixed **before** anything was wired, so no downstream step ever read bad data. Worth noting the pattern: this is now three imports in a row where a new data map arrived without a `.meta` and Unity's default was wrong. **Any packed data map - metallic/smoothness, masks, roughness - needs sRGB off; any normal map needs Normal Map type.** Cheap to check, silent when missed.

**Six texture repoints applied** (three maps x two materials - `Yemoja_Eye_mat` shares the skin atlas and is the one that fails silently if forgotten):
- `Yemoja_Body_mat` and `Yemoja_Eye_mat`: BaseColor -> `Yemoja_Color_SkinGrade_v7_lipsFinal`, Normal -> `Yemoja_Normal_v7_seam`, MetallicSmoothness -> `Yemoja_MetallicSmoothness_v8_lipbreakup`.
- Also ran a sweep across every material in the Yemoja folder for references to any superseded map (`Yemoja_Normal_v6_lips`, the older skin atlases, the older metallic/smoothness maps): **zero stale references** remain.

**No rig reset was needed, and that was verified rather than assumed.** Bone hierarchy is unchanged at 94 bones, so per the rule as narrowed across v2/v3/v4 the preserved `.meta` was safe here: avatar came through at `isHuman = true`, 54 mapped slots, `skeleton[]` still 104 with `Armature` present, 0 bones referenced that don't exist, 0 slots bound to hair bones, 15/15 fingers on both hands. **All 12 markers still on real bones, 0 stranded**, on both prefabs; `attackColliders[0]` = `A_Trident`.

**Everything else re-verified:** measured source height exactly 1.80000 / 3.4500 in-game (measured, not assumed, per standing policy); body 8,763 Unity verts / **14,680 tris exactly matching the reported figure**; skin weight balance `.L` 2671.34 vs `.R` 2667.39, **ratio 1.0015**, 0 unweighted verts, 24 weighted groups per side - the new lip verts ride on `mixamorig:Head` (she has no jaw bone) so they add to the centre total without skewing the L/R balance; trident-to-hand distance holds at exactly 0.1574 across every sample of all 8 clips; toe clearance -0.03 to -0.39; both hands' fingers animating; 9/9 submesh slots bound with 0 nulls; 6 distinct materials; no stray auto-generated Materials folder.

**A CORRECTION TO THE v3 AND v4 RECORDS.** Those said the six "self-intersecting `Hair` polygon" messages seen on the v2 import "did not recur". That was wrong, and the mistake was mine: those messages are logged at **Log** level, not Warning, and my post-import console checks on v3 and v4 filtered to errors and warnings only, so they were never actually in scope. They reappeared on this import when the filter was widened. They have most likely been present on every import since v2. Nothing has changed about the hair mesh - only the accuracy of the reporting. The underlying item (1 non-manifold hair edge Blender-side) is already on the modeller's open list; the Unity-side count is 6 discarded polygons and is worth relaying, but it is not new and not a regression.

**Acceptance criteria:**
- Review the diff: the FBX binary and its `.meta`, the two `.mat` files, and three new texture `.meta` files (two of which carry the corrected import settings).
- **Rename `Yemoja_v2.fbx` to `Yemoja.fbx`.** Four versions stale now. GUID-safe; give it its own commit and re-check `isHuman`, the 54-slot map and marker parenting afterwards.
- Playtest: the regression test that still matters is **body-vs-clothes tearing during `SwordAndShieldAttack` and `SwordAndShieldSlash`** - a static pose looks fine even when skin weights are wrong. New for this build: check the mouth at **fight-camera distance** as well as in CharacterSelect. The lip work is a close-up asset and should simply read as a normal mouth at fight range, not draw attention.

**Still open, unchanged by this pass:** animation events (`PerformAttack` / `StopAttacking` / `EndHit`) absent from all 8 clips, so **her attacks still deal no damage** - unchanged across v2, v3, v4 and now v5, and by a distance the biggest remaining gap. Root `BoxCollider` and hitbox sizes still date from pre-resculpt proportions. `CharacterPhysics.groundLayerMask` still 0 on Yemoja.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` (+meta), `materials/Yemoja_Body_mat.mat`, `materials/Yemoja_Eye_mat.mat`, `textures/Yemoja_Normal_v7_seam.png.meta`, `textures/Yemoja_MetallicSmoothness_v8_lipbreakup.png.meta`, `textures/Yemoja_Color_SkinGrade_v7_lipsFinal.png.meta`, `claude/yemoja-v5-import-record.md`.

**Done (2026-09-03):** Committed together with v3/v4/v6 and the 2026-09-02 material correction as one combined commit - see the shared note on the v6 entry below. Branch `task/yemoja-v3-v6-import-review`.

---
### [x] Review + commit the Yemoja_v6 import (done live by Cowork)
**Why:** Modeller shipped `BlenderTools\_export\Yemoja_v6.fbx` (2,379,404 bytes) with a full guideline at `BlenderTools/YemojaDesignArtifacts/README_unity_import.md`. Rig 94 -> 80 bones (34 stale `hair_loc*` replaced by 20 `hair_grp*`), rest mesh re-grounded onto z=0, weights normalised to max 4 influences, clavicle bleed trimmed, hair split into an opaque body material and an alpha-tested tip material, lash cards and eyeliner each merged to one object, clothes decorations decimated, and a new `Yemoja_Tattoos` decal mesh. Materials 6 -> 10, submeshes 9 -> 13, renderers 9.

Imported by overwriting `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` in place. GUID preserved; both prefabs and the roster arrays stayed wired with zero scene edits.

---

#### THE MISTAKE I MADE, AND THE NEW RULE IT PRODUCES

I did the mandatory avatar reset **before** setting the final `globalScale`, following the README's step order literally. That is wrong, and it broke retargeting catastrophically in a way that is worth understanding because nothing errors.

`humanDescription.skeleton[]` stores the avatar's rest pose as **absolute positions**. It is captured at reset time and is **not** recomputed when `globalScale` changes afterwards. Because I reset first and then measured and re-scaled, the rest pose ended up **10,000x larger** than the actual imported transforms - `mixamorig:Hips` recorded at Y 10754.44 against an actual local Y of 1.075444, `Armature` at 345.7842 against 0.034578.

The symptom: **the bind pose looked perfect** (Hips Y 2.13, toes on the floor), `isHuman` was `true`, the humanoid map audited clean, and the console said nothing. The fault only appeared the instant a clip drove the rig - a single sample of the idle clip threw Hips to **Y 18,857** and the feet to Y -760. I nearly mis-attributed it to my own sampling loop accumulating root motion; sampling the same frame twice and getting an identical value is what ruled that out and proved it was a genuine retarget fault.

Fixed by re-running the None -> Human reset **after** the scale was final. `skeleton[]` then matched the live transforms exactly, and the retarget came back sane.

**Rule to add to the standing notes: set `globalScale` FIRST, then do the avatar reset.** Never the reverse. If any scale change happens after a reset, reset again. The failure is silent, passes every static check including `isHuman`, and only shows under animation.

---

#### FLOOR CLEARANCE MOVED THE WRONG WAY - needs a Blender-side decision

The README predicted toe clearance would **improve by ~0.03** source units now that the rest mesh sits on z=0, and said explicitly that if it did not, it was worth chasing rather than repeating the old note. It did not improve. It got **worse by a consistent 0.0332** across all 8 clips (range -0.023 to -0.038):

| clip | v6 toe Y | v5 | delta |
|---|---|---|---|
| Idle | -0.1479 | -0.1116 | -0.0363 |
| Walk | -0.0690 | -0.0307 | -0.0383 |
| Attack | -0.2742 | -0.2380 | -0.0362 |
| Slash | -0.2716 | -0.2434 | -0.0282 |
| Jump | -0.2090 | -0.1727 | -0.0363 |
| BlockIdle | -0.2161 | -0.1838 | -0.0323 |
| HitReaction | -0.4175 | -0.3943 | -0.0232 |
| StumbleBackwards | -0.0882 | -0.0537 | -0.0345 |

**Cause, measured not guessed:** the `Armature` object's own Y offset is **0.034578** source units (the 0.14324 Blender-Z translation the README says is intentional). The mean measured shift is **0.0332** - the same number to within 1.4 mm. In the **bind** pose the offset is honoured and the toes sit at +0.0216, correctly above the floor. Under Humanoid retargeting it is **not** honoured: Unity rebuilds the root from the avatar's hips/feet relationship and the Armature's translation is discarded, so the animated pose lands ~0.0346 lower than the rest pose implies. All 8 clips already have `heightFromFeet = true` and `heightOffset = 0`, so nothing on the clip side is compensating.

Two ways to resolve, and I have deliberately applied **neither**, because both change gameplay-visible foot placement:
- **Blender side (cleaner):** zero the `Armature` translation and bake the 0.14324 into the mesh instead, so the rest pose and the retargeted pose agree. Structural fix, no per-clip bookkeeping.
- **Unity side (quick):** set `heightOffset = +0.0346` on all 8 clips. One line, but it is a per-clip value that has to be re-applied to every future clip, including the planned custom animations.

Recommend the Blender-side fix. Flagging for Stephanie / the modeller rather than deciding unilaterally.

---

#### SHOULDER MUSCLE RANGES - the numbers the Blender side asked for

The animation plan depends on ~30 deg of clavicle elevation surviving retargeting. Unity's per-muscle clamps, read from `HumanTrait` on this avatar (all slots are on `useDefaultValues = true`):

| muscle | min | max |
|---|---|---|
| Left / Right Shoulder Down-Up | **-15.0 deg** | **+30.0 deg** |
| Left / Right Shoulder Front-Back | -15.0 deg | +15.0 deg |
| Left / Right Arm Down-Up | -60.0 deg | +100.0 deg |
| Left / Right Arm Front-Back | -100.0 deg | +100.0 deg |

**Answer: 30 deg of clavicle elevation survives - but it sits exactly ON the ceiling, with zero headroom.** Anything above 30 deg is silently clipped, and because it clamps rather than errors, an animator would see the pose flatten with no diagnostic. If the plan wants 30 deg as a working value rather than an absolute maximum, either keep authored elevation at ~25 deg to leave margin, or widen the limit per-avatar (set `useDefaultValues = false` and raise the max on the two Shoulder slots) - that is a Unity-side change I can make on request.

---

#### SEVEN TEXTURE IMPORT FAULTS FIXED, PLUS THE MOBILE MEMORY ITEM
Type / colour-space corrections (the README's own table plus three the README omitted but the manifest specifies):
- **Normal Map type** (were importing as plain sRGB textures): `Yemoja_Normal_v11_navelMirrored`, `Clothes_Normal_v2_seam`, `Yemoja_Fuzz_CoilNormal_v3_wisp`.
- **sRGB OFF** (packed data): `Yemoja_MetallicSmoothness_v10_pores`, plus `Yemoja_Hair_Mask_v3_deblotch`, `Yemoja_Hair_BaseColor_v6_rootDark` and `Yemoja_Hair_TipCurl_Alpha` - the last three are named only in the manifest (`hair_shading_2026_08_28`, notes 29 and 32), not in the README, and would have been missed by following the README alone.
- `Yemoja_Hair_BaseColor_v6_rootDark` also set to **Wrap Mode Clamp** per manifest note 29 - the gradient occupies V 0..0.058 while loc V runs to 1.04, so under Repeat every loc tip wraps onto the dark root band.

Mobile texture memory - the README's largest single number, and it is fixed purely by import settings: **Android and iPhone overrides set to Max Size 1024, ASTC 6x6, mipmaps ON** on all six 2048 maps (skin base/normal/metallic-smoothness and the same three for clothes). Per the README's own figures that takes one character from 101 MB uncompressed to roughly 3 MB, i.e. two fighters from ~200 MB to ~6 MB before stage, UI or VFX. Source assets untouched at 2048, so CharacterSelect close-ups keep full detail on desktop.

#### MATERIALS - four created, render state applied
Created `Yemoja_Hair_Opaque_mat`, `Yemoja_Jewelry_mat`, `Yemoja_Tattoos_mat`, `Yemoja_Fuzz_mat`. Skin repointed on **both** `Yemoja_Body_mat` and `Yemoja_Eye_mat` to the v17/v11/v10_pores set; clothes normal to `Clothes_Normal_v2_seam`; eye emission wired to `Yemoja_Eye_Emission_v2_soft` at `RIG_eye_glow` 0.9375.

Render Face applied exactly per the README table - Front on eight materials, Both on `Yemoja_Fuzz_mat` and `Yemoja_Lashes_mat`. **The hair split is correct and verified by triangle count**, which is the check that matters since getting it backwards silently forfeits the whole benefit: `Yemoja_Hair_Opaque_mat` carries **5,704 tris, alpha clipping OFF, queue 2000**; `Yemoja_Hair_mat` carries **920 tris, alpha-clipped at 0.5, queue 2450**. Both figures match the README exactly.

All **13 submesh slots bound, 0 null, 10 distinct materials, 9 renderers** - matching the README's stated budget.

#### Three things I had to choose because nothing specified them - **ALL THREE SUPERSEDED, see the 2026-09-02 correction below**
1. **`Yemoja_Jewelry_mat` look.** No spec anywhere. Set metallic 1.0, smoothness 0.80, near-white. Used by `Yemoja_Nails` and the 2,388-tri loc-bead submesh of the scalp, so it is visible - worth an eye.
2. **`Yemoja_Tattoos_mat` transparency mode.** Alpha-clipped at 0.5, matching every other cutout on this character and the README's alpha-tested budget framing. A tattoo decal on skin might read better Transparent (softer edges) at the cost of sorting - a look decision.
3. **Hair base map.** The README's "do not fix" list still says hair is flat colour + normal only, but that text is carried over verbatim from v4/v5 and the manifest's hair work of 2026-08-19..28 supersedes it. I assigned `Yemoja_Hair_BaseColor_v6_rootDark` per manifest note 29. **Related and larger:** manifest notes 30 and 33 state that the Blender hair shader's `root_fade` and `is_card` chains are generic FLOAT attributes that FBX does not carry and stock URP Lit cannot read, and rule 213's mask-green-multiplies-albedo is a node-graph operation URP Lit has no slot for. The hair will therefore not match Blender until someone writes a Shader Graph or bakes those chains into vertex colours. That is a real piece of work and a decision, not something to slip into an import.

#### Other verification
Scale re-measured from scratch as instructed: at `globalScale` 1 the model is **7.45644** units, so `globalScale = 1.80 / 7.45644 = 0.24140200` - **different from v2's 0.23985990**, so reusing the old factor would have been wrong. Result exactly 1.80000 source / 3.4500 in-game. Rig: `isHuman` true, 54 slots, 15/15 fingers both hands, eyes on the real eye bones, 0 slots bound to hair bones (the auto-mapper grabbed `hair_grp07_0` for Jaw - **third time running** after `hair_loc16_0` on v2 and v4 - removed). All 12 markers plus the trident still on real bones, **0 stranded**, despite 14 bones being removed from under `Head`; `attackColliders[0]` = `A_Trident`. Weights: every one of the 9 meshes has **0 unweighted and 0 non-normalised vertices**, body L/R balance ratio 0.9989. Trident-to-hand distance constant at 0.1574 across every sample of all 8 clips. **Console completely clean at Log level** - and unlike the v3/v4 records, that claim is checked at Log level, so the ~6 self-intersecting Hair polygon entries really are gone (that mesh was rebuilt as `Yemoja_Scalp`).

One deviation from the README worth noting, benign: it warned that `Yemoja_Scalp` would carry a zero-face `Yemoja_LocGuides_Invisible` slot needing any material. The actual import has a 4th scalp submesh of **234 real triangles** bound to `Yemoja_Body_mat`. No null slots either way, nothing to fix.

**Acceptance criteria:**
- Review the diff **as it stands**, not against a list written before the fact. The material and texture set was revised again on 2026-09-02 by the modeller working directly in Unity, and four textures named in this entry have since been retired to `Backups/`. See the follow-up entry below before judging any material or texture change here as unintended.
- **Rename `Yemoja_v2.fbx` to `Yemoja.fbx`** - five versions stale. GUID-safe; own commit; re-check `isHuman`, the 54-slot map and marker parenting after.
- Decide the floor-offset question above.
- Playtest: body-vs-clothes tearing during `SwordAndShieldAttack` / `SwordAndShieldSlash`; tattoo, brow and eye read at both CharacterSelect and fight-camera distance; and confirm the hair reads acceptably given the shader gap noted above.

**Still open, unchanged:** animation events (`PerformAttack` / `StopAttacking` / `EndHit`) absent from all 8 clips - **her attacks still deal no damage**, open since v2. Root `BoxCollider` and hitbox sizes still pre-resculpt. `CharacterPhysics.groundLayerMask` still 0. Hair motion needs a Unity-side spring/jiggle system - the 20 `hair_grp*` bones are not humanoid bones, so any animation keyed onto them is dropped by Humanoid import.

**Relevant files:** `Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` (+meta), `Assets/CharacterModels/Yemoja/materials/*` (10), `Assets/CharacterModels/Yemoja/textures/*.meta`, `claude/yemoja-v6-import-record.md`, `BlenderTools/YemojaDesignArtifacts/README_unity_import.md`.

**Done (2026-09-03):** This session picked TASKS.md back up and found the working tree already held the *final* state - v3 through v6 plus the 2026-09-02 material correction had all landed live on top of the same `Yemoja_v2.fbx`/materials/textures with nothing committed in between, so there was no way to recover per-version diffs; committing "the v3 import" and "the v6 import" separately would mean fabricating history that never existed as discrete commits. Bundled all of it (this entry, v3, v4, v5, and the material-correction entry below) into one commit on `task/yemoja-v3-v6-import-review`.

Reviewed rather than taken on faith: cross-checked every deleted texture against every `.mat`/`.prefab` in the repo (only hits were stale mentions in two uncommitted status docs, not live references); spot-checked the described mobile-texture-override .meta changes (ASTC, capped max size) against what's actually on disk; confirmed `Yemoja_Trident_mat` still resolves its MetallicGlossMap by GUID to the surviving combined map, justifying the deleted separate Metallic/Roughness textures.

**Found something outside this task's scope while reviewing, fixed on its own branch first:** the shared `EarthMageAnimCTRL.controller` (used by EarthMage/Ninja/WarriorPrincess, not Yemoja - she has her own `YemojaAnimCTRL`) had its Walk state's Motion accidentally repointed to Yemoja's `SwordAndShieldWalk.fbx` clip - almost certainly a stray drag in the Animator window during a Yemoja session, since nothing in this session's history describes touching the shared controller. Left as-is, every character sharing that controller would have played Yemoja's walk animation instead of their own. Reverted on its own branch, `task/fix-earthmage-walk-anim-miswire`, before touching anything else, since it's a correctness bug with a much bigger blast radius than the Yemoja work it was hiding inside.

**Not done, explicitly declined rather than silently skipped:** three "retired" hair textures (`Yemoja_Hair_BaseColor_v11_paler.png`, `Yemoja_Hair_CardAtlas_v3_tipTeal.png`, `Yemoja_Hair_Mask_v3_deblotch.png`) currently exist as identical duplicates both live under `Assets/CharacterModels/Yemoja/textures/` and archived under `Backups/2026-09-02_yemoja-v6-cleanup/`. Confirmed unreferenced by any Unity `.mat`/`.prefab`, but Stephanie flagged that Blender-side tooling may still reference them by path even though Unity doesn't - so the dedup/cleanup was left undone rather than guessed at. Left for Cowork. Same goes for the rest of the `Backups/`/`BlenderTools/` churn sitting in the working tree alongside this (old milestone `.blend` files deleted, new `WORKING_v108-v115` files and a `yemoja_manifest.json` reorganization added) - that's the modeller's/Cowork's working area, not touched from the Unity side.

Playtest (idle/walk/jump/attack/heavy/block/hit/knockback, trident + hitbox tracking, body-vs-clothes tearing on the asymmetric attacks, console clean) still needs manual in-Editor verification.

---
### [x] Review + commit: Yemoja v6 material correction, mobile-settings repair, and the new CharacterImport scene (done live by Cowork, 2026-09-02)
**Why:** three separate things landed together. Read the first one before reviewing any material diff.

---

#### 1. CORRECTION - I wired the wrong textures, and why I did not catch it

On the v6 import I bound Yemoja's materials to the texture set listed in the modeller's `README_unity_import.md`. The modeller later audited the Unity project herself and found the look was wrong because newer textures already existed that the README's table did not mention.

**The specific reasoning error:** on v6 I did cross-read the manifest against the README - but only for *import settings* (colour space, normal-map type). I never asked the different question of whether the texture **files** the README named were still the newest ones on disk. They were not. `Clothes_BaseColor_Ocean6_gradient.png` was created 2026-08-27 and `Yemoja_Jewelry_BaseColor_v3_nails.png` the same day; I wired `Clothes_BaseColor_Ocean2.png` from 2026-08-12. Both newer files were sitting in the project the whole time.

The proof is in the `.meta` files: the mobile compression overrides I applied on v6 landed on Ocean2 and never on Ocean6_gradient, which is only possible if Ocean2 is what I bound.

**Rule to add to the standing notes: before wiring a material, sort the texture folder by date and reconcile it against the handoff doc.** A handoff document records what was true when it was written. Files on disk record what is true now. When they disagree, the disk wins, and the discrepancy itself is worth reporting back to the modeller.

Worth being precise about what was and was not wrong, because two claims in the v6 entry above are now false:
- **False:** "the hair look was never baked" and needs a Shader Graph. The bake exists. `Yemoja_Hair_Baked_BaseColor_v2` / `_Normal_v2` / `_MetallicSmoothness_v2` were produced on 2026-09-02 and the hair now reads correctly in Unity with stock URP Lit. The Shader Graph question is closed for now.
- **Misleading:** "four new `.mat` files" as a thing to review. Those four material assets still exist and are still the ones bound - the modeller edited them in place rather than replacing them. What changed is the textures inside them.
- **Still true:** every measurement I reported on v6 was numerically accurate. They were accurate measurements of the wrong texture set.

Yemoja now renders correctly: white/teal hair with the baked tip gradient, the gradient outfit, silver jewelry, tattoos reading at fight-camera distance. Verified by direct camera capture, not by inference.

---

#### 2. Mobile texture settings re-applied - a real regression, silently introduced

Swapping a texture inside a material does **not** carry the old texture's import settings across. They live on the new texture's `.meta`, and a freshly added PNG defaults to no platform override at all. So the moment the new bakes were plugged in, part of the v6 mobile-memory work was undone with nothing logged anywhere.

Re-applied Android + iPhone overrides (ASTC 6x6, mipmaps on, max size capped at 1024 but **never above the source resolution**, so a 128px jewelry map stays 128 rather than being upscaled) to 16 referenced textures. Colour space and normal-map type were checked at the same time and were already correct on the new files.

**Rule: after any material or texture swap, re-audit platform overrides on the newly referenced textures.** This will recur on every future delivery.

---

#### 3. New `CharacterImport` scene - a place to audit models without loading the game

`Assets/Scenes/CharacterImport.unity`, deliberately **not** in Build Settings, so it never ships and never affects the game.

Lighting, ambient mode and the post-processing volume are copied from FightScene (directional white at intensity 2, rotation 50/330/0, soft shadows, `SampleSceneProfile`), and `CharacterStage` carries the same 1.813 scale the fighters use in game - so a look judged here is the look the game will render, which is the entire point.

Driven by `Assets/DevTools/CharacterPreview/CharacterPreview.cs`. It builds a `PlayableGraph` by hand rather than using an AnimatorController, which means it can play any clip on any rig with no state machine wired, and because the graph runs on manual time it gives pause, frame scrub and slow motion. It offers: a clip picker labelled by **source file name** (every Mixamo clip is internally called `mixamo.com`, so the clip's own name is useless), per-renderer visibility toggles for isolating clothes-vs-body clipping, full/torso/face framing with orbit, a sky/dark/light background swap, live mesh statistics including empty material slots, and a live foot-clearance readout taken from the **toe bones** rather than renderer bounds - `SkinnedMeshRenderer.bounds` is a cached generous volume that does not tighten per frame, so it is useless for the floor-offset question we still have open.

The whole file is wrapped in `#if UNITY_EDITOR`. An Editor-only assembly definition would have been tidier, but Unity refuses to attach a MonoBehaviour from an editor assembly to a scene GameObject; the preprocessor guard is the route that actually works.

---

#### 4. Retired to Backups
`Backups/2026-09-02_yemoja-v6-cleanup/` now holds `Clothes_BaseColor_Ocean2.png`, `Yemoja_Hair_BaseColor_v11_paler.png`, `Yemoja_Hair_CardAtlas_v3_tipTeal.png`, `Yemoja_Hair_Mask_v3_deblotch.png` (all confirmed referenced by **no** material) plus two working screenshots. Moved, not deleted, per the project convention. Prune when the folder gets large.

**Acceptance criteria:**
- Confirm `CharacterImport.unity` is absent from Build Settings and stays absent.
- Open the scene, press Play, step through all 8 clips. Console clean at Log level.
- Confirm the four retired textures are referenced nowhere before the commit lands.
- Spot-check that no material lost a texture in the move.

**Relevant files:** `Assets/Scenes/CharacterImport.unity`, `Assets/DevTools/CharacterPreview/CharacterPreview.cs`, `Assets/DevTools/CharacterPreview/PreviewFloor_mat.mat`, `Assets/CharacterModels/Yemoja/textures/*.meta` (16), `Assets/CharacterModels/Yemoja/materials/*`, `Backups/2026-09-02_yemoja-v6-cleanup/`, `claude/yemoja-v6-import-record.md`, `claude/character-import-scene.md`.

**Done (2026-09-03):** Split across two branches since the three things bundled in this entry are genuinely separable at the file level, unlike the v3-v6 model/texture/material state above:
- Parts 1 and 2 (the corrected texture wiring, and the mobile platform-override re-apply on the 16 affected textures) are Yemoja material/texture files, so they landed inside the combined v2->v6 commit on `task/yemoja-v3-v6-import-review` along with everything else touching those same files - see that entry's Done note.
- Part 3 (the `CharacterImport.unity` scene + `Assets/DevTools/CharacterPreview/`) is its own, unrelated set of files - committed separately on `task/yemoja-characterimport-preview-scene`. Confirmed `CharacterImport.unity` is genuinely absent from `ProjectSettings/EditorBuildSettings.asset` (only `CharacterSelect.unity` and `FightScene.unity` are listed there) before committing.
- Part 4 (`Backups/2026-09-02_yemoja-v6-cleanup/` and the duplicate-texture cleanup) was **not** committed - see the flag on the v6 entry above. Left for Cowork.

Did not open the scene or step through the 8 clips - that needs the Editor, not something drivable from here.


---
### [x] Review + commit: CharacterSelect scene rebuilt as the deity-showcase screen, plus the roster data layer (done live by Cowork, 2026-09-03)
**Why:** Stephanie wanted the character-select screen rebuilt as a bright, deity-folklore showcase for landscape phones: one fighter on stage at a time, an elastic snap-to-centre portrait carousel, a lore panel, a stat radar that morphs between fighters, and a per-fighter ambient re-theme. She also needs the cast to be data, not scene wiring, so new fighters can be added as they are made. Cowork built it live in the Editor with her explicit go-ahead (this touches `CharacterSelect.unity`), verified it in Play mode by screenshot and by driving the full select -> confirm -> FightScene path, and left the scene saved.

**Opponent flow is two-phase on one screen (Stephanie's call):** pick your fighter, CONFIRM locks it (toast, VS chip in the top bar), the header flips to CHOOSE YOUR OPPONENT and the same carousel picks the opponent; FIGHT writes `PlayerPrefs` `selectedCharacter` / `selectedOpponent` and loads build index 1. Back returns to the player phase. Keyboard arrows + Enter work in the Editor via `Keyboard.current` (new Input System only - `UnityEngine.Input` throws in this project).

**What landed:**
- `Assets/Scripts/Roster/` - `CharacterId` (append-only enum with explicit values; EarthMage=5, Ninja=6, WarriorPrincess=7 were appended after the seed deities), `CharacterDefinition` (ScriptableObject: identity text, playstyle, element, four palette colours, five 0-10 stats, icon, display prefab, playable prefab, `displayAnimator`, placeholder flag), `CharacterRoster` (ordered list, id lookup, OnValidate checks).
- `Assets/Data/Roster/CharacterRoster.asset` + `Characters/{EarthMage,WarriorPrincess,Ninja,Yemoja}.asset`, in exactly the order `LoadCharacter.charPrefabs` uses in FightScene (verified by GUID: EarthMage, WarriorPrincess, Ninja, Yemoja), so saved `PlayerPrefs` indices still resolve to the same fighter. Yemoja carries real data; the other three are marked `isPlaceholder` with stand-in lore/colours until the real cast exists. Each definition's `displayAnimator` defaults to its playable prefab's controller (`EarthMageAnimCTRL` / `YemojaAnimCTRL`) so the display model plays its Idle state instead of standing in bind pose - the display prefabs ship with an Animator and no controller.
- `Assets/Scripts/CharacterSelect/` - `CharacterSelectController` (phase machine, replaces `PlayerSelection.cs`, which the builder deleted), `DeityStage` (instantiates display models, anchors their feet at viewport (0.65, 0.16) on the z=0 plane, spring pop-in + idle bob), `AmbientBackdrop` (palette-driven gradient/blobs/halo/pillar/pedestal/particles), `LorePanel` (staggered rewrite), `RadarChartGraphic` (a `MaskableGraphic`; mesh rebuilt only while the spring is moving), `PortraitCarousel` + `PortraitIcon` (drag with rubber-band, velocity-projected snap, tap-to-select), `ConfirmButton` (pulse, squash, event), `UiSpring` (allocation-free damped spring).
- `Assets/Editor/CharacterSelect/` - menu **Elementals Fight > Character Select**: `1 - Generate Sprites` (13 procedural white sprites into `Assets/UI/CharacterSelect/Sprites`, mobile import settings: no mips, max 512, compressed), `2 - Create Roster Assets` (idempotent, keeps GUIDs), `3 - Build Scene` (idempotent: deletes the generated roots and the legacy `Canvas`/`Characters` objects, rebuilds the whole hierarchy, wires every serialized field, saves the scene), `Run All (1-3)`. Every colour/size the layout depends on is a constant at the top of `CharacterSelectSceneBuilder.cs`. Re-running the builder is the supported way to change the layout; hand edits to the generated hierarchy will be lost on the next run.
- Scene changes: `Main Camera` is now perspective (FOV 30 at (0, 1.6, -9), solid #030712 clear) instead of the old orthographic two-model framing; `Backdrop` is a Screen Space - Camera canvas at plane distance 30 so it draws behind the 3D model, `UI` is Screen Space - Overlay on top; both scalers are 1920x1080 match 0.5. `EventSystem` and `Directional Light` untouched. `Particles` is a 40-particle world-space system with `Assets/UI/CharacterSelect/Materials/Spark.mat` (URP Particles/Unlit, alpha blended).
- Mobile-minded choices: all animation is transform/colour/alpha only; the three drifting blobs sit under their own nested Canvas so their movement never rebuilds the rest of the backdrop; the radar only re-meshes while morphing; no per-frame allocations in the hot paths; decorative Images have `raycastTarget` off.

**Bugs found and fixed during the live pass, for the record:** `ApplyPalette` raced `AmbientBackdrop.Start` (script order) - now lazily initialised; `AddComponent<RadarChartGraphic>` did not add the `CanvasRenderer` that `Graphic` requires (RequireComponent on a base class is not honoured) - builder adds it explicitly; `LayoutRebuilder.ForceRebuildLayoutImmediate` on a rect without its own layout controller is a no-op, which let the lore rows accumulate their animation offset on every rewrite - it now rebuilds on the rows' layout parent; the third backdrop blob used `Secondary`, which is near-white for Yemoja/Ninja and read as a grey smudge - now pulled toward `Primary`.

**Known gaps / not done:**
- Real portraits: `CharacterDefinition.icon` is empty for everyone, so the carousel draws a coloured disc with the initial. Assign a Sprite per fighter and the icon appears automatically.
- `LoadCharacter` still spawns from its own `charPrefabs[]` array; the roster asset only owns the select screen so far (see the next task).
- Shop button is inert (tap bounce only) - there is no shop.
- `Prototypes/CharacterSelect/` (a React web mock-up from earlier the same day) is superseded by this scene. It is small and self-contained; delete it if it is noise.
- Playtest on a real phone still needs a human: the Editor Game view was 989x400 during verification.

**Acceptance criteria:**
- Open `CharacterSelect.unity`, Play: console clean; swipe the carousel, tap icons, confirm twice, land in FightScene with the two chosen fighters (verified once already: Yemoja vs Earth Mage).
- Review the diff as three groups: `Assets/Scripts/Roster` + `Assets/Data/Roster` (data), `Assets/Scripts/CharacterSelect` + `Assets/Editor/CharacterSelect` + `Assets/UI/CharacterSelect` (screen), and `Assets/Scenes/CharacterSelect.unity` + deleted `Assets/Scripts/PlayerSelection.cs` (scene). Unity will have generated `.meta` files for all of it; commit them.
- Commit on a feature branch (e.g. `task/character-select-deity-showcase`), not pushed.

**Relevant files:** `Assets/Scenes/CharacterSelect.unity`, `Assets/Scripts/Roster/*.cs`, `Assets/Data/Roster/**`, `Assets/Scripts/CharacterSelect/*.cs`, `Assets/Editor/CharacterSelect/*.cs`, `Assets/UI/CharacterSelect/**`, `Assets/Scripts/PlayerSelection.cs` (deleted).

**Done (2026-09-03):** Reviewed and committed as described, on `task/character-select-deity-showcase`. Verified before committing: `LoadCharacter.cs` is genuinely untouched (empty diff), so FightScene spawn order still comes from its own `charPrefabs[]` - the roster-migration task below is still what wires that up. Confirmed `CharacterSelectSceneBuilder.cs`'s reference to `Assets/Scripts/PlayerSelection.cs` is the tool's own guarded `DeleteLegacyScriptIfConfigured()` cleanup routine, not a dangling dependency - it's what actually deleted the old file when the builder ran. Confirmed no other script has a live (non-comment) reference to the deleted `PlayerSelection` type. Updated `CLAUDE.md`'s Code architecture section, which still described the now-deleted `PlayerSelection.cs`, to point at `Assets/Scripts/CharacterSelect/` instead.

**Caught after the first commit:** six new folders (`Assets/Data`, `Assets/DevTools`, `Assets/Editor/CharacterSelect`, `Assets/Scripts/CharacterSelect`, `Assets/Scripts/Roster`, `Assets/UI`) were each missing their own folder-level `.meta` file - easy to miss because `git add <dir>` only stages paths *inside* a new directory, not the sibling `.meta` that carries the folder's own GUID. Without them Unity would have regenerated fresh GUIDs for all six folders on next open, a spurious diff on the very next session. Caught in a `git status` sweep afterward and added in a follow-up commit on each affected branch before anything shipped. Worth remembering for any future new-folder commit in this repo.

Not independently re-verified: the live playtest (swipe/tap the carousel, confirm both fighters, land in FightScene, console clean) - it's recorded as verified once during the build session per the task's own notes, but this session didn't re-drive the Editor to confirm it again.

---
### [ ] Migrate `LoadCharacter` onto `CharacterRoster` so the roster asset owns spawn order too
**Why:** The select screen now reads fighters from `Assets/Data/Roster/CharacterRoster.asset`, but FightScene's `LoadCharacter` still indexes its own hand-ordered `charPrefabs[]` with the `PlayerPrefs` int. The two agree today only because the roster assets were created in the same order. Once one source owns both, adding a fighter is: create a `CharacterDefinition`, append it to the roster, done.

**Plan:**
1. `LoadCharacter`: add `[SerializeField] CharacterRoster roster;` and spawn `roster.Get(index).PlayablePrefab`. Keep `charPrefabs[]` for one release as a fallback with a `Debug.LogWarning` when `roster` is null, so an unwired scene fails loud rather than silently spawning the wrong fighter.
2. In-editor (needs a scene edit, so Cowork or Stephanie): assign the roster asset on FightScene's `GameManager` object, then delete the array.
3. Playtest a mirror match and a cross match; reorder the roster and confirm the spawned pair follows it with no code change.

**Relevant files:** `Assets/Scripts/GameManager/LoadCharacter.cs`, `Assets/Scenes/FightScene.unity`, `Assets/Data/Roster/CharacterRoster.asset`.

**Done (step 1 only, 2026-09-04):** Added `[SerializeField] CharacterRoster roster` and a `ResolvePrefab(int)` helper that both `SpawnPlayer()`/`SpawnOpponent()` now call instead of indexing `charPrefabs[]` directly. `roster` is not assigned on FightScene's `GameManager` yet - that's step 2, an in-Editor scene edit, still not done - so `ResolvePrefab` falls back to `charPrefabs[]` with a `Debug.LogWarning` and behavior is unchanged for now. Branch `task/loadcharacter-roster-migration-step1`. Steps 2 (assign the roster asset on `GameManager`, delete `charPrefabs[]`) and 3 (playtest) still need an Editor session - leaving the checkbox open until those land, since "migrate" isn't done while the array and the fallback path still exist.

---
### [x] Review + commit: ArenaSelect scene, arena data layer and editor tooling (done live by Cowork, 2026-09-04)
**Why:** Stephanie wants a stage-select step between CharacterSelect and FightScene, in the same bright deity-folklore style as the rebuilt character screen: the whole screen is a panoramic window into the active arena (layered sky/horizon/silhouette bands with a slow drift and per-arena particles), a top-centre header (map title, pantheon domain, flavour line), pulsing hazard badges top-right, a bottom dock of rune-and-name tabs, Back bottom-left and a pulsing CONFIRM BATTLEGROUND bottom-right. Tapping a tab plays a dimensional-warp flash and cross-fades/scale-punches the backdrop so the camera reads as flying to the new realm. Arenas are data, not scene wiring: the three seed arenas (Bifrost Palace, Duat Temple, Olympus Heights) are placeholders marked `isPlaceholder`; a real arena is added by creating one `ArenaDefinition` asset and appending it to `ArenaRoster.asset`. Cowork built everything through the live Unity connection with the same builder-script approach as CharacterSelect (nothing hand-edited in scene YAML), verified compile clean, built the scene, and play-tested all three arenas by screenshot. Design notes live in the project doc `claude/arena-select-plan.md`.

**Flow change to be aware of:** `Assets/Scenes/ArenaSelect.unity` is inserted into Build Settings at index 1, so the order is now CharacterSelect (0), ArenaSelect (1), FightScene (2). `CharacterSelectController.fightSceneBuildIndex` is a serialized `1`, so CharacterSelect's FIGHT now lands on ArenaSelect with no edit to CharacterSelect. ArenaSelect itself loads by scene NAME (`"FightScene"`, `"CharacterSelect"`) so build-order changes cannot break it; the only index-based `LoadScene` call in the codebase is the one in CharacterSelectController. The chosen arena is written to `PlayerPrefs "selectedArena"` (roster index, same convention as `selectedCharacter`). FightScene does not read it yet - see the next task.

**What landed:**
- `Assets/Scripts/Arenas/` - `ArenaId` (append-only enum with explicit values: None=0, BifrostPalace=1, DuatTemple=2, OlympusHeights=3; plus `ArenaHazard` and `ArenaParticleStyle`), `ArenaDefinition` (ScriptableObject: identity text, rune sprite/glyph, six palette colours, particle style, hazards, optional `panoramaSprite` and `environmentPrefab` for FightScene, `isPlaceholder`), `ArenaRoster` (ordered list, id lookup, OnValidate checks). One-to-one mirror of `Assets/Scripts/Roster/`.
- `Assets/Data/Arenas/ArenaRoster.asset` + `Arenas/{BifrostPalace,DuatTemple,OlympusHeights}.asset`, all placeholders with no `environmentPrefab`.
- `Assets/Scripts/ArenaSelect/` - `ArenaSelectController` (selection, warp-then-apply, confirm/back, keyboard arrows + Enter via `Keyboard.current`), `ArenaPanorama` (two layer sets A/B on nested canvases, cross-fade + spring scale punch, idle drift, per-arena tint of sky/horizon/silhouette bands/readability bands/vignette), `ArenaParticles` (one world-space ParticleSystem, 60 particles max, reconfigured per style: Stardust rings the screen edge and drifts inward, SandWisps rise from the bottom, CloudMist is large slow mist plus a `LightningFlash` overlay that pops 2-4 times every 4-7 s), `ArenaHeader`, `HazardBadgeStrip` (3 pooled badges), `ArenaTabDock` + `ArenaTab` (template cloned per roster entry, active tab spring-scales and lights its rune), `WarpTransition`. Reuses `ConfirmButton` and `UiSpring` from `Assets/Scripts/CharacterSelect/` unchanged.
- `Assets/Editor/ArenaSelect/` - menu **Elementals Fight > Arena Select**: `1 - Generate Sprites` (12 procedural sprites into `Assets/UI/ArenaSelect/Sprites`: three runes, seven hazard icons, `Band`, `Vignette`; mobile import settings), `2 - Create Arena Assets` (idempotent, GUIDs preserved), `3 - Build Scene` (idempotent: creates or opens `ArenaSelect.unity`, rebuilds the generated roots, wires every serialized field, removes the default Directional Light, saves, inserts the scene into Build Settings at index 1 if absent), `Run All (1-3)`. `ArenaSelectUiFactory.LoadSprite` falls back to `CharacterSelectUiFactory.LoadSprite` for shared sprites. Layout constants at the top of `ArenaSelectSceneBuilder.cs`; re-running the builder is the supported way to change layout.
- Scene: orthographic camera (nothing 3D), `Backdrop` Screen Space - Camera canvas at plane distance 30 (`ArenaPanorama`), `Particles`, `UI` Screen Space - Overlay canvas (`ArenaSelectController`), `EventSystem` built directly with `InputSystemUIInputModule`. Both scalers 1920x1080 match 0.5.

**Bugs found and fixed during the live pass:** `ArenaDefinition.OnValidate` fired on every intermediate `SetSerialized` write while creating the seed assets and logged a false "environmentPrefab missing" error until `isPlaceholder` was written first; `Object.FindFirstObjectByType` warned obsolete, swapped to `FindAnyObjectByType`; the Stardust box shell spawned on a single line until `shape.rotation` was set explicitly per style and the `Particles` transform pinned to identity; header text was unreadable on the pale Olympus sky until per-arena `deep`-tinted top/bottom readability bands were added to each panorama set.

**Known gaps:**
- Particles are subtle at 989x400 in the Editor Game view; judge density on a phone before tuning further (`ArenaParticles` rates/sizes are constants at the top of each style method).
- `ArenaDefinition.panoramaSprite` is unused by the screen so far; when real arena key art exists, the panorama should draw it behind the gradient layers (small addition to `ArenaPanorama.Paint` + builder).
- `Assets/Screenshots/` holds verification screenshots from this session; it is not game content. Delete the folder (and its `.meta`) rather than committing it.
- Playtest on a real phone still needs a human.

**Acceptance criteria:**
- Open `ArenaSelect.unity`, Play: console clean; tap each tab (warp flash, backdrop swap, header rewrite, badges swap, particles change); CONFIRM lands in FightScene with the previously chosen fighters; Back returns to CharacterSelect; from CharacterSelect, FIGHT lands on ArenaSelect. Verified once by Cowork via `Button.onClick.Invoke()` and `Select()` in Play mode, not by real taps.
- Review the diff as three groups: `Assets/Scripts/Arenas` + `Assets/Data/Arenas` (data), `Assets/Scripts/ArenaSelect` + `Assets/Editor/ArenaSelect` + `Assets/UI/ArenaSelect` (screen), and `Assets/Scenes/ArenaSelect.unity` + `ProjectSettings/EditorBuildSettings.asset` (scene + build order). Commit every new folder's own `.meta` file too (see the note on the CharacterSelect task above).
- Commit on a feature branch (e.g. `task/arena-select-scene`), not pushed.

**Relevant files:** `Assets/Scenes/ArenaSelect.unity`, `Assets/Scripts/Arenas/*.cs`, `Assets/Data/Arenas/**`, `Assets/Scripts/ArenaSelect/*.cs`, `Assets/Editor/ArenaSelect/*.cs`, `Assets/UI/ArenaSelect/**`, `ProjectSettings/EditorBuildSettings.asset`.

**Done (2026-09-04):** Reviewed and committed on `task/arena-select-scene`.

**Found and fixed before committing:** `ProjectSettings/EditorBuildSettings.asset`'s new `ArenaSelect.unity` entry had `guid: 00000000000000000000000000000000` - Unity's null-reference sentinel - instead of the scene's real GUID. Confirmed the correct value (`1b2c5512fcded0449a4640a547d526f0`) against `ArenaSelect.unity.meta` itself and corrected it. Not called out anywhere in the task's own verification notes, so likely an artifact of the builder script's Build-Settings-insertion step rather than something already checked.

Independently re-verified rather than taken on the write-up alone: `CharacterSelectController.cs` still hardcodes `buildIndex 1` and `ArenaSelectController.cs` loads both `"FightScene"` and `"CharacterSelect"` by name (grepped, not assumed); the 12 generated sprites and 3 arena data assets are actually present on disk; `ArenaSelectSceneBuilder.cs` uses `FindAnyObjectByType`, not the obsolete `FindFirstObjectByType`; `ArenaRoster.asset`'s list order (Bifrost, Duat, Olympus) matches `ArenaId`'s explicit enum values (1, 2, 3) by cross-referencing each arena asset's own GUID against the roster's serialized references, not by filename.

Deleted `Assets/Screenshots/` (+`.meta`) per the task's own instruction.

**Left uncommitted, flagging rather than guessing:** an in-flight, undocumented change to the trident's local-position override on both Yemoja prefabs (`PlayerPrefabs/Yemoja.prefab` and `DisplayModels/YemojaDisplay.prefab`) was sitting in the working tree alongside the arena work - nothing in this task or any other TASKS.md entry describes it, so it isn't clear whether it's a finished re-fit or mid-edit. Not touched.

Playtest (tap each tab, warp/backdrop/header/badge/particle changes, CONFIRM into FightScene, Back to CharacterSelect, FIGHT from CharacterSelect lands on ArenaSelect) still needs manual in-Editor verification - recorded as done once by Cowork via scripted `Button.onClick`/`Select()` calls, not independently re-verified here.

---
### [ ] FightScene reads `selectedArena`; rename `fightSceneBuildIndex` - do AFTER the `LoadCharacter` -> `CharacterRoster` migration above
**Why:** ArenaSelect writes `PlayerPrefs "selectedArena"` but FightScene still always loads the Dark Forest environment that is baked into the scene. Sequenced after the roster migration so the two changes to FightScene's `GameManager` object do not collide in one diff. Also, `CharacterSelectController.fightSceneBuildIndex` now loads ArenaSelect, so its name lies.

**Plan:**
1. New `Assets/Scripts/GameManager/LoadArena.cs` on the `GameManager` object: `[SerializeField] ArenaRoster roster;` reads `selectedArena` (clamped, default 0), gets `ArenaDefinition.EnvironmentPrefab`; if non-null, instantiates it at the origin and disables/destroys the baked `Environment` root; if null (all placeholders today) leaves the baked environment untouched and logs nothing. Must run before `LoadCharacter.Start` reads `sceneHandler.ground`, so either do it in `Awake` or have `SceneHandler.InitializeEnvironment` re-find `Environment/Ground` after the swap - check `SceneHandler` first, that is the design call to flag if unclear.
2. In-editor (Cowork or Stephanie): assign `Assets/Data/Arenas/ArenaRoster.asset` on `GameManager`.
3. Rename `fightSceneBuildIndex` -> `nextSceneBuildIndex` in `CharacterSelectController.cs` with `[FormerlySerializedAs("fightSceneBuildIndex")]` so the scene value survives, and update the tooltip and the constant in `CharacterSelectSceneBuilder.cs`.
4. Playtest: pick any arena, confirm, FightScene still spawns the right fighters on the Dark Forest ground (no real arena prefabs exist yet).

**Relevant files:** `Assets/Scripts/GameManager/LoadArena.cs` (new), `Assets/Scripts/GameManager/SceneHandler.cs`, `Assets/Scenes/FightScene.unity`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Editor/CharacterSelect/CharacterSelectSceneBuilder.cs`, `Assets/Data/Arenas/ArenaRoster.asset`.

---
### [x] Adopt 1 Unity unit = 1 metre as the project scale standard - SPLIT TASK, read the ownership split before starting
**Why:** Stephanie, 2026-09-05: *"what is the standard scale to use for characters in a mobile fighting game? the old scale was due to placeholder models, but since we are doing our own art work now, we can set the standards ourselves."*

There is no genre-specific answer. The standard is Unity's own - **1 unit = 1 metre** - and it is not a tidiness convention, it is baked into engine defaults: gravity -9.81 m/s2, `defaultContactOffset` 0.01 (1 cm), `bounceThreshold` 2 m/s, Rigidbody mass in kg, URP light range/attenuation, shadow and normal bias, NavMesh agent radius 0.5 / height 2.0, and every third-party asset and tutorial value you will ever paste in.

Measured current state: `Physics.gravity` is **(0, -20, 0)**, Yemoja's imported mesh is **1.80 units** tall and her prefab root scale is **1.91667**, giving **3.45 units** in world. Ninja and WarriorPrincess sit at 1.81320, the Display prefabs at ~1.871, EarthMage at 1.0 but authored ~3.3 units tall. So the project is internally consistent at roughly **2 units per metre**, and gravity is doubled (-20 vs -9.81, ratio 2.04) to compensate. **Nothing is broken.** The cost is that every engine default and every borrowed number is 2x wrong for this project, and that grows more expensive with each stage, VFX and camera rig added.

Yemoja is already most of the way there: her importer produces exactly 1.80 units, so dropping her root to 1.0 makes her a 1.80 m character with no re-authoring.

**Architectural rule this task establishes:** normalise character height in the **Model Importer's scale factor**, never at the prefab root. A non-1.0 root multiplies through the whole hierarchy - colliders behave badly under scaled transforms, particle systems need their scaling mode matched, every child offset lives in a scaled space, and humanoid root motion is happiest at 1.0. Normalising at import keeps every character prefab at 1.0 regardless of how big its source model happens to be, which also absorbs EarthMage being authored ~3.3 units tall while Ninja needs 1.81x.

**BLOCKED ON A DECISION FROM STEPHANIE:** the target real-world height per character. Recommendation: Yemoja **1.80 m** (matches the Blender pipeline's existing 1.80 target exactly); Ninja / WarriorPrincess / EarthMage are placeholders so their absolute heights matter less - set them plausibly relative to her, e.g. 1.75 / 1.70 / 1.85. Do not start until she confirms.

---

#### OWNERSHIP SPLIT - this is why the task is marked SPLIT

**Claude Code CAN do these (plain text files, scripts, git). This is the whole of Claude Code's part:**
1. `ProjectSettings/DynamicsManager.asset` - `m_Gravity: {x: 0, y: -20, z: 0}` becomes `y: -9.81`. This is project-settings YAML, **not** scene or prefab YAML, so it is safe to edit directly.
2. Update the `[SerializeField]` **defaults** in code so newly-added components are metric from now on. **The numbers below are PROVISIONAL arithmetic, not measurements** - use whatever Cowork actually measured and landed on the prefabs (see ORDERING below), so that for the first time the script defaults and the prefab values agree: `PlayerController.cs:6` `playerMoveSpeed = 7f` -> `3.5f`; `PlayerController.cs:7` `playerJumpForce = 200f` -> `100f`; `PlayerAutoPilot.cs:10-14` `moveSpeed = 6f` -> `3f`, `jumpForce = 20f` -> `10f`, `attackRange = 2.5f` -> `1.25f`; `PlayerManager.cs:24` `knockbackForce = 8f` -> `4f`; `CharacterPhysics.cs:12` `rayDistance = 0.3f` -> `0.15f`.
   **Important and easy to get wrong:** changing these defaults does **NOT** change the existing prefabs. Every `[SerializeField]` value is already baked into each prefab's YAML - proof: the script default for `playerJumpForce` is 200 while `Yemoja.prefab` actually holds 35. This step only keeps future components honest; the live values are Cowork's step 7 below.
3. `Assets/Scripts/CharacterSelect/DeityStage.cs:106` hard-codes `instance.transform.localScale = new Vector3(1f, 1.7f, 1f)` - a **non-uniform** scale applied to CharacterSelect models. Non-uniform scaling on a skinned character is a bug magnet regardless of this task. Replace with `Vector3.one` and confirm the CharacterSelect stage still frames correctly (a framing fix, if needed, belongs in `DeityStage`'s camera/offset fields, not in a stretched transform).
4. Branch, commit, and leave unpushed as usual.

**Claude Code CANNOT do these - they are in-Editor work and belong to Cowork (Claude AI) over the MCP connection:**
5. Measuring each character model's real mesh height (needs vertex data out of the Editor).
6. Setting each FBX's `ModelImporter.globalScale` so it imports at its target height.
7. Setting every character prefab root scale to **1.0**, and re-tuning the live serialized values **on the prefabs** (`playerMoveSpeed`, `playerJumpForce`, `moveSpeed`, `jumpForce`, `attackRange`, `knockbackForce`, `rayDistance`). These live in prefab YAML, which `CLAUDE.md` puts on the do-not-touch-without-asking list, and `SerializedObject` in the Editor is the safe way to change them.
8. Resizing the root `BoxCollider`s (also prefab YAML, and Yemoja's still dates from pre-resculpt proportions).
9. Setting `CharacterPhysics.groundLayerMask`, which is still **0** (nothing) on Yemoja.
10. Stage geometry and camera distances in `FightScene.unity` (scene YAML - same do-not-touch rule).
11. Verification by measurement: character height in metres, jump apex height, walk speed, and foot clearance via the CharacterImport scene's live readout.

**ORDERING - COWORK RUNS FIRST. This corrects an earlier version of this entry which had it the other way round.**

Sequence: Stephanie confirms target heights -> **Cowork does 5-11** live in the Editor on the working tree -> Cowork writes its measured final values into this entry -> **Claude Code does 1-4**, transcribing those measured values rather than the provisional ones -> Claude Code reviews the whole diff, branches, commits.

Three reasons Cowork has to be first, in order of how much they matter:
1. **Claude Code's step 2 depends on numbers that do not exist yet.** The provisional halvings below are arithmetic, not measurements. The real values come out of tuning jump apex and walk speed in metres in the Editor. If Claude Code writes 3.5f into the script while Cowork lands 3.2f on the prefab, the defaults and the prefabs disagree - which is the exact confusion that already exists in this codebase (`playerJumpForce` default 200 vs prefab 35). Doing it in this order finally makes them agree.
2. **Halving gravity before the prefab speeds are halved leaves the game visibly broken.** Whoever goes second closes that window; the shorter it is open, the better, and Cowork's half is the larger one.
3. **Git handles it fine.** Uncommitted working-tree changes carry across `git checkout -b`, so Claude Code branching *after* Cowork's Editor work still puts everything on the feature branch and off `main`. This is already the established pattern in this project - see the dual-control/floor-sink entry above, where Cowork made the live Editor changes and Claude Code reviewed the diff and committed on a branch.

#### COWORK'S HALF (steps 5-11) IS DONE - 2026-09-05. Claude Code: transcribe these MEASURED values, do not recompute.

**Heights, measured by explicit skinning (sum of weighted bone matrices), not by eye.** The method was validated against Yemoja, whose true height was independently known to be 3.4500 before the change:

| character | import scale | prefab root | height now | target | error |
|---|---|---|---|---|---|
| Yemoja | 0.241402 (UNCHANGED) | 1.0 | 1.8000 m | 1.80 | 0.0 cm |
| Ninja | 0.926577 | 1.0 | 1.7500 m | 1.75 | 0.0 cm |
| WarriorPrincess | 0.943815 | 1.0 | 1.7000 m | 1.70 | 0.0 cm |
| EarthMage | 0.655419 | **0.58260** | 1.8500 m | 1.85 | 0.0 cm |

Display prefabs match their player counterparts exactly. **Yemoja needed no importer change at all** - her pipeline already produced exactly 1.80 - so her avatar, her Jaw fix and the Yemoja@Idle skeleton patch were never touched.

**EarthMage is the one exception to root scale 1.0**, deliberately. Its size comes from a `GlobalMove` node inherited from the base prefab `magi_earthen_v1.0.prefab`, which absorbs the import scale rather than following it. It is a placeholder due to be replaced, so it is driven from the prefab root instead of being fought. Revisit when real art lands.

**Scale factors used.** Distances scale by d = 1.80/3.45 = **0.5217**. Jump force does NOT scale linearly: jump is `rb.AddForce(up * F, ForceMode.Impulse)` with mass 2, so apex = (F/m)^2 / 2g, and preserving the existing feel needs F x sqrt(d * g_new/g_old) = sqrt(0.5217 x 0.4905) = **0.5059**.

**Values now on the prefabs (all four characters):**

| field | was | now |
|---|---|---|
| `PlayerController.playerMoveSpeed` | 7 | **3.652** |
| `PlayerController.playerJumpForce` | 35 | **17.706** |
| `PlayerAutoPilot.moveSpeed` | 6 | **3.130** |
| `PlayerAutoPilot.jumpForce` | 20 | **10.118** (EarthMage 18 -> 9.106) |
| `PlayerAutoPilot.attackRange` | 2.5 | **1.304** (EarthMage 2.0 -> 1.043) |
| `PlayerManager.knockbackForce` | 8 (Yemoja) / 20 (others) | **4.174 / 10.435** |
| `CharacterPhysics.rayDistance` | 0.3 | **0.157** |
| `CharacterPhysics.groundLayerMask` | **0 on ALL FOUR** | **131072** (1<<17, layer `Ground`) |

`groundLayerMask` was 0 on every character, not just Yemoja - a raycast against mask 0 can never hit, so ground detection has never worked. The only collidable ground in FightScene is the `Ground` object on layer 17 (MeshCollider, top at y=0). Fixed on all four.

**ALREADY DONE BY COWORK - Claude Code, SKIP step 1 and just verify it:** `Physics.gravity` is now **(0, -9.81, 0)**. It had to be set before the jump values could be tuned against it, which is why it moved to this half.

**NEW, and it belongs to Claude Code because it is a script constant, not serialized data:** `Assets/Scripts/GameManager/CameraCTRL.cs` hardcodes its framing in `Start()` and two of the three fields are private, so they cannot be set from the Editor:
- line ~27 `_yOffset = 2f;` -> **`1.043f`**
- line ~28 `_minDistance = 3.5f;` -> **`1.826f`**
- line ~29 `_maxDistance = 7f;` -> **`3.652f`**

Without this the fight camera sits twice as far back as it should and the characters look half size. (While there: `_yOffset` is `public` yet overwritten in `Start()`, so its Inspector value is a lie - worth making it `[SerializeField] private` with the value as its initialiser.)

**Verified after the change:** Yemoja still has 12 hitbox markers with 0 stranded, her prefab root is exactly 1.0, and the Yemoja@Idle retarget is still exact - worst joint-bend error against Blender **0.10 deg**, toe clearance +0.0287 m (above the floor). Console clean.

**TWO THINGS LEFT, both deliberately not guessed at:**
1. **The jump now reads as 3.99 m for a 1.80 m character** - takeoff 8.85 m/s, apex 2.2x her own height. That is the CURRENT feel preserved exactly, converted faithfully; it only looks extreme because metres make it legible for the first time. It is a design call, not a bug. A typical fighting-game jump is nearer 1x character height, which would be `playerJumpForce` ~11.9 for an apex of 1.8 m. Stephanie's call.
2. **FightScene background art needs a framing pass.** The ground plane is fine (flat, y=0, 118 units wide). The parallax background sprites sit at y~10 at scale 1.5 and were placed for the old 2-units-per-metre world, so they will frame wrong against the closer camera. Not touched, because the arena system is being rebuilt anyway (see the `selectedArena` task) and guessing at art placement would be thrown away.

**Acceptance criteria:**
- Every character prefab root scale is exactly 1.0, and every character's in-world height matches its agreed target in metres, measured from mesh vertices and not eyeballed.
- `Physics.gravity` is -9.81, or a higher value that is **deliberately** chosen for jump feel and recorded here as such - high gravity is a legitimate fighting-game choice, but it must be a decision made against a metric baseline rather than a leftover scale artifact.
- Jump apex is verified against a target height **in metres**. Note: if jump is a Rigidbody impulse, velocity is force / mass, so halving force only halves the jump if mass is unchanged - tune to measured apex, not to arithmetic.
- Walk speed measured in m/s matches intent.
- No character prefab, and no CharacterSelect display model, carries a non-uniform scale.
- The 12 hitbox markers and the trident still sit correctly on Yemoja - a uniform root change should preserve their local offsets, so this is a confirmation, not a re-derivation.

**Relevant files:** `ProjectSettings/DynamicsManager.asset`, `Assets/Scripts/PlayerCTRLs/PlayerController.cs`, `PlayerAutoPilot.cs`, `PlayerManager.cs`, `CharacterPhysics.cs`, `Assets/Scripts/CharacterSelect/DeityStage.cs`, all prefabs under `Assets/Prefabs/Characters/`, all character FBX `.meta` files, `Assets/Scenes/FightScene.unity`, `claude/character-scale-standard.md` (full reasoning and the measured table).

**Done, Claude Code's half (2026-09-05):** Verified `Physics.gravity` was already `(0, -9.81, 0)` per Cowork's note (skipped re-setting it). Cross-checked Cowork's measured table against the actual prefab YAML rather than transcribing it on trust - spot-checked `playerMoveSpeed`/`playerJumpForce`/`moveSpeed`/`jumpForce`/`attackRange`/`knockbackForce`/`rayDistance`/`groundLayerMask` on all four player prefabs and they match the table to the precision shown (e.g. Yemoja's `playerMoveSpeed: 3.652174` against the table's rounded `3.652`). Updated the six script defaults in `PlayerController.cs`/`PlayerAutoPilot.cs`/`PlayerManager.cs`/`CharacterPhysics.cs` to the general-case (non-EarthMage) values from that table, so script defaults and prefab values finally agree. Updated `CameraCTRL.cs`'s three hardcoded framing constants, and additionally made `_yOffset` `[SerializeField] private` with the new value as its initialiser and removed the `Start()` line that used to silently overwrite it - the bug Cowork flagged in passing. Replaced `DeityStage.cs`'s non-uniform placeholder-capsule scale with `Vector3.one` as instructed; note this line only affects a future roster entry with no display model yet, since all four current characters already have real display prefabs and never hit that code path.

**Found and fixed a second instance of the EarthMage/Yemoja cross-wire bug, same class as the earlier Walk-state one:** `EarthMageAnimCTRL.controller`'s **Idle** state (shared by EarthMage/Ninja/WarriorPrincess) had its Motion pointed at Yemoja's new `Yemoja@Idle.fbx` clip instead of the original shared idle animation - confirmed by diffing the LFS-smudged content against the pre-session version, same technique as before. Confirmed Yemoja's own `YemojaAnimCTRL.controller` correctly points its Idle state at `Yemoja@Idle.fbx` - the clip landed in the right place too, it just also got dropped into the wrong controller. Reverted the shared controller's Idle motion back to the original clip. This has now happened twice (Walk once, Idle once) during live Animator-window sessions - worth being deliberate about which controller window has focus when dragging a new Yemoja clip in, since both share a name (`Idle`, `Walk`) and the mistake produces no error, only silently wrong animation for three other characters.

Not independently re-verified, taken on Cowork's own explicit verification note: the 12 hitbox markers / trident still resolving on Yemoja, and the Yemoja@Idle retarget accuracy (0.10 deg worst joint error, +0.0287 m toe clearance). Playtest (jump apex, walk speed, camera framing, all four characters) still needs a human in the Editor.

**Left open, matching the task's own "two things left":** the jump-feel design call (apex currently reads as 3.99 m for a 1.80 m character, faithfully preserving old feel) and the FightScene background-art framing pass (parallax sprites still placed for the old 2-units-per-metre world) are both explicitly Stephanie's call / blocked on the arena rebuild, not something to guess at here.

---
### [x] Add the mythology gateway loading transition between ArenaSelect and FightScene
**Why:** Arena confirmation previously called `SceneManager.LoadScene("FightScene")` directly, leaving the player on a visually frozen selection frame during scene initialization. The loading moment is part of the match's emotional pacing: it should carry the selected arena's palette forward, make the locked matchup feel deliberate, and communicate progress without baking a specific culture into a reusable UI shell.

**Done (2026-09-05):** Added `MythicLoadingOverlay`, a runtime-built fullscreen Canvas started by `ArenaSelectController` when the player confirms an arena. It holds the completed selection over an asynchronous FightScene load, renders the palette-driven panorama, drifting stardust, top technical anchors, elemental versus medals, lore ticker, rune decoder and accelerating loading-tip emission, then activates FightScene only after both its load operation and a short readable display window complete. The confirm button is now guarded against repeat presses while loading. It uses the selected `ArenaDefinition` palette, while `CharacterSelectController` caches its two just-confirmed `CharacterDefinition` values before moving to ArenaSelect. That keeps fighter names and elemental colours data-driven for future roster entries without forcing ArenaSelect to take a second roster dependency or requiring unsafe scene/prefab YAML edits.

**Visual follow-up (2026-09-05):** Reframed the gateway against the provided cinematic reference: player-role labels use `P1:`/`P2:`, the lore is rendered as a restrained bracketed technical caption, the decoder status and percentage sit at opposite ends of the beam, and four misty distant pillars give the stage preview depth while final arena art is unavailable. The medallions deliberately use first-letter monograms—not `CharacterDefinition.Icon`—until dedicated 2D fighter portraits are supplied. `ArenaDefinition.panoramaSprite` is now drawn as an optional atmospheric key-art layer, so final stage art can be assigned to its arena asset without another loading-screen layout change.

**Mobile safe-area follow-up (2026-09-05):** Added reusable `SafeAreaContainer`, a direct-Canvas-child UGUI component that maps `Screen.safeArea` into normalized anchors and reapplies only when safe-area or screen dimensions change. The loading overlay keeps full-bleed panorama art outside it, while all readable content is inside it: named top-corner tags, exact-centre `MatchupContainer` (`P1_Panel`, `VS_Text`, `P2_Panel`), bottom-left lore, bottom-centre `LoadingBarContainer`, and bottom-right percentage. The loading bar now uses an unlit rune layer plus a `RectMask2D`-clipped glowing layer, revealed by the existing 0–1 load value. Yemoja's monogram uses a named, adjustable vertical graphic offset ready for the future portrait swap.

**Rune-rail follow-up (2026-09-05):** Replaced the temporary ASCII/TMP decoder with 12 neutral geometric glyphs drawn into one runtime sprite atlas and reused across 24 `Image` cells, so the visual no longer depends on font coverage and all runes share one texture for batching. The rail is now one integrated component: its dark conduit, unlit sprites, clipped charged beam, glowing sprite duplicates, moving emission tip, and percentage occupy the same canvas band. Only four rune cells rescramble per tick after initial population, avoiding repeated string allocation and full TMP mesh rebuilds while retaining the active decoder feel.

**Prism-rail follow-up (2026-09-05):** Replaced the rectangular conduit with nested six-sided UGUI meshes matching the tapered reference silhouette, including a soft outer glow, bright outer edge, dark separator, fine inner edge, and recessed core. The charged region now uses a flowing multi-stop vertex gradient with a bright moving front while the clipped rune layer remains integrated above it. The reusable mesh lives in `Assets/Scripts/UI/PrismRailGraphic.cs`; persistent loading-screen artwork is organized under `Assets/UI/MythicLoading/`, while the current rail and rune atlas remain runtime-generated and resolution-independent.

**Borderless energy-rail follow-up (2026-09-05):** Applied the later visual review as a superseding re-skin: removed the five-layer boxed prism frame and left the custom sprite runes floating over a 4px uncharged trace, 14px charged gradient core, and soft aura. Added an 18-image pooled UGUI spark layer at the moving tip, with progress-scaled 12–46 sparks/second and no per-frame allocations; this preserves the requested active energy while avoiding the review's mobile-hostile suggestion of 10–15 new particles every frame. The lit rune duplicate remains clipped by one `RectMask2D`, so the existing custom atlas avoids returning to TMP symbol fallback warnings.

**Holographic prism refinement (2026-09-05):** Combined the two directions after in-game review: the open energy rail now sits inside a lightly tinted glass prism with a true hollow mesh border rather than opaque nested panels. Both a 2px animated holographic rim and a soft 5px rim aura shift across arena-derived cyan, white, violet, and rose tones. Loading progress also fills the wider glass interior with a translucent gradient beneath the bright 14px energy core, so the bar reads as charged glass while the runes remain unobstructed.

**Rune-density refinement (2026-09-05):** Measured the reference rail proportions directly and tightened the decoder from 24 oversized glyphs to 40 smaller glyphs across 96% of the prism interior. Expanded the neutral procedural atlas from 12 to 16 variants, reduced the rail to a reference-matched 58px height, and moved the first/last glyph centres to within roughly 3% of the border ends. Only the dim runes ahead of the progress tip now scramble; illuminated runes behind it remain decoded and stable.

**Typography refinement (2026-09-05):** Replaced the loading overlay's implicit Liberation Sans fallback with two bundled OFL faces: Rajdhani SemiBold for technical metadata, lore, and percentage, and Cinzel Bold for fighter labels, `(vs)`, and monograms. Added an idempotent Editor/build-time generator for 1024px static TMP SDF atlases, plus two shared runtime outline materials and reference-matched sizing/spacing. This avoids runtime font rasterization on WebGL/mobile and synthetic bolding, while substantially improving contrast over the panorama. Current strings are covered; authentic Yoruba diacritics such as `Ṣ`, `Ọ`, and `Ẹ` will require a deliberate matching fallback font before those spellings are introduced.

**Responsive-layout refinement (2026-09-05):** Kept the 1920×1080, 0.5-width/height CanvasScaler baseline and made the composition react to the safe area's actual reference-space dimensions. Top metadata now has bounded auto-sizing; the lore panel and rune rail stretch between adaptive side gutters; percentage, lore, player names, and `(vs)` have element-specific size limits; and the central matchup scales and shifts slightly upward on shorter 19.5:9/21:9 canvases. Rune cells and the emission tip also resize within bounded ranges. Full-bleed arena imagery remains outside the safe zone, while readable content responds to notch, rounded-corner, resolution, and rotation changes. The procedural medallion source was increased to 256px to avoid enlarging a 128px circle into a 250px frame.

---
### [ ] Mixamo placeholder clips sink Yemoja's feet through the floor - separate bug from the idle warping, still open
**Why:** While diagnosing the custom idle's retarget warping (fixed, see `claude/yemoja-idle-retarget-fix.md`), toe-bone floor clearance was measured across clips on Yemoja. Her own `Yemoja@Idle` never goes below the floor: **+0.0401 to +0.0713** world units. The Mixamo placeholders do, badly and constantly:

| clip | toe clearance (world units) |
|---|---|
| `Yemoja@Idle` (hers) | **+0.0401 .. +0.0713** |
| `SwordAndShieldIdle` | **-0.1585 .. -0.0905** |
| `SwordAndShieldAttack` | -0.2814 .. +0.2149 |
| `SwordAndShieldSlash` | -0.3004 .. +1.1032 |

`SwordAndShieldIdle` has her feet roughly 4-8 cm underground through **every frame** of the clip. That is almost certainly what Stephanie has been reading as the animations "never landing well" on the placeholders - and it is a *different* fault from the rest-pose mismatch that warped her custom idle, even though the two look similar in motion.

Checked and ruled out: the Mixamo clips are **not** badly retargeted in terms of joint angles. Sampling `SwordAndShieldIdle` on all three characters gives elbowL 13.4 / 12.7 / 9.3 deg and kneeR 18.2 / 15.9 / 15.9 deg on Yemoja / Ninja / WarriorPrincess - Yemoja is within a few degrees of the others on everything except thigh elevation (~10 deg out), which is consistent with genuinely different leg proportions. So the pose is broadly right and the whole body is simply sitting too low.

Known likely cause, from `claude/yemoja-v6-import-record.md`: the `Armature` object's Y translation (0.034578 source units) is honoured in the bind pose but **discarded** under Humanoid retargeting, because Unity rebuilds the root from the avatar's hips/feet relationship. Two candidate fixes were identified there and neither was applied because both change gameplay-visible foot placement:
- **Unity side (quick):** set `heightOffset` per clip to lift each one. Must be re-applied to every future clip.
- **Blender side (structural):** zero the Armature translation and bake it into the mesh, so rest and retargeted poses agree.

**Do not start without Stephanie's call on which route.** Also note the fix may be per-clip rather than one constant - the range differs a lot between clips, so measure each rather than applying one offset blind.

**Acceptance criteria:**
- Toe clearance never negative across every frame of all 8 placeholder clips, measured off the toe bones (not renderer bounds - `SkinnedMeshRenderer.bounds` is a cached generous volume and useless for this).
- Her own `Yemoja@Idle` is unchanged by whatever fix is applied - it is already correct.

**Relevant files:** `Assets/Animations/YemojaAnimations/*.fbx` (8 placeholders), `claude/yemoja-v6-import-record.md`, `claude/yemoja-idle-retarget-fix.md`, `Assets/Scenes/CharacterImport.unity` (has a live foot-clearance readout).

---
*Bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
