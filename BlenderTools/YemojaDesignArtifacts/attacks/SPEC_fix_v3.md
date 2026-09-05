# Attack clips — fix round v3 (decisions on the verification findings)

Read /home/claude/work/VERIFY_attacks.md first; every number there was
reproduced independently and stands. This file says what to do about each
finding. Decisions marked FABLE are settled; do not re-litigate them.

## Acceptance is no longer the harness's own audit

The verifier's scripts are in /home/claude/work/attacks/verify/ (eval_all.py
= per-frame floor/foot/twist/arc, pen.py = signed BVH ray-parity penetration
for the trident shaft and the clothes, arc.py = quaternion hemisphere and
per-frame ankle path, keys.py, cmp.py = v114-vs-v115 integrity, lean.py,
deep.py). Adapt their paths, run them against the rebuilt file, and paste
their output into BUILD_NOTES.md. A claim without a verify/ script number
behind it does not count. Replace `harness.trident_clearance` with the
signed test from pen.py (or delete it) so the old metric cannot be used again.

## Fixes, in priority order

1. **Quaternion continuity, all four actions.** After keying each action,
   walk every bone's four `rotation_quaternion` fcurves key by key and negate
   the whole quaternion at any key whose dot with the previous key is < 0.
   Then run arc.py on every frame: the right ankle in HardKick must move
   monotonically forward-and-up from f6 to f12 (no frame with world y greater
   than the previous frame's between f6 and f12), and the per-frame step of any
   ankle or wrist must be < 0.9 world units. Add this as
   `fix_quaternion_hemispheres(action)` in harness.py and call it for every clip.

2. **HardKick f12/f16 trident.** FABLE: the trident stays BEHIND her body
   plane during the kick. Right hand at f12: world (−1.30, 1.25, 6.00); shaft
   direction armature normalised(−0.20, 0.95, −0.22) — nearly vertical, tip
   up, the butt hanging down behind her right hip. f16: hand (−1.15, 1.30,
   6.05), same shaft direction. f6 and f22 keep their v2 values. Verify with
   pen.py: zero shaft samples inside Yemoja_Body other than the gripping
   fingers at EVERY frame of the clip (not only keys), and shaft-to-UpLeg.R /
   Leg.R segment distance > 0.25 at every frame.

3. **Kick: plant her.** FABLE: when the left foot lifts, the right (support)
   foot settles flat. At f4, f7, f9 the support target is the idle right ankle
   with its world z lowered so that the sole lies on z = 0 (rotate Foot.R so
   the sole is flat: ToeBase.R tail and Foot.R head at the same height above
   the floor as they are in a flat rest foot; measure lowest body z after and
   iterate Hips z until it is within [−0.005, +0.005]). Keep the ankle x/y at
   idle. Write the breakdown so f2–f3 and f10–f15 settle/rise smoothly (add
   breakdown keys at f2 and f13 if needed). Fix the report so the excluded
   region is named explicitly ("excluding Foot.L/ToeBase.L").

4. **Between-key pinning, all clips.** Add `enforce_pins(action, sides,
   tol=0.005)`: after the keys are built and hemispheres fixed, step every
   frame; where a pinned support ankle deviates > tol from its target or the
   lowest body z leaves [−0.005, +0.02] (kicking-foot region excluded as
   above), capture the interpolated pose, re-run pin_foot (and the Hips z
   correction), and key ALL humanoid bones at that frame. Iterate until every
   frame passes (cap 6 passes). Report the breakdown frames added per clip.

5. **Head tracking.** Add Neck/Head Y so the face is within ±10° of armature
   +Z at every key of every clip (HardKick: ±15°). Verify with lean.py-style
   measurement at every key.

6. **Feet at impact.** Kick f7: plantarflex Foot.L (and ToeBase.L) so the
   toe-vs-shin angle is 20–35° (ball-of-foot leading, not a pointed toe);
   HardKick f16: same treatment as f12 (toe within 15° of the shin). Keep the
   Foot twist budget.

7. **Clavicles.** FABLE: elevation is only for arms above shoulder height.
   Punch f7/f9: Shoulder.L elevation ≤ 8°, protraction 18° as now, plus
   Shoulder.R retraction 6°. HardPunch f12/f15: Shoulder.R elevation ≤ 8°,
   protraction 20°, Shoulder.L retraction 6° (remove its +16° elevation).
   Accept the resulting Shoulder.L/R area ratios (≈0.89–0.91) and REPORT
   them; they are a joint-weight issue for the model of record, not a pose
   issue, and will be fixed there. HardKick f16: Shoulder.R elevation ≤ 1/3 of
   Arm.R's rise; Shoulder.L elevation 0 when the left arm descends.

8. **HardKick arms in the chest frame.** The left arm's balance targets were
   world-space while the chest yawed 75–95°, which is what crushed Shoulder.L
   to 0.75. Rebuild the left arm targets relative to Spine2's posed frame:
   fist at chest-local (forward 0.45, down 0.95, left 0.35) at f12 and
   (forward 0.30, down 1.05, left 0.30) at f16, elbow pole chest-local
   (forward 0.1, down 0.6, left 0.9). Report Shoulder.L/R and Arm.L/R body
   ratios at every HardKick key; target ≥ 0.90 for all, ≥ 0.95 preferred.

9. **HardPunch hold / Punch hold.** Punch f9: keep the fist where it is but
   move Hips forward 0.05 world (−Y) and yaw −14° as specced so the torso
   drives; HardPunch f15 likewise +0.05 forward. Minor, but do it.

10. **Export ranges.** For each action set `use_frame_range = True` and
    `frame_start/frame_end` (Blender: `action.frame_start`, `action.frame_end`
    via `use_frame_range`) to 1–14 / 1–26 / 1–16 / 1–28. Before saving, assign
    `Yemoja_Idle_MASTER` back to the Armature and set the scene to frame 1.

11. **Code hygiene (from VERIFY §10).** (a) key from a snapshot dict of
    matrix_basis, not from live rig state; (b) getattr fallback for
    `strip.channelbags` vs `strip.channelbag(slot)`; (c) invalidate
    `_REST_CACHE` when preview_mode changes or key it on the modifier state;
    (d) try/finally around review() state; (e) one REVIEW_DIR; (f) parameterise
    paths via argv/env so attacks_build.py can be pointed at another file.
    Do NOT change yemoja_anim_lib.py itself (it is a shared project file);
    wrap or override in harness.py.

12. **Front camera.** FABLE: the game is a 2.5D side-on fighter, so `_AR_side`
    is the silhouette that must read; `_AR_front` is informational. No change
    required; note it in BUILD_NOTES.

13. **BUILD_NOTES.md.** Rewrite the acceptance section from verify/ output
    only: per clip, per key AND worst interpolated frame: lowest z, support
    ankle error, Hand/Foot twist, Shoulder/Arm/ForeArm/UpLeg/Leg/Spine2 ratios
    with crushed counts, trident penetration (signed), clothes-inside count,
    head angle to +Z, hemisphere flips remaining (must be 0), breakdown frames
    added. State plainly what still misses a threshold. Correct the camera
    count and the Kick lowest-z bone.

Save as Yemoja_WORKING_v115_attacks.blend again (overwrite; the version number
is for my stream only), and regenerate the review renders and the four
report files.
