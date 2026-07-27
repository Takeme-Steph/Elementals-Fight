# TASKS.md — Elementals-Fight work queue

Maintained by Claude AI (Cowork). **Claude Code:** read top-to-bottom, pick up the first task not marked done, create a feature branch, implement it, commit, then mark the entry `[x]` with a one-line summary of what changed before moving to the next. If a task lacks enough context to act on safely, stop and flag it rather than guessing at design intent.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked — needs Stephanie's explicit go-ahead before executing

## Format for new tasks
Each task gets: a scope-limited title, the *why* (architectural reasoning, not just the ask), acceptance criteria, and any files known to be relevant. Claude AI keeps entries in this shape going forward.

## Queue

### [x] Delete `connectwebgl.zip` and gitignore it
**Why:** Confirmed dead weight, not a decision call — verified independently (not just taking the hypothesis on faith): no `.github/workflows` exist in this repo at all, so it can't be feeding a CI/deploy pipeline; `git grep` across every tracked file at HEAD for "connectwebgl" or "webgl_sharing" returns zero references anywhere; the actual documented WebGL build output already lives correctly under `WebGL Builds/` (a proper, complete Unity export — index.html, TemplateData/, Build/*.data.br); and the zip's last modifying commit ("Moved take damage function to late update") is completely unrelated to any build/deploy work, which is the fingerprint of a file getting swept up incidentally rather than maintained on purpose. This alone is a normal, reversible commit — no history rewrite involved, so it doesn't need to wait for the LFS decision below. Note: deleting it here does NOT shrink the repo — the ~213 MiB already spent across its 9 historical revisions stays in every clone until the history rewrite task below actually runs. This task only stops it from getting worse.

**Acceptance criteria:** `connectwebgl.zip` removed from the working tree and committed as a deletion. `.gitignore` updated with a `connectwebgl.zip` entry so it can't silently get re-added by an old build script or manual export. No other files reference it (already confirmed via repo-wide grep, but re-check if anything looks off).

**Relevant files:** `connectwebgl.zip` (root), `.gitignore`

**Done:** Independently re-verified Cowork's findings before deleting (no `.github/workflows`, `WebGL Builds/` is a complete real export, zip's last touching commit is unrelated to build/deploy). Removed `connectwebgl.zip` via `git rm` and added it to `.gitignore`. As Cowork noted, this doesn't shrink the repo - the ~213 MiB across its 9 historical revisions stays until the (blocked) history-rewrite task runs.

---
### [!] BLOCKED — Rewrite repo history: migrate binary assets to LFS + purge `connectwebgl.zip` history
**Do not run any part of this without Stephanie's explicit go-ahead first**, same as the CharacterSelect EventSystem fix. This rewrites every commit hash in the repo and requires a force-push — not a normal revertible commit.

**Why:** Claude AI (Cowork) independently measured the actual repo history (full clone, `git rev-list --objects --all` + `git cat-file --batch-check`, deduplicated by content hash) rather than estimating: 892.8 MiB of unique binary content exists across all history; 448.6 MiB of that is Unity asset types (`.fbx`/`.tga`/`.png`/etc. — double check whether `.anim` is covered too, since 28 `.anim` files total 150 MiB and average over 5 MiB each, unusually large for Unity clips, and may not be in the current `.gitattributes` LFS pattern list); 212.9 MiB is `connectwebgl.zip`'s 9 recommits. Current packed `.git` is 460 MiB. GitHub's actual current LFS free tier (corrected from an earlier 1 GiB/1 GiB assumption — GitHub moved to a metered model) is 10 GiB storage + 10 GiB bandwidth/month on Free/Pro accounts, so migrating ~449 MiB of assets uses under 5% of the monthly quota — comfortably covered, not a tight fit. Doing the LFS migration and the zip's history purge as one combined rewrite (rather than two separate disruptive events) should drop the packed repo from ~460 MiB to roughly 40-60 MiB.

**Preconditions before this can run:** both currently-open branches (`task/input-system-eventsystem-migration`, `task/yemoja-roster-addition`) merged and deleted, per the usual flow, so `main` is the only branch left and there are no open PRs. No in-progress local worktrees or uncommitted changes at rewrite time.

**What the task involves once unblocked:**
1. Confirm the exact extension list in the current `.gitattributes` (added in commit `84b63d6`, not yet pushed) and flag to Stephanie if `.anim` isn't covered.
2. `git lfs migrate import --everything --include="<confirmed extension list>"` to convert historical blobs to LFS pointers.
3. `git filter-repo --path connectwebgl.zip --invert-paths` to fully purge the zip's history (not converting it to LFS — it's being deleted anyway, so full removal is strictly better).
4. Force-push the rewritten `main` to origin.
5. Stephanie re-clones fresh locally rather than reconciling GitHub Desktop against rewritten history (Desktop doesn't handle force-pushed rewrites gracefully).
6. Note the rewrite in `CLAUDE.md` (date + resulting size) so a future session isn't confused by a `main` that looks like it diverged wildly from an old local copy.

**Relevant files:** whole-repo operation; `.gitattributes`, `CLAUDE.md` need updates as part of it.

---
*Bring the next feature or bug to Claude AI (Cowork) and it'll be broken down into tasks here.*
