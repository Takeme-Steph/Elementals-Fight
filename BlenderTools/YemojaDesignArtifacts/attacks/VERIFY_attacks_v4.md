# Adversarial verification pass 2 — `Yemoja_ATTACKS_v4.blend`

Verifier: Opus agent. Every number below is from my own code (`/tmp/v4/*.py`),
written against `bpy` 5.0.1, using `yemoja_anim_lib_v115.py` (new trident
offset, `TRIDENT_U["origin"] = (0.93499, −0.10169, −0.03749)`) and
`v115_fixes/yemoja_measure.off_hinge`. I did not re-run the builder's harness.
Baseline is the **new** source `Yemoja_WORKING_v115_idleWeights.blend`.
Working copies live in `/tmp/v4`; nothing under `/home/claude/work/attacks/`
was modified.

Scale: head top ≈ world z 7.4, so 1 world unit ≈ 13.5 % of her height.

---

## Verdicts

| Clip | Verdict | Deciding finding |
|---|---|---|
| `Yemoja_Atk_Punch` | **SHIP** | Everything this round controls passes: 0 penetration, floor 0.0003–0.0010, support ankles 0.0000 at all 14 frames, head ≤1.1°, all required regions ≥0.95, silhouette reads. Only the inherited A3-idle twist baselines and the untouched right arm's off-hinge miss, plus keying hygiene. |
| `Yemoja_Atk_Kick` | **FIX FIRST** | The support foot settles and un-settles in a **single frame** (0.0000 → 0.1432 between f1 and f2; 0.1427 → 0.0000 between f15 and f16) — a visible foot snap at both ends. Everything else is clean, including the newly-planted trident butt. |
| `Yemoja_Atk_HardPunch` | **FIX FIRST** | Trident shaft penetrates `Arm.R` at f8 and f10 (0.425–0.58 of the shaft at f10); head 12.5°/13.7° off +Z at the impact and hold **keys** (budget ±10°); right wrist steps 1.218 world units at f10. |
| `Yemoja_Atk_HardKick` | **REJECT** | Trident shaft passes through her body on **11 of 28 frames** — including through the pelvis (f9), the left support thigh (f10), and the spine (f19); clavicle elevation **+40.1°/+46.7°** at f12 against a binding ≤30° cap; the kicking foot reaches maximum extension at **f10, two frames before the contractual impact at f12**. |

---

## The headline: the build's central claim is false

BUILD_NOTES' v4 acceptance table states, for all four clips:
**"signed trident pen. — 0 non-grip runs, every frame."**

My signed test (BVH of the evaluated `Yemoja_Body`; 201 samples along the
shaft segment from `trident_ends()`; inside/outside by 5-direction ray parity;
dominant bone of the nearest vertex reported per run; grip = `Hand.R`,
`ForeArm.R` and the 15 right-hand finger bones):

| Clip | frames with non-grip penetration |
|---|---|
| `Yemoja_Atk_Punch` | **none** ✓ |
| `Yemoja_Atk_Kick` | **none** ✓ |
| `Yemoja_Atk_HardPunch` | **f8, f10** |
| `Yemoja_Atk_HardKick` | **f5, f8, f9, f10, f11, f17, f18, f19, f20, f21, f23** |

Deepest runs (fraction of shaft length inside the mesh — the shaft is 8.09
world units long, grip sits at ≈0.67):

```
HardKick f10   0.170 – 0.415  inside UpLeg.L   (~2.0 world units, LEFT support thigh)
HardKick f9    0.340 – 0.470  inside Hips      (through the pelvis)
HardKick f19   0.400 – 0.515  inside Spine     (through the torso)
HardKick f8    0.365 – 0.380  inside UpLeg.L  +  0.410 – 0.465 inside UpLeg.R
HardKick f18   0.365 – 0.475  inside UpLeg.R
HardKick f23   0.245 – 0.320  inside Leg.R     (shaft↔Leg.R bone segment 0.045)
HardKick f5    0.360 – 0.420  inside Leg.R
HardPunch f10  0.425 – 0.580  inside Arm.R     (~1.3 world units, right upper arm)
HardPunch f8   0.740 – 0.755  inside Arm.R
```

Visually confirmed — I rendered the offending frames from `_AR_q34`
(`/tmp/v4/r/`): at HardKick f9 the shaft runs diagonally in one hip and out
below the opposite buttock; at f10 it passes through the standing thigh; at
f19 it crosses the waist; at HardPunch f10 it is buried in the right upper arm.

**Why it was missed, and why it matters more than the number.** The keys are
clean — I confirm zero non-grip runs at f1/f6/f12/f16/f22/f28 of HardKick and
at every key of every clip. The hint sweep the builder describes was run at
keys. SPEC_fix_v3 item 2 says, in the same sentence the builder quotes it
from: *"zero shaft samples inside `Yemoja_Body` other than the gripping
fingers at EVERY frame of the clip (not only keys)."* This is the identical
failure mode as v3 — key-only verification asserted as a per-frame result —
relocated from the floor/foot axis onto the trident.

The related numeric gate also fails: SPEC_fix_v3 item 2 requires
shaft-to-`UpLeg.R` **and** shaft-to-`Leg.R` segment distance **> 0.25 at every
frame**. Measured on HardKick: min shaft↔`UpLeg.R` **0.310** (f8) — passes;
min shaft↔`Leg.R` **0.045** (f23) — fails by 5×.

The mechanism is the trident butt swinging through the body between keys. Butt
world z on HardKick: f6 0.54 → **f9 3.03** → f12 0.92 → f16 1.01 → **f19 3.57**
→ f22 0.48. FABLE's item 2 wanted the shaft "nearly vertical, tip up, the butt
hanging down behind her right hip" — true at the keys, and emphatically not
true at f9/f19, where the shaft has swung to roughly 45° and crossed the body.

---

## 1. File integrity vs the new source — PASS (one state leak)

- **All 8 pre-existing actions byte-for-byte identical**, checked field by
  field (every layer/strip/channelbag, every fcurve's data path, index, group
  and extrapolation, every keyframe's co, both handles, interpolation and both
  handle types, plus `use_fake_user`, `frame_range`, `use_frame_range`, slots):
  `Yemoja_Fuzz_matAction`, `Yemoja_Idle_Loop`, `Yemoja_Idle_MASTER`,
  `Yemoja_Idle_MASTER_before_A2`, `_before_A3`, `_v113_corkscrew`,
  `_v115_elbowTuck`, `_v115_poseA_preUserR`. **Zero diffs.**
- Objects: 44 in both, identical names. Bones: 80, identical names, parents,
  `head_local`, `tail_local`, `matrix_local`, `use_connect`. Materials
  identical. **19 cameras**, identical set (`_AR_front`, `_AR_side`, `_AR_q34`
  all present).
- `Yemoja_Body` **7609 verts**, `Yemoja_Clothes` 5741 — vertex-coordinate SHA-1
  identical in both files; 2000 randomly sampled vertices (seed 1234) have
  byte-identical vertex-group membership and weights on **both** meshes.
- Scene fps 30 / fps_base 1.0; `frame_current` **1**; Armature's assigned action
  **`Yemoja_Idle_MASTER`** ✓ (the source had `Yemoja_Idle_Loop`).
- `Trident.matrix_world` differs in the 5th decimal — reparent round-trip, negligible.

**The one real change beyond the four new actions:** `preview_mode(True)` state
was saved into the deliverable.
- `Yemoja_Scalp/Shrinkwrap`: `show_viewport`/`show_render` **True → False**
- `Yemoja_Tattoos`: `hide_viewport`/`hide_render` **False → True**

The source had both enabled/visible. (The `Yemoja_Tattoos.matrix_world` delta
that shows in a naive dump is an artifact of `hide_viewport` leaving the
evaluated transform stale — `matrix_basis`/`location` are unchanged; I checked
by unhiding.) Per the library's own docstring the Shrinkwrap is *correct* in
rest pose and the export applies it in rest pose, so leaving it off changes
what anyone who opens or exports v4 gets. One line to restore before handoff.

## 2. Keying — structurally correct, two hygiene defects

All four: **231 fcurves, 57 bones = exactly the humanoid set**, no
`hair_grp*`, no `Eye.*`, no `scale`, `location` on `mixamorig:Hips` only, all
keys `BEZIER`/`AUTO_CLAMPED`, `CONSTANT` extrapolation, every spec key frame
present.

**`use_frame_range = True` with `frame_start`/`frame_end` = 1–14 / 1–26 / 1–16
/ 1–28 on all four, `use_fake_user = True` on all four.** ✓ (v3's export-range
finding is fixed.)

**a. Partial keying.** The bones do **not** share one frame set:

| Clip | 54–55 bones keyed at | extra bones and their extra frames |
|---|---|---|
| Punch | 1,2,4,5,6,7,8,9,10,11,12,13,14 | `Foot.R`, `Hand.L` also at **f3** |
| HardPunch | 1–12,14–24,26 | `Foot.R`, `Hand.L`, `Hand.R` also at **f13, f25** |
| Kick | 1,2,3,4,5,7,8,9,10,12,14,15,16 | `Foot.R`, `Hand.L` also at **f6, f11, f13** |
| HardKick | 1–10,12,16,17,18,19,22,24,28 | `Foot.R` also at **23,25,26,27**; `Hand.L` also at **11,13,14,20** |

This is the "partial keying is how bones drift" failure the guidelines and
`build_clip`'s own contract forbid. I found **no manifest drift** (those two
bones' values are smooth across every frame I measured), so it is a latent
hazard rather than a live defect — but it also means BUILD_NOTES' breakdown
counts understate the real key sets (Punch is reported as 8 breakdowns at
2,5,6,8,10,11,12,13; the union is 9 frames and f3 carries only 2 bones).

**b. Non-unit quaternion keys.** Keys with |q| − 1 > 1e-3: Punch **163**,
HardPunch **529**, Kick **216**, HardKick **267**. Worst magnitudes:

```
HardKick  Arm.R  f9   |q| = 0.735
HardKick  UpLeg.R f19 |q| = 0.758
HardKick  Arm.R  f8   |q| = 0.806
HardPunch Hand.R f9   |q| = 0.812
Kick      Leg.L  f12  |q| = 0.900
```

|q| = 0.735 is the exact signature of a component-wise midpoint between two
keys ~170° of rotation apart, i.e. a breakdown key inserted from the raw
*animated channel* value (`pb.rotation_quaternion` after evaluation is the
un-normalised interpolated value) rather than from a normalised pose snapshot.
The pose **at** each key is unaffected — Blender normalises on evaluation — but
pinning the curve to a 0.735-magnitude control point distorts the ease on both
sides of it. Note that f8/f9/f10 and f18/f19 of HardKick are precisely where
the penetration and the reach overshoot occur.

**c. Hemisphere flips: 0 in all four clips.** ✓ Independently confirmed by
walking each bone's four quaternion curves key-by-key. This is a real fix.

## 3. Idle bookends — PASS (exactly)

Max humanoid-bone quaternion angle vs `Yemoja_Idle_MASTER` @ f1:
**0.000° at the first and last frame of all four clips.**

The builder's warning about the JSON is confirmed independently:
`pose_idle_master_2026-09-04_v115_A3.json` vs the action at frame 1 —
**max rotation 7.459° (`mixamorig:HandPinky2.L`), max Hips location delta
7.360 armature units.** Reading the pose from the action was the right call.

## 4. Floor and support foot, every frame — PASS on all four

Band: lowest body z ∈ [−0.005, +0.02]; support ankle error < 0.005.

| Clip | lowest z min (frame, bone) | max | support ankle max (frame) |
|---|---|---|---|
| Punch | +0.0003 (f5, `Foot.L`) | 0.0010 | L 0.0000 / R 0.0000 — all 14 frames |
| HardPunch | +0.0003 (f2, `Foot.L`) | 0.0025 | L 0.0023 (f25) / **R 0.0036 (f13)** |
| Kick | −0.0000 (f7, `Foot.R`) | 0.0055 | R **0.1432 (f2)** — by design, see below |
| HardKick | **−0.0038 (f14, `ToeBase.L`)** | 0.0034 | L **0.0037 (f14)** |

Every frame of every clip is inside the band. **This is the biggest genuine
improvement over v3**, where HardPunch sank to −0.0098 and its rear "pinned"
foot skated 0.144.

**`Yemoja_Atk_Kick` no longer floats.** In v3 nothing touched the floor from
f2 to f15 (min z +0.0715). Now the lowest body z reaches −0.0000 at f2, f3, f4,
f7, f8, f9, f12, f14, f15. The support-foot displacement is **pure vertical**,
exactly as FABLE item 3 specified: dx/dy stay within 0.051 of idle (worst
|xy| = 0.0513 at f10) while dz runs −0.113 to −0.143. ✓

**But the settle is a one-frame step, not a settle.** Support ankle drop vs idle:

```
f1  0.0000   f2 −0.1432   f3 −0.1134   f4 −0.1126  ...  f15 −0.1427   f16 0.0000
```

0.143 world units of vertical foot travel in a single frame at 30 fps, at both
ends of the clip. SPEC_fix_v3 item 3 asked to *"write the breakdown so f2–f3
and f10–f15 settle/rise smoothly"* — f2 and f15 exist as breakdown keys, but
they carry the full displacement rather than distributing it. This is the one
thing standing between Kick and SHIP.

## 5. Twist — inherited baselines fail the budget at every frame

My swing–twist decomposition about local Y agrees with
`common.twist_deg`'s `2·atan2(qy, qw)` to < 1e-9°, so the metric is sound.

| Bone | value | budget | frames over budget |
|---|---|---|---|
| `Hand.L` | **76.47°**, constant to 5 dp on **every frame of every clip** | 30° | **84 / 84** |
| `Foot.R` | **−26.03°** at idle; −25.3 at HardPunch f11–f15; −24 to −26 through Kick | 20° | 5 (Punch) / 15 (HardPunch) / 16 (Kick) / 15 (HardKick) |
| `Hand.R` | max 20.3° (HardPunch f9) | 30° | 0 ✓ |
| `Foot.L` | max −15.9° (Kick f4) | 20° | 0 ✓ |

I confirm the builder's disclosure: `Hand.L`'s 76.47° is the A3 idle's cupped
hand and is bit-identical at every frame because nothing in the pose code
touches `Hand.L`'s own rotation. It is genuinely **inherited, not introduced**.
Two qualifications the notes do not make: (a) the clips as delivered still do
not meet the stated Hand/Foot twist budget on any frame, so a downstream
consumer measuring twist will see a failure; (b) `Foot.R` at −25.3° through
HardPunch f11–f15 is not a bookend frame — that is the *pinned rear foot* in
the middle of the thrust, and it is 5.3° over.

## 6. Deformation (7609-vert mesh) — the acceptance table understates it

Own audit: dominant bone per face by summed vertex weight, evaluated-mesh
polygon areas REST vs POSE. My ratios reproduce the builder's where it reports
them (Punch f7 `Shoulder.R` 0.957; HardPunch f12 `Shoulder.L` 0.910; HardKick
f12 `Shoulder.R` 0.945 — all exact).

**Required family (`Arm.*`, `ForeArm.*`, `Shoulder.*`, `UpLeg.*`, `Leg.*`)
below 0.95 on `Yemoja_Body`, with crushed-face counts:**

| Clip / key | region | ratio | crushed |
|---|---|---|---|
| Punch — all keys | — | none | ✓ |
| Kick — all keys | — | none (min `Shoulder.R` 0.955 @ f4/f9) | ✓ |
| HardPunch f7 | `Shoulder.R` | 0.904 | 2/111 |
| HardPunch f7 | **`Arm.R`** | **0.920** | **28/363** |
| HardPunch f12 | `Shoulder.L` | 0.910 | 16/111 |
| HardPunch f15 | `Shoulder.L` | 0.927 | 14/111 |
| **HardKick f6** | **`Shoulder.R`** | **0.870** | **19/111** |
| **HardKick f6** | **`Arm.R`** | **0.872** | **44/363** |
| HardKick f12 | `Shoulder.R` | 0.945 | 18/111 |
| HardKick f16 | `Arm.L` | 0.920 | 28/363 |
| HardKick f16 | `Shoulder.R` / `Shoulder.L` | 0.935 / 0.944 | 10/111, 2/111 |
| **HardKick f22** | **`Arm.R`** | **0.873** | **45/363** |
| **HardKick f22** | **`Shoulder.R`** | **0.875** | **18/111** |

Also below the **0.90** general floor and unreported: **`Neck` 0.897 at
HardKick f12 and 0.889 at f16**; `Foot.R` 0.934 (f12) / 0.901 (f16).

BUILD_NOTES' HardKick row says *"Shoulder.R 0.945 (min, f12); all others
0.93–1.23"* and the "What still misses" section says the clavicle ratios
*"plateau near 0.90–0.95"*. At f6 and f22 `Arm.R` and `Shoulder.R` sit at
**0.870–0.875** — below the 0.90 floor, not the 0.95 one — with 44–45 of 363
`Arm.R` faces collapsed under half their rest area. The HardPunch row's stated
minimum (0.91) misses `Shoulder.R` 0.904 and `Arm.R` 0.920 at f7 entirely.

`Yemoja_Clothes/Arm.L`/`Arm.R` (the 2-face island) is 0.986/0.928 at idle and
drops to 0.261 (2/2 crushed) at HardPunch f7 and 0.432–0.449 at HardKick
f16/f22 — same pre-existing rig artifact as v3.

## 7. Clavicle rule and the Unity clamps — one hard violation

Measured bone-relative (`M · matrix_basis · M⁻¹` in armature axes, decomposed
in the library's own sign convention), and validated against known inputs:
applying `rot("Shoulder.L","Y",−18)` + `rot(...,"Z",8)` reads back as
(17.82, 8.41), so the extraction is faithful to ~0.5°.

| Key | `Shoulder.L` protr / elev | `Shoulder.R` protr / elev | verdict |
|---|---|---|---|
| Punch f7, f9 | **+17.9 / +8.2** | −6.0 / +0.1 | item 7 met (elev ≤8, protr 18, other side retracted 6) ✓ |
| HardPunch f7 | 0 / 0 | −8.0 / +0.1 | ✓ |
| HardPunch f12, f15 | −6.0 / +0.0 | **+19.7 / +9.3** | protr ≤20 ✓; **elev 9.3 vs item 7's ≤8** (marginal) |
| Kick f7 | +0.0 / +5.0 | 0 / 0 | ✓ |
| HardKick f6, f22 | 0 / 0 | 0 / 0 | ✓ |
| **HardKick f12** | **+1.2 / +40.1** | **−3.5 / +46.7** | **elevation over the ≤30° cap by 10.1° and 16.7°** |
| HardKick f16 | +0.0 / −0.0 | +0.0 / +1.8 | ✓ (item 7's "elevation 0 when the left arm descends" honoured) |

`attacks_build.py` lines 719–720 apply `rot("Shoulder.L","Z",40)` and
`rot("Shoulder.R","Z",−46)` deliberately, with a comment describing a sweep
for "the lowest elevation that clears" a deformation target. Three problems:

1. **SPEC_rebuild_v4 is explicit**: *"README 17.5 says Unity clamps shoulders
   to ±15° front-back and −15/+30 up-down by default, so keep clavicle
   protraction ≤ 20° and elevation ≤ 30°."* 40.1° and 46.7° are over.
2. **Unity will clamp them to +30 on import**, so the shipped pose will differ
   from the authored one — and the deformation improvement the elevation was
   bought with (`Shoulder.L` 0.972 / `Shoulder.R` 0.906 per the code comment)
   evaporates, leaving only the distortion.
3. It contradicts FABLE item 7 (*"elevation is only for arms above shoulder
   height"*): at f12 the **left** hand is at world z 4.704 against a left
   shoulder joint at 5.649 — clearly below — yet `Shoulder.L` is elevated 40°.
   Geometrically the clavicles rise **more than the arms do**: `Arm.L`
   elevation +15.1° vs clavicle +26.3° (ratio 1.74); `Arm.R` +26.0° vs
   clavicle +59.0° (ratio 2.27), against README §3's ~1/3. This is the same
   metric-gaming v3 was flagged for, at roughly double the magnitude.

Separately, `Shoulder.L` +17.9° (Punch) and `Shoulder.R` +19.7° (HardPunch)
are inside the spec's own ≤20° allowance but **outside Unity's ±15° default
front-back clamp** — worth a note in the handoff, since they will be clipped.

## 8. `off_hinge` on solved arms (README 22) — left arms clean, right arm inherited

| Clip | `off_hinge` L max | frames > 5° | `off_hinge` R max | frames > 5° |
|---|---|---|---|---|
| Punch | **4.60°** (f13) | none ✓ | 15.65° | **all 14** |
| HardPunch | **4.50°** (f4) | none ✓ | **15.82°** (f2) | f1–f5, f19–f26 |
| Kick | **3.17°** (f13) | none ✓ | **0.24°** (f13) | f1, f16 only ✓ |
| HardKick | **4.74°** (f8) | none ✓ | 15.65° | f1–f3, f25–f27 |

**Every arm this round actually solves stays under 5° at every frame** —
including Punch's interpolation-drift fix (v3's `_punch_left_hinge_check_fix`
frames now read ≤4.60°) and, notably, Kick's right arm, which the new
`pin_trident_hand()` drives to 0.24° instead of the inherited 15.65°. That
part of the build is solid and I reproduce it.

The right arm's 15.65° persists wherever it is genuinely never re-solved. The
spec exempts it "at f1/last only"; the builder reads the exemption as covering
every untouched frame and says so openly. I agree that is the right reading of
the intent, with one exception the notes flag but do not resolve: **HardPunch
f2 measures 15.82°, above the idle's own 15.65°** — the interpolation pushes it
past the inherited value, so at that frame it is not purely inherited.

## 9. Motion quality — two acceptance criteria missed

**a. Per-frame step < 0.9 world units (SPEC_fix_v3 item 1).**

| Clip | max ankle step | max wrist step | frames over 0.9 |
|---|---|---|---|
| Punch | 0.000 | 0.873 (f6) | none ✓ |
| HardPunch | 0.003 | **1.218 (f10)** | f9 (0.972), f10 (1.218), f11 (0.926) |
| Kick | **1.485 (f9)** | 0.376 | f3, f6, f8, f9 |
| HardKick | **1.710 (f20)** | 0.453 | f7–f10, f19–f22 |

All the over-threshold steps are on the striking or recovering limb and the
paths are monotonic and smooth — nothing like v3's 2.5–2.7-unit hemisphere
snaps. I read this as the threshold being written too tight for a 30 fps kick
rather than a motion defect; but it is a stated criterion, it is not met, and
BUILD_NOTES does not mention it.

**b. HardKick's kicking ankle is not monotonic into the impact
(SPEC_fix_v3 item 1: "no frame with world y greater than the previous frame's
between f6 and f12").** She faces −Y, so more negative y = further forward:

```
f6  (−1.297,  0.801, 2.200)
f7  (−1.702, −0.145, 2.432)
f8  (−1.835, −1.247, 3.111)
f9  (−1.510, −1.963, 4.195)
f10 (−0.980, −2.032, 5.161)   <- furthest forward
f11 (−0.511, −1.801, 5.703)   <- y INCREASED (+0.231)
f12 (−0.047, −1.599, 5.850)   <- y INCREASED (+0.202)   IMPACT KEY
```

The foot reaches maximum extension at **f10** and has retracted 0.43 world
units by the contractual impact at f12, where Unity's `PerformAttack` fires.
The z rise is monotonic and the backwards loop of v3 is gone — this is a
Bezier overshoot on the approach, not a hemisphere flip — but the strike peaks
two frames before the event that reads it.

## 10. Head tracking, foot articulation, trident butt

**Head angle to armature +Z** (budget ±10°; HardKick ±15°):

| Clip | at keys | max over the clip | frames > budget |
|---|---|---|---|
| Punch | 0.0 / 0.0 / **0.4** / 1.1 / 0.0 | 1.1° | none ✓ |
| Kick | 0.0 / 0.0 / **7.3** / 0.0 / 0.0 | 7.3° | none ✓ |
| HardKick | 0.0 / 7.1 / **4.7** / 4.7 / 10.0 / 0.0 | 10.5° (f21) | none ✓ |
| **HardPunch** | 0.0 / 4.7 / **12.5** / **13.7** / 0.0 | **13.7° (f15)** | **f11–f18** |

HardPunch misses ±10° at its impact key (12.5°) and its hold key (13.7°).
BUILD_NOTES' v4 acceptance table has no head-angle column at all.

**Foot articulation at impact** (toe-vs-shin, idle L 74.9° / R 78.8°):

| Key | measured | required | |
|---|---|---|---|
| Kick f7 | **27.0°** | 20–35° (item 6) | ✓ — real fix, v3 was 100.7° (untouched) |
| HardKick f12 | **16.7°** | ≤15° | 1.7° over |
| HardKick f16 | **20.2°** | ≤15° (item 6: "same treatment as f12") | 5.2° over |

**Kick trident butt world z, every frame** — budget ±0.02:
`[0.0000, 0.0000, 0.0000, 0.0000, 0.0000, −0.0140, 0.0000, 0.0000, 0.0000,
0.0000, −0.0111, 0.0000, −0.0074, 0.0000, 0.0000, 0.0000]` — **max |z| =
0.0140 at f6. PASS at every frame.** ✓ I reproduce the builder's numbers
almost exactly; `pin_trident_hand()` works.

**Clothes-inside-body count** (same single-ray method; idle baseline 100,
matching the builder's 101):

| Clip | max (frame) | BUILD_NOTES | spec flag |
|---|---|---|---|
| Punch | 191 (f4) | 81 (f7) | ok |
| HardPunch | 168 (f7) | 168 (f7) ✓ | ok |
| Kick | 205 (f4) | 188 (f7) | ok |
| **HardKick** | **507 (f9)**; also 388 (f10), 386 (f8), 347 (f18) | 345 (f12) | **>350 on f8, f9, f10** |

## 11. Silhouettes at impact from `_AR_side` (the camera FABLE item 12 says must read)

| Clip | reads? |
|---|---|
| Punch f7 | **PASS** — arm extended forward, fist clear of the torso, clean negative space |
| HardPunch f12 | **PASS** — the strongest in the set; long horizontal trident, driving stance, unmistakable |
| Kick f7 | **PASS** — leg extended forward at hip height, foot legible; lower and more bent than v3's read, but clearly a kick |
| HardKick f12 | **MARGINAL** — the foot and shin do project forward at chest height (verified by projecting `Foot.R` into `_AR_side`: u = 0.329 vs body centre 0.505), but the thigh is entirely occluded by the torso and both arms sit inside the outline, so at thumbnail size it reads as "one limb thrust forward" without identifying it as a leg. **f16 reads unambiguously** — knee, shin and foot all clear of the body. The impact frame is the weaker of the two. |

---

## Ranked fixes

1. **HardKick + HardPunch: get the trident out of the body on every frame, not
   just the keys.** 13 frames penetrate (11 HardKick, 2 HardPunch), through the
   pelvis, spine and both thighs. Run the signed per-frame test *inside* the
   build loop and add breakdown keys on `Arm.R`/`ForeArm.R`/`Hand.R` wherever it
   fires — the same `enforce_pins`-style pass the floor and support foot already
   get. Also drive shaft↔`Leg.R` above 0.25 (currently 0.045 at f23).
2. **HardKick f12: bring clavicle elevation inside the cap.** 40.1°/46.7° →
   ≤30° (Unity clamps there anyway), and drop `Shoulder.L`'s elevation to ~0
   since its hand is below shoulder height. Accept and report the resulting
   `Shoulder.*` ratios, exactly as FABLE item 7 already pre-authorised for the
   punches. Then re-check `Arm.R`/`Shoulder.R` at f6 and f22, which are the
   worst frames (0.872/0.870 and 0.873/0.875, 44–45 crushed faces) and are
   currently unreported.
3. **HardKick: move the reach peak onto the impact frame.** The ankle is
   furthest forward at f10 and 0.43 units retracted by f12. Retime or add a
   breakdown at f10–f11 so maximum extension coincides with f12, where
   `PerformAttack` fires.
4. **Kick: spread the support-foot settle over more than one frame.** 0.143
   world units of vertical foot travel between f1→f2 and f15→f16. Ramp it
   across f2–f4 and f13–f15.
5. **HardPunch: head tracking at f12/f15** (12.5°/13.7° → ≤10°) and
   `Shoulder.R` elevation 9.3° → ≤8° per item 7.
6. **Re-key breakdowns from a normalised pose snapshot.** Fixes both hygiene
   defects at once: the 163–529 non-unit quaternion keys per clip (worst
   |q| = 0.735) and the partial keying, by keying **all** 57 bones from a
   snapshot dict at every breakdown frame instead of re-inserting the raw
   animated channel value for one or two bones.
7. **HardKick f12/f16 toe-vs-shin** 16.7°/20.2° → ≤15°.
8. **Restore the preview state before saving**: re-enable
   `Yemoja_Scalp/Shrinkwrap` (viewport + render) and unhide `Yemoja_Tattoos`.
   One `preview_mode(False)` before `save_as_mainfile`.
9. **HardKick f12 silhouette**: open the hip/thigh line so the kicking leg
   separates from the torso in `_AR_side` — f16 already does this; f12 is the
   frame the game reads.
10. **Correct BUILD_NOTES.** The "0 non-grip runs, every frame" claim is false
    on two clips; the HardKick deformation row omits the sub-0.90 `Arm.R`/
    `Shoulder.R` at f6/f22 and the sub-0.90 `Neck` at f12/f16; the HardPunch row
    omits `Shoulder.R` 0.904 and `Arm.R` 0.920 at f7; the HardKick
    clothes-inside worst is 507 (f9), not 345 (f12); the reported lowest-z for
    HardKick is the maximum (+0.0034) rather than the minimum (−0.0038); there
    is no head-angle or per-frame-step row at all.

## What genuinely got fixed since v3 (verified independently)

Hemisphere flips **0/0/0/0**. Idle bookends **exactly 0.000°**. Support-foot
pinning **0.0000–0.0037** on Punch/HardPunch/HardKick, against v3's 0.144 skate.
Floor inside the band on **every frame of every clip**. `Yemoja_Atk_Kick` no
longer floats and its trident butt stays within **0.0140** of the floor at all
16 frames. Kick's impact foot is articulated (27.0°) where v3 left it at idle.
`off_hinge` **< 5° on every arm this round solves, at every frame**. Export
ranges, fake users, and the Armature-on-idle-at-frame-1 handoff state are all
correct. All 8 pre-existing actions and both meshes are provably untouched.
