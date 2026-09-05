# Yemoja attack clips — build notes

**This file's top section was rewritten again 2026-09-04 for rebuild round
v4 (`SPEC_rebuild_v4.md`).** Read `## v4 rebuild` first — it is the current,
authoritative acceptance record, built entirely from `verify/eval_all.py`,
`verify/pen.py`, `verify/deep.py`, `verify/keys.py`, a `cmp.py`-equivalent
multi-action byte-identity check, and `v115_fixes/yemoja_measure.off_hinge`,
all run per-frame against the rebuilt file. Everything below `## v4 rebuild`
is fix-round-v3 history against the OLD source (`Yemoja_WORKING_v114_idleClean.blend`
-> `Yemoja_WORKING_v115_attacks.blend`) and is kept for context only — the
model of record has moved on (see v4's own "what changed" section) and none
of v3's specific numbers (targets, hints, ratios) apply to the current
deliverable, `Yemoja_ATTACKS_v4.blend`.

## v4 rebuild (2026-09-04, `SPEC_rebuild_v4.md`)

### What changed and why a rebuild, not a patch

The idle agent replaced the model of record mid-round: new source
`Yemoja_WORKING_v115_idleWeights.blend` (new idle master pose "A3" — Hips
moved (−14.7, 0, −0.1) armature units, every major arm bone rotated
27–129°), new `Yemoja_Body` mesh (7609 verts, was 7417, elbow sculpt +
re-done shoulder-girdle weights), new trident grip offset (`yemoja_anim_lib_v115.py`,
`TRIDENT_U["origin"]` moved from 16.8% to 55.6% up the shaft), and a new
binding rule (README 22): the elbow is a hinge, and `yemoja_measure.off_hinge(side)`
must stay under 5° on any arm this round solves (the old `yemoja_anim_lib.limb_ik`
aims at the bend-plane normal, not the rig's actual hinge axis, and was
measured at up to 60.7° off-hinge on this rig's left arm). All four clips
were rebuilt from scratch on the new source with `attacks_build.py`
(unchanged file, now parameterised — see "Architecture" below); nothing was
pasted from v3's own numbers except *intent* (direction, height, impact
frame, timing).

### Architecture: one file, two models

`attacks_build.py` now reads `YEMOJA_BLEND_IN`/`YEMOJA_LIB`/`YEMOJA_BLEND_OUT`/
`YEMOJA_REVIEW_DIR` from `os.environ`, defaulting to the v4 trio
(`Yemoja_WORKING_v115_idleWeights.blend`, `yemoja_anim_lib_v115.py`,
`Yemoja_ATTACKS_v4.blend`, `review/v4`). A module-level `V4` flag (true
whenever the source/lib path names the v115 pair) selects, throughout the
same ~900 lines of pose-building code:
- `RESET_POSE` = `harness.apply_idle_action` (reads `Yemoja_Idle_MASTER`
  frame 1 from the loaded file's own action — see "JSON vs action" below)
  under v4, vs `common.apply_json_pose` (the old v114 JSON) otherwise.
- `ARM()`/`ARM_CURRENT()`/`LEG()` — thin wrappers every pose function calls
  instead of `L.arm_ik`/`L.leg_ik` directly. Under v4 they retarget an
  old-v114-relative literal world point via `harness.retarget_shoulder`/
  `retarget_hips` (see below) and solve arms through the hinge-safe
  `harness.arm_ik_hinge` (wraps `v115_fixes/apply_pole._limb_ik`,
  `off_hinge=0.0` explicit). `ARM_CURRENT` skips retargeting for a target
  already expressed in the current frame (e.g. `chest_local_target`'s
  output, or a value built from an already-retargeted base). Under v3
  (`V4=False`) all three are plain pass-throughs to the original solver —
  the same pose functions reproduce the v3 build when pointed at the old
  source, satisfying "idempotent against the new source" without a fork.

### Targets: re-derived, not pasted

`harness.v4_deltas(L)` reads `Yemoja_Idle_MASTER` frame 1 from the loaded
file and measures world-space Arm.L/Arm.R/Hips head positions, diffed
against the frozen v114 values (`V114_LANDMARKS`, captured once from the
old source). `retarget_shoulder(L, side, old_world)` and `retarget_hips(L,
old_world)` return `old_world + delta` — the rigid landmark-shift the spec
asks for, applied literally at every call site that used to carry a bare
v114-relative tuple. Measured deltas: Arm.L −0.0892,+0.0911,−0.1236 (mag
0.178), Arm.R −0.0992,−0.0623,+0.0317 (mag 0.121), Hips −0.1472,+0.0013,0
(mag 0.147) armature units. Foot.L/Foot.R world positions are IDENTICAL
between the old and new idle (the legs were re-solved by the idle agent to
compensate for the hip shift) — `IDLE_SNAP` (measured live from the loaded
file, same as v3) needed no retargeting logic at all.

Three "ray-extension" call sites (`kick_f7`'s leg reach, `punch_f9`'s and
`hardpunch_f15`'s "0.1 further out" fist reach) build a direction vector
from an old-relative literal AND a freshly-measured current-frame point —
mixing an un-retargeted old point with a current one would point the ray
the wrong way under the new geometry. Fixed by retargeting the literal
FIRST, then computing the direction from two now-consistent points, and
routing the final (already-current) target through `ARM_CURRENT`/plain
`leg_ik` rather than `ARM`/`LEG` (which would retarget it a second time).
Same bug, same fix, at three more `clamp_reach(...)` call sites
(`punch_f7`, `hardpunch_f7`, `hardpunch_f12`) where `clamp_reach` itself
measures the ray's origin from the CURRENT shoulder head, so its literal
target argument has to already be in the current frame.

### JSON idle pose vs the action: do not trust the JSON

`pose_idle_master_2026-09-04_v115_A3.json` does **not** match
`Yemoja_Idle_MASTER` frame 1 on the actual source file: measured directly,
Hips location differs by 7.36 armature units and `HandPinky2.L`'s
quaternion by 7.46°. The spec's own text hedges this ("the authority is the
action itself... check it equals the JSON") — it does not. `RESET_POSE`
under v4 (`harness.apply_idle_action`) reads the pose from the action
directly (snapshots `matrix_basis` at frame 1, detaches the action, re-
applies the snapshot) and never touches the JSON.

### HardPunch f12/f15: yaw re-tuned instead of lunge depth (deviation)

v3 kept HardPunch f12's yaw at the spec's literal +25° and shaved the lunge
translation to 95% to clear `pin_foot`'s 0.005 tolerance on the rear ankle.
Under the new idle (Hips moved), the same 0.95×/yaw-25 pair now errs
0.00837 — and swept across the WHOLE depth range (0.5×–0.95×) the rear-
ankle error never drops below ~0.0075: at this geometry it is the *yaw*,
not the lunge depth, that is close to infeasible (yaw 20 errs 0, yaw 25
errs 0.00884). Bisected yaw alone at FULL spec depth (`LUNGE_SCALE=1.0`):
yaw 23.5 errs 0.00227, comfortable margin. Kept `HP_YAW=23.5` (1.5° off the
spec's 25, visually negligible) and `LUNGE_SCALE=1.0` — this reproduces the
lunge DEPTH exactly (better than v3's own 5% cut) at the cost of a small
yaw deviation instead. `hardpunch_f15` re-verified at the same yaw with its
own existing y=−0.3330 depth (errs 0.0, comfortable) — re-checked whether
the new yaw allows carrying the lunge further per the spec's original
"+0.05 world forward" (item 9): y=−0.35 (matching f12) errs 0.00227, y=−0.38
jumps to 0.01416 — still infeasible, same conclusion as v3, so the existing
"extra 3° of Spine1/Spine2 forward lean carries the rest of the intent"
fix was kept unchanged.

### All seven hand-Y trident hints re-swept (deviation, all values changed)

The new trident offset and idle geometry moved every free-spin optimum
found under v3. Re-measured with the OLD hints in place: Hand.R twist blew
the 30° budget at every `orient_hand_for_shaft` key — HardPunch f7 −32.2°,
f12/f15 −66.7°/−63.1°; HardKick f6 −58.5°, f12 −117.4°, f16 −125.6°, f22
−46.0°. Re-swept each the same way v3 did (5° coarse step over the full
circle in the hand-local plane perpendicular to the shaft, minimum |twist|
subject to zero `trident_penetration_bad()` runs at both `n_samples=201`
and `401`, then a 1° refine, then a ±3° stability check):

| key | shaft | new hint (armature) | twist | margin from nearest pocket |
|---|---|---|---|---|
| HardPunch f7 (windup) | (0,0,1) | (0.694658, 0.719340, 0) | +1.24° | wide, stable ±3° |
| HardPunch f12/f15 (shared, `HP_HINT12`) | (0,0,1) | (−0.958820, −0.284015, 0) | −1.35°/+1.22° (joint-optimised) | wide |
| HardKick f6 (`HK_HINT_UP`) | (−0.25,0.80,−0.55) | (−0.684767, −0.545905, −0.482786) | −0.06° | wide, stable ±3° |
| HardKick f22 (`HK_HINT_UP22`) | (−0.25,0.80,−0.55) | (−0.299254, −0.602303, −0.740053) | −10.78° | backed off from the nearest-zero point (+114° from axis, 1° margin) to +108° (7° margin) — a thin pocket opens at +115° |
| HardKick f12 (`HK_HINT_BACK12`) | (−0.20,0.95,−0.22) | (0.926238, 0.111621, −0.360033) | −0.44° | wide, stable ±3° |
| HardKick f16 (`HK_HINT_BACK16`) | (−0.20,0.95,−0.22) | (0.979459, 0.191769, −0.062324) | −0.48° | wide, stable ±3° |

All seven land with zero non-grip `trident_penetration_bad()` runs. The
`_hardpunch_hand_twist_check_fix()` breakdown-key fix (f9's hint lerped
between f7's and f12's own hints) now lerps between the new `HP_HINT7`/
`HP_HINT12` — did not fire this build (f9 already reads clean without it,
`extra pass breakdown frames: []`).

### New rule: Kick's trident stays planted (README/spec new requirement)

Kick never explicitly poses the right arm (it just holds the trident the
way idle does) — under v3 with the old, nearly-centred idle this read as
"the trident doesn't move," but it was never actually pinned: as Hips
moves through the kick the trident (rigidly parented to Hand.R) sweeps
with it. Measured directly before any fix: butt world z ranged −0.29 to
0.00 across the clip's interior frames, far outside the new ±0.02 budget.

Fix: `pin_trident_hand()` (new, `attacks_build.py`) re-solves Arm.R/ForeArm.R
via the hinge-safe IK to put Hand.R's HEAD back at its exact idle world
position, then forces Hand.R's own matrix to the idle snapshot too (same
technique `pin_foot` uses for Foot — IK alone only guarantees the head
lands on target). Since the trident is rigidly parented to Hand.R with a
fixed local transform, this reproduces the trident's idle world transform
exactly at every frame — the butt does not merely stay inside budget, it
is bit-for-bit at the idle value (worst measured drift 0.014 world units,
from between-key Bezier interpolation of the ARM/FOREARM solve itself, not
from the pin's own math — see below). Called from `kick_f4`/`kick_f7`
after `apply_captured_grip("R")` and before `attach_trident()`, and from a
new third check in `_kick_check_fix`'s per-frame pass (`trident_bad =
abs(trident_butt_z()) > 0.02`) so interpolated/breakdown frames that drift
get re-pinned too, always run LAST (after the support/kicking-leg floor
fixes, whose own Hips-z iteration would otherwise re-break an already-good
plant).

Side effect measured and fixed: pinning Hand.R's WORLD transform while
Arm.R/ForeArm.R compensate for Hips motion forces Hand.R's own LOCAL
rotation to absorb whatever the elbow solve didn't — with `pronation=0`
this pushed Hand.R's raw twist to 33–37° at several Kick frames, over the
30° budget (idle itself never touched this value before). `apply_pole._limb_ik`'s
own `pronation` parameter (an explicit twist about the forearm's axis)
cancels this ~1:1; swept `kick_f4` (needs +30 for ~zero) and `kick_f7`
(needs +22) together for the shared constant with the lowest worst-case
|twist|: `KICK_TRIDENT_PRONATION = 27.0` gives f4=+4.96°, f7=−7.09° (worst
7.09°, comfortably under budget). Measured after the full build: Kick's
Hand.R twist now peaks at 15.9° (f4/f9), and the trident butt world z
across all 16 frames is `[0, 0, 0, 0, 0, −0.014, 0, 0, 0, 0, 0, 0, −0.007,
0, 0, 0]` (rounded) — every frame inside the ±0.02 budget, most exactly at
idle.

### New rule: off_hinge < 5° on every solved arm (README 22)

Measured `v115_fixes/yemoja_measure.off_hinge(side)` at every frame of
every clip after the full build. Every explicit KEY of every solved arm
measures ≤ ~0.5° (all arm poses go through `harness.arm_ik_hinge`,
`off_hinge=0.0` explicit) — the one exception being the idle bookends'
right arm (Stephanie's hand-authored grip, 15.65° off-hinge, exempt per
spec). One genuine interpolation-drift violation was found: Punch's LEFT
arm, between the f9 (jab extended, off_hinge=0.0) and f14 (return to
idle, off_hinge=0.0) keys, peaks at −16.22° at f11 (also over budget at
f10/f12/f13: −8.19/−13.17/−4.60) — off_hinge, like twist, is a nonlinear
function of the pose, so Bezier interpolation between two off_hinge=0 keys
does not itself stay near 0 in between. No other clip/side showed this
(Kick's left arm peaks 3.17°, HardPunch/HardKick both arms stay under 5°
at every frame not immediately adjacent to an untouched-idle bookend).

Fix: `_punch_left_hinge_check_fix()` (new extra `enforce_pins` pass on
Punch, same pattern as `_hardpunch_hand_twist_check_fix`) — at each bad
frame, re-solves Arm.L/ForeArm.L via `arm_ik_hinge` (`off_hinge=0.0`) to
the CURRENT interpolated Hand.L position, using the current interpolated
ForeArm.L head as the pole (a "keep whatever bend direction interpolation
already chose, just zero the hinge deviation" solve — this never touches
Hand.L's own rotation, so the fist shape is untouched). Re-measured: worst
−16.22° → −4.6° (f13; every other previously-bad frame now reads 0.0°),
under budget everywhere. `extra pass breakdown frames: [2, 5, 10, 11, 12]`.

**Inherited-baseline caveat on off_hinge, same category as the spec's own
right-arm exemption**: wherever the right arm is genuinely never re-solved
across a WHOLE clip (Punch, Kick pre-fix, and the frames of HardPunch/
HardKick immediately adjacent to the idle bookends before the first
explicit thrust/kick key), its off_hinge reads the idle-inherited 15.65°
constantly, not just at the literal f1/last frames the spec's wording
names. This is Stephanie's untouched grip pose being carried by the
kinematic chain, not a pose this round authored — flagged here rather
than force-solved to 0, since forcing it to 0 would mean re-posing an arm
the spec never asked this round to touch.

### Two more idle-inherited baselines exceeding the (historical) twist budget

`v115_fixes/yemoja_measure`/`harness.twist_deg`'s reading of `Hand.L` and
`Foot.R` at the idle pose itself: Hand.L twist 76.47° (the new A3 idle's
cupped-hand shape), Foot.R twist −26.03° (Kick's own support foot, before
`settle_R`'s re-solve; HardKick's kicking foot elsewhere). Both exceed the
30°/20° `_TWIST_BUDGET` constants — and, like the right arm's off-hinge,
both are IDENTICAL across every frame of every clip, unchanged to 5+
decimal places, because nothing in the pose-building code ever touches
Hand.L's own local rotation (only `Arm.L`/`ForeArm.L` via IK, plus finger
curl) or `Foot.R` before it is actively re-solved. Reported as measured,
inherited model-of-record numbers, same category as the right-arm
off-hinge exemption — not something this round introduced or can fix
without re-posing bones the spec never asked it to touch.

### `Yemoja_ATTACKS_v4.blend`: what's provably unchanged

A `cmp.py`-equivalent script dumped both the source (`Yemoja_WORKING_v115_idleWeights.blend`)
and the output with `verify/dump_json.py` and diffed every field:
- All 8 pre-existing actions (`Yemoja_Fuzz_matAction`, `Yemoja_Idle_Loop`,
  `Yemoja_Idle_MASTER`, `_before_A2`, `_before_A3`, `_v113_corkscrew`,
  `_v115_elbowTuck`, `_v115_poseA_preUserR`) are **byte-identical** —
  `cmp.py`'s own hardcoded 2-action check would have missed 6 of these;
  extended here to check every non-`Yemoja_Atk_*` action.
- `Yemoja_Body`'s vertex-coordinate hash is identical (mesh never touched).
- Bones (80, same names, same `matrix_local`), materials, and the object
  set are all identical; camera set is identical (19 cameras, including
  `_AR_front`/`_AR_side`/`_AR_q34`, all present and confirmed usable for
  review renders).
- Only the four `Yemoja_Atk_*` actions are new, all `use_fake_user=True`,
  correct `frame_range` (1–14/26/16/28). Armature's assigned action is
  `Yemoja_Idle_MASTER` at frame 1 in the saved file (confirmed by re-
  opening `Yemoja_ATTACKS_v4.blend` fresh and reading it back).

### v4 acceptance table (impact frame; per SPEC_rebuild_v4.md's own list)

| clip | impact | worst interp. floor/support | Hand/Foot twist max | off_hinge max (L/R) | Arm/ForeArm/Shoulder/UpLeg/Leg ratio @ impact (min) | signed trident pen. | clothes-inside (worst) | flips remaining | breakdown frames added |
|---|---|---|---|---|---|---|---|---|---|
| Punch | f7 | lowz 0.0011 (f9, key); interp worst 0.0010 (f10); support_err ~0.00001 throughout | Hand.L 76.5°*/Hand.R −12.2°* (both inherited-idle, untouched R arm)/Foot.L −14.1°*/Foot.R −17.8° (within 20° budget) | L 4.6° (f13) / R 15.65°* (inherited, untouched) | Shoulder.R 0.957 (min); all others 0.98–1.07 | 0 non-grip runs, every frame | 81 verts (f7) | 0 | 8 (2,5,6,8,10,11,12,13) |
| HardPunch | f12 | lowz 0.0026 plateau (f8–f21, interp); support_err ≤0.0037 | Hand.R 20.3° (f9, interp, within 30° budget) / Foot.L −12.5°(max)/Foot.R −25.2° (support, inherited-idle range) | L 4.5° (f4) / R 15.82°* (f2, still idle-adjacent; explicit keys read ≤0.5°) | Shoulder.L 0.91 (min, f12); all others 0.92–1.05 | 0 non-grip runs, every frame | 168 verts (f7) | 0 (3 fixed: Hand.R f12/15/26) | 19 (2–6,8–11,14,16–24) |
| Kick | f7 | lowz 0.0053 (f5, interp, within band); support_err 0.1432 (f2, interp; 0.1429 at the f7 key itself — support target intentionally lowered to flatten the idle-raised heel, same design as v3) | Hand.R 15.9° (f4/f9, within 30° budget, post-pronation-tune) / Foot.L −15.9°(kicking)/Foot.R 0.0°(support, flattened) | L 3.17° (f13) / **R 0.24°** (now solved every frame via `pin_trident_hand`, no longer inherited) | Arm.R 0.966 (min, f7); all others 0.95–1.08 | 0 non-grip runs, every frame | 188 verts (f7) | 0 | 8 (2,3,5,8,10,12,14,15) |
| HardKick | f12 | lowz 0.0034 (f8, interp, within band); support_err ≤0.0038 | Hand.R ≤15.65°* (idle-adjacent only; explicit keys ≤0.9°) / Foot.L −12.9°(max, support)/Foot.R 0.0°(kicking, flattened) | L 4.74° (f8) / R 14.92° (f27, idle-adjacent; explicit keys ≤0.4°) | Shoulder.R 0.945 (min, f12); all others 0.93–1.23 | 0 non-grip runs (only expected grip-bone contacts), every frame | 345 verts (f12) | 0 (2 fixed: UpLeg.R f12/16) | 12 (2–5,7–10,17–19,24) |

`*` = idle-inherited baseline (see the two caveat sections above), not a
number this round's pose code produced or can move without re-posing a
bone the spec left to the idle agent.

### Deviations made (v4, summary)

1. **HardPunch f12/f15 yaw**: 25° → 23.5° (`HP_YAW`), full spec lunge depth
   kept (`LUNGE_SCALE=1.0`, better than v3's 5% depth cut) — see its own
   section above for the measured pin-tolerance numbers.
2. **All seven hand-Y trident hints re-swept** for the new trident offset
   and idle geometry — every old hint blew its twist budget under the new
   geometry; new values and margins tabulated above.
3. **HardKick f22's hint** (`HK_HINT_UP22`) traded 2.6° of extra twist
   (−10.78° vs the nearest-zero −8.15°) for 7° of margin from a thin
   penetration pocket, same risk-management call v3 made at its own f16.
4. **Kick's trident-plant mechanism** (`pin_trident_hand`,
   `KICK_TRIDENT_PRONATION=27.0`) is new — not a deviation from a v3
   number (Kick's right arm was never actively posed before), but is the
   single largest new piece of pose-building logic this round added.
5. **Punch's off-hinge breakdown-key pass** (`_punch_left_hinge_check_fix`)
   is new, same category as v3's own `_hardpunch_hand_twist_check_fix`.

### What still misses (v4)

- **Right-arm off-hinge inherited baseline (15.65°) persists outside the
  literal f1/last frames** in Punch (whole clip, until the trident-plant
  fix makes Kick's own case moot), and in the idle-adjacent stretches of
  HardPunch/HardKick before the first explicit thrust/kick key. The
  spec's exemption names "f1/last only"; this reads that exemption as
  extending to every frame the arm is genuinely untouched (matching its
  stated INTENT — "inherited, not yours" — rather than its literal two-
  frame wording), and is called out here rather than silently assumed.
- **Hand.L (76.47°) and Foot.R (−26.03°) idle-inherited twist baselines**
  exceed the historical 30°/20° `_TWIST_BUDGET` constants at every frame,
  same inherited-not-introduced category — see that section above.
- **Clavicle region ratios still plateau near 0.90–0.95** at a few frames
  (HardPunch Shoulder.L 0.91 at f12; HardKick Shoulder.R 0.945 at f12) —
  reported, not chased further past the 0.90 floor, same precedent v3
  established (a joint-weight characteristic of the model of record, not
  a pose choice this round can trade away without re-litigating item 7's
  elevation caps).
- **Clothes self-penetration counts (81–345 verts, worst at HardKick f12)**
  not eliminated — informational per v3's own established precedent, not
  a gating number in either spec.
- No render-level (pixel) QA was performed beyond confirming the renders
  exist and the four `_AR_*`/general cameras used are present and correctly
  named; a human visual pass on `review/v4/*.png` is still worth doing
  before this ships.


Built against `Yemoja_WORKING_v114_idleClean.blend`, saved as
`Yemoja_WORKING_v115_attacks.blend`. All four clips (`Yemoja_Atk_Punch`,
`Yemoja_Atk_HardPunch`, `Yemoja_Atk_Kick`, `Yemoja_Atk_HardKick`) built,
reported, and reviewed. 80 bones, 43 objects, same **18 cameras** as v114
(corrected here — earlier drafts of this file said 17; `cmp.py` counts 18 in
both v114 and v115, none added or removed) — nothing added.
`Yemoja_Idle_MASTER` untouched. All four actions carry `use_fake_user = True`.

**This file was rewritten 2026-09-04 for fix round v3** (`SPEC_fix_v3.md`,
itself written against `VERIFY_attacks.md`'s independent verification of the
v2 build below). Read `## Fix round v3` near the end first — it is the
current, authoritative acceptance record, built entirely from the numbers
`verify/eval_all.py`, `verify/pen.py`, `verify/deep.py`, `verify/arc.py`,
`verify/keys.py` and `verify/cmp.py` printed against this file, per-frame
(not key-only), as SPEC_fix_v3 item 13 requires. Everything below that
point is the original build's history (v1 and the HardKick v2 rebuild) and
is kept for context; where a v3 change supersedes a specific number in it
(e.g. HardKick f12's Shoulder elevate, changed again below), the old
paragraph is left as-is with the superseding change noted in the v3 section
rather than rewritten in place.

## What was already there

`harness.py` and the first WIP pass on `Yemoja_Atk_Punch` existed from an
earlier session. I read it, kept the design, fixed two real bugs in it
(below), finished tuning Punch, and built HardPunch/Kick/HardKick from
scratch, then wrote `attacks_build.py` as the single idempotent entry point.

## Two harness bugs fixed (not spec deviations — these were wrong before
## any clip was built on top of them)

1. **`world_delta_to_armature` used the wrong axis mapping.** It returned
   `(x*100, y*100, z*100)`, documented as "no rotation between the two
   frames, only the 0.01 object scale." That's false: armature space has
   +Y up / +Z forward, world space has +Z up and she faces world −Y — a
   90° axis permutation, not identity. Measured directly
   (`loc("Hips", *old_fn((0,0.1,0)))` moved the Hips by world `(0,0,+0.1)`,
   not `(0,0.1,0)`). Every Hips "move (x,y,z) world" instruction in the spec
   that has a nonzero Y or Z would have landed wrong — e.g. HardPunch f7's
   `(-0.15,+0.20,-0.10)` would have RISEN by 0.20 instead of dropping 0.10.
   Fixed mapping: armature args = `(world_x, world_z, -world_y)`; verified
   to round-trip exactly for arbitrary vectors.
2. **`trident_clearance`'s exclusion list didn't cover the gripping
   fingers.** It excluded `Hand.R`/`ForeArm.R` only, but the finger bones
   are separate dominant-bone names (`HandPinky1.R` etc.), so the single
   nearest point on any real grip was always a knuckle and was never
   actually excluded. Measured before the fix: HardPunch f7 reported
   clearance 0.0019 at `HandPinky1.R` — literally the grip, not a body
   collision. Added all 15 right-hand finger bones to the exclusion set;
   HardPunch f7 clearance became 0.395.

## Deviations from the spec's literal numbers, with the reason and the
## measured numbers

### Left fist shape (all four clips)
Spec: curl `(70,95,60)`, thumb `(30,40,20)`. Measured this crushes the left
hand badly **independent of arm position** — with the arm untouched at
idle, closing to that curl alone gives `HandPinky3.L` 0.646 (7/63 crushed),
`HandRing3.L` 0.659 (5/63). This rig has no per-finger-joint weight
smoothing (README §12 only smoothed the wrist/elbow), and README §14
documents the identical problem on the right hand's grip fingers. Eased to
curl `(50,68,42)`, thumb `(24,32,16)` — still reads as a closed fist (finger
detail is invisible at silhouette scale — confirmed by rendering both and
comparing) while keeping the worst *non-exempt* region measurably higher.
Several left-hand finger regions remain below 0.90 regardless of curl choice
— see "Cannot be fixed" below.

### Punch f7/f9 — Shoulder.L elevate 6° → 24°
At the spec's literal 6°, `Yemoja_Body/Shoulder.L` measured 0.888 (6/74
crushed) — below both the 0.90 floor and the 0.95 `Shoulder.*` floor.
Protract was kept at spec's 18° throughout (that's the strike's real
driver); elevate was swept: 6°→0.888, 12°→0.896, 18°→0.930, 24°→0.970. Used
24°.

### Punch f7/f9 — fist reach clamped
Spec's frame-7 target `(0.45,−1.95,5.75)` is 2.054 world units from the
(rotated) shoulder; max straight-arm reach is `(Arm+ForeArm)*0.97` = 1.683.
`limb_ik`'s own tolerance is 99.9% of raw length, so an unclamped call
returns `ok=False`. Pulled the target back along the *same ray* (direction
and height preserved) to the 0.97-of-max budget. f9's "0.1 further out" is
also clamped to the same ceiling — the arm is already at full stretch at f7,
so f9 lands at essentially the same extension.

### HardPunch f12 — Hips lunge scaled 0.95×
Spec: Hips move `(0.10,−0.35,−0.15)` at Hips yaw +25°. At that literal
magnitude, the rear (right) leg's ankle comes out 0.00504 world units short
of the fixed idle ankle position — `leg_ik` itself reports `ok=False`
there (asked to fully straighten past its own length), and `pin_foot`'s
`<0.005` tolerance fails by 0.00004. Scaled the translation to 95% —
`(0.095,−0.3325,−0.1425)` — which measured 0.0046, under tolerance. Yaw and
direction are exactly as specified; only the lunge *depth* is reduced 5%.

### HardPunch f12/f15 — Shoulder.R elevate 5° → 15°, Shoulder.L elevate 0° → 16°
Spec only specifies Shoulder.R elevate 5° (protract 20° kept exact). At 5°,
`Shoulder.R` measured 0.913 (below the 0.95 floor); swept to 15° → 0.965.
Shoulder.L isn't mentioned at f12 (this arm swings back near shoulder
height, not overhead), but at 0° elevate `Shoulder.L` measured 0.935 (below
0.95); +16° → 0.982. Added per the same clavicle mechanism the spec already
invokes for the striking shoulder.

### HardPunch f12/f15 — trident hand-Y hint changed
Spec says orient the shaft with `orient_hand_for_shaft`, windup hint +Y. At
that hint at f12/f15, the forearm/hand geometry produced Hand.R twist −58°
(budget 30°) and `trident_clearance` 0.027 (budget 0.15). Swept the hint
through a full circle in the plane perpendicular to the shaft; `(-0.87,0.5,0)`
(≈−150° from the windup's hint) is the point nearest zero twist that also
clears the body: twist 3.4°/8.7° (f12/f15), clearance 0.289/0.290. Shaft
direction itself is unchanged (armature +Z, per spec) — only the free spin
DOF (which hand-side faces which way) moved.

### Kick f7 — ankle target extended along its own ray, 58% → 88% of max reach
Spec's literal target `(0.40,−1.90,3.1)` is only 2.29 world units from the
(rotated) hip socket against a max straight-leg reach of 3.96 — 58%
extension. At that extension the knee necessarily sticks out sideways by
~38% of the leg's length (2-bone-triangle geometry: h=1.49 of a 3.96 chain)
and the render showed a visibly *chambered*, not extended, leg — it did not
read as "leg straight, foot ball leading" as the spec's own text asks for,
confirmed by rendering both. Extended the *same ray* (direction and height
from the hip socket through the spec's point preserved) out to 88% of max
reach. Leg-region deformation at that extension is fine (`Leg.L` 0.984,
`Foot.L` 1.003, `UpLeg.L` 1.024) — README §4 already notes full leg
extension is safe on this rig.

### HardKick f6 — elbow pole changed for the trident-hold arm
At the "generic hold" pole `(-1.6,0.9,4.8)` (used for every other
trident-hold pose), Hand.R twist measured −61° (budget 30°) and was *stuck*
there across a full hint sweep on `orient_hand_for_shaft` (−48° to −73°
every direction) — the free-spin DOF wasn't the cause, the forearm's own
orientation was. Swept the pole instead: `(-1.0,1.0,4.5)` measured −10.1°,
with `trident_clearance` 0.73.

### HardKick f12/f16 — trident shaft tilted (0,0,1) → (0,0.5,1)
At a shaft held flat along armature +Z (spec's literal instruction for the
other clips), the raised kicking thigh (`UpLeg.R`, swinging through head
height) crosses the shaft's fixed line — measured `trident_clearance` 0.029,
and the nearest point's dominant bone was `UpLeg.R`, well outside the
hand/finger exclusion, i.e. a real clash with her own leg, not a grip
artifact. Tilted the shaft up ~27° off horizontal (still "swept back for
counterbalance," just angled) — clearance rose to 0.473/0.337 (f12/f16),
Hand.R twist 16.8°/12.1°, both comfortably in budget.

### HardKick f12/f16 — support foot: plain `pin_foot`, not a custom pivot
Spec says the support foot is "allowed to rotate about its own vertical
axis... not to translate" at the +55°/+70° hip yaw. Read literally, I first
built a custom `pivot_support_foot()` that pinned position and then applied
an *explicit* world-vertical rotation matching the hip yaw on top. That
made things worse, not better: `Foot.L` twist scaled with how much of the
yaw got imposed on it (−34° at f12, −43° at f16, both over the 20° budget),
while the floor-sink it was meant to fix (see next item) turned out to be
unrelated to yaw at all. Reverted to plain `H.pin_foot` (full idle position
*and* orientation lock, exactly as every other clip's support foot) —
measured cleanly at both yaws: zero position error, `Foot.L` twist only
3.0–3.9°. The spec's clause is a permission, not a requirement, and the
plain lock already satisfies everything it exists to protect.

### HardKick f12/f16 — support-foot floor sink, fixed by the same change
Before the fix above, `lowest_world_z` (kicking foot excluded) measured
−0.0057 at f12 and −0.0132 at f16 — both outside the required
[−0.005, +0.02] band, and the lowest vertex was on `Foot.L` (the *support*
foot), not the kicking leg. The custom pivot's incidental leg-chain
orientation was tilting the support sole into the floor; forcing the
idle-flat orientation (via plain `pin_foot`) fixed it as a side effect:
−0.0012 / −0.0020.

## Cannot be fixed within this scope (documented, not silently accepted)

- **`Yemoja_Body/HandPinky2.L` sits at 0.859 in the untouched
  `Yemoja_Idle_MASTER` pose itself** — measured with *zero* posing applied,
  before any attack pose is built. This is below the spec's 0.90 floor and
  is present at frame 1/last of every clip. It is not introduced by any
  attack; no left-hand curl choice changes it (tested curl values from 0°
  to 70°, all give 0.73–0.86 for this region). Fixing it needs finger-joint
  weight smoothing on the model of record (README §12's fix only covered
  wrist/elbow), which is out of scope for an animation-only pass and would
  need to go through the two-agent working agreement in README §11.
- **`Yemoja_Clothes/Arm.L` and `Arm.R`** (a 2-face cloth patch near each
  shoulder, likely a strap/tassel) sit at 0.751/1.073 even at idle and drop
  as low as 0.475–0.663 under almost any arm motion in any direction, on
  either side, across all four clips. This is required to be ≥0.95
  (`Arm.*`). Given the idle baseline already fails 0.95 for the left side,
  and the same 2-face region reacts sharply regardless of which arm moves
  or which way, this reads as a rigging/weighting artifact on a tiny mesh
  island rather than anything a pose choice can correct.
- **HardKick f16/f22 — `Yemoja_Body/Shoulder.R` plateaus around 0.89–0.90**,
  never reaching the 0.95 `Shoulder.*` floor. Swept elevate from 10° to 28°
  at f12 (0.878 → 0.892 → 0.904, essentially flat past ~20°); the
  counterbalance sweep at f16/f22 measured 0.893/0.898. This is the
  trident-holding arm during the most extreme pose in the set (a
  head-height kick with +55°→+70° hip rotation); elevation alone doesn't
  clear it the way README §3's table (measured on a simple sideways raise)
  would predict. Left at elevate 20-25° (best measured) rather than pushing
  further for diminishing returns.
- **HardKick f12 — the "roundhouse" doesn't read as a lateral arc.**
  Kicking to head height (z=6.2) from a hip socket at z≈4.3 with a leg that
  maxes out at 3.96 world units leaves very little slack for the knee to
  swing wide once the leg is that close to full extension — moving the
  knee hint across a wide range barely changed the silhouette (confirmed by
  rendering several). The impact pose reads clearly as a dramatic, extreme
  high kick (foot at/above head height, unmistakable in the front and q34
  silhouettes) rather than a classic side-on roundhouse sweep. Given
  Stephanie's brief is "dramatic moves and combos" (README §1), I judged
  this an acceptable reading of "roundhouse to the head" rather than a
  failure, but it's a real shape deviation worth a human look.

## Acceptance table

All four actions: frame 1 and the last frame equal `Yemoja_Idle_MASTER`
exactly (max bone-quaternion delta 0.0000°, computed by the harness on
every clip's first/last key). `Yemoja_Idle_MASTER` itself measured `Hand.R`
twist 29.88° and `Foot.R` twist −16.14° — both inside budget but the hand
figure is only 0.12° under the 30° ceiling *at rest*, pre-existing, not
introduced by any clip.

| Clip | Impact f | Worst non-exempt region (impact) | Crushed (impact) | Hand.L/R twist (impact) | Foot.L/R twist (impact) | Support-foot err (max, all keys) | Lowest body z (min, all keys) |
|---|---|---|---|---|---|---|---|
| Punch | 7 | Yemoja_Body/HandPinky2.L 0.732 | 6/28 | 19.8° / 29.9° | 1.5° / −8.5° | 0.0000 | −0.0000 |
| HardPunch | 12 | Yemoja_Clothes/Arm.L 0.475 | 1/2 | 19.8° / 3.5° | −0.4° / −12.6° | 0.0046 | −0.0000 |
| Kick | 7 | Yemoja_Body/HandPinky2.L 0.732 | 6/28 | 19.8° / 29.9° | −6.7° / −9.2° | 0.0000 | 0.0715 (kicking foot; support/body floor clean) |
| HardKick | 12 | Yemoja_Clothes/Arm.R 0.485 | 1/2 | 19.8° / 16.8° | 3.0° / −16.1° | 0.0000 | −0.0057→−0.0012 after fix (see deviations) |

All four: support-foot ankle error stays ≤0.0046 world units at every key
frame (spec floor 0.005) — HardPunch f12/f15 is the only clip that isn't
exactly 0.0000, for the reason above. Lowest body z stays in
[−0.005, +0.02] at every key except the kicking foot itself (Kick/HardKick),
which is excluded by spec. Hand twist stays ≤29.9° everywhere (budget 30°,
and that ceiling figure is the untouched idle pose, not anything built
here). Foot twist stays ≤19.8° in magnitude everywhere sampled that isn't
the untouched idle Foot.R figure of −16.14° (budget 20°). `Arm.*`,
`ForeArm.*`, `Shoulder.*`, `UpLeg.*`, `Leg.*` on `Yemoja_Body` are ≥0.95 at
every key in every clip *except* HardKick's `Shoulder.R` (documented
above). The `Yemoja_Clothes/Arm.L`/`Arm.R` 2-face patch and several
left-hand finger regions are the only other sub-0.95 items, all documented
above as pre-existing/unfixable in this scope. HardPunch's
`trident_clearance` measured 0.29–0.40 across f7/f12/f15 (budget 0.15,
after the exclusion-list fix); HardKick's measured 0.18–0.73 across
f6/f12/f16/22. HardKick's clothing-penetration count (spec's "flag above
350" check) measured 190/233/223/317 at f6/f12/f16/22 against an idle
baseline of 102 measured with the same method — all comfortably under 350.

Full per-frame tables, every region below 0.95, and the worst-5 list per
frame are in `review/{clip}_report.md`. Renders for every reported frame
(clay ×3 cameras + silhouette ×3 cameras) are in `review/`.

## HardKick v2 rebuild (2026-09-03, SPEC_hardkick_v2.md)

Reviewer verdict on the v1 block above ("HardKick f12 — the roundhouse
doesn't read as a lateral arc"): confirmed and acted on. v1 read as a
straight-up front high kick and the trident lay flat across her chest at
f6/f22. Rebuilt `Yemoja_Atk_HardKick` only, same 28 frames / impact f12 /
key frames (1,6,12,16,22,28); Punch, HardPunch and Kick are untouched (same
keys, re-verified byte-for-byte identical report numbers after the rebuild
— see the acceptance table above, unchanged).

**Support-foot pivot redesign.** v1's `pivot_support_foot(side, yaw_deg)`
rotated `Foot.L`'s orientation alone on top of an knee hint that was never
rotated, so the leg solve and the forced foot orientation disagreed and the
mismatch landed entirely on `Foot.L`'s own twist (−34° at yaw 55, −43° at
yaw 70, budget 20°) — exactly the failure the v2 spec calls out. Replaced
with a new `pivot_support_foot(L, side, snap, pivot_deg, ...)` in
`harness.py` that rotates the WHOLE support leg — the knee hint AND the
final foot orientation — together about world-vertical through the idle
ankle (pseudocode straight from SPEC_hardkick_v2.md). Measured Foot.L twist
at every key with the new function: f1 −6.7°, f6 (pivot 15°) −3.0°, f12
(pivot 55°) −4.7°, f16 (pivot 70°) −5.2°, f22 (pivot 15°) −2.0°, f28 −6.7°
— all comfortably inside the 20° budget and all near the idle figure, and
support-foot ankle error is 0.0000 at every key (floor 0.005). Also added
`seg_seg_dist()` to `harness.py` (closest distance between two 3D segments)
for the shaft-vs-kicking-leg check the spec asks for.

**New keyframe numbers.** All five posed keys (f6/f12/f16/f22, f1/f28 are
untouched idle) rebuilt from SPEC_hardkick_v2.md's table: Hips yaw sign
flipped from v1 (now positive, right hip forward — v1 had it backwards,
which is the root cause of the "front kick" read), pelvis translated
per-key via the (bugfixed) `world_delta_to_armature`, kicking leg driven
straight off the spec's ankle/knee-hint world coordinates, torso lean/twist
via an even split of the spec's "total" spine-chain figures across
Spine/Spine1/Spine2 (no per-bone split given), Neck/Head per the spec
(explicit values at f12/f16, an even 8/7 split of f6/f22's stated "total").

**Toe plantarflexion at f12 (new).** `leg_ik` only orients `UpLeg.R`/`Leg.R`
— `Foot.R`/`ToeBase.R` are left at their idle-relative angle, which reads
badly once the shin has swung up to head height: measured `ToeBase.R` tail
vs `Leg.R` direction at 79.3° before any correction (spec floor 15°). A
local-X-only pitch fixes the toe angle (−65° → 14.3°) but pushes `Foot.R`'s
own twist from its baseline −16.1° (already close to the 20° ceiling at
this extreme leg pose, baked in by how `limb_ik` constructs `Leg.R`'s frame
here — every other key's untouched `Foot.R` sits at the same −16.1°) to
−30.1°, over budget. Fixed by adding a local-Y spin on top (twist is ~1:1
sensitive to local Y; local Y barely moves the toe angle) — swept and
locked `lrot("Foot.R","X",-65)` + `lrot("Foot.R","Y",30)`: toe/shin 14.5°,
Foot.R twist −0.1°, both comfortably in budget.

**Trident hand-orientation hints (new, per key).** `orient_hand_for_shaft`
leaves one spin DOF free; the spec's fixed shaft directions needed a
per-key hint sweep (3° steps in the plane perpendicular to the shaft) for
min `|twist_deg("Hand.R")|` with `trident_clearance ≥ 0.15`:
- f6: shaft ≈(−0.25,0.80,−0.55), hint (0.868,−0.066,−0.491) → twist 16.5°,
  clearance 0.194.
- f22: same shaft/target as f6, but the pose's own hips/spine numbers
  differ enough that f6's hint only cleared 0.146 here (just under floor)
  — re-swept for this frame specifically: hint (0.739,−0.209,−0.640) →
  twist 26.3°, clearance 0.174.
- f12: shaft ≈(−0.35,0.55,−0.75), hint (0.731,0.666,0.148) → twist −23.6°,
  clearance 0.125 (see wrist-target deviation below).
- f16: same shaft as f12, hint (0.876,0.479,−0.057) → twist −21.1°,
  clearance 0.125-0.150 depending on the shoulder-elevate value (below).

**f12/f16 wrist-target deviation (clearance shortfall, documented per the
spec's own fallback clause).** At the spec's literal f12 hand target
(−1.05,1.55,5.60), the trident's grip sits 55.6% up the shaft, so ~44% of
it hangs on the OTHER side of the hand from the tip — this "butt" end swept
down through `Spine1` at only 0.045 clearance, a real torso hit (confirmed:
nearest vertex's dominant bone was `Spine1`, not a grip/hand false
positive), because the torso is leaning back (spine X −20) directly into
that segment's path. No hint choice fixes this (rotation about the shaft
axis barely moves the shaft's own line, confirmed by sweeping the full
360° at the literal target: best achievable there was 0.045). Moved the
wrist target sideways/back, away from the torso, along directions roughly
perpendicular to the swing, holding it at the arm's own reach budget;
best found within BOTH the reach budget and the twist budget was
(−1.534,1.169,5.688): clearance 0.125, twist −23.6°. This is short of the
0.15 clearance target — recorded per SPEC_hardkick_v2.md's own instruction
("if a number cannot be met, move the target minimally along its own ray
... record it; do not fall back to the v1 vertical kick") rather than
flattening the shaft direction, which would fix clearance but recreate the
v1 "flat across the chest" problem the whole rebuild exists to fix. Same
issue, same fix, at f16 (target (−1.713,0.791,5.694) vs spec's literal
(−0.85,1.55,5.65)): clearance 0.125-0.150, twist −21.1°. In both cases the
nearest-approach point that limits clearance is the KICKING LEG itself
(`UpLeg.R`, confirmed by nearest-vertex dominant-bone lookup) rather than
the torso once the wrist is moved this far — i.e. the shaft is threading
between the raised leg and the torso, which is inherent to a swept-back
counterbalance arm during a head-height kick, not a construction error.
`seg_seg_dist` against the kicking leg's own bone segments never reaches
zero at any key (f6 0.72/1.17, f12 0.29/1.05, f16 0.009/0.62, f22 0.57/1.04
— thigh/shin respectively): the shaft's centerline comes within 0.009 of
`UpLeg.R`'s bone segment at f16, but the actual mesh-clearance check above
(0.125-0.150, against the real deformed surface, not the bone centerline)
confirms it does not cross the leg.

**f16 Shoulder elevate (new — spec is silent at this key).** With no
Shoulder rotation at all (matching v1's omission), `Shoulder.L`/`Shoulder.R`
on `Yemoja_Body` measured 0.728/0.698 at f16 — clearly worse than f12's own
spec-driven numbers, because the arm is further into the follow-through
here. Added elevate scaled up from f12's pattern (README's clavicle rule:
elevate with arm height) and swept: 25°/−35° raised them to 0.816/0.882
while keeping Hand.R twist (−21.1°) and trident_clearance (0.125) both in
budget. Diminishing returns past this — swept up to 50°/−70°, which only
reaches 0.905/0.800, still short of the 0.95 floor, so kept at 25°/−35°
rather than chasing an unreachable target with increasingly unnatural
clavicle rotation.

**f12 Shoulder elevate kept at the spec's literal 8°/−15°, not pushed
further.** Unlike f16, the spec gives explicit numbers here. A sweep up to
40°/−60° shows `Shoulder.R` ratio DOES keep climbing (0.751 → 0.902 at
30°/−40° → 1.038 at 40°/−60°) but `Hand.R` twist degrades in lockstep
(−23.6° at 8°/−15° → −37° at 30°/−40° → −50° at 40°/−60°, budget 30°) —
twist is measured relative to `ForeArm.R`'s OWN orientation, which re-bends
as the elbow accommodates a different shoulder position even though the
wrist target itself doesn't move. Kept the spec's own 8°/−15°, which is the
only value in that family that keeps twist safely inside budget;
`Shoulder.L`/`Shoulder.R` measure 0.750/0.751 at f12 — short of 0.95, a
measured rig limit under this constraint, not a silent miss.

**Spec-asked-for numbers, all keys, final build:**

| f | Foot.L twist | Hand.R twist | trident_clearance | shaft↔UpLeg.R / shaft↔Leg.R dist |
|---|---|---|---|---|
| 1 (idle) | −6.7° | 29.9° | 0.725 | 1.209 / 0.946 |
| 6 | −3.0° | 16.5° | 0.194 | 0.722 / 1.173 |
| 12 (impact) | −4.7° | −23.6° | 0.125 | 0.290 / 1.053 |
| 16 | −5.2° | −21.1° | 0.125 | 0.009 / 0.617 |
| 22 | −2.0° | 26.3° | 0.174 | 0.570 / 1.040 |
| 28 (idle) | −6.7° | 29.9° | 0.725 | 1.209 / 0.946 |

f12-specific (spec-required): thigh (`UpLeg.R`) angle to horizontal 39.5°
(spec range +20° to +40°, rising toward the target — passes, near the
upper edge); knee lateral offset from the hip→foot line 1.557 world units
toward −X (spec floor 0.5 — comfortably clears it; the knee sits well to
her right of the line, never above it); hip→foot(target) distance 2.216 =
56.0% of the 3.96 max reach (spec target 60-70% — slightly under; this
comes directly from the spec's own literal f12 ankle/knee-hint numbers, and
the resulting silhouette still clearly reads as a lateral roundhouse with
the knee well clear of the hip→foot line in the q34/side renders, so left
as measured rather than re-extending the ankle target purely to hit the
percentage).

Visual check (q34/side renders, `review/Yemoja_Atk_HardKick_f12_*`,
`_f6_*`, `_f16_*`): the kick now reads as a horizontal arc with the knee
folded to her right at impact, torso leaning back and away, and the
trident tip swept up and back over her right shoulder at every key —
never flat across the chest — fixing both complaints in the reviewer
verdict.

Idle delta at f1/f28 remains exactly 0.0000° and support-foot error exactly
0.0000 at every key (see full report table above). `Yemoja_WORKING_v115_attacks.blend`
re-saved; `review/Yemoja_Atk_HardKick_report.md` and all six frames'
renders regenerated/overwritten.

**Correction to this section, made in fix round v3 below:** the "0.0715"
Kick idle-bookend figure quoted throughout this file's old acceptance table
was mislabeled "(kicking foot; support/body floor clean)". `eval_all.py`'s
own `lowbone` field at f1/f16 (with `Foot.L`/`ToeBase.L` excluded, as the
Kick clip always excludes) is **`ToeBase.R`** — the SUPPORT foot's own toe,
not the kicking foot, and it does not mean the floor is "clean" at those
frames independent of the exclusion; it is simply how `Yemoja_Idle_MASTER`
itself is shaped (idle_delta = 0.000° there, i.e. this is the untouched
idle pose, not a built pose) — a normal one-foot-weighted idle stance where
the off-weight foot's toe sits fractionally clear of the floor. See the v3
acceptance table for the corrected per-clip numbers.

## Fix round v3 (2026-09-04, `SPEC_fix_v3.md`)

`VERIFY_attacks.md` independently re-derived the v2 build's own numbers and
found them reproducible, then flagged real defects the v2 acceptance table
had missed because it only sampled key frames: a live quaternion-hemisphere
bug (HardPunch `Hand.R`, HardKick `UpLeg.R` — Blender interpolates
`rotation_quaternion` component-wise, not by SLERP, so two keys that land in
opposite quaternion hemispheres make the interpolated frames between them
swing the long way round), the trident actually inside `UpLeg.R`/`Leg.R` at
HardKick f12/f16 (the old `trident_clearance` metric only checked distance
to the *nearest vertex*, which cannot tell "buried inside a limb" from
"grazing its surface" — replaced per item 13's own instruction), the Kick
clip's support foot never actually touching the floor between keys, and no
between-key pinning at all (a pinned support ankle or floor plant was only
ever checked/fixed AT the five to six keyframes each clip built by hand;
Blender's own interpolation was free to drift on every frame in between).
`SPEC_fix_v3.md` is the response to every one of `VERIFY_attacks.md`'s
findings, in priority order; this section reports what was done and the
resulting numbers, sourced only from `verify/`'s own scripts run against the
rebuilt file (copied into `/tmp/vf/` alongside a fresh `Yemoja_WORKING_v114_idleClean.blend`
and `Yemoja_WORKING_v115_attacks.blend`, per the spec's own instruction to
"adapt their paths and run them against the rebuilt file").

### What changed, item by item

1. **Quaternion hemisphere continuity.** `harness.fix_quaternion_hemispheres(action)`
   walks every bone's four `rotation_quaternion` fcurves key-by-key and
   negates the whole quaternion (all 4 components + both handles) at any key
   whose dot product with the previous key is negative, then every clip runs
   it right after `build_clip`. Flips fixed on every rebuild: HardPunch
   `Hand.R` at f12/f15/f26 (3), HardKick `UpLeg.R` at f12/f16 (2), Punch and
   Kick 0 (their hand/leg motion never crosses a hemisphere boundary).
   `verify/arc.py` and an ad-hoc per-frame scan (below) confirm 0 remaining
   flips and 0 twist-budget violations anywhere in any of the four clips.
   Two further, narrower classes of the same underlying problem (a large
   free-spin difference between two individually-fine keys; plain
   Bezier/AUTO_CLAMPED curve overshoot on a long inter-key gap) were found by
   the per-frame arc scan on top of this and are fixed separately — see
   "Two further twist fixes" below.
2. **HardKick f12/f16 trident.** Rebuilt to the FABLE-exact numbers (hand
   world position, shaft direction) SPEC_fix_v3 item 2 gives. `verify/pen.py`
   (signed 5-direction BVH ray-parity test, replacing the old unsigned
   nearest-vertex `trident_clearance` — `harness.trident_clearance` no
   longer exists, only `trident_penetration`/`trident_penetration_bad`) finds
   **zero non-grip shaft-inside-body runs at every frame of every clip,
   HardKick f12/f16 included** — the only runs anywhere are the expected
   grip contacts (`ForeArm.R`, `HandPinky2.R`, `HandRing2.R`,
   `HandMiddle2.R`, `HandIndex2.R`, all "0.0cm-deep-ish", i.e. surface
   grazing, not penetration). This is a complete fix of VERIFY's finding
   (old numbers: 0.28-0.29 world units *inside* `UpLeg.R` at f12, 0.65
   *inside* `Leg.R` at f16).
3. **Kick: plant her.** `harness.settle_floor()` rewritten (two real bugs
   found and fixed in it — see "settle_floor bugs" below) and called from
   `attacks_build.py`'s `settle_R()` with `exclude=KICK_LEG_L_FULL`
   (`UpLeg.L`/`Leg.L`/`Foot.L`/`ToeBase.L`/`Toe_End.L`, not just the foot —
   the earlier, narrower exclusion let the still-airborne kicking leg
   contaminate its own convergence measurement). `verify/eval_all.py`'s
   per-frame `lowz_ex` for Kick now reads ≈0.0000 at every interior frame
   (f2-f15) and the report is worded to name the excluded region explicitly
   ("excluding Foot.L/ToeBase.L", matching `eval_all.py`'s own `kick_ex`
   list) rather than the old ambiguous "kicking foot" phrasing corrected
   above.
4. **Between-key pinning.** `harness.enforce_pins(L, name, fix_frame,
   check_frame, max_iter=6)` steps every frame of a built clip; wherever the
   support ankle deviates or the excluded-region floor leaves
   [−0.005, +0.02], it detaches the action, calls the clip's `fix_frame` to
   re-solve that frame's pose, and re-keys ALL humanoid bones there as a new
   breakdown key, iterating to a cap of 6 passes. Breakdown frames added
   this build (folds in every later pass, including the twist-overshoot
   backstop, item 1's extra fixes, and HardPunch's own extra pass):
   Punch `[2,3,5,6,8,10,11,12,13]` (converged in 9 passes then 0),
   HardPunch `[2,3,4,5,6,8,9,10,11,13,14,16,17,18,19,20,21,22,23,24,25]`
   (17 passes then 0, plus 1 extra pass at f9, plus twist-overshoot fixes at
   f13/14/16/20), Kick `[2,3,8,10,11,12,13,14,15]` (6 then 3 then 0 passes),
   HardKick `[2,3,4,5,7,8,9,13,18,20]` (10 passes then 0, plus a
   twist-overshoot fix at f25). `verify/eval_all.py`'s own per-frame scan
   (every integer frame, not just keys) confirms 0 floor-band violations
   anywhere.
5. **Head tracking.** `harness.aim_head(L, target_angle=0.0, tol=...)`
   splits the correction between Neck (half) and Head (remainder) to keep
   the face within budget of armature +Z; called at every posed key of every
   clip with `tol=10.0` (Punch/HardPunch/Kick) or `tol=15.0` (HardKick).
   Measured head angle to +Z at every key (from the build's own report,
   cross-checked against `verify/eval_all.py`'s per-frame print, which
   reports the same figure at every non-key frame too — no key or
   interpolated frame anywhere exceeds budget): Punch 4.58°/8.15°/4.51°/
   4.35°/4.58° (f1/4/7/9/14); HardPunch 4.58°/0.0°/0.0°/0.0°/4.58°
   (f1/7/12/15/26); Kick 4.58°/4.58°/9.25°/4.58°/4.58° (f1/4/7/9/16);
   HardKick 4.58°/10.42°/0.0°/0.0°/11.97°/4.58° (f1/6/12/16/22/28, budget
   15°). All well inside budget.
6. **Feet at impact.** `harness.plantarflex_and_detwist` (bisects local-X for
   the toe-shin angle, then local-Y to cancel the twist introduced) replaces
   the v2 hardcoded X/Y numbers at HardKick f12 and now also runs at f16.
   `verify/pen.py`'s own toe-angle measurement (ankle→toe tail vs shin):
   HardKick f12 **14.7°**, f16 **12.5°** (both within the 15° budget). Kick
   f7's plantarflex target is 20-35°; not independently re-measured by
   `pen.py` (it only reports HardKick's toe angle) but built the same way as
   the harness's other toe-angle bisections, with the target set in-range.
7. **Clavicles.** Punch f7/f9 Shoulder.L capped to the spec's literal ≤8°
   elevation with 18° protraction, plus Shoulder.R retraction 6° (built and
   verified — `deep.py`'s f7/f9 key-region print shows `Shoulder.L` 0.883/
   0.887, `Shoulder.R` 1.012, in the ≈0.89-0.91 range item 7 explicitly
   accepts and asks to be reported, not chased further — a joint-weight
   issue for the model of record). HardPunch f12/f15: Shoulder.R elevation
   ≤8° with 20° protraction, Shoulder.L retraction 6° (the v2 +16° elevate
   removed) — `deep.py` shows `Shoulder.L`/`Shoulder.R` 0.927/0.931 (f12),
   0.943/0.922 (f15), same accepted range. HardKick f16: Shoulder.R capped
   to `HK_F16_SHOULDER_R = 1.71°` (measured as 1/3 of Arm.R's own f12→f16
   world-Z rise, per the spec's exact rule), Shoulder.L held at 0.
8. **HardKick arms in the chest frame.** `harness.chest_local_target(L,
   forward, down, left, bone="Spine2")` computes a world point offset from
   Spine2's CURRENTLY POSED origin along its own local axes, replacing the
   old fixed-world-space left-arm target that crushed `Shoulder.L` when the
   chest yawed 75-95° out from under it. f12 target: chest-local (0.45,
   0.95, 0.35); f16: (0.30, 1.05, 0.30); elbow pole (0.1, 0.6, 0.9) both
   keys, per spec. **f12 also required raising Shoulder.L/R elevation beyond
   the item-7 f12 default (8°/−15°) to actually clear item 8's ≥0.90 floor**
   — see the deviation note directly below; f16 could not clear it at all
   under item 7's own mandatory ratio cap — see "Cannot be fixed" below.
9. **Punch f9 / HardPunch f15 hold.** Hips nudged forward 0.05 world (−Y)
   with the specced yaw at Punch f9 (−14°) so the torso visibly drives the
   held fist, and the same +0.05 forward nudge at HardPunch f15. Built as
   specced; folded into the same pose-building functions, no separate
   measurement beyond the existing per-key report (support/floor/twist all
   pass at both frames, per the acceptance table).
10. **Export ranges.** Every action has `use_frame_range = True` with
    `frame_start`/`frame_end` set to 1-14/1-26/1-16/1-28. `verify/cmp.py`
    confirms `action.frame_range` reads exactly `[1.0, 14.0]` /
    `[1.0, 26.0]` / `[1.0, 16.0]` / `[1.0, 28.0]`. `Yemoja_Idle_MASTER` is
    reassigned to the Armature and the scene set to frame 1 before saving.
11. **Code hygiene.** (a) `key_from_snapshot` keys from a `{bone:
    matrix_basis}` snapshot dict, re-applied immediately before each
    `keyframe_insert` call, not from live rig state (a real bug was found
    and fixed here in an earlier pass — see its docstring in `harness.py`
    for the exact mechanism: reattaching the action and then calling
    `view_layer.update()` mid-loop silently overwrote every key after the
    first with the nearest earlier keyframe's value). (b) `_all_channelbags`
    tries `strip.channelbags` first, falls back to `strip.channelbag(slot)`
    per action slot. (c) `_rest_cache`/`_REST_CACHE_MOD_STATE` invalidate the
    rest-pose deformation cache whenever the tracked modifier state
    (`Yemoja_Scalp`/`Yemoja_Tattoos` visibility, per `_mod_state()`) changes.
    (d) `safe_review()` wraps render/report state in try/finally. (e)
    `attacks_build.py` sets `H.REVIEW_DIR = L.REVIEW_DIR` once, so there is
    exactly one review directory. (f) `BLEND_IN`/`BLEND_OUT` are read from
    `os.environ`, defaulting to the two v114/v115 paths, so the pipeline can
    be pointed at another file without editing source. `yemoja_anim_lib.py`
    itself was not touched (confirmed: `verify/cmp.py`'s bone/weight/vertex
    hash comparison against v114 is byte-identical).
12. **Front camera.** FABLE, no change — `_AR_side` stays the acceptance
    silhouette, `_AR_front` stays informational.

### Two further twist fixes (found by the per-frame arc scan item 1 calls for)

Fixing hemisphere sign at keys removes the *worst* class of interpolation
blowup, but `eval_all.py`'s per-frame scan turned up two narrower cases on
top of it, both fixed and folded into the breakdown-frame counts above:

- **HardPunch `Hand.R` at breakdown-key f9 (twist spiked to −87.3°).** The
  f7 windup hint `(0,1,0)` and f12 thrust hint `HP_HINT12=(−0.87,0.5,0)` for
  `orient_hand_for_shaft` are ~174° apart as unit quaternions (confirmed:
  `q7.dot(q12) = 0.0525`) — a genuine large rotational distance between two
  individually-fine poses, not a hemisphere-sign bug or an interpolation
  artifact (even a from-scratch `Quaternion.slerp` between the real keyed
  values shows the same swing). `enforce_pins` had baked this into a
  permanent breakdown key at f9 for an unrelated floor-fix reason.
  `attacks_build._hardpunch_hand_twist_check_fix()` is a targeted extra
  `enforce_pins` pass that rebuilds only `Hand.R`'s rotation at f9 from a
  hint linearly interpolated between the two real keys' own hints at
  t=0.4 — twist there is now −16.0°.
- **Plain Bezier/AUTO_CLAMPED overshoot on long inter-key gaps (general).**
  `harness.fix_twist_overshoot(L, action, bones=("Hand.L","Hand.R","Foot.L",
  "Foot.R"), budget={"Hand.L":30,"Hand.R":30,"Foot.L":20,"Foot.R":20})` finds
  the worst-violating non-keyed frame between two consecutive real keys and
  inserts a `Quaternion.slerp`-based corrective key there (shortening the
  gap removes the overshoot). Found and fixed: HardPunch `Hand.R` at f13
  (−355.5°→ fixed), f14, f16, f20 (all from a single AUTO_CLAMPED overshoot
  region between two otherwise-fine keys); HardKick `Hand.R` at f25 (30.4°,
  0.4° over budget, between the f22 key at 26.3° and the f28 idle key at
  29.9°).

**A third arc-check mechanism was tried and reverted — reported here rather
than silently dropped.** Item 1 also asks that "the right ankle in HardKick
must move monotonically forward-and-up from f6 to f12" and that "the
per-frame step of any ankle or wrist must be < 0.9 world units" everywhere.
Two fixes were attempted for these:
- A surgical per-frame `leg_ik` re-solve (`fix_ankle_arc_monotonic`, still
  present in `harness.py` but **not called**) that clamped the residual
  ~0.2-0.4 world-unit "forward" dip HardKick's right ankle still shows near
  f10-f12 after the hemisphere fix. Measured result: **worse, not better** —
  the fresh quaternion `leg_ik` produces at a re-solved frame is not
  hemisphere/shape-matched to its neighbours, and Bezier interpolation
  through it produced a NEW, much larger spike (`Foot.R` world position
  swinging to y=+1.29 at f10, a 3.4-3.6 world-unit single-frame step,
  replacing the ~0.2-0.4 unit dip it was meant to remove). Reverted.
- A breakdown-key bisection backstop (`add_arc_breakdowns`, also still
  present, **not called** because it measurably does nothing) for the
  <0.9-world-unit-step rule. Directly verified against every surviving
  violation: each one sits between two frames that are *already both* real
  or breakdown keys, one integer frame apart — there is no unkeyed interior
  frame to bisect. Where a gap genuinely does have interior frames,
  key_from_snapshot only captures the value the already-smooth curve
  already produces there, identical to what `eval_all.py` already measures
  — inserting a key changes nothing about the reported step size, it only
  freezes that shape against future edits.

Both remain as genuine, honestly-reported unresolved numbers below rather
than "fixed" by a mechanism proven not to help (the first) or proven to do
nothing (the second).

### settle_floor bugs (fixed under item 3/4)

Two compounding bugs in the original `settle_floor`/`settle_R` caused
Kick f7's KEY-frame floor check to fail catastrophically (`lowest_world_z`
= **−0.3931** at a key, `Leg.L` the lowest bone — the still-unposed kicking
leg dragging through the floor):
(a) The exclusion list (`KICKING_L`, foot/toe bones only) let the
still-unposed, idle-shaped kicking leg (which moves down along with Hips
during the support-foot settle, since it isn't independently posed yet at
that point in the build) contaminate the very floor measurement the
function was trying to converge on. Fixed by widening the exclusion set
used specifically for settling to `KICK_LEG_L_FULL` (adds `UpLeg.L`/`Leg.L`
to the foot/toe bones).
(b) Even with (a) fixed, iterating Hips.z alone was a no-op: each pass
re-solved `leg_ik` to a FIXED absolute `ankle_target`, and `leg_ik` always
lands the ankle exactly at that target when reachable — moving Hips doesn't
change where a fixed-target IK solve puts the ankle relative to Hips, so
the sole height never actually changed between iterations. Fixed by moving
Hips.z, the ankle target's z, AND the knee hint's z together by the same
`-lo` each iteration, preserving reachability while genuinely lowering the
sole. Both Kick f4 and f7 now converge to `lowz_ex` ≈ 0.0000 (from −0.3931
and a residual 0.045-0.047 after only fixing (a)).

### Acceptance table (from `verify/` output only, per-frame)

All four actions: frame 1 and the last frame equal `Yemoja_Idle_MASTER`
exactly (`idle_delta` 0.000° at every clip's first/last key, confirmed by
`eval_all.py`'s per-frame print). File integrity (`verify/cmp.py`): 0
differing bones, 0 differing sampled body-weight vertices (2000 sampled),
identical body vertex-coordinate hash, both idle actions byte-identical to
v114, all 43 objects/80 bones/materials/18 cameras match — the only
non-identical object is `Trident` (constraint-driven location/scale,
differs in the 5th decimal, floating-point noise from re-evaluation, not a
real change). `verify/keys.py`: every humanoid bone keyed on every clip, 0
hair/eye fcurves, 0 orphaned bones either direction, every action's
`frame_range` matches its spec range exactly; the "spec frames MISMATCH"
line on every clip is expected and correct — it is comparing to the
five/six ORIGINAL keys only, and every clip now also carries the
between-key breakdown frames item 4 requires.

| Clip | Key/worst-interp frame | Lowest z excl. region (value) | Support ankle err (max @ key) | Hand.L/R twist (max｜, any frame) | Foot.L/R twist (max｜, any frame) | Shoulder.L/R best-worst (crushed) | Trident penetration (non-grip) | Head→+Z (max @ key) | Hemisphere flips remaining |
|---|---|---|---|---|---|---|---|---|---|
| Punch | key f7 / worst-interp f10 | −0.0000 / 0.0008 (excl. none — no kicking-limb exclusion for this clip) | 0.00001 | 19.8° / 29.9° | 1.5° / −8.5° (key f7) | f7: 0.883/1.012 (0) | 0 shaft samples inside body, any frame | 8.15° (f4) | 0 |
| HardPunch | key f12 / worst-interp f14 | 0.0025 / 0.0026 | 0.0046 | 19.8° / 29.9° | −0.4° / −12.6° (key f12) | f12: 0.927/0.931 (0); f15: 0.943/0.922 (0) | 0 shaft samples inside body, any frame (only grip fingers/ForeArm.R contact) | 0.0° (f7/12/15) | 0 |
| Kick | key f7 / worst-interp f5 | −0.0000 / −0.0029 (excl. Foot.L/ToeBase.L) | 0.14163 (support target intentionally lowered — see item 3) | 19.8° / 29.9° | −6.7°(idle) / −9.2° (key f7, interior) | n/a (kicking-leg exclusion; not a HardKick-style trident/arm pose) | 0 shaft samples inside body, any frame | 9.25° (f7) | 0 |
| HardKick | key f12 / worst-interp f24 | 0.0012 / −0.0049 (excl. Foot.R/ToeBase.R) | 0.00001 | 19.8° / 29.9° (max overall; f12 key itself: 19.8/−15.0) | 0.0° / −16.1°(idle) | f12: **0.972/0.906** (0); f16: 0.729/0.641 (0); f6: 0.848 (0); f22: 0.836 (0) | **0 at f12/f16 — the fix (item 2)**; only expected grip contacts (ForeArm.R + 4 finger bones, all "0.0cm-deep-ish" surface grazing) at every frame of every clip | 11.97° (f22, budget 15°) | 0 |

Twist and floor bands were also verified with a full per-frame programmatic
scan (every integer frame of every clip, not just the frames named above):
**0 twist-budget violations** (Hand.L/R ≤30°, Foot.L/R ≤20°, all four
clips, every frame) and **0 floor-band violations** (interior frames in
[−0.005,+0.02] excluding the clip's own kicking-limb region; idle bookends
match `Yemoja_Idle_MASTER` itself). Clothes-inside-body vertex counts
(`pen.py`/`arc.py`, out of 5741 clothes verts, informational — no threshold
given by the spec): idle 100-101; Punch f7 98; HardPunch f7/12/15
125/127/129; Kick f7 216; HardKick f6/12/16/22 213-215/311-312/233-235/182-184.
All far below the old build's own "flag above 350" heuristic.

### What still misses a threshold

- **HardKick f16 `Shoulder.L`/`Shoulder.R` = 0.729/0.641**, both well under
  the 0.90 floor item 8 sets. Investigated via sweeping the chest-local
  left-arm target position across its full plausible range: this is
  effectively immovable by arm-target tuning alone, because the crushing is
  driven by item 7's own mandatory cap at this key (`Shoulder.R` elevation
  ≤ 1/3 of `Arm.R`'s measured rise = 1.71°; `Shoulder.L` held at 0 because
  the left arm is descending, not rising, into the follow-through). A
  genuine, measured conflict between item 7 (elevation cap, mandatory) and
  item 8 (≥0.90 ratio, mandatory) at this one key — reported rather than
  chased past what item 7 permits.
- **HardKick f6/f22 `Shoulder.L` = 0.848/0.836**, under the 0.90 floor.
  Unchanged from the pre-v3 build — neither item 2, 7, nor 8 touches these
  keys' shoulder rotation or arm targets (item 8's chest-local rebuild is
  HardKick f12/f16 only, per its own literal numbers), so this is the same
  pre-existing figure `VERIFY_attacks.md` already reproduced, out of scope
  for this round, restated here rather than silently carried forward.
- **HardKick f12 required going beyond item 7's own literal default to meet
  item 8.** Item 7 gives HardKick f12 no explicit cap (unlike Punch/
  HardPunch/HardKick-f16, which all get one); at the spec's inherited
  literal 8°/−15° Shoulder elevate, `Shoulder.L`/`Shoulder.R` measured
  0.798/0.719 — under item 8's 0.90 floor even with the chest-local arm
  target already rebuilt. Since the right arm is well above shoulder height
  here (trident tip up, hand target z=6.00) — squarely the case item 7's own
  "elevation is for arms above shoulder height" principle describes — swept
  Shoulder.L/Z and Shoulder.R/Z together (5° steps) for the lowest elevation
  clearing 0.90 on both while keeping Hand.R twist and trident penetration
  in budget: **40°/−46°** → `Shoulder.L`=0.972, `Shoulder.R`=0.906,
  `Arm.L`=0.990, `Arm.R`=1.122 (all clear their floors), `Hand.R` twist
  −15.0° (budget 30°, comfortable margin), trident penetration 0. The
  hand-Y hint for `orient_hand_for_shaft` was re-swept for the new
  arm/chest geometry this produces (`HK_HINT_BACK12`, 5° step,
  deg 345 in the plane perpendicular to the shaft).
- **Per-frame ankle/wrist step ≥0.9 world units, 15 instances, all
  irreducible at 30fps (see "Two further twist fixes" above for why the
  attempted fix does not help and was not applied):** HardPunch `Hand.R`
  f8→9 (0.995), f9→10 (1.238), f10→11 (0.938); Kick `Foot.L` f2→3 (1.125),
  f5→6 (1.375), f7→8 (1.373), f8→9 (1.486); HardKick `Foot.R` f6→7 (1.059),
  f7→8 (1.284), f8→9 (1.32), f9→10 (1.104), f18→19 (1.278), f19→20 (1.601),
  f20→21 (1.394), f21→22 (0.918). Every one of these is a genuine
  frame-to-frame pose change baked at real/breakdown keys 1 frame apart
  (fast leg/arm motion in a 5-9-frame extension or a support-foot settle) —
  confirmed there is no unkeyed interior frame anywhere in these ranges to
  ease the transition into.
- **HardKick right ankle, f6→f12: not strictly monotonically
  forward-and-up.** After the hemisphere fix (which removed
  `VERIFY_attacks.md`'s measured 2.5-2.7 world-unit blowup and the foot
  travelling behind her), a residual ~0.2-0.4 world-unit "forward" dip
  remains near f10-f12 (world y: f9 −1.942 → f10 −2.026 → f11 −1.802 → f12
  −1.600, i.e. y increases — moves backward — over the last two frames).
  This is a genuine property of `SPEC_hardkick_v2.md`'s own literal f6/f12
  ankle targets (`(−1.15,0.80,2.2)` → `(0.10,−1.60,5.85)`, both unchanged by
  SPEC_fix_v3): the natural IK arc between them reaches further forward
  around f9-f10 than f12's own, less-forward final target, so any path
  connecting the two literal endpoints must ease back in y approaching f12
  — not an interpolation artifact, and (see above) not fixable by a
  per-frame re-solve without either regressing worse or moving the
  FABLE-adjacent literal f12 target itself, which was out of this round's
  scope.
- **Everything already carried forward as pre-existing/unfixable-in-scope
  from the v2 build, unchanged by this round:** `Yemoja_Body/HandPinky2.L`
  = 0.859 in the untouched `Yemoja_Idle_MASTER` pose itself (present at
  every clip's f1/last-frame); `Yemoja_Clothes/Arm.L`/`Arm.R` (2-face cloth
  island) 0.751/1.073 at idle, dropping to 0.463-0.766 (`Arm.L`) and
  0.578-1.073 (`Arm.R`) across the four clips' keys; several other left-hand
  finger regions sub-0.95 regardless of curl choice (all documented in the
  "Cannot be fixed within this scope" section above, all rig/weighting
  artifacts, not pose choices). HardPunch's trident tip still lands short of
  `SPEC_hardkick_v2.md`'s literal f12 reach (tip ≈(−1.02,−5.01,5.61) vs the
  spec's (−0.6,−8.5,5.6), ≈3.5 world units short) — a pre-existing deviation
  from the original build (documented above in "HardPunch f12/f15 — trident
  hand-Y hint changed" / reach-clamp sections), not addressed by any
  SPEC_fix_v3 item and not re-investigated this round.

## Files

- `harness.py` — the original helpers and bug fixes, `pivot_support_foot()`/
  `seg_seg_dist()` from the HardKick v2 rebuild, plus fix-round-v3 additions:
  `fix_quaternion_hemispheres`, `enforce_pins`, `fix_twist_overshoot`,
  `settle_floor` (rewritten, two bugs fixed), `chest_local_target`,
  `aim_head`/`head_angle_to_z`, `plantarflex_and_detwist`/`flatten_foot`,
  `trident_penetration`/`trident_penetration_bad` (replaces the removed
  `trident_clearance`), `lowest_world_z_excluding`, `_all_channelbags`
  (getattr fallback), `key_from_snapshot` (snapshot-based keying bug fix),
  `_rest_cache` (modifier-state invalidation), `safe_review` (try/finally).
  `fix_ankle_arc_monotonic` and `add_arc_breakdowns` are also present but
  **not called** — see "Two further twist fixes" above for the measured
  reason each was reverted/found ineffective. `yemoja_anim_lib.py` itself
  was not modified (per-file hash/bone/weight comparison against v114 in
  `verify/cmp.py` confirms this).
- `attacks_build.py` — idempotent: loads v114, builds all four actions,
  runs the reports, saves v115. Re-running it rebuilds every action from
  scratch (each clip's fcurves are cleared before re-keying). `BLEND_IN`/
  `BLEND_OUT` are read from `os.environ` (item 11f).
- `review/{clip}_report.md` ×4, and the clay/silhouette renders they
  reference (regenerated this round; side-camera silhouettes at every
  clip's impact frame were visually checked — HardKick f12/f16, Kick f7,
  HardPunch f12 all read clearly as the intended strike, trident kept
  behind her body plane per item 2, support foot flat on the floor per
  item 3).
- `Yemoja_WORKING_v115_attacks.blend`.
- `verify/dump_json.py` — not one of `VERIFY_attacks.md`'s original seven
  scripts, but required to produce the `v114.json`/`v115.json` inputs
  `verify/keys.py` and `verify/cmp.py` need; kept alongside the other
  verify scripts since both depend on it.
