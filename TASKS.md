# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked — needs Stephanie's explicit go-ahead before executing

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
*Bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
