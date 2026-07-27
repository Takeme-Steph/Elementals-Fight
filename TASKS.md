# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked — needs Stephanie's explicit go-ahead before executing

## Format for new tasks
Each task gets: a scope-limited title, the *why* (architectural reasoning, not just the ask), acceptance criteria, and any files known to be relevant. Claude AI keeps entries in this shape going forward.

## Queue

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

**Done:** Independently re-verified Cowork's findings before deleting (no `.github/workflows`, `WebGL Builds/` is a complete real export, zip's last touching commit is unrelated to build/deploy). Removed `connectwebgl.zip` via `git rm` and added it to `.gitignore`. Pushed as `task/remove-stale-webgl-zip`, not yet a PR. As Cowork noted, this doesn't shrink the repo - the ~213 MiB across its 9 historical revisions stays until the (blocked) history-rewrite task runs.

---
### [x] Extend `.gitattributes` LFS patterns to cover `.anim` (and consider `.controller`) before `task/enable-git-lfs` merges
**Why:** Cowork verified the pushed `.gitattributes` (branch `task/enable-git-lfs`, commit `84b63d6`) against the actual repo history and found two gaps. `.anim` isn't covered at all — 28 files, 150 MiB, averaging over 5 MiB each — genuinely large for Unity clips, likely dense imported/baked curve data. These are plain ASCII YAML text (`%YAML 1.1`, force-text serialization), not binary, confirmed by inspecting a sample directly - so this is a real tradeoff, not a pure bug: LFS-tracking them reclaims 150 MiB but turns them into opaque blobs for git (no line diffs, no partial merges). In practice a 5+ MiB text diff is already unreviewable in any PR view, so the diffability being traded away is mostly theoretical - leaning toward including it, but flag the tradeoff to Stephanie rather than deciding silently. `.controller` is also missing (22 files, 4.9 MiB) - real but small, lower priority either way.

Separately, Cowork tested (not assumed) whether the existing `*.fbx` pattern actually catches the uppercase `.FBX` files that make up 126 of the 147 MiB of FBX weight in history (`Assets/Animations/**/*.FBX` from imported animation packs). It does, on this repo, because `core.ignorecase = true` is set (standard for a Windows checkout) - verified by testing `git check-attr` with that config explicitly. This is correct as-is and doesn't need a code change, but it's a config-dependent behavior: if this repo were ever cloned somewhere with case-sensitive git defaults (e.g. a Linux CI runner), the uppercase `.FBX` files would silently stop going through LFS. Worth a one-line comment in `.gitattributes` noting this, so a future reader isn't confused about why there's no separate `*.FBX` pattern.

**Acceptance criteria:** `.gitattributes` on `task/enable-git-lfs` (or a follow-up branch, Claude Code's call) adds `*.anim filter=lfs diff=lfs merge=lfs -text` unless Stephanie decides the diffability tradeoff isn't worth it for a currently-small roster - flag it rather than deciding unilaterally. `*.controller` optional, lower priority. Add a short comment near the `*.fbx` line noting it also covers `.FBX` via `core.ignorecase`, and that this is Windows-checkout-dependent.

**Relevant files:** `.gitattributes`

**Done:** Independently re-verified before acting: on `task/enable-git-lfs`'s HEAD alone (single snapshot, not full history) there are 14 `.anim` files / 67.5 MiB and 8 `.controller` files / 4.7 MiB - smaller than Cowork's full-history-across-all-branches figures, as expected given the different scope, but the core finding held up: individual clips run up to 14.6 MiB (`MageIdle.anim`, `WPIdle.anim`), confirmed as plain-text YAML, genuinely large. Also independently confirmed `core.ignorecase=true` and that `*.fbx` does catch a real `.FBX` file via `git check-attr`. Added `*.anim` and `*.controller` LFS patterns plus the `.FBX`/`core.ignorecase` comment near `*.fbx`, on `task/enable-git-lfs` directly (not yet merged). **Flagging per Cowork's note, not deciding silently: this trades away native git diffs on `.anim`/`.controller` files in exchange for ~72 MiB (on this branch's current file set; more once the rest of history is counted) once the separate history-migration task runs. If you'd rather keep those diffable, tell me and I'll drop the `.anim`/`.controller` lines before this branch merges.**

---
### [!] BLOCKED — Rewrite repo history: migrate binary assets to LFS + purge `connectwebgl.zip` history
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

---
*Bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
