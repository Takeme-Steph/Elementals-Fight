# Adversarial verification — Yemoja attack clips (v115)

Verifier: Opus agent. All numbers below are from my own measurement code
(`/tmp/vf/*.py`), written against `bpy` 5.0.1 without reusing the build's
`harness.audit()` / `trident_clearance()` / `report()`. Where my method
differs from the build's I say so and give both numbers. Working copies of
the two .blend files were made in `/tmp/vf`; nothing under
`/home/claude/work/attacks/` was modified.

Scale reference: head top sits at world z ≈ 7.42, so 1 world unit ≈ 13.5 %
of her height; 0.07 world units ≈ 1 % of height ≈ 1.6 cm on a 1.7 m figure.

---

## Verdicts

| Clip | Verdict | The thing that decides it |
|---|---|---|
| `Yemoja_Atk_Punch` | **FIX FIRST** | Front-camera silhouette does not read as a strike; support foot slides 0.029 on in-betweens; clavicle elevated 24° with the fist below shoulder height; f7–f9 "hold" is a 3-frame freeze |
| `Yemoja_Atk_HardPunch` | **FIX FIRST** | Floor sink −0.0098 @ f9 (2× outside the band) and rear "pinned" foot skates 0.144 @ f10; head 14–18° off +Z at every posed key (budget ±10°); quaternion hemisphere flip on `Hand.R` |
| `Yemoja_Atk_Kick` | **FIX FIRST** | Nothing on her body touches the floor from f2 to f15 — she balances on a toe 0.056–0.072 above z=0; the impact foot is never articulated, so "sole faces +Z, ball leading" is unimplemented; front silhouette does not read |
| `Yemoja_Atk_HardKick` | **REJECT** | The trident shaft passes **through her own right shin at f16** (0.65 world units buried, visible in the shipped renders) and through the thigh at f12; the kicking leg's in-betweens loop backwards and overshoot because of two quaternion hemisphere flips on `UpLeg.R`; head 21.4°/25.7° off +Z vs its own spec's ±15° |

---

## 0. The finding the build did not look for: the in-betweens are broken

Every key value in all four actions is clean. The build only ever measured
key frames. Blender interpolates `rotation_quaternion` F-curves
**component-wise**; when two consecutive keys land in opposite hemispheres
(dot < 0) the bone takes the long way round. The build never ran a
quaternion-continuity pass, and three flips shipped:

| Action | Bone | Key pair | dot |
|---|---|---|---|
| `Yemoja_Atk_HardPunch` | `mixamorig:Hand.R` | f7 → f12 | **−0.0511** |
| `Yemoja_Atk_HardKick` | `mixamorig:UpLeg.R` | f6 → f12 | **−0.2784** |
| `Yemoja_Atk_HardKick` | `mixamorig:UpLeg.R` | f16 → f22 | **−0.2347** |

Measured consequence — the HardKick **right ankle in world space, every
frame** (hip→ankle length stays ≈ 2.27 throughout, so this is pure rotation,
not stretch). She faces world −Y; +Y is behind her.

```
f6  (-1.150,  0.800, 2.200)   chamber
f7  (-1.125,  0.844, 2.241)
f8  (-0.782,  1.642, 2.673)   <- travelling BACKWARD, +0.80 in y
f9  (-0.266,  2.171, 4.766)   <- 2.17 behind her hips, +2.09 z in one frame
f10 (-0.139,  0.400, 6.375)
f11 (-0.076, -0.994, 6.237)
f12 ( 0.100, -1.600, 5.850)   impact
...
f16 ( 0.950, -1.100, 5.100)   follow-through
f17 ( 0.674, -1.242, 5.295)
f18 (-0.455, -1.093, 5.782)
f19 (-2.033,  0.810, 4.834)   <- overshoots the f22 target by 1.28 in x
f20 (-1.161,  1.578, 2.347)   <- 2.7-unit jump in one frame
f22 (-0.750,  0.750, 1.000)   recover
```

Between chamber and impact the foot swings behind her; between
follow-through and recover it overshoots well past both bracketing keys and
snaps back at 2.5–2.7 world units per frame. In motion this is not a
roundhouse arc. The stills the build reviewed are fine; the animation is
not. This also accounts for the −0.0056 floor sink at f9 and the 0.060
support-foot slide at f8.

HardPunch's `Hand.R` flip is milder (a shaft is nearly rotationally
symmetric) but the trident still swings wide: at f9 the butt end reaches
world (−3.60, 4.65, 4.78) and the shaft sits 15° off horizontal between two
keys where it is exactly horizontal.

**Fix:** after building, walk each bone's four quaternion curves and negate
any key whose 4-vector dot with the previous key is negative (or run
`bpy.ops.pose.quaternions_flip()`), then re-check the arcs.

---

## 1. File integrity — PASS

- **Actions**: v114 had `Yemoja_Fuzz_matAction`, `Yemoja_Idle_MASTER`,
  `Yemoja_Idle_MASTER_v113_corkscrew`. v115 has those three plus the four
  `Yemoja_Atk_*`. All four new actions have `use_fake_user = True`.
- **`Yemoja_Idle_MASTER` and `Yemoja_Idle_MASTER_v113_corkscrew`: byte-for-byte
  identical** between v114 and v115. I compared every layer, strip and
  channelbag; for every fcurve the data path, array index, group, extrapolation,
  and for every keyframe the co, both handles, interpolation and both handle
  types — plus `use_fake_user`, `frame_range`, `use_frame_range` and the slot
  list. Zero differences.
- **Objects**: 43 in both files, identical names. Only two differences in the
  whole object table:
  - `Armature.animation_data.action`: `Yemoja_Idle_MASTER` → `Yemoja_Atk_HardKick`
    (cosmetic, but v115 opens with the rig mid-roundhouse rather than on idle).
  - `Trident.matrix_world`: differs in the 6th decimal (≈1e-5 world units),
    a round-trip artifact of `attach_trident()`'s reparent. Parent, parent_type
    and parent_bone are unchanged (`Armature` / `BONE` / `mixamorig:Hand.R`).
  - No modifier added, removed, renamed or toggled; no material slot changed;
    no `hide_viewport`/`hide_render` changed. (v114 was already saved in
    preview state: `Yemoja_Scalp/Shrinkwrap` off, `Yemoja_Tattoos` hidden.)
- **Bones**: 80 in both, identical names, parents, `head_local`, `tail_local`,
  `matrix_local` and `use_connect`. No bone added.
- **Cameras**: 18 in both. (BUILD_NOTES says "same 17 cameras as v114" — the
  count is wrong; nothing was added, so this is a doc error only.)
- **Materials**: identical list.
- **`Yemoja_Body`**: SHA-1 of all 7417 vertex coordinates identical.
  2000 randomly sampled vertices (seed 1234, same indices in both files) have
  byte-identical vertex-group membership and weights — **0 differences**.
- **Scene**: fps 30, fps_base 1.0 in both. `frame_start`/`frame_end` still 1/250.

---

## 2. Keying — PASS, with one export caveat

Walked `action.layers[*].strips[*].channelbags[*].fcurves` myself.

| Action | fcurves | bones keyed | keyed frames | expected |
|---|---|---|---|---|
| `Yemoja_Atk_Punch` | 231 | 57 | 1, 4, 7, 9, 14 | ✓ range 1–14 |
| `Yemoja_Atk_HardPunch` | 231 | 57 | 1, 7, 12, 15, 26 | ✓ range 1–26 |
| `Yemoja_Atk_Kick` | 231 | 57 | 1, 4, 7, 9, 16 | ✓ range 1–16 |
| `Yemoja_Atk_HardKick` | 231 | 57 | 1, 6, 12, 16, 22, 28 | ✓ range 1–28 |

- 57 bones = exactly the humanoid set (`mixamorig:*` minus `*_end` minus
  `Eye.*`). **Keyed-but-not-humanoid: none. Humanoid-but-not-keyed: none.**
- **Zero `hair_grp*` fcurves. Zero `Eye.*` fcurves.**
- `mixamorig:Hips` is the only bone with `location` keys; no bone has `scale`
  keys. 231 = 57×4 quaternion + 3 Hips location.
- **Every single fcurve carries keys at exactly the same frame set** — no
  partial keying, no stray keys, no missing channels.
- All keyframes `BEZIER` / `AUTO_CLAMPED`; all curves `CONSTANT` extrapolation.
- All quaternion keys are normalised to 1e-4.
- Scene fps is 30.
- **Caveat**: `action.use_frame_range` is `False` on all four and the scene
  range is still 1–250. An FBX export driven off the scene range will emit
  250-frame clips. The spec's frame ranges are not encoded in the file.

---

## 3. Idle bookends — PASS (exactly)

Assigned each action, `frame_set` to its first and last frame, read
`matrix_basis` on every humanoid bone, compared against `Yemoja_Idle_MASTER`
evaluated at frame 1.

| Action | max angular diff, first frame | max angular diff, last frame |
|---|---|---|
| Punch | **0.000°** | **0.000°** |
| HardPunch | **0.000°** | **0.000°** |
| Kick | **0.000°** | **0.000°** |
| HardKick | **0.000°** | **0.000°** |

Exactly zero, not merely under the 0.05° tolerance. Separately confirmed that
`pose_idle_master_2026-09-03_v114clean.json` and `Yemoja_Idle_MASTER` @ f1
agree to 0.00000°, so the two idle sources are the same pose.

---

## 4. In-place and floor — **FAIL on three clips, on interpolated frames**

Measured at **every** frame, not only keys: lowest world-z vertex of the
evaluated `Yemoja_Body` (with and without the kicking foot's dominant-bone
region), the dominant bone owning that vertex, and both ankle (`Foot.L/R`
head) positions against their idle positions.

Spec band: lowest body z ∈ [−0.005, +0.02]; support ankle error < 0.005.

| Clip | worst lowest-z (frame, bone) | worst support-ankle error (frame) | at keys |
|---|---|---|---|
| Punch | −0.0019 @ f5 (`Foot.L`) — **ok** | L 0.0128 @ f5, **R 0.0289 @ f3** | 0.0000 |
| HardPunch | **−0.0098 @ f9** (`ToeBase.L`); also −0.0087 f21, −0.0085 f20, −0.0084 f10 | **R 0.1440 @ f10**, R 0.1437 @ f9; L 0.0586 @ f5 | ≤ 0.0046 |
| Kick | see below — body **never reaches the floor** f2–f15 | R (support) 0.0393 @ f12, 0.0265 @ f11 | 0.0000 |
| HardKick | **−0.0056 @ f9**, −0.0054 @ f4, −0.0052 @ f8, −0.0047 @ f3 | L 0.0602 @ f8, 0.0561 @ f9, 0.0513 @ f4 | 0.0000 |

I confirm the build's key-frame numbers (HardPunch f12/f15 support error
0.0046; everything else 0.0000). The failures are entirely between keys, which
the build never sampled. The spec's own wording is not key-only: *"the support
foot never leaves its idle position"*, and HardPunch's *"Both feet pinned
throughout"*.

**`Yemoja_Atk_Kick` floats — and the build report mislabels it.**
At idle the **left** foot is the planted one: whole-body min z = −0.000003,
and the lowest vertex once `Foot.L`/`ToeBase.L` are excluded is **+0.0715 on
`ToeBase.R`** — the rear foot is raised in the idle stance. `pin_foot("R")`
holds the right foot at exactly that raised idle position, so once the left
foot lifts at f2, **nothing on her body touches z = 0 from f2 to f15**:

```
f1  lowest body z -0.0000 (Foot.L, planted)
f2  +0.0588   f4  +0.0715 (key)   f7  +0.0715 (key)   f9  +0.0715 (key)
f12 +0.0561   f15 +0.0373          f16 -0.0000 (planted again)
```

That is 3.6× outside the +0.02 ceiling for the whole strike. The build's own
`Yemoja_Atk_Kick_report.md` prints `lowest body z 0.0715` on **every** row
including frame 1, and BUILD_NOTES glosses it as
*"0.0715 (kicking foot; support/body floor clean)"*. That is the wrong bone:
`Foot.L`/`ToeBase.L` were the ones excluded, so the reported number is by
construction the **support** foot. The support/body floor is not clean; she is
airborne.

---

## 5. Twist — PASS

I implemented an explicit swing–twist decomposition about the bone's local Y
and confirmed it agrees with `common.twist_deg`'s `2·atan2(q.y, q.w)` to
< 1e-9° on every bone and frame — so the build's metric is sound.

Maxima over **every frame** of every clip:

| Bone | max |twist| | where | budget |
|---|---|---|---|
| `Hand.L` | **19.84°** (constant, all 84 frames) | — | 30° |
| `Hand.R` | **29.88°** | idle frames (pre-existing) | 30° |
| `Hand.R` | 27.4° | HardKick f14 (worst authored) | 30° |
| `Foot.L` | 7.6° | Kick f4/f9 | 20° |
| `Foot.R` | **16.14°** | idle frames (pre-existing) | 20° |

Nothing exceeds budget on any frame. Two notes: (a) the `Hand.R` 29.88° and
`Foot.R` −16.14° ceilings are the untouched idle master, as BUILD_NOTES says;
(b) `Hand.L` is **19.836667°** on all 84 frames of all four clips — the left
wrist is never posed in any clip, it inherits the idle wrist angle even at the
jab's impact. Within budget, but the punching wrist is not aligned to the
forearm at contact.

---

## 6. Deformation — my numbers reproduce the build's, but its acceptance claim is false

Own surface-area audit: dominant bone per face by summed vertex weight,
evaluated-mesh polygon areas REST vs POSE, on `Yemoja_Body` and
`Yemoja_Clothes` at every key frame. My ratios match the build's report
`.md` files to three decimals everywhere I checked.

**Confirmed** (the build's honest disclosures):
- `Yemoja_Body/HandPinky2.L` = **0.859** in the untouched idle master with
  zero posing. Genuinely pre-existing.
- `Yemoja_Clothes/Arm.L` = **0.751** and `Arm.R` = **1.073** at idle on a
  2-face island; `Arm.L` drops to **0.261 (2/2 faces crushed)** at HardKick
  f12. Rig artifact, not a pose choice.
- `Yemoja_Body` Arm/ForeArm/UpLeg/Leg/Spine2 at Punch, HardPunch and Kick keys:
  all ≥ 0.95. Punch f7 `Shoulder.L` 0.975, HardPunch f12 `Shoulder.R` 0.965 —
  matches the build's sweep results.

**Refuted.** BUILD_NOTES' acceptance table states: *"`Arm.*`, `ForeArm.*`,
`Shoulder.*`, `UpLeg.*`, `Leg.*` on `Yemoja_Body` are ≥0.95 at every key in
every clip except HardKick's `Shoulder.R`."* Measured on `Yemoja_Body`:

| HardKick key | `Shoulder.L` | `Shoulder.R` | `Arm.L` | `Arm.R` | `Foot.R` |
|---|---|---|---|---|---|
| f6 | **0.848** (8/74 crushed) | **0.949** | 0.980 | **0.949** | 0.999 |
| f12 | **0.750** (**27/74** crushed) | **0.751** (**25/74**) | **0.940** | 1.004 | **0.930** |
| f16 | **0.816** (**28/74**) | **0.882** (**26/74**) | **0.956** | 1.075 | 0.990 |
| f22 | **0.836** (8/74) | **0.939** | 0.979 | **0.945** | 0.990 |

`Shoulder.L` is below the **0.90 general floor** at all four posed keys and is
mentioned nowhere in BUILD_NOTES except one parenthetical for f12. `Arm.L`
0.940 (f12) and `Arm.R` 0.945/0.949 (f22/f6) are below the 0.95 `Arm.*` floor
and are not mentioned at all. The crushed-face counts — 25–28 of 74 shoulder
faces collapsed below 50 % of rest area at f12/f16 — appear nowhere.

---

## 7. Clavicle rule (README §3) — partially honoured, two clear violations

Method: elevation = change in the bone's inclination above the armature
horizontal plane vs idle. (Note: `Shoulder`'s tail *is* `Arm`'s head, so
comparing their world z is degenerate — it always gives ratio 1.00.)

| Frame | striking arm Δelev | same-side `Shoulder` Δelev | ratio | README ~1/3 |
|---|---|---|---|---|
| Punch f7/f9 | `Arm.L` **+43.8°** | `Shoulder.L` **+20.0°** | 0.46 | over |
| HardPunch f12 | `Arm.R` +19.3° | `Shoulder.R` +14.2° | 0.73 | over |
| HardKick f12 | `Arm.R` +49.3° | `Shoulder.R` +27.7° | 0.56 | over |
| **HardKick f16** | `Arm.R` +40.6° | `Shoulder.R` **+47.4°** | **1.17** | clavicle raised *more* than the arm |
| **HardKick f16** | `Arm.L` **−2.4°** (arm goes **down**) | `Shoulder.L` **+16.5°** | −6.96 | clavicle shrugs while the arm drops |

**The trigger condition is not met on either punch.** README §3's elevation
clause applies when the arm goes *above shoulder height*. At Punch f7 the
striking hand is at world z 5.820 with `Arm.L` head at 6.123 — **below**
shoulder height. At HardPunch f12 the hand is at 5.450 with the shoulder at
5.866 — also below. No elevation is called for; 24° and 15° were applied
anyway, purely to move an area-ratio number (BUILD_NOTES says as much). At
20° and 14° of measured clavicle rise on a straight jab and a thrust, this
will read as a shrug.

**Protraction:** Punch f7/f9 `Shoulder.L` −Y 18° ✓ (spec exact).
HardPunch f7 `Shoulder.R` retract 8° ✓; f12/f15 `Shoulder.R` +Y 20° protract ✓.

**Missing:** the spec's clavicle rule requires *"retract the other side 5–8°"*
on any punch/thrust. Neither punch does it. Punch never touches `Shoulder.R`
(Δelev +0.3°, no Y rotation); HardPunch **elevates** `Shoulder.L` +16° instead
of retracting it.

---

## 8. Trident — **REJECT-level penetration at HardKick f16, real penetration at f12**

Method: shaft segment from `trident_hand_local_matrix()` composed with the
posed `Hand.R` matrix; 401 samples along it; inside/outside decided by
5-direction ray-parity against a BVH of the evaluated `Yemoja_Body`; for each
inside run I report the dominant bone of the nearest vertex. Separately, a
segment–segment distance from the shaft to the `UpLeg.R` and `Leg.R` bone
segments.

| Key | shaft dir (armature) | shaft inside body | nearest-vertex dist | build's `trident_clearance` |
|---|---|---|---|---|
| every idle key | (−0.03, 1.00, 0.02) | none | — | 0.725 |
| Punch f4/f7/f9; HardPunch f7/f12/f15; Kick f4/f7/f9; HardKick f6/f22 | various | only 0.55–0.59 = the gripping fingers (expected) | — | 0.19–0.83 |
| **HardKick f12** | (−0.352, 0.553, −0.755) | **0.28–0.29 inside `UpLeg.R` (right thigh)** + grip | 0.131 | 0.1248 |
| **HardKick f16** | (−0.352, 0.553, −0.755) | **0.17–0.25 inside `Leg.R` (right shin)** — 0.65 world units of shaft buried, up to 0.230 from the nearest surface | 0.230 | 0.1254 |

Shaft centerline to bone segment (my own seg/seg): f12 shaft↔`UpLeg.R`
**0.290**, f16 shaft↔`UpLeg.R` **0.018** (BUILD_NOTES reports 0.290 and 0.009 —
we agree the centerline is essentially on the bone axis at f16).

**BUILD_NOTES' reasoning here is invalid.** It states: *"the shaft's centerline
comes within 0.009 of `UpLeg.R`'s bone segment at f16, but the actual
mesh-clearance check above (0.125-0.150, against the real deformed surface,
not the bone centerline) confirms it does not cross the leg."*
`harness.trident_clearance` is an **unsigned nearest-vertex distance**. A shaft
lying fully inside a limb reports a positive number equal to its distance from
the nearest surface *vertex*; it cannot distinguish inside from outside, and on
a mesh this coarse a segment through the middle of a shin is ~0.2 from the
nearest vertex. The metric can never confirm non-penetration.

**Visually confirmed in the build's own shipped renders**:
`review/Yemoja_Atk_HardKick_f16_side.png` and `_f16_q34.png` show the trident
shaft passing straight through her right calf, entering one side and exiting
the other. `_f12_q34.png` shows it grazing the raised thigh.

**Separate, unreported:** HardPunch's trident **tip reaches only
world (−1.03, −5.01, 5.61)** at f12; the spec asks for *"tip roughly at
(−0.6, −8.5, 5.6)"*. It is **3.5 world units short**, because the grip sits
55.6 % up an 8.09-long shaft so only 44 % projects forward of the hand. Not
mentioned anywhere in BUILD_NOTES, and it materially changes the thrust's
reach for hitbox authoring.

**Clothes penetration, HardKick** (my 5-ray parity count of `Yemoja_Clothes`
vertices inside `Yemoja_Body`, 5741 verts total):

| frame | mine | build's | spec |
|---|---|---|---|
| f1 (idle baseline) | 100 | 102 | baseline |
| f6 | 215 | 190 | ok |
| f12 | **344** | 233 | at the threshold |
| f16 | **460** | 223 | **above the 350 flag threshold** |
| f22 | 184 | 317 | ok |

Our idle baselines agree (100 vs 102), so the methods are comparable; the f16
divergence (460 vs 223) is large and lands on the wrong side of the spec's own
"above 350, flag it" rule. BUILD_NOTES concludes "all comfortably under 350".

---

## 9. Silhouettes at the impact frame — front camera fails on three of four

Viewed `review/*_f{impact}_sil_front.png` and `_sil_side.png` at native
300×412 and downsampled to thumbnail size.

| Clip | `_AR_front` | `_AR_side` |
|---|---|---|
| Punch f7 | **FAIL** — reads as a neutral standing figure. The jabbing arm points at the camera and is entirely absorbed into the torso outline; the only silhouette event is the vertical trident, which is idle. | **PASS** — unambiguous straight punch, clean negative space under the extended arm |
| HardPunch f12 | **FAIL** — the trident foreshortens to a short diagonal stub across the chest; no thrust reads at all | **PASS** — the strongest silhouette in the set; horizontal shaft, extended arm, driving stance |
| Kick f7 | **FAIL** — reads as a standing figure with a slightly bent knee; the kicking leg points at the camera | **PASS** — clear front snap kick, leg and foot fully legible |
| HardKick f12 | **MARGINAL PASS** — high raised knee plus leaning torso does read as a kick, but the shin/foot foreshortens away | **PASS** as a dramatic high kick — but the trident visibly crosses the raised leg (see §8), and at f16 the raised foot is *dorsiflexed* (toes up, sole facing forward), reading as a "stop" gesture rather than a strike |

Root cause: she strikes toward world −Y and `_AR_front` looks at her from the
front, so every forward strike foreshortens to nothing. Only the side camera
reads. The spec requires both. If the shipping game camera is side-on this may
be an acceptable product call — but it is not what was signed off, and
BUILD_NOTES claims the silhouettes pass without qualification.

Two additional pose observations from the measurements:

- **Head tracking fails at four keys.** Spec: *"the face keeps pointing +Z
  within ±10°"*; SPEC_hardkick_v2 f12/f16: *"within 15° of +Z"*. Measured
  angle between the head's forward axis and armature +Z:
  Punch f7 **0.1°** ✓, f9 1.0° ✓; Kick f7 **7.4°** ✓;
  **HardPunch f7 18.0° ✗, f12 14.0° ✗, f15 14.0° ✗**;
  **HardKick f12 21.4° ✗, f16 25.7° ✗** (f6 6.3° ✓, f22 9.3° ✓).
- **Kick f7's foot is never articulated.** Spec: *"Foot.L extended so the sole
  faces +Z"* (ball leading). `kick_f7()` applies no `Foot.L`/`ToeBase.L`
  rotation at all. Measured toe-vs-shin angle at f7 = **100.7°**, identical to
  idle (100.7°) — the ankle sits at exactly its idle relative angle. The f4
  chamber's toe-up (135.4°) *is* applied; the impact's plantarflexion is not.
  HardKick f12 *does* get it right (ankle→toe-tail vs shin **14.5°**, matching
  BUILD_NOTES, inside the 15° spec) — but f16 does not (105.3°, = idle).

**Where the numbers confirm the build:** HardKick f12 thigh angle to horizontal
**39.5°** (spec 20–40°, upper edge — matches BUILD_NOTES); knee lateral offset
from the hip→foot line **1.557 toward −X** (spec floor 0.5 — matches); hip→foot
distance **2.216 = 56.0 %** of the 3.96 max reach (spec 60–70 %, under, as
BUILD_NOTES admits). One nuance BUILD_NOTES omits: the full perpendicular
offset is (−1.557, +0.326, **+0.402**), i.e. the knee is also 0.40 **above**
the hip→foot line, against SPEC_hardkick_v2's *"never above it"*. The lateral
component dominates, so the read is mostly right.

**"Hold" keys are near-freezes.** Punch f7 vs f9 differ on 44 of 231 channels
but the wrist moves only **0.016** world units (0.456,−1.565,5.820 →
0.455,−1.581,5.818), because `clamp_reach` pins both to the same reach ceiling;
frames 7–9 are visually identical. HardPunch f12 vs f15 differ on 12 channels,
wrist moves 0.10. BUILD_NOTES explains *why* the Punch clamp happens but not
that the consequence is zero secondary motion across the contractual impact
window.

---

## 10. Code review — `harness.py`, `attacks_build.py`

**a. Silent-failure risk in `build_clip` (highest).** It reattaches the action
and then calls `keyframe_insert` 231 times without any depsgraph flush,
trusting that nothing re-evaluates the animation and overwrites the
hand-built `matrix_basis` first. The module docstring documents exactly that
hazard — and then depends on the opposite behaviour holding. It does hold in
5.0.1 (I verified the keyed poses hit the intended IK targets exactly, e.g.
HardPunch f12 `Hand.R` head lands on (−0.550, −1.400, 5.450)). It is not
guaranteed in 5.2, and if it breaks it fails **silently**, keying the
constant-extrapolated neighbouring pose. Safer: snapshot every bone's
`matrix_basis` into a dict after `fn()`, then write quaternion/location values
into the fcurves from the dict rather than from live rig state.

**b. Unguarded 5.x action API.** `_all_channelbags` walks
`action.layers[*].strips[*].channelbags`. That is the 4.4/5.0 shape and is not
wrapped in a fallback. If 5.2 exposes only `strip.channelbag(slot)`,
`set_interpolation_5x` and `_clear_action` raise `AttributeError` mid-build,
after some actions have already been written. Add a `getattr` fallback.

**c. Hidden state in `_REST_CACHE`.** It is a module global keyed only on
object name, populated on the first `audit()` call, capturing whatever
modifier state was live at that moment, with no invalidation.
`attacks_build.main()` happens to call `preview_mode(True)` first so it is
consistent — but any caller who audits before flipping preview mode silently
gets a rest baseline from a different modifier stack and every ratio after
that is wrong, with no error.

**d. Unrestored global scene state on exception.** `L.get_action()` sets
`bpy.context.scene.render.fps` as a side effect of *fetching an action*.
`L.review()` mutates the render engine, resolution, camera, workbench shading
and the `Yemoja_Source` layer-collection `exclude` flag with **no
`try/finally`** — an exception inside a render leaves the scene in silhouette
mode with the source collection excluded. Harmless headless; a mess the user
has to undo by hand in an interactive 5.2 session.

**e. Not runnable on the user's machine as written.** `attacks_build.py` calls
`common.load()` → `bpy.ops.wm.open_mainfile()`, which in a live Blender
**discards the current session without warning**, then
`bpy.ops.wm.save_as_mainfile()` overwrites v115. It also hardcodes
`sys.path.insert(0, "/home/claude/work/attacks")`, the v114 path, and
`/mnt/user-data/uploads/.../pose_idle_master_2026-09-03_v114clean.json` — none
of which exist there. Needs argv/env parameterisation and a guard before it
can be handed over.

**f. Writes outside the .blend.** `yemoja_anim_lib.capture_grip()` rewrites
`yemoja_anim_lib.py` *in place* via regex (not called by the build, but one
call away). More practically: `harness.REVIEW_DIR` is a **separate module
constant** hardcoded to `/home/claude/work/attacks/review` and used for the
`.md`, while the PNGs go to `L.REVIEW_DIR`, which `common.load()` overrides at
runtime and whose in-file default is still a `C:\Users\steph\...` Windows path.
On another machine the report and its renders land in two different places, one
of which does not exist.

**g. `trident_clearance` is the wrong shape of test.** Unsigned nearest-vertex
distance used as a penetration check (see §8). Replace with a signed test —
BVH ray parity, or `closest_point_on_mesh` plus a normal dot — or it will keep
certifying shafts that are inside limbs.

**h. `L.rot()` axis frame is not what the spec's wording means.** It rotates
about an axis expressed in the bone's **rest** parent frame
(`M.inverted() @ R @ M` with `M = bone.matrix_local`), not the posed armature
frame. Under HardKick's +75°/+95° hips yaw, the spine chain's "lean back −X" is
applied about an axis 75–95° away from her actual side-to-side axis. It happens
to produce the right *world* lean here (measured torso tilt at f12: 25.2° from
vertical, 21.7° of it backward — close to the spec's intended √(20²+10²)), but
the code and the spec's wording disagree about the frame, and any change to the
hips yaw silently changes what the spine numbers mean.

**i. Correct, verified.** `world_delta_to_armature`'s bug fix is right — I
re-derived the mapping independently (armature +Y = world +Z, armature +Z =
world −Y ⟹ `arm = (wx, wz, −wy) × 100`) and it round-trips. The finger-bone
exclusion fix in `trident_clearance` is also right, and `_exempt()` correctly
does *not* exempt `mixamorig:Hand.R` itself (only the 15 named finger bones).
`harness.key()`'s dead `Trident` branch is a documented no-op and harmless.

---

## Ranked fixes

1. **Quaternion continuity pass on all four actions.** Negate any key whose
   4-vector dot with the previous key is negative, then re-verify the ankle /
   hand arcs frame-by-frame. Fixes HardKick's backwards-looping kicking leg and
   HardPunch's wide trident swing. Cheapest change, largest visible payoff.
2. **HardKick f12/f16: get the trident out of her leg.** Re-target the
   counterbalance arm (or change the shaft direction) until a *signed*
   inside/outside test returns zero penetration, not just a positive
   nearest-vertex distance. Replace `harness.trident_clearance` with that
   signed test first, or the same mistake recurs.
3. **`Yemoja_Atk_Kick`: plant her.** Drop the Hips ≈0.07 (or re-pin the support
   foot to the floor rather than to its raised idle position) so something on
   her body is at z ≈ 0 for the whole clip. Fix the report's lowest-z labelling
   so the excluded region is named.
4. **HardPunch f7→f12: stop the skate and the sink.** Rear ankle moves 0.144 at
   f10 and the toe sinks −0.0098 at f9. Add breakdown keys with `pin_foot`
   applied, or re-time the lunge; then re-measure every frame, not just keys.
5. **Head tracking.** Add Neck/Head Y at HardPunch f7/f12/f15 (18°/14°/14° off)
   and HardKick f12/f16 (21.4°/25.7° off) until inside ±10° / ±15°.
6. **Kick f7: articulate the striking foot.** Plantarflex `Foot.L`/`ToeBase.L`
   for a ball-leading snap kick — currently identical to idle. Same at
   HardKick f16 (105.3° vs f12's correct 14.5°).
7. **Clavicles.** Add the missing 5–8° opposite-side retraction on both
   punches. Back Punch f7's `Shoulder.L` elevation off 24° toward README's ~1/3
   (or, better, fix the underlying `Shoulder.L` area number with joint weight
   smoothing per README §12 — that is the real cause, and elevation is only
   masking it). Cap HardKick f16's `Shoulder.R` at ≤1/3 of the arm's rise
   (currently 1.17×) and remove `Shoulder.L`'s +16.5° shrug against a
   descending arm.
8. **Front-camera readability.** Punch, HardPunch and Kick do not read as
   strikes from `_AR_front`. Either add lateral/angular offset to the strike
   line so it silhouettes from the front, or get an explicit sign-off that only
   the side camera matters for this game's framing.
9. **Export range.** Set `use_frame_range` + `manual_frame_range` per action
   (or document the export range) so Unity gets 14/26/16/28 rather than the
   scene's 1–250. Also reset the Armature's active action to
   `Yemoja_Idle_MASTER` before saving.
10. **Correct BUILD_NOTES.** Its acceptance table is wrong on: `Shoulder.L`
    (0.750–0.848 at all four HardKick keys, with 8–28 of 74 faces crushed),
    `Arm.L`/`Arm.R` sub-0.95 on HardKick, the HardKick f16 clothes-penetration
    count (460 vs the reported 223, over the 350 flag threshold), the camera
    count (18, not 17), and the `Yemoja_Atk_Kick` lowest-z bone (support foot,
    not kicking foot).
