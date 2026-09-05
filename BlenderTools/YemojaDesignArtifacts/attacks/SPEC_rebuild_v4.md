# Attack clips — rebuild v4 on the new model of record

The idle agent has changed the model of record underneath us. Everything the
clips bookend on has moved, so this is a rebuild, not a patch. Read
/mnt/user-data/uploads/Elementals-Fight/BlenderTools/YemojaDesignArtifacts/README_animation_guidelines.md
sections 17–23 first (the idle agent's addendum); its rules are binding.

## What changed (measured)

- New source file: `/home/claude/work/attacks/Yemoja_WORKING_v115_idleWeights.blend`
  (copy of `Backups/Yemoja_WORKING_v115_idleWeights.blend`). Do not touch v114
  or the old v115_attacks build except to read them.
- `Yemoja_Idle_MASTER` is a different pose ("A3"): vs v114, ForeArm.L 129°,
  Hand.L 60°, Hand.R 47°, Arm.L 45°, ForeArm.R 38°, Arm.R 32°, Shoulder.L 27°,
  fingers 13–15°, Foot.R 12°, UpLeg.R 10°; Hips location moved (−14.7, 0, −0.1)
  armature units. The JSON is `pose_idle_master_2026-09-04_v115_A3.json` in
  this folder, but the authority is the action itself: read the base pose from
  `Yemoja_Idle_MASTER` at frame 1 in the new file, and check it equals the JSON.
- `Yemoja_Body` mesh changed: 7609 verts (was 7417), elbow loop cuts and an
  elbow sculpt; shoulder-girdle weights re-done (990 verts); breast guard kept.
  `Yemoja_Clothes`: bracelets refitted, groin flap now 100 % Hips.
- Trident grip offset moved: use `yemoja_anim_lib_v115.py` (the project's
  current library, TRIDENT_U origin (0.93499, −0.10169, −0.03749)). In the idle
  the trident butt is planted on the floor (z = 0.000), shaft 12.7° from
  vertical. Load this library instead of the old one.
- Other actions in the file (`Yemoja_Idle_Loop` 1–121, the `_before_*` /
  `_v113_corkscrew` / `_elbowTuck` / `_poseA_preUserR` masters): leave every
  one of them untouched; cmp.py must show zero diffs on all of them.
- New rule (README 22): **the elbow is a hinge.** `yemoja_measure.off_hinge(side)`
  must stay under 5° on any arm you solve. `yemoja_anim_lib.limb_ik` violates
  this (it aims local X at the bend-plane normal); use
  `v115_fixes/apply_pole._limb_ik(A, b1, b2, target, pole, off_hinge=0,
  pronation=...)` for arms (read its docstring and `_elbow_solve`). Leg IK may
  stay on the old solver; report knee direction visually. The right arm at the
  idle bookends is Stephanie's hand pose with off-hinge 15.7°; that is
  inherited, not yours, and is exempt at f1/last only.
- `v115_fixes/yemoja_measure.py` has `scene_snapshot()/scene_restore()`,
  `region_area_audit`, `pose_twist_table` (includes `elbow_hinge`), and render
  helpers. Prefer it for measurements; keep the verify/ scripts as the
  per-frame acceptance harness (adapt paths: file, lib, mesh vert count).

## Targets: re-derive from the new idle, keep the intent

The spec's world-space targets (SPEC_attacks.md, SPEC_hardkick_v2.md,
SPEC_fix_v3.md) were written against the v114 idle. Do not paste them in.
For each key, compute the new landmark set (shoulder joints, hips, feet, the
Hand.L/Hand.R idle positions) and rebuild each target as
`new_idle_landmark + (old_target − old_idle_landmark)` using the landmark it
was clearly relative to (fists and guard hands → same-side shoulder joint;
ankles → hips; Hips moves → unchanged), then clamp reach as before. Where the
new idle makes a target read wrong (e.g. the left hand now starts somewhere
else so the f4 anticipation pull would move it the wrong way), keep the
intent (direction of strike, height, impact frame) and record the number you
used. Left-hand shape at the idle bookends is the new idle's cupped hand; the
fist closes over f1→f4 and reopens over the recovery.

Trident during kicks: the right arm holds and the butt stays planted (z within
0.02 of 0) in `Yemoja_Atk_Kick` at every frame; in `Yemoja_Atk_HardKick` it
lifts with the counterbalance arm as before (behind the body plane, nearly
vertical, tip up) and comes back to planted at the last key.

Everything else from SPEC_fix_v3.md stands: hemisphere continuity, signed
penetration test every frame, planted support foot for the front kick,
between-key pinning with breakdown keys, head tracking, foot articulation at
impact, clavicle limits (elevation only above shoulder height; protraction
15–20° on the striking side, 6° retraction on the other; README 17.5 says
Unity clamps shoulders to ±15° front-back and −15/+30 up-down by default, so
keep clavicle protraction ≤ 20° and elevation ≤ 30°), chest-frame arm targets
in the roundhouse, export ranges, idle bookends exact, code hygiene.

## Deliverables

- `/home/claude/work/attacks/Yemoja_ATTACKS_v4.blend` (saved from the new
  source; contains everything the source had plus the four `Yemoja_Atk_*`
  actions with fake users; Armature's assigned action set back to
  `Yemoja_Idle_MASTER`, frame 1, before saving).
- `attacks_build.py` parameterised (source path, lib path, output path via
  argv/env) and idempotent against the new source.
- `review/v4/` reports and renders (side camera is the one that must read; the
  file's `_AR_*` cameras still exist — check, and use yemoja_measure's render
  helpers if they suit better).
- `BUILD_NOTES.md` gets a `## v4 rebuild` section: acceptance from verify/ +
  off_hinge per key per arm + trident butt height in Kick + the deviations and
  what still misses.

Final message: the compact acceptance table per clip (impact frame; worst
interpolated-frame floor / support-foot; Hand/Foot twist max; off-hinge max;
Shoulder/Arm/ForeArm/UpLeg/Leg ratios at impact; signed trident penetration;
clothes-inside count; flips remaining; breakdown frames added), the
deviations, and what still misses.
