# Yemoja attack clips — spec for the implementing agent

Author: Fable (orchestrator). Implementer: Sonnet agent. Verifier: Opus agent.
Read `/mnt/user-data/uploads/Elementals-Fight/BlenderTools/YemojaDesignArtifacts/README_animation_guidelines.md`
sections 2, 3, 5, 7, 13 and 15 before writing code. Everything in it is binding.

## Environment (offline, no shared Blender)

- `bpy` 5.0.1 is installed as a Python module (`import bpy` works in python3).
- Working file: `/home/claude/work/attacks/Yemoja_WORKING_v114_idleClean.blend`
  (opens headless; saved by Blender 5.2, loads fine in 5.0.1).
- Library: `/home/claude/work/attacks/yemoja_anim_lib.py` (import via
  `common.load()` in `/home/claude/work/attacks/common.py`, which also gives
  `apply_json_pose` for the idle master and `twist_deg`).
- Idle master pose JSON:
  `/mnt/user-data/uploads/Elementals-Fight/BlenderTools/YemojaDesignArtifacts/pose_idle_master_2026-09-03_v114clean.json`
- Renders go to `/home/claude/work/attacks/review/`. Cameras `_AR_front`,
  `_AR_side`, `_AR_q34` exist in the file; `L.review(tag)` uses them
  (workbench clay + silhouette). Call `L.preview_mode(True)` first.
- Output: save as `/home/claude/work/attacks/Yemoja_WORKING_v115_attacks.blend`.
  Never write outside `/home/claude/work/attacks/`.

Known API gap: Blender 5.x removed `Action.fcurves`. The library's
`set_interpolation()` raises. Write a replacement that walks
`action.layers[*].strips[*].channelbag(slot).fcurves` (or
`channelbags`), and use it.

Rendering re-evaluates the assigned action (see README 15.3). Detach the
action (`A.animation_data.action = None`) while constructing poses, key with
`L.get_action(name)` + `L.key_pose(frame)` only once a pose is final, and
re-apply `apply_json_pose` before building the next key.

## Frame and conventions

- Armature space: +X her left, +Y up, +Z forward toward the opponent. World:
  she faces −Y; world Z is up; armature object scale 0.01 with Z +0.14324.
  Use `L.w2a`/`L.a2w`, `L.arm_ik`, `L.leg_ik` (world-space targets) or
  `L.limb_ik` (armature-space targets).
- 30 fps. Author in place: the Hips may shift and drop inside the stance, but
  frame 1 and the last frame are the idle master pose exactly, and the support
  foot never leaves its idle position.
- Never key `hair_grp*` or `Eye.*`. `L.key_pose` already restricts to humanoid
  bones and keys Hips location.
- Build arm and leg poses with `limb_ik` / `arm_ik` / `leg_ik`, never by
  rotating bones by hand into place (README 13: shortest-arc aiming
  corkscrews the roll). Pole = where the elbow/knee should point.
- Twist budget: after every key, `|twist_deg("Hand.L/R")| < 30°` and
  `|twist_deg("Foot.L/R")| < 20°`. Put arm pronation on `ForeArm` via the
  `twist` argument of `limb_ik`.
- **Clavicle rule (README 3):** arm above shoulder height → elevate
  `Shoulder` by ~1/3 of the arm's elevation. Any punch/thrust → protract the
  striking side's clavicle 15–20° (`Shoulder.L −Y` / `Shoulder.R +Y`), and
  retract the other side 5–8°.
- Fingers: left hand uses a closed fist for the jab: `L.hand_shape("L", **L.COILED_HAND)`
  is too loose for a fist; use curl (70, 95, 60), thumb (30, 40, 20),
  spread as COILED. Right hand keeps the trident grip at all times: call
  `L.apply_captured_grip("R")` (falls back to GRIP_HAND) after any change to
  the right arm, then `L.attach_trident()` so the preview prop follows.
- Head tracks the opponent: `Head`/`Neck` counter-rotate the torso yaw so the
  face keeps pointing +Z within ±10°.

## Idle landmarks (world space, from v114)

    Hips head   (-0.004, 0.297, 4.527)
    Foot.L head ( 0.572,-0.006, 0.464)   lead foot (forward = −Y)
    Foot.R head (-0.884, 1.469, 0.605)   rear foot
    Hand.L head ( 0.946,-0.751, 5.104)   lead hand, open, palm up
    Hand.R head (-1.448, 1.200, 4.913)   rear hand, trident grip
    Arm.L head  ( 0.587, 0.342, 5.993)   shoulder height ≈ z 6.0
    Head tail   ( 0.028, 0.600, 7.422)   top of head
    Trident: butt (-1.817, 1.345, 0.422) tip (-2.054, 1.160, 8.510), length 8.09

Bone lengths (armature units, 100 per world unit): Arm 94.8, ForeArm 78.7,
Hand 49.9, UpLeg 199.4, Leg 196.5, Foot 70.4.

## Helpers to write first (`/home/claude/work/attacks/harness.py`)

1. `snapshot_feet()` → world head position and world matrix of `Foot.L/R`.
2. `pin_foot(side, snap)`: `leg_ik(side, ankle_world, knee_hint)` to the
   snapshot ankle, then set `Foot.<side>.matrix` to the snapshot world
   orientation (armature space). Knee hint = snapshot knee position pushed 0.4
   units forward. Assert ankle error < 0.005 after.
3. `audit()` → per-dominant-bone area ratio and crushed-face count for
   `Yemoja_Body` and `Yemoja_Clothes` vs rest (method in README 8 / 13; code
   for it exists in this session's history: dominant bone per face by summed
   vertex weight, evaluated-mesh polygon areas in REST vs POSE). Return the
   rows below 0.95 and the worst five.
4. `set_interpolation_5x(action, mode="BEZIER", handle="AUTO_CLAMPED")`.
5. `key(frame)`: `L.key_pose(frame)`; also key the trident preview (not
   exported, so optional).
6. `build_clip(name, keys)` where `keys` is an ordered list of
   `(frame, fn)` and `fn()` builds the pose from a fresh idle master; it
   keys every humanoid bone at every listed frame (README: partial keying
   is how bones drift). Frames not listed interpolate.
7. `report(name)`: for each key frame, `audit()` + `L.review(f"{name}_f{frame}")`
   and the twist checks; write `/home/claude/work/attacks/review/{name}_report.md`
   with a table (frame, worst region, ratio, crushed, hand/foot twist,
   lowest body z, support-foot error).

## The four clips

Impact frames are contractual: Unity's `PerformAttack` event goes there.
`StopAttacking` goes on the last frame. First and last key = idle master.

### `Yemoja_Atk_Punch` — left jab, 14 frames, impact f7

| f | pose |
|---|---|
| 1 | idle master |
| 4 | anticipation: Hips yaw +8° (armature Y, turning her left shoulder back), Hips drop 3 units; left hand pulls to (0.55, 0.05, 5.55) world, fist closed; Shoulder.L retracted 5° |
| 7 | impact: left fist to (0.45, −1.95, 5.75) world, straight arm, elbow pole (0.9, −0.9, 5.4); Shoulder.L protract 18°, elevate 6°; Hips yaw −12° (left hip drives forward), Spine1/Spine2 yaw −8° total; right arm holds; head faces +Z |
| 9 | hold: same as 7 with the fist 0.1 further out and Hips yaw −14° |
| 14 | idle master |

Fist reach check: Arm+ForeArm = 173 units = 1.73 world units from the
shoulder; do not exceed 0.97 of that or `limb_ik` will report `ok=False`.

### `Yemoja_Atk_HardPunch` — trident thrust, 26 frames, impact f12

| f | pose |
|---|---|
| 1 | idle master |
| 7 | wind-up: Hips yaw −18° (right shoulder pulled back), weight to rear leg (Hips move (−0.15, +0.20, −0.10)), right hand to (−1.30, 1.85, 5.30) world with the trident shaft pointing along +Z (use `L.orient_hand_for_shaft` with hand-Y hint +Y); left arm comes forward and up as a guard, fist at (0.65, −0.45, 5.9); Shoulder.R retracted 8° |
| 12 | impact: right hand to (−0.55, −1.40, 5.45) world, arm straight along +Z, elbow pole (−0.9, −0.4, 4.9); Shoulder.R protract 20°, elevate 5°; Hips yaw +25° (right hip through), Hips move (+0.10, −0.35, −0.15) (lunge inside the stance; support feet pinned, knees bend to allow it); Spine1+Spine2 yaw +12°; left arm swings back to (0.75, 0.70, 5.2); trident shaft along +Z (world −Y), tip roughly at (−0.6, −8.5, 5.6) |
| 15 | hold: as 12, hand 0.1 further forward |
| 26 | idle master |

Both feet pinned throughout; hips motion is absorbed by the legs via
`pin_foot`. Check the shaft stays clear of her own body and legs (sample
`L.trident_ends` against the body's evaluated mesh: nearest-vertex distance to
the shaft segment > 0.15 world units, except at the hand).

### `Yemoja_Atk_Kick` — left front snap kick, 16 frames, impact f7

| f | pose |
|---|---|
| 1 | idle master |
| 4 | chamber: weight onto the right foot (Hips move (−0.25, +0.05, −0.10), Hips roll so the pelvis is level), left knee up: ankle at (0.45, −0.25, 2.6) world, knee hint (0.45, −0.9, 3.4); toes pulled up (Foot.L −X 20° local, ToeBase.L −X 15°); arms: left fist up as guard (0.55, −0.5, 5.7), right arm holds the trident, trident butt stays off the ground |
| 7 | impact: left ankle to (0.40, −1.90, 3.1) world, knee hint (0.45, −1.2, 3.6) (leg straight, foot ball leading), Foot.L extended so the sole faces +Z; Hips yaw −10°, Hips lean back 6° (Spine −X ... use `rot("Hips","X",−6)`), Spine counter 4°; Shoulder.L elevate 5° |
| 9 | re-chamber: as frame 4 |
| 16 | idle master |

Support (right) foot pinned; `lowest_world_z` of the body must stay ≥ −0.005
(no sinking) and the support sole at its idle height.

### `Yemoja_Atk_HardKick` — right roundhouse to the head, 28 frames, impact f12

| f | pose |
|---|---|
| 1 | idle master |
| 6 | wind-up: weight onto the left foot (Hips move (+0.30, −0.15, −0.15)), Hips yaw −20°, right knee chambers out to the side: ankle (−1.10, 0.95, 2.4), knee hint (−1.5, 0.8, 3.3); arms open for balance: left fist (0.5, −0.6, 5.6), right arm keeps trident angled back (hand at (−1.35, 1.35, 5.05)) |
| 12 | impact: hips rotate through, Hips yaw +55° (support foot pivots: it is allowed to rotate about its own vertical axis at the idle position, not to translate), Hips lean away from the kick 15° (Spine chain +X lean-back 8° plus roll); right ankle to (−0.20, −1.75, 6.2) world, knee hint (−1.4, −1.1, 6.3), foot horizontal, shin near-straight; left support leg slightly bent; both clavicles: Shoulder.L elevate 10° (left arm swings down/back to (0.9, 0.5, 4.6)), Shoulder.R elevate 10° and the trident arm sweeps back for counterbalance, hand at (−1.6, 1.5, 5.0) |
| 16 | follow-through: right ankle to (0.30, −1.20, 5.4), Hips yaw +70°, torso continuing |
| 22 | recover: right foot back to (−0.9, 0.9, 0.9), Hips yaw +20° |
| 28 | idle master |

Hip flexion this high is inside the measured-safe envelope (README 4).
Watch the clothes: the shorts on the kicking side will penetrate; report the
inside-body vertex count against the rest baseline of 82 (README 4 table) —
below 350 is acceptable for a 4-frame event, above that flag it.

## Acceptance for every clip

- Frame 1 and last frame equal the idle master (max bone quaternion angle
  difference 0.05°).
- Support foot ankle error < 0.005 world units on every key frame, lowest body
  vertex z in [−0.005, +0.02] except the kicking foot.
- Body region ratios: every humanoid region ≥ 0.90; ≥ 0.95 for `Arm.*`,
  `ForeArm.*`, `Shoulder.*`, `UpLeg.*`, `Leg.*`. Report all below 0.95.
  (Finger bones on the right hand are exempt: Stephanie's grip, known.)
- Hand twist < 30°, foot twist < 20°.
- Silhouette renders at the impact frame read as a clear strike at thumbnail
  size from `_AR_front` and `_AR_side`.
- One save at the end: `Yemoja_WORKING_v115_attacks.blend`, all four actions
  with fake user, `Yemoja_Idle_MASTER` untouched, no new bones or objects
  (the diagnostic camera from earlier was removed; do not add cameras).

Deliver: the harness, `attacks_build.py` (idempotent: loads v114, builds all
four, saves v115), the four report .md files, and the review renders.
