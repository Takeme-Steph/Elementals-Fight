# Yemoja — animation guidelines

Authoritative reference for whoever authors Yemoja's animations, human or agent.
Peer document to `README_rig_conventions.md` and `README_eye_standard.md`.
Written 2026-08-30 against `Yemoja_WORKING_v103_preClavicle.blend` (80 bones).

Read sections 1, 2 and 3 before keying anything. Section 3 is the one that will
otherwise cost you a redo.

---

## 1. What this character is

A playable fighter in **Elementals Fight** — Unity 6, URP, **mobile target**.
Imported as a **Unity Humanoid** avatar, so clips retarget through Unity's muscle
system rather than replaying raw bone rotations. That single fact drives most of
the rules below.

Stephanie's brief: *dramatic moves and combos*. The rig has been reviewed against
five extreme reference poses (jumping jack, high kick, deep squat, forward lunge,
backflip) and the results are in section 4.

---

## 2. Rig inventory — 80 bones

Root bone is `mixamorig:Hips`. Every bone is a deform bone; there are no
control or helper bones. **Do not add bones.** A bone-count change forces a Unity
`animationType = None -> reimport -> Human + CreateFromThisModel -> reimport`
avatar reset plus a full re-audit of the humanoid map (see the v4 import record).

Naming is **Blender convention, not Mixamo convention**: `mixamorig:Arm.L`, not
`mixamorig:LeftArm`. The `mixamorig:` prefix is retained; the side suffix is
`.L` / `.R`. Mixamo-style names break Blender's mirror-aware tools and were
renamed project-wide for that reason. Any clip written against Mixamo names will
not bind.

| Group | Count | Bones |
|---|---|---|
| Core | 8 | `Hips`, `Spine`, `Spine1`, `Spine2`, `Neck`, `Head`, `HeadTop_End`, `HeadTop_End_end` |
| Arms | 6 | `Shoulder.L/R`, `Arm.L/R`, `ForeArm.L/R` |
| Hands | 32 | `Hand.L/R` + 15 finger bones per side |
| Legs | 12 | `UpLeg.L/R`, `Leg.L/R`, `Foot.L/R`, `ToeBase.L/R`, `Toe_End.L/R`, `Toe_End_end.L/R` |
| Eyes | 2 | `Eye.L/R` |
| Hair | 20 | `hair_grp00_0/1` .. `hair_grp09_0/1` |

`mixamorig:Shoulder.L/R` **is the clavicle.** There is no separate collarbone
bone and none is needed — it runs from the sternum end (0.176 from the midline)
to the acromion (0.523), where `Arm` begins. It maps to Unity's
`LeftShoulder` / `RightShoulder` humanoid slots, so clavicle motion survives
retargeting.

There is **no jaw bone**. Unity's auto-mapper has twice tried to bind the Jaw
slot to a hair bone; if the avatar is ever rebuilt, remove that entry.

### The 20 hair bones do not belong to you

`hair_grp*` are **not humanoid bones**. Unity's Humanoid retargeting transfers
only the humanoid muscle set — animation keyed onto non-humanoid bones is
**dropped on import**, silently, with nothing in the console.

So: **never keyframe hair.** Hair motion is a Unity-side runtime job (spring /
jiggle driven off head motion). Keying it in Blender wastes the work entirely.
Same applies to `Eye.L/R` if the eye slots turn out to be unmapped — verify
before investing in gaze animation.

---

## 3. THE CLAVICLE RULE — the one that matters

**Whenever the arm rises above shoulder height, rotate the clavicle with it,
by roughly one third of the arm's elevation.**

A real shoulder splits elevation roughly two-to-one between the arm socket and
the collarbone. If `Arm` does all the work while `Shoulder` stays still, the
shoulder cap crushes. Measured on this character, jumping-jack arm position,
arm identical in every sample, only the clavicle varying:

| Clavicle elevation | Shoulder surface area vs rest | Faces losing >half their area |
|---|---|---|
| 0 deg  | 78.8% | 106 of 457 |
| 10 deg | 83.6% | 72 |
| 20 deg | 89.8% | 46 |
| **30 deg** | **97.0%** | **22** |
| 40 deg | 104.7% | 12 |

Surface area is the collapse detector: skin slides and creases, it does not lose
area. At 0 deg, roughly a quarter of the shoulder's surface has been crushed into
itself. At 30 deg it is essentially intact.

**This is an animation problem, not a rig problem.** No weighting change fully
rescues a joint that never moves. Key the clavicle.

Also key clavicle **protraction** (sliding the shoulder forward) on punches and
thrusts. The ratio above is for elevation only; a straight-arm strike is
dominated by protraction and is not covered by it.

Do not attempt to automate this with a Copy Rotation constraint from `Arm` onto
`Shoulder` — `Arm` is a child of `Shoulder`, so that is a dependency cycle. It
will appear to work in Local Space and then produce evaluation-order-dependent
nonsense.

---

## 4. Measured failure envelope

From a whole-body scan of body, clothes and tattoo decal across the five
reference poses. Numbers are surface area as a fraction of rest.

**Safe — no action needed**
- Deep knee and hip flexion (deep squat, forward lunge): body worst region 0.92,
  essentially no crushed faces. Squat and lunge as deep as you like.
- High hip flexion (high kick, foot above head): thigh and shin unaffected.
- The tattoo decal everywhere: never below 0.97, zero crushed faces across all
  five poses. Worst stretch 2.45x on the shin twist. The decals are not a
  constraint on posing.

**Needs the clavicle rule**
- Arm above shoulder height: shoulder drops to 0.77-0.79 with 35-43% of its
  faces crushed. Occurs in jumping jack, high kick and backflip alike. This is
  the single worst deformation on the character.

**Cloth risk — check visually, do not assume**
Baseline: 82 clothing vertices already sit inside the body at rest (max depth
0.0255). That is the control; measure against it, not against zero.

| Pose | Verts inside body | Max depth | vs baseline |
|---|---|---|---|
| Forward lunge | 89 | 0.0276 | clean |
| Deep squat | 122 | 0.1587 | mild |
| Jumping jack | 138 | 0.1717 | mild |
| High kick | 347 | 0.2008 | real |
| Backflip | 686 | 0.1535 | worst |

Deep spine extension is the cloth killer: on the backflip, 66 of 539 clothing
faces over `Spine` lose more than half their area and one stretches 7x. Arching
her back hard will show through the shorts. Prefer a tucked flip to an arched one
until the clothes are reweighted.

(Penetration is measured by nearest-vertex signed distance, which is unreliable
in concave areas — treat the two bad rows as indicative, not exact.)

---

## 5. Units, scale, timing

- Character height **7.4564 Blender units**; exports to **1.80000** Unity source
  units. Ratio **4.1424 Blender units per Unity unit**. In-game height 3.4500 at
  the prefab's 1.91667 root scale.
- The **Armature object has scale 0.01.** Anything parented to it must copy
  `matrix_parent_inverse` from an existing child, not identity.
- Scene fps **30**. Keep clips at 30 unless there is a reason not to.
- She faces approximately **-Y** in Blender. Neutral forward in Unity is **+Z**
  (`CharacterPhysics.FaceOpponent` snaps rotation to Y 90/270 for the 2.5D setup).
- **Root motion:** the game drives movement through `CharacterPhysics`, not root
  motion. Author clips **in place**. Unity uses
  `Root Transform Position (Y): Based Upon = Feet` on all existing clips.

### Floor plane — FIXED 2026-08-30, was a long-standing fault
Her feet now rest **exactly on z = 0** (body lowest point -2e-6). Plant feet
against z = 0 with confidence.

History worth keeping: until v104 the rest mesh sat **0.1432 Blender units below
z = 0** (-0.0346 Unity source units). That constant is what had been appearing as
the **-0.03 end** of the "toe clearance -0.03 to -0.39" note in every Unity
import record since v2, where it was repeatedly attributed to pose lean. It was
not pose lean. Fixed by shifting all 18 root objects up by 0.14324; character
height unchanged at 7.45644.

Consequence for the Unity side: the `Armature` object now carries a **Z
translation of 0.14324** instead of sitting at the origin. That is intentional.

## 6. What not to touch

The character carries live modifiers and geometry nodes that animation must not
disturb:

| Object | Stack | Note |
|---|---|---|
| `Yemoja_Tattoos` | `Conform` (Shrinkwrap) then Armature | Shrinkwrap is **before** the armature. Edit decals in **REST** pose only — it targets the body's evaluated mesh, so editing in pose conforms to a posed body. Apply before FBX export; Unity has no shrinkwrap. |
| `Yemoja_Scalp` | Shrinkwrap, `Locs_Generator` (nodes), `Hair_Weights` (nodes), Armature | Generates the locs and writes the 20 hair-bone weights. Do not reorder or modify. |
| `Yemoja_Fuzz` | Armature, Shrinkwrap, 2x Displace | Shrinkwrap is **after** the armature here, deliberately. |
| `Yemoja_Body` / `Yemoja_Clothes` | Armature | 57 / 72 vertex groups. |

Vertex groups live in the mesh **deform layer**, not `mesh.attributes`. Reading
weights via `.attributes` returns nothing and looks like corruption.

The armature modifier resolves weights via the object's **vertex groups**, not
named attributes. Geometry Nodes can write a perfect float attribute and the
armature will ignore it until a real (even empty) vertex group of that name
exists. Nothing warns you.

If pose tests read exactly 0.0 movement, **check `armature.data.pose_position`
first** — it has been left on `REST` more than once.

---

## 7. Unity handoff requirements

**Animation events are mandatory and currently missing.** `AttackCTRL` and
`PlayerStateMachine` require these on the clips or the states play visually and
do nothing:

| Event | Where |
|---|---|
| `PerformAttack` | impact frame of each attack clip |
| `StopAttacking` | end of each attack clip |
| `EndHit` | end of hit / knockback clips |

These have been absent since v2 and are the reason **her attacks currently deal
no damage**. Any new attack clip must ship with its impact frame identified, or
the same gap reopens.

Existing clip set (all Mixamo placeholders, to be replaced): Idle, Walk, Jump,
Attack, HeavyAttack, Block, Hit, KnockBack — wired in `YemojaAnimCTRL`.

Import settings for any new clip: `animationType = Human`,
**`avatarSetup = CreateFromThisModel`**. Never `CopyFromOther` — it skips
proportional retargeting and sank the character half underground on the first
attempt.

---

## 8. Test protocol

`stress_poses.json` is stored as a text datablock inside the .blend. It holds the
five reference poses as target limb directions plus hip offsets, so they can be
re-applied identically at any time. Re-run them after any weight or rig change
and compare surface-area ratios against the tables in section 4 — that is what
makes a before/after comparison meaningful rather than impressionistic.

Method notes worth reusing:
- Judge collapse by **triangle surface area vs rest**, not by eye.
- **Always measure the null case.** Without the rest-pose penetration control,
  the forward lunge looks like a cloth failure when it is identical to standing.
- A vertex-only check is not a check. Sample face centres and edge midpoints.

---

## 9. Open — verify before relying on

- **Unity shoulder muscle ranges.** Unity clamps humanoid rotations to per-muscle
  limits, and shoulder defaults are tight. It is possible for 30 deg of clavicle
  baked in Blender to be clamped on import, silently. Unity was not running when
  this document was written; check `humanDescription.human[]` limits for the
  `LeftShoulder` / `RightShoulder` slots before assuming the rule survives export.
- Whether `Eye.L/R` are mapped in the current humanoid map (they were on v4).
- Clothes reweighting for deep spine extension — not yet done.

---

## 10. Export sequence — order is load-bearing

Several pre-export steps are only safe in a particular order. Doing them out of
order silently breaks things.

1. **Apply the `Conform` shrinkwrap on `Yemoja_Tattoos`.** Unity has no
   shrinkwrap. Must be done in REST pose.
2. **Apply the `Yemoja_Scalp` modifier stack** — Shrinkwrap, `Locs_Generator`,
   `Hair_Weights` — to realise the locs and bake the 20 hair-bone weights into
   real vertex groups.
3. **Only now remove the `hair_attach` UV layer.** It is **not** a stray layer:
   `Locs_Generator` reads it through its "Scalp UV" input. Removing it before
   step 2 destroys hair generation entirely. It becomes redundant only once the
   generator has run.
4. **Apply `Yemoja_Fuzz`'s Shrinkwrap and both Displace modifiers.**
5. Verify weights: every deforming mesh must sum to 1.0 with **at most 4
   influences**. Blender's armature modifier normalises internally at deform
   time, so unnormalised weights look perfectly fine in Blender and deform
   *differently* in Unity, which clamps to 4 influences first and then
   normalises. Nothing reports the discrepancy. (`Yemoja_Clothes` shipped 96
   five-influence vertices this way until v104.)
6. Confirm only one armature exists, and no stale actions are present.
7. Export FBX.


---
---

# ADDENDUM — 2026-09-03, added by Opus

Sections 1–10 above were written by the Blender agent against v103 and remain
authoritative for the rig itself. This addendum records what has been **measured
or changed since**, and sets the working agreement now that two agents are
animating this character in parallel.

Where the two conflict, the addendum wins — it is newer and every claim in it was
measured, not assumed.

---

## 11. Two agents, one character — the working agreement

| | Opus | Fable |
|---|---|---|
| Owns | Idle, locomotion (walk, jump, block) | Attacks, hits, knockback |
| Working file | `Backups/Yemoja_WORKING_v113_idlePose.blend` | its own `Yemoja_WORKING_v1xx_<stream>.blend` |
| Action prefix | `Yemoja_Idle_*`, `Yemoja_Move_*` | `Yemoja_Atk_*`, `Yemoja_Hit_*` |

### The rule that matters most: only one file is the model of record

Both streams need the **same** mesh, weights and rig. If both agents fix weights
in their own file, the fixes diverge and only one can ever ship.

- **The model of record is the highest-numbered `Yemoja_WORKING_v1xx` file.**
- **Mesh, weight and modifier edits are made ONLY in the model of record**, and
  only by the agent that currently holds it. Announce it in chat before editing.
- The other agent **pulls** changes by starting a new version *from* the model of
  record and re-appending its own actions — never by re-doing the same fix.
- Actions are portable: an action with a fake user appends cleanly into any file
  with matching bone names. Animation is the cheap thing to move; weights are not.

### Blender itself is a single shared instance

There is one live Blender on Stephanie's machine. Two agents driving it at once
will silently clobber each other — one changes the pose while the other is
measuring it. **Before running anything, check `bpy.data.filepath`**; if it is not
your file, stop and hand back. Do not open your file over someone else's
unsaved work.

### What is finished and must not be re-litigated

Re-deriving these wastes hours and risks regressions:

- The trident's Unity offset, the slimmed shaft, the bracelet binds and the joint
  weight smoothing (sections 12–13 below).
- The armature-space axis conventions (section 13).
- Stephanie's hand-authored idle master pose, stored as action
  `Yemoja_Idle_MASTER` and as `pose_idle_master_2026-09-03.json`.

---

## 12. Changes made since v103

### The trident (Unity-side asset, not in the .blend)

Lives at `Assets/CharacterModels/Yemoja/models/Yemoja_Trident.fbx`, attached in
Unity as a direct child of `mixamorig:Hand.R` on **both**
`Prefabs/Characters/PlayerPrefabs/Yemoja.prefab` and
`Prefabs/Characters/DisplayModels/YemojaDisplay.prefab`. `A_Trident` (the weapon
hitbox) is a child of the trident, so it tracks any offset change automatically.

- **Shaft slimmed 41%**, 58 mm → 34 mm diameter. Length (8.093) and tine spread
  (2.084) unchanged. Original backed up as `Yemoja_Trident.fbx.bak_2026-09-03`.
  At 58 mm no hand pose could close around it — that was a real blocker, not a
  posing failure.
- **Grip offset moved** from 16.8% up the shaft to ~55.6%, so the butt can plant
  on the ground with her hand at waist height. Current
  `localPosition = (0.72241, -0.05068, -0.02370)`; rotation and scale untouched.

**To preview the trident in Blender**, use `attach_trident()` in the library — it
reproduces Unity's exact transform. Do NOT place it by eye; a hand-placed trident
means what you animate is not what ships. The preview prop lives in an
`ANIM_PREVIEW_PROPS` collection so it can never ride an export.

**Unity hand-local → Blender hand-local: `(x, y, z) → (-x, y, z) × 414.248`.**
Solved from three shared bone positions; max error 0.0016 armature units.

### Weighting fixes (in the model of record)

- **Bracelets are now rigid.** Every bracelet vertex was weighted across the wrist
  joint (~68% ForeArm / 32% Hand) while the hands carry up to 91° of twist. The
  left bracelet lost 37% of its surface area with **127 of 324 faces crushed**.
  Now bound 100% to `ForeArm.L` / `ForeArm.R`: **area ratio 1.0000, zero crushed**.
  *General rule: anything solid — bracelet, anklet, buckle, ring, mask — binds to
  ONE bone. Weight blending exists to make skin bend; on a rigid object it can only
  distort it.*
- **Arm joint weights smoothed.** With no twist bones, pronation folds the skin.
  The handover between bones was both too abrupt and not centred on the joint.
  Re-ramped with `smooth_joint_weights()` — wrist half-width 0.22, elbow 0.18, in
  child-bone lengths:

  | region | before | after |
  |---|---|---|
  | `ForeArm.L` | 0.856 | **0.962** |
  | `Arm.L` | 0.921 | **0.970** |
  | `Hand.L` | 0.911 | **0.936** |
  | `ForeArm.R` | 0.942 | **0.978** |

- **Tested and rejected:** moving twist off the wrist onto the forearm. It only
  relocates the damage, and downhill — the forearm degrades faster than the hand
  improves (ForeArm.L 0.856 → 0.723 at 70% transfer). Leave twist where the pose
  puts it; fix it with weights, not rotation.

### The locs shrink when posed — viewport artifact only

`Yemoja_Scalp`'s Shrinkwrap sits BEFORE its Armature modifier and targets the
**evaluated** body, so a rest-pose scalp is projected onto a moved body and
`Locs_Generator` rebuilds the locs off the wrong surface: **-23.1% total loc
length** on a fight stance (0.00% with the modifier off). `Yemoja_Tattoos` has the
same fault via `Conform` (+24.2%).

Not a shipped defect — the export sequence (section 10) applies both in rest pose.
But you cannot judge a pose from a render that is lying, so **call
`preview_mode(True)` before rendering or measuring anything**, and
`preview_mode(False)` before export prep.

---

## 13. The measurement toolkit — `YemojaDesignArtifacts/yemoja_anim_lib.py`

Load it into Blender with `importlib`; it is not a Blender addon. Everything in it
was measured on this rig.

### Armature space (verified, not assumed)

The armature **data** is still in FBX Y-up space; the Armature **object** carries
the Y-up→Z-up conversion, scale 0.01, and the z=0 floor fix. Inside armature space:

    +X = her LEFT      +Y = UP      +Z = FORWARD (toward the opponent)

- Spine/Neck/Head — `+X` lean forward, `+Y` turn to her left, `+Z` tilt to her right
- `UpLeg` `-X` = knee lifts forward; `Leg` `+X` = knee bends
- Same-axis rotations **add** down a chain, so a flat sole needs
  `Foot_X = -(UpLeg_X + Leg_X)`
- `Arm.L +Z` / `Arm.R -Z` raise the arm sideways
- `ForeArm -X` = elbow curl (both sides, same sign)
- `Shoulder.L +Z` / `Shoulder.R -Z` elevate; `Shoulder.L -Y` / `Shoulder.R +Y` protract
- Finger bones: **local** `+X` = curl, local `Z` = spread, local `Y` = useless twist

**Mirror rule:** X keeps its sign, Y and Z flip. Proved algebraically and measured.

### Posing limbs — do not use shortest-arc aiming

"Point this bone at that target" leaves the bone's **roll** unconstrained: joint
centres land correctly, but the skin corkscrews and elbows appear to bend sideways.
Use `limb_ik()`, which *constructs* the orientation — local Y along the bone, local
X along the hinge. Measured: on **both** arms the elbow hinge is local X, and for a
correct curl the bend-plane normal is anti-parallel to it (dot = -0.999).

### Useful entry points

`preview_mode` · `apply_pose` / `mirror_pose` · `rot` / `lrot` / `rot_axis` ·
`limb_ik` / `arm_ik` / `leg_ik` / `set_bone_orientation` · `hand_shape` with
`OPEN_WATER_HAND` / `COILED_HAND` / `GRIP_HAND` · `orient_hand_for_shaft` ·
`attach_trident` / `trident_ends` · `plant_feet` / `lowest_world_z` ·
`smooth_joint_weights` / `normalise_weights` · `review` (clay + silhouette) ·
`get_action` / `key_pose` / `set_interpolation` · `capture_grip` /
`apply_captured_grip`

### Deformation budget

Run the surface-area audit (method in section 8) after every new extreme pose and
compare against section 4. Working thresholds:

| area ratio | verdict |
|---|---|
| ≥ 0.95 | fine |
| 0.90 – 0.95 | acceptable; check it visually |
| 0.85 – 0.90 | investigate — usually a weighting fault, not a posing one |
| < 0.85 | fix before shipping the clip |

**Always check the silhouette render, not just the clay render.** A pose that does
not read as a solid black shape at thumbnail size will not read on a phone
mid-fight. This caught a guard pose whose rear hand vanished into the torso
outline — invisible in play, and invisible in a lit render too.

### Two habits worth copying

- **Measure the null case.** Section 4's cloth numbers are only meaningful because
  the rest-pose penetration baseline was measured first.
- **When a number and a picture disagree, believe the picture and fix the metric.**
  A grip once measured as "zero penetration" while the render plainly showed the
  hand closed *beside* the shaft. The metric was answering the wrong question.

---

## 14. Still open

- **Right-hand grip fingers** deform under the tight curl: `HandPinky3.R` 0.675,
  `HandPinky2.R` 0.787, `HandRing2.R` 0.789. Easing the pinky and ring a few
  degrees would clear it. Stephanie authored this grip — ask before changing it.
- **The bracelets clip the forearm skin slightly** now that they are rigid. This is
  a *fit* problem (ring diameter vs arm at that point), not a deformation one, and
  is best fixed by nudging the bracelet geometry, not the weights.
- Unity shoulder muscle clamping (section 9) is still unverified.
- `stress_poses.json` has not been re-run since the weight changes. Re-running it
  would confirm nothing regressed.

---
---

# ADDENDUM — 2026-09-03 (later), added by Fable

Stephanie asked me to clear the deformations on the idle master pose so Opus
can move on. This section records what was wrong, what changed, and where the
work now lives. Everything below was measured on the rig, same method as
sections 4, 8 and 13.

## 15. Model of record is now `Backups/Yemoja_WORKING_v114_idleClean.blend`

Started from v113. Opus: pull from v114, do not re-apply anything below. The
file is open in the shared Blender instance as of this writing; I will hand it
back the moment you say so.

What v114 contains beyond v113:

| Item | Change |
|---|---|
| Action `Yemoja_Idle_MASTER` | Re-keyed at frame 1 with the cleaned arm chains. Same name, so nothing downstream changes. |
| Action `Yemoja_Idle_MASTER_v113_corkscrew` | The previous pose, untouched, fake user. Keep it until Stephanie signs off on the clean one, then it can go. |
| `pose_idle_master_2026-09-03_v114clean.json` | The cleaned pose, same schema as the original JSON. Original JSON left in place. |
| `Yemoja_Body` weights | 102 breast/upper-chest vertices: `Shoulder.L/R` weight moved to `Spine2` (see 15.2). Backup of the exact before-values is in the text datablock `fable_weight_backup_breast_v113`. |
| Text datablock `fable_armchain_snapshot_v113` | Head/tail/matrix of both arm chains as authored in v113, for reproducing the fix or reverting. |
| Renders | `anim_review/idle_clean_fable_*` (standard review set) and `anim_review/fable_diag_*` (diagnostics, rest vs pose, before vs after). |

No bones added, no modifiers touched, weights still sum to 1.0 with at most
four influences (verified with `normalise_weights`, 0 vertices needed capping).

## 15.1 What was actually wrong: the arm chains were corkscrewed

The joint positions were fine. The **roll** was not. Twist about each bone's
own axis, measured off `matrix_basis` (swing/twist decomposition):

| bone | v113 twist | v114 twist |
|---|---|---|
| `Arm.L` | +102° | +64° |
| `ForeArm.L` | −80° | +157° |
| `Hand.L` | −133° | +20° |
| `Arm.R` | +57° | +7° |
| `ForeArm.R` | −38° | +64° |
| `Hand.R` | +86° | +30° |

Every joint carried a large twist that the next joint partly cancelled, which is
exactly the "shortest-arc aiming leaves roll unconstrained" failure section 13
warns about. It is what viewport rotation produces when you drag a chain into
place by eye. The skin at every joint was wound around itself, and, worse, the
wound `Arm` bones were dragging the chest: with `Arm.L` zeroed the `Spine2`
region stretch dropped from 1.187 to 1.062, so most of the "breast deformation"
was the upper arms, not the breasts.

**Fix:** re-solved both chains with `limb_ik()` using the v113 elbow as the
pole and the v113 wrist as the target, then set each `Hand` back to its exact
v113 world orientation. Joint positions are preserved to 0.000 units; the
pose Stephanie authored is unchanged in silhouette, only the roll differs.

The residual twist that the authored hand orientation needs (about 175° on the
left, 100° on the right, in bone terms: the left hand is fully supinated,
palm-up) was then split between `ForeArm` and `Hand` by sweeping the split and
measuring:

| `ForeArm.L` twist | `Hand.L` twist | Arm.L | ForeArm.L | Hand.L |
|---|---|---|---|---|
| 0° | 175° | 0.982 / 10 crushed | 0.947 / 13 | 0.992 / 2 |
| 80° | 90° | 0.978 / 15 | 0.961 / 7 | 1.009 / 1 |
| **150°** | **20°** | **0.975 / 9** | **0.977 / 5** | **1.024 / 0** |
| 180° | −9° | 0.973 / 9 | 0.980 / 4 | 1.024 / 0 |

So on this rig, with the smoothed joint weights from section 12, **the forearm
carries pronation better than the wrist does.** That is the opposite of the
section 12 finding, but that test was run on the corkscrewed chain, where the
forearm twist was stacking on top of a wound upper arm. On a clean chain, put
the twist where the anatomy puts it: forearm for pronation, wrist near zero.
Rule of thumb for the attack clips: keep `|Hand twist| < 30°`, let `ForeArm`
take the rest, and always build arm poses with `limb_ik()` rather than rotating
by hand.

Regions after the fix (body, area ratio / crushed faces): `Shoulder.L` 0.972/4,
`Arm.L` 0.975/9, `ForeArm.L` 0.977/5, `Hand.L` 1.024/0, `Shoulder.R` 0.990/0,
`Arm.R` 0.990/14, `ForeArm.R` 0.981/3, `Hand.R` 1.080/0. All in the "fine" band.
Before: `Arm.L` 0.950/24, `Arm.R` 0.970/33, `ForeArm.L` 0.940/17.

## 15.2 The breasts were hanging off the clavicle

`Yemoja_Body` had the lateral breast weighted 0.6–0.9 to `Shoulder.L/R` down
to y ≈ 540 in armature space, 60 units below the clavicle line (y = 598). Any
clavicle motion, and the idle has 20° on the left, swung the breast mass with
it while the top (weighted `Spine1`/`Spine2` only) stayed put, so the body
pushed through the garment.

Moved that weight to `Spine2` with a smoothstep on height: fully moved at
y ≤ 575, untouched at y ≥ 598, front and sides only (z > −8), back left alone.
Measured on the idle pose, motion of those 102 vertices *in the chest's own
frame*: mean 8.9 → 1.7 units. The clothes top needed no change: rendered with
a camera locked to `Spine2`, rest and pose are nearly identical. The peaked
shape at the neckline is the garment's design, not a deformation.

## 15.3 Two things that will bite the next person

- **Rendering re-evaluates the assigned action.** `bpy.ops.render.render`
  calls `frame_set`, which overwrites every `matrix_basis` you set by script
  with the keyed values. My first before/after renders were pixel-identical for
  this reason while the depsgraph measurements showed the change. Either
  `A.animation_data.action = None` while iterating, or key the pose first.
- **Blender 5.2: `Action.fcurves` no longer exists** (layered actions). The
  library's `set_interpolation()` will raise; curves live under
  `action.layers[..].strips[..].channelbag(slot)`. Not fixed in the lib yet.

## 15.4 Still open (carried from section 14, unchanged)

Right-hand grip fingers (`HandPinky3.R` 0.62, `HandRing3.R` 0.68,
`HandMiddle3.R` 0.80) remain the only regions under 0.95 on the idle. They are
Stephanie's grip; ask before touching. Bracelet fit, Unity shoulder muscle
clamps and the `stress_poses.json` re-run are also still open.

## 16. Fable's stream: attacks

Working file will be `Backups/Yemoja_WORKING_v1xx_attacks.blend`, started
from v114. Actions: `Yemoja_Atk_Punch`, `Yemoja_Atk_HardPunch`,
`Yemoja_Atk_Kick`, `Yemoja_Atk_HardKick`. Each ships with its impact frame
listed here for the `PerformAttack` event (section 7). Filled in as they land.


---
---

# ADDENDUM 2026-09-03 (evening), added by the idle agent (Fable, session 2)

The first idle agent was stopped. This session owns the idle stream now. The
attack agent released Blender at 16:20 UTC; it will append its four
`Yemoja_Atk_*` actions into the highest version when they are done and will ask
before taking Blender.

## 17. Model of record is now `Backups/Yemoja_WORKING_v115_idleWeights.blend`

Started from v114 as it was open in Blender (cleaned pose, hair textures
restored, tattoos visible). Everything below was measured offline on a copy
with the bpy module, then applied to the live file with the same scripts, which
produced identical numbers. Pull from v115; do not re-apply anything here.

### 17.1 What v114 still had wrong

An audit of the idle master pose (module `v115_fixes/yemoja_measure.py`,
report `audit/REPORT.md` in the session workspace, numbers reproduced in the
table below) found that the elbow and wrist ramps from section 12 are correct
and are not the problem. The remaining faults were:

1. Shoulder girdle weights were raw auto weights, never smoothed. `Arm.*` held
   0.15 or more out to 0.556 arm-lengths perpendicular to its axis, including
   scapula and trapezius verts half an arm-length behind the shoulder. The
   Shoulder-to-Arm handover was non-monotonic with its 0.5 crossing 0.13
   arm-lengths distal of the joint, and `Shoulder.*` (the clavicle) never
   crossed 0.5 anywhere. In the idle pose this stretched the left armpit band to
   1.270 (21 of 74 faces over 2x, max 2.774) and opened a visible tear between
   arm and chest wall.
2. The bracelets did not fit the forearm even in REST (clearance negative at one
   station) and spanned 0.874 to 1.102 of the ForeArm length, so 10 percent of
   the ring was over the hand while being rigid to ForeArm. The ring is also
   tilted 18 degrees to the bone axis, which matters for how it can be moved.
3. The left elbow carried 128 degrees of relative twist (true skinning rotation
   160 degrees) plus 66 degrees of flexion. A single LBS joint collapses its
   50/50 ring by cos(theta/2), so no ramp width fixes this.
4. Weights were exactly L/R symmetric, so every L/R difference in the numbers
   is pose-driven, not weight-driven.

### 17.2 What v115 changes

All three scripts live in `YemojaDesignArtifacts/v115_fixes/` and are
idempotent; `apply_all.run_in_place()` runs them in order on the open file.

| Item | Change |
|---|---|
| `Yemoja_Body` weights, shoulder girdle | `fix_shoulder_weights.py`: 990 verts touched. Arm confined to the arm by a smooth torso-ness field (radial and medial terms), the clavicle given real territory (Shoulder reaches about 0.7 over the lateral clavicle, acromion and the scapula, since the clavicle is the only girdle bone this rig has), Shoulder-to-Arm handover re-ramped as a monotonic smoothstep of half-width 0.25 centred on the acromion, then 10 Laplacian passes, then 4-influence cap and normalisation. Breast guard kept: Shoulder is 0.000 over the chest below y 575. Exact before-values are in the text datablock `fix_shoulder_backup.json`. |
| Action `Yemoja_Idle_MASTER` | Re-keyed at frame 1 with a new twist split on both arms (`retwist.py`). Hand world orientations and every joint position unchanged to 1e-6; only bone roll moved. L elbow 128 to 168 degrees, L wrist 30 to -10; R elbow 52 to 42, R wrist 18 to 28. |
| `Yemoja_Clothes` bracelets | `fix_bracelet_fit.py`: each bracelet moved as one rigid unit in its own ring frame: 0.06 ForeArm-lengths proximal along the ring axis, uniform scale 1.08 about the ring centre, 1.3 units of re-centring on the skin cross-section. Bead proportions unchanged. Still 100 percent ForeArm. Stamp `YEMOJA_BRACELET_FIX` on the object. |
| `pose_idle_master_2026-09-03_v115weights.json` | The re-keyed pose incl. the 17.4 elbow tuck, same schema. |
| Renders | `anim_review/idle_v115_weights_*` (standard set) and `anim_review/v115_diag_*` (close-ups). |

Body still sums to 1.0 with at most 4 influences (0 verts needed capping after
normalisation); symmetry check 0 pairs differ by more than 0.1.

### 17.3 Numbers, idle master pose, Yemoja_Body (area ratio / crushed / stretched over 2x / max)

| region | v114 | v115 |
|---|---|---|
| band_armpit.L | 1.270 / 4 / 21 / 2.77 | 0.965 / 3 / 0 / 1.75 |
| band_clavicle.L | 1.165 / 2 / 15 / 2.77 | 0.980 / 2 / 0 / 1.74 |
| band_shoulder.L | 1.032 / 8 / 19 / 2.77 | 0.932 / 2 / 0 / 1.76 |
| dom_Shoulder.L | 0.972 / 4 / 4 / 2.38 | 1.101 / 0 / 0 / 1.76 (region grew from 74 to 111 faces; on the frozen 74 it is 1.036) |
| dom_Spine2 | 1.085 / 2 / 23 / 2.77 | 1.014 / 1 / 0 / 1.48 |
| band_elbow.L | 0.753 / 9 / 0 / 1.22 | 0.774 / 6 / 0 / 1.29 |
| band_elbow.R | 0.865 / 7 / 0 / 1.43 | 0.881 / 6 / 0 / 1.46 |
| band_wrist.L | 1.036 / 0 | 1.015 / 0 |
| band_wrist.R | 0.916 / 0 | 0.891 / 0 (under the bracelet) |
| bracelet L, skin outside ring / beads inside skin (pose) | 8 (2.02 deep) / 17 (2.51) | 0 / 8 (0.75) |
| bracelet R | 7 (4.16) / 28 (4.04) | 1 (0.30) / 8 (0.97) |

Stress poses (jumping jack with clavicle 0 and 30 degrees, straight punch with
20 degrees protraction, A-pose): dom_Shoulder 0.820 to 1.034 (clav 0), 0.658 to
0.878 (punch); band_shoulder at clav 30 0.821 to 0.969, crushed 48 to 17. The
one region that gets worse is the clavicle band with the arm raised and the
clavicle NOT elevated (1.041 to 1.098), which is section 3's rule doing what it
says: the clavicle now owns skin, so key it.

### 17.4 The left elbow: pose tuck applied (approved by Stephanie's side, 2026-09-03)

Any split of the twist between ForeArm and Hand leaves the sum near 186 degrees
of Arm-to-Hand twist on the left; a human forearm does not have that much, so
weights cannot fix it. The elbow pole was swept (humeral rotation about the
shoulder-to-wrist line, hand position and orientation fixed) and the approved
result is applied as step 4 of `apply_all.py` (`apply_pole.py`, stamp
`YEMOJA_POLE_FIX` on the Armature): left elbow rotated -40 degrees about that
line, i.e. it moves 32 units (0.34 upper-arm lengths) toward the ribs and
forward; every other bone moves under 1e-6 and the hand orientation under 1e-8.
Split after the tuck: elbow -122.8, wrist -45.0 (relative twist). Left elbow
band 0.774 to 0.844; band_wrist.L 1.015 to 0.889 (0 crushed, under the
bracelet); dom_Arm.L 0.955 to 0.987; band_armpit.L 0.965 to 0.942. Silhouette
change 2.7 percent of body pixels at the front camera; renders
`anim_review/idle_v115_final_*` and `v115_final_*`. The right arm was left as
authored (the sweep gained under 0.01 there).

The idle master pose therefore differs from Stephanie's v113 authoring in the
left elbow position. `Yemoja_Idle_MASTER_v113_corkscrew` still holds the
original if it has to come back.

### 17.5 Unity facts that change how to read these numbers (measured in the editor, 2026-09-03)

The imported avatar (`Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx`,
Human, CreateFromThisModel) has `upperArmTwist = lowerArmTwist = 0.5` and
default muscle limits (`useDefaultValues` on every arm slot). Consequences:

- Unity redistributes forearm and hand twist itself along the chain, so the
  split you see in Blender is not what ships. Judge twist in Unity, not in a
  Blender render. A round-trip test of the idle pose is the next verification
  step and is still open.
- Default Shoulder limits are Down-Up -15 to +30 and Front-Back -15 to +15
  degrees. The idle master has 29.4 degrees of left clavicle protraction; Unity
  will clamp it to 15 unless the limits are raised on the avatar. The 30 degree
  elevation in section 3 sits exactly on the default limit. Raise the shoulder
  limits on the avatar before trusting either.

### 17.6 Still open

- Unity round trip of v115 (17.5).
- Wrist mesh resolution: only 4 vertex rings across the wrist ramp, weight jumps
  0.28 to 0.65 in one polygon. Hidden by the bracelet; add 2 or 3 loops only if
  it shows in motion.
- Clothes weights: 5260 of 5741 verts sum to about 0.54 (only the bracelets are
  normalised). Harmless in Blender and, at 4 influences or fewer, identical
  after Unity's own normalisation; normalise before export anyway.
- Right-hand grip fingers, bracelet fit to the millimetre, `stress_poses.json`
  re-run and the `set_interpolation` fix for Blender 5 are unchanged from 15.4.

## 19. Base pose re-authored from rest: candidate A "Offering" (2026-09-03, in v115)

Stephanie's side judged the authored stance's arms, shoulders and armpits
unnatural (left arm pressed into the body, hand placements off) and asked for
the base to be redone from rest. Legs, hips, spine and the right-hand grip
fingers were kept; Shoulder, Arm, ForeArm and Hand on both sides plus the head
turn were rebuilt by construction with `v115_fixes/author_idle_pose.py`
(candidates A, B, C rendered to `anim_review/pose_v2_*`; A chosen).
`v115_fixes/promote_pose.py` promotes a candidate: the previous master is kept
as `Yemoja_Idle_MASTER_v115_elbowTuck`, the new pose is keyed as
`Yemoja_Idle_MASTER`, and `Yemoja_Idle_Loop` is rebuilt on it. Stamp
`YEMOJA_POSE_V2 = "A"` on the Armature. Pose JSON:
`pose_idle_master_2026-09-03_v115_poseA.json`.

How the arms are built (reuse this for any new stance):

- Targets are defined relative to the shoulder joint in upper-arm lengths
  (U = 94.76, F = 78.65 armature units), solved with the twist-free 2-bone IK
  and the hand oriented by construction from a palm normal and a finger
  direction. Measured hand axes in REST, left hand-local: finger axis
  (-0.016, 0.989, 0.150), palm normal (-0.069, -0.473, 0.878).
- Twist budget per joint, enforced with set_split and a pole sweep:
  glenohumeral <= 85 deg, elbow <= 90, wrist <= 40. A: L 80 / 90 / 18,
  R 45 / 61 / 0. Palm-up on this rig costs about 215 deg of roll, so the left
  palm is tipped 39 deg about the finger axis instead; invisible from the side
  camera.
- Clavicle rule applied (elevation about a third of arm elevation, 8 to 12 deg
  protraction on the forward arm) plus a small clavicle axial roll to keep the
  glenohumeral twist inside budget.
- Trident: Hand.R placed so the butt sits on the ground (z -0.013, shaft 6.8 deg
  off vertical) with the hand at hip height. The Unity grip offset holds the
  hand axis 76.5 deg off the shaft, so the right wrist bends about 60 deg
  whatever the pole does; band_wrist.R 0.89 to 0.78 is the cost of a planted
  trident and is under the bracelet.
- Left forearm/hand to torso clearance 36 units (was 11).

Deformation, idle master A vs the tucked pose it replaced: band_armpit.L 0.942
to 1.024, band_shoulder.L 0.995 to 0.912, band_elbow.L 0.844 to 0.857,
band_wrist.L 0.889 to 0.950, dom_Arm.L 0.987 to 0.957 (22 crushed), band_wrist.R
0.891 to 0.777. Loop checks after rebuild: seam 2e-6, feet drift 0.07 / 0.05
units, floor 0.0014, Hand.R drift 0.07 units, 57 humanoid bones keyed. Previews
`anim_review/idle_loop_v115_poseA_side.mp4` (in-game view) and `_q34.mp4`.

`yemoja_measure.py` now has `scene_snapshot()` / `scene_restore()`; every render
helper restores the scene (engine, camera, visibility, modifiers, viewport
shading) when it is the outermost call. Wrap any new render code in it.

## 20. Right arm by hand, trident offset moved, left arm A2 (2026-09-03, late)

Stephanie's side reviewed pose A from the side camera and flagged two faults on
the left arm (a sunken facet at the back of the upper arm behind the shoulder,
and a knife-edge elbow crease) and rejected the right hand's 60 degree wrist
bend, which existed only to plant the trident. Their instruction: never bend the
hand to reach the ground, move the trident's offset instead.

### Right arm and trident
- The right arm (Shoulder.R, Arm.R, ForeArm.R, Hand.R) is now hand-posed by
  Stephanie's side in Blender and keyed into `Yemoja_Idle_MASTER` as authored.
  Do not re-solve it. Capture: `pose_live_capture_user_handR.json`.
- With that hand the shaft leans 12.7 degrees from vertical (tip forward) and
  the butt floated 0.885 Blender units above the floor. Fix: slide the trident
  0.907 units down its own shaft. The grip point moved from 55.6 to 66.9 percent
  up the shaft; fingers unchanged since the slide is along the grip axis.
- Unity `Yemoja_Trident` localPosition under `mixamorig:Hand.R` is now
  `(0.93499, -0.10169, -0.03749)` on both `Yemoja.prefab` and
  `YemojaDisplay.prefab` (rotation and scale untouched, `A_Trident` follows).
  `yemoja_anim_lib.TRIDENT_U["origin"]` matches; previous lib saved as
  `yemoja_anim_lib.py.bak_2026-09-03_trident`. Butt z = 0.000 in Blender.
- The 12.7 degree lean is kept on purpose: a planted staff that leans reads as
  leaning on it. If it must be vertical, rotate the hand, not the trident.

### Left arm A2
The clavicle-to-hand roll a palm-up cupped hand needs on this rig is 187 to 193
degrees and does not change with wrist target, pole or flexion; only the hand's
own orientation moves it. So the budget is met by (1) tipping the palm toward
the body until palm normal dot up = 0.64 (still reads as palm up from the side
camera), and (2) booking the rest as clavicle axial roll, which moves no joint
centre and lands on the clavicle band (1.028 to 1.042). Result, left arm:
glenohumeral 78.7 to 38.0, elbow 98.1 to 60.0, wrist 16.2 to 33.6, elbow flexion
117 to 108 degrees, elbow 0.38 U out from the shoulder, forearm-to-torso
clearance 39.5 units. band_shoulder.L 0.935 to 0.971, dom_Arm.L 0.961 to 0.977.
The back-of-shoulder facet is gone in the renders
(`anim_review/pose_v2_compare_clientview.png`).

The elbow crease remains (band_elbow.L 0.854 to 0.842). The ramp is textbook
(Arm.L to ForeArm.L monotonic, w50 at -0.026, ring std 0.038) and the pose is
inside budget; the band holds 48 vertices, so one ring folds 108 degrees. This
is mesh density at the elbow, fixable only by adding 2 to 3 edge loops around
both elbows (vertex count changes, bone count does not, so no Unity avatar
reset; Locs/Tattoos/Fuzz shrinkwraps are unaffected). Decision pending.

`author_idle_pose.PARAMS["A2"]` reproduces the left arm with `skip_right=True`;
`promote_pose.promote("A2")` keeps the previous master as
`Yemoja_Idle_MASTER_before_A2`, keys the new one and rebuilds the loop. Loop
pins after rebuild: seam 3e-6, feet drift 0.05 / 0.02 units, Hand.R 0.11 units.
Pose JSON `pose_idle_master_2026-09-03_v115_A2.json`.

## 21. Elbow loop cuts and the groin flap (2026-09-04)

### Elbow: what the mesh actually had
Section 20 blamed the elbow crease on ring density. Measured properly (closed quad
edge-rings walked along the arm axis, t = 0 at the elbow), each elbow already had
four rings within 0.16 units of the joint (16-vert rings at t = -0.074, -0.011,
0.037, 0.093) and the Arm/ForeArm blend lives entirely inside that dense zone
(-0.10..+0.09). The coarse parts are the 0.10-long strips either side
(-0.174..-0.074 and 0.102..0.201, 12-vert rings).

Five variants were measured on the posed loop, frame 1, with face-area ratios and
dihedral angles over faces within 0.35 of the elbow
(`pose_v2/elbow_experiment.json`, `anim_review/elbow_variants.png`):

| variant | L dihedral p95 | L max | L area | R p95 |
|---|---|---|---|---|
| base | 58.3 | 136.4 | 0.945 | 35.9 |
| cuts in the two coarse strips | 52.4 | 136.4 | 0.973 | 35.4 |
| cuts + blend widened to +-0.15 | 51.8 | 143.1 | 0.980 | 35.4 |
| four cuts + blend widened to +-0.20 | 50.8 | 158.0 | 0.924 | 34.9 |
| blend +-0.15, no cuts | 54.8 | 143.1 | 0.943 | 36.0 |

Cuts help the general fold and cost nothing. Widening the blend makes the inner
crease sharper every step: with linear skinning, verts at 50/50 are pulled to the
bisector of a 108 degree bend, so the more verts you put in the blend the more
volume collapses on the inside. Unity skins linearly, so this is what the game
sees. Do not widen elbow ramps to chase this crease.

Applied: `elbow_and_flap.apply()` loop-cuts the two coarse rings on both arms
(+48 verts, 7417 to 7465; +48 faces; n-gon count unchanged at 70). Weights and
UVs are interpolated by the cut; Arm/ForeArm ramps re-measured monotonic, w50 at
-0.026, ring std 0.030, every vertex still sums to 1. Bone count unchanged, so no
Unity avatar reset, but the FBX has to be re-exported for the new vertices to
reach the game. Shrinkwrapped Locs/Tattoos/Fuzz follow the surface, unaffected.

What is left of the crease is the flexion angle. The remaining lever is the pose:
bringing left elbow flexion from 108 toward 95 degrees moves the cupped hand
slightly outward/down and would take most of the inner fold with it. Not done;
Timi's call.

### Groin flap (the leaf loincloth)
The leaf is the `CL_GroinFlap` tag group of `Yemoja_Clothes` (92 verts). It was
carrying 42 percent of its deform weight on UpLeg.L/R (52 and 46 verts), which is
why it stretched between the thighs with the shorts. `pin_flap()` sets every leaf
vertex to Hips = 1.0 and removes the thigh groups; it now hangs rigid from the
pelvis (`anim_review/flap_after_loop.png`, frames 1/25/50/75). Rule: the flap
never takes leg weights. If it should swing, that is a one- or two-bone chain
hung from Hips with a small idle sway, not thigh weights; adding bones changes
the Unity avatar, so batch that with the next export.

Stamp `Armature["YEMOJA_MESH_V2"] = "elbow_cuts2+flap_hips"`; the previous mesh
is in `Yemoja_WORKING_v115_idleWeights_preMeshV2.blend` if it needs to come back.

## 22. The elbow is a hinge (2026-09-04)

The "bends like flat paper" elbow was not mesh density and not weights. It was
the solver bending the elbow off its hinge.

### The metric
`yemoja_measure.off_hinge(side)`: signed angle, about the Arm bone's axis,
between the forearm's bend direction in the pose and the forearm's bend
direction in the REST pose (the Mixamo rest has a 12.3 degree elbow bend
pointing forward, which is the anatomical flexion plane), after mapping the
posed bend direction back through Arm's pose rotation. A hinge elbow reads 0 at
any flexion. `HINGE_TOL` = 5 degrees. It is part of `pose_twist_table` as
`elbow_hinge` and `build_idle_loop.verify()` checks it at every frame.

### What was measured
| pose | L off-hinge | R off-hinge |
|---|---|---|
| every solver pose (A, A2, elbowTuck) | -60.7 | 61.6 in the loop |
| corkscrew | -98.4 | 12.0 |
| Timi's hand-posed right arm, master | | 15.7 |
| same arm after the old loop rebuild | | 61.6 |

Two bugs. `apply_pole._limb_ik` aimed the upper bone's local X at the
bend-plane normal, a convention that has nothing to do with where the bind
pose's hinge sits; on this rig the two differ by 60.7 degrees, so the forearm
folded toward the side of the upper arm in every solver pose. And
`retwist.set_split` exchanged rotation about the upper-arm axis across the
elbow, compensating in ForeArm local, so the hand stayed put while the
upper-arm skin rotated 46 degrees and the elbow bent sideways; that is how the
loop corrupted a right arm that was correct in the master. A third bug found on
the way: `author()` built on whatever action was assigned (the loop), so it
inherited the loop's corrupted right arm; it now binds the master first.

### Fixes
`_limb_ik` now constructs the upper bone's frame so the rest bend direction maps
onto the posed bend direction, then places the forearm by rotating the rest
forearm frame about the hinge axis (flexion) plus an explicit pronation about
the forearm's own axis. `set_split` asserts off-hinge is unchanged, so twist
can move Shoulder<->Arm and ForeArm<->Hand but never across the elbow. Rule:
any future limb solver on this rig must report off-hinge and keep it under 5.

### A3 (live)
| | A2 | A3 |
|---|---|---|
| off-hinge L | -60.7 | 0.0 |
| flexion | 105.7 | 99.9 |
| glenohumeral twist | 74.9 | 35.0 |
| elbow twist | 60.0 | 60.0 |
| wrist twist | 33.6 | 91.7 |
| band_shoulder.L | 0.973 / 3 crushed | 0.994 / 0 |
| band_armpit.L | 0.939 / 9 | 1.010 / 2 |
| band_elbow.L | 0.868 / 10 | 0.891 / 13 |
| band_wrist.L | 0.975 / 0 | 0.843 / 0 |
| elbow dihedral p90 | 44.7 | 37.3 |

The hinge removes the upper arm's roll as a free variable, which is what the old
solver was quietly using to hide demand. With the elbow inside 0.35..0.5 U of
the ribs and the palm reading up, elbow plus wrist need 152 degrees against a
95 budget. Elbow 60 / wrist 92 was chosen because the wrist takes its load as
uniform mild compression (0 crushed faces, max 1.21) where the same load at the
elbow crushes 17 faces, and the bracelet sits over the wrist blend. If the wrist
is judged too twisted, the lever is the palm: letting it turn further toward
the body (palm dot up 0.45 instead of 0.60) takes the wrist down, not the
elbow. Every other left-arm region improved; the elbow now reads as a rounded
joint with the crease along the inside (`anim_review/compare_elbow_A3.png`).

Loop pins after promote: seam 4.6e-6, feet drift 0.05 / 0.02, Hand.R 0.11 /
0.0 deg, off-hinge L 0.00 and R 15.65 at frames 1/25/50/75. Previous master
kept as `Yemoja_Idle_MASTER_before_A3`; pose JSON
`pose_idle_master_2026-09-04_v115_A3.json`.

Section 21's loop cuts stay (they help the fold); its "remaining lever is
flexion" conclusion was wrong: the lever was the hinge.

## 23. Elbow anatomy (2026-09-04)

Client screenshot of A3 from Blender (her left side) with a photo of a real
bent arm: "the cubital fossa especially". Measured on the rest mesh, the arm was
a constant-radius cylinder (0.152 to 0.166) from 0.55 above the elbow to 0.50
below it. A folded cylinder has no biceps, no forearm mass, no olecranon and
therefore no hollow for a fossa to be. The hinge fix (section 22) was
necessary; this is the rest.

### Pass 1, `elbow_anatomy.py` (stamp YEMOJA_MESH_V3)
Radial displacement fields in the rest-space arm-axis frame (t along the axis,
0 at the joint, lobes from the radial dot with the extensor direction read from
the bind skeleton, so the sculpt is pose-independent and L/R mirror by
construction): biceps +12 percent radius on the flexor side ending 0.10 U above
the joint; fossa -8.5 percent across the joint; brachioradialis +19 percent
peaking 0.10 U below the joint and gone by mid-forearm; olecranon +0.026 on the
extensor side. Skinning on the flexor side only: sharper handover (half 0.06)
with the w50 line shifted 0.035 proximally so the forearm mass slides under the
biceps and the fold reads as a hollow, not a line. Extensor ramp untouched.

### Pass 2, `elbow_anatomy_v4.py` (stamp YEMOJA_MESH_V4, requires V3)
Loop-cuts the three 16-vert joint strips and the two flare strips on both arms
(+144 verts, 7465 to 7609; +144 faces; n-gons unchanged at 70), restates the
flexor ramp on the new vertices, then four narrow terms designed against the V3
surface: olecranon tip to +0.035 total, fossa deepened, a lip where the
brachioradialis starts, flare peak moved up to the fold. Rest flexor profile now
0.183 (biceps) to 0.144 (hollow) to 0.177 (lip) to 0.202 (forearm mass).

| | verts | band_elbow.L | dihedral p90 | p95 |
|---|---|---|---|---|
| A3, no sculpt | 7465 | 0.891 / 13 | 37.4 | 57.1 |
| V3 | 7465 | 1.100 / 10 | 48.1 | 70.4 |
| V4 | 7609 | 1.119 / 15 | 39.7 | 50.8 |

V4 is smoother than V3 while carrying sharper anatomy; that is what the rings
buy. Renders: `anim_review/compare_elbow_v4.png` (A3 / V3 / V4 at the client's
view, fossa, from behind, rest, right elbow). Rules: the sculpt terms are
relative to the current radius, so never re-run a pass (the stamps enforce
this); apply order is A3 -> V3 -> V4. Shrinkwrapped Tattoos/Locs/Fuzz follow
the surface (arm tattoo max 0.009 off surface).

Remaining honesty: at the client's framing the elbow now reads as a joint with a
hollow and a corner, but the photo's crispness is beyond ~20 verts per ring. If
more is wanted, it is a real sculpt pass by hand on the rest mesh, not more
scripts.

---
---

# ADDENDUM 2026-09-04, added by Fable (attack stream) — HANDOFF, work paused

Stephanie paused the attack stream to batch the remaining work. This section
is the resume point. It supersedes section 16. Anyone (any agent, any account)
can pick it up; everything needed is on disk, nothing lives only in a chat.

## 24. State of the attack clips

Four clips exist, blocked and keyed, on the current model of record
(`Backups/Yemoja_WORKING_v115_idleWeights.blend`, idle master A3):

| Action | Frames | Impact (`PerformAttack`) | What it is |
|---|---|---|---|
| `Yemoja_Atk_Punch` | 1–14 | **7** | left jab (lead hand) |
| `Yemoja_Atk_HardPunch` | 1–26 | **12** | right-arm trident thrust, tines forward |
| `Yemoja_Atk_Kick` | 1–16 | **7** | left front snap kick, support foot settles flat, trident stays planted |
| `Yemoja_Atk_HardKick` | 1–28 | **12** | right roundhouse to the head, trident swept up behind the body |

`StopAttacking` goes on the last frame of each. All start and end on
`Yemoja_Idle_MASTER` frame 1 exactly (0.000°), so they blend with the idle
loop's seam pose.

**Where they live:** `Backups/Yemoja_ATTACKS_v4.blend`. It is the v115 model
of record plus the four actions (fake users, `use_frame_range` set); every
pre-existing datablock in it is byte-identical to v115 (verified). The
actions have NOT yet been appended into the working file, on purpose: they
are not finished (see 24.2). To preview them, open `Yemoja_ATTACKS_v4.blend`,
or append the four actions into any v1xx file (File > Append > Action).

**Tooling:** `YemojaDesignArtifacts/attacks/`:
- `attacks_build.py` — idempotent builder; env vars `YEMOJA_BLEND_IN`,
  `YEMOJA_LIB`, `YEMOJA_BLEND_OUT`, `YEMOJA_REVIEW_DIR`. Runs headless with the
  `bpy` pip module (5.0.1 was used; the file is 5.2 and round-trips) or inside
  Blender. It re-poses every key from the idle master, so re-running it on a
  newer model of record rebuilds the clips on that base.
- `harness.py` — pin_foot / pivot_support_foot / enforce_pins /
  fix_quaternion_hemispheres / set_interpolation_5x / audit / report. Uses the
  idle agent's hinge-correct arm solver `v115_fixes/apply_pole._limb_ik` and
  `yemoja_measure.off_hinge` (README 22). `yemoja_anim_lib.limb_ik` is NOT
  hinge-correct; do not use it for arms.
- `verify/` — the independent per-frame acceptance scripts (written by the
  verifier, not the builder): `eval_all.py` (floor, support foot, twist,
  steps, every frame), `pen.py` (signed BVH ray-parity trident and clothes
  penetration), `arc.py` (hemisphere flips, ankle paths), `keys.py`, `cmp.py`
  (datablock-identity vs source), `lean.py` (head angle), `deep.py`.
- `SPEC_attacks.md`, `SPEC_hardkick_v2.md`, `SPEC_fix_v3.md`,
  `SPEC_rebuild_v4.md` — the design, in order. `BUILD_NOTES.md` — what the
  builder did and every deviation. `VERIFY_attacks.md`, `VERIFY_attacks_v4.md`
  — the two adversarial verification reports.
- `review_v4/` — side-camera key-frame sheets and impact silhouettes.

### 24.1 Verified good (VERIFY_attacks_v4.md, every frame)
0 quaternion hemisphere flips; idle bookends exact; support foot pinned
(≤ 0.004); floor inside [−0.005, +0.02] on every frame; the front kick no
longer floats and its trident butt stays within 0.014 of the floor; elbow
`off_hinge` < 5° on every solved arm; export ranges set; Armature handed back
on `Yemoja_Idle_MASTER` frame 1; all pre-existing actions and both meshes
identical to v115. `Yemoja_Atk_Punch` verdict: SHIP.

### 24.2 QUEUED — the next round, fully specified in `attacks/SPEC_fix_v5.md`
Verifier verdicts: Punch SHIP, Kick FIX FIRST, HardPunch FIX FIRST, HardKick
REJECT. All key poses are clean; every remaining fault is on interpolated
frames, and the builder twice claimed per-frame results it had only checked at
keys. So item 0 of the next round is a `final_gate()` inside
`attacks_build.py` that reruns the `verify/` checks on every frame of the
saved file and fails the build; the acceptance section of BUILD_NOTES becomes
that gate's verbatim output. Then, in order:

1. Trident through the body between keys: HardKick f5, 8–11, 17–21, 23
   (through the standing thigh, hips, spine); HardPunch f8, f10 (through the
   upper arm). Fix: `enforce_trident_clear` — slerp the shaft direction
   between bracketing keys, re-orient Hand.R, key all bones at the offending
   frame, iterate.
2. Full keying and unit quaternions on every inserted key (some breakdowns
   carry only Hand.L/Foot.R/Hand.R; 163–529 non-unit keys per clip, worst
   |q| 0.735).
3. HardKick apex: the ankle peaks at f10 and has retracted 0.43 by f12; add
   breakdowns at f9/f11 on the chamber→impact arc so f12 is the apex. Same
   rule for the front kick.
4. Clavicles: elevation ≤ 30°, protraction ≤ 20° everywhere (HardKick f12 is
   at 40/46). Unity's default avatar limits are 30 up / 15 front-back
   (README 17.5): raising the avatar's shoulder limits to 30/20 is a Unity
   task that has to happen before these clips are judged in the editor.
5. HardKick f6/f22 Arm.R and Shoulder.R at 0.87 (44 crushed): sweep the
   trident-hold pole/hint.
6. HardPunch head 12.5°/13.7° at f12/f15 (budget ±10°); HardKick toe-vs-shin
   16.7°/20.2° (≤ 15°); per-frame steps > 0.9 (HardPunch wrist f10, HardKick
   ankle f20); Kick support foot settles 0.143 in one frame (spread over
   f2–f4 and f13–f16).
7. Save with `preview_mode(False)` (v4 was saved with the Scalp shrinkwrap off
   and Tattoos hidden; harmless for the actions, wrong as a viewing state).
8. Clothes-inside worst frame is 507 at HardKick f9 (flag threshold 350);
   report per-frame worst, no fix expected before the clothes reweight.

After the gate passes: append the four actions into the highest
`Yemoja_WORKING_v1xx` file, save as the next version, then follow section 10
for export. Unity: add the `PerformAttack` events at the impact frames above,
`StopAttacking` at the last frame, `animationType = Human`,
`avatarSetup = CreateFromThisModel`, and raise the shoulder muscle limits.

### 24.3 Decisions already taken (do not reopen)
- Punches: regular = left jab, hard = trident thrust. Kicks: front snap +
  roundhouse. Impact frames as in the table.
- The game camera is side-on (2.5D); `_AR_side` is the silhouette that must
  read. `_AR_front` is informational.
- Clavicle elevation only when the arm is above shoulder height; never used
  to chase an area-ratio number.
- Twist budget: |Hand| < 30°, |Foot| < 20°, forearm carries pronation.
- Arms are solved with the hinge-correct solver; legs may use
  `yemoja_anim_lib.limb_ik`.

### 24.4 Process note for whoever runs the next round
Have one agent build and a different agent verify, and make the builder use
the verifier's scripts. Both times the builder's own audit passed at the
keys while the in-betweens failed; the verifier caught it both times.

### 24.5 Other loose ends from this stream
- Section 15's breast-weight transfer and arm-chain re-solve are superseded
  by the idle agent's v115 girdle weights and A3 pose; keep 15.1's twist
  finding (forearm carries pronation better than the wrist on a clean chain).
- The trident preview prop renders magenta in the viewport (no material);
  it is the `ANIM_PREVIEW_PROPS` prop and never exports. Cosmetic.
- The three hair source textures the 2026-09-02 cleanup moved were copied
  back to `Assets/CharacterModels/Yemoja/textures` (they are Blender sources
  for the hair bake, rule 220). `Clothes_BaseColor_Ocean2` and
  `WIN_20251119_14_38_18_Pro.jpg` are still missing and unused.


---

## 25. Cloth & decal fit pass — v119/v120 (Opus, 2026-09-04)

Stephanie signed off on the idle motion ("the idle animation itself is perfect") but
reported three artifacts during playback: tearing in the top at her LEFT side, tearing
under the shorts between the thighs, and the arm tattoos clipping.

### 25.1 Root cause (all three)
Every one was a **weight mismatch between a surface layer and the skin under it**, not
an animation problem. Measured averages before the fix:

| layer            | its own weights            | skin underneath            |
|------------------|----------------------------|----------------------------|
| CL_Shorts        | UpLeg.L 0.46, UpLeg.R ~0   | UpLeg.L 0.57 / UpLeg.R 0.36|
| CL_Top           | Arm.L 0.21, Spine2 0.20    | Spine2 0.60 / Arm.L 0.16   |
| Yemoja_Tattoos   | Shrinkwrap `Conform` evaluated BEFORE the Armature, targeting the *posed* body |

If a garment and the skin under it are driven by different bones, they separate the
moment the pose leaves bind. That reads as tearing. **The rule: a garment's job is to
follow the skin it covers, so it should carry the skin's weights, not its own guess.**

### 25.2 Fix — proximity skin-weight transfer
For each garment vertex, take the 4 nearest **body** vertices in REST pose,
inverse-distance weight their bone weights, keep the top 4 influences, normalise to 1.0.
Applied to `CL_Top` + `CL_Shorts` (689 verts) and `Yemoja_Tattoos` (1118 verts).
Deliberately NOT applied to the rigid/pinned pieces (bracelets, anklets, bead cords,
necklace) — those are solids and must stay rigidly bound (see section 18).

Tattoos additionally needed the `Conform` shrinkwrap **baked in REST pose** first
(export-sequence step 1), because a shrinkwrap ahead of the armature chases the posed
body and drags the decals off the skin: posed offset was 0.26, rest 0.021.

### 25.3 Results (threshold: penetration deeper than 0.0015 units ≈ 0.33 mm)
Sampled every 10 frames across the 121-frame loop:

* `CL_Top` — **0 penetrating verts on every frame** (was 7–16 verts up to 0.049)
* `CL_Shorts` — **0 penetrating verts on every frame**
* `Yemoja_Tattoos` — mean offset from skin 0.0098 posed vs 0.0099 at rest; max 0.021
  posed vs 0.021 rest. The decals now ride the skin *exactly* as they do at rest.
* Residual: `CL_HipBand` 1 vert @ 0.0018, `Bracelet_R` 25 @ 0.024, anklets 8–10 @ 0.0083,
  necklace 2 @ 0.0025. **All of these are present at REST at the same depths** — they are
  static modelling fit, not animation tearing. Left alone.

Scale reference: the character is 7.398 blender units tall ≈ 1.65 m, so **1 unit ≈ 22 cm**
and 0.01 units ≈ 2 mm. Use this before calling a number large or small.

### 25.4 The groin flap — section 21's rule REVISED
Section 21 states: *"Rule: the flap never takes leg weights."* Fable's reason was that at
42 % thigh weight the flap stretched between the thighs. **That reason is correct and
was re-measured, but the rule as written also causes a defect**, so it is now qualified
rather than absolute.

Measured both extremes on the current idle (243 flap edges, ratio vs REST edge length):

| flap binding                | p95 stretch | edges >1.10 | verts inside body | worst |
|-----------------------------|-------------|-------------|-------------------|-------|
| Hips = 1.0 (section 21 rule)| 1.000       | 0           | 1                 | 0.043 |
| full skin match             | 1.55–1.71   | 35–47       | 0                 | 0     |

Neither is acceptable alone: rigid pinning is stretch-free but lets the (now
skin-matched) shorts slide out from under it and poke through; full skin match kills the
penetration but stretches the flap far past what the rest of the outfit does.

**Acceptance bar** — the garments Stephanie already approved:
`CL_Shorts` p95 = 1.28 (18.2 % of edges > 1.10), `CL_Top` p95 = 1.26 (12.7 %).
Anything at or under that reads as normal skinned cloth.

**Adopted: a graded blend.** Skin-matched weights scaled by a factor `t` that
smoothsteps from 1.0 at the top seam (world Z 4.51) down to `TMIN` at Z 4.10 and below;
the remaining `1 - t` goes to `Hips`. So the seam moves with the shorts (no poke-through)
and the hanging tongue stays near-rigid (no stretch).

Parameters in use: `ZTOP = 4.51, ZLO = 4.10, TMIN = 0.40`. Result:
p95 stretch **1.21–1.25**, 24–26 of 243 edges over 1.10 (~10 %, i.e. *below* the shorts'
18 %), penetration **0 verts on 10 of 13 sampled frames and 1 vert at worst 0.0033
(0.7 mm) on the other three**.

Tuning curve if this ever needs revisiting (ZLO 4.10 fixed):

| TMIN | p95 stretch | worst penetration |
|------|-------------|-------------------|
| 0.00 | 1.02–1.04   | 0.043             |
| 0.25 | 1.12–1.15   | 0.017             |
| **0.40** | **1.21–1.25** | **0.002**     |
| 0.60 | 1.32–1.39   | 0                 |
| 1.00 | 1.55–1.71   | 0                 |

Revised rule: **the flap takes leg weights only where it meets the waistband, tapering
to a rigid Hips bind by the time it hangs free.** Never a uniform leg weight across the
whole piece — that is the failure section 21 describes.

### 25.5 Files
* `Backups/Yemoja_WORKING_v119_clothFix.blend` — top/shorts/tattoo fix
* `Backups/Yemoja_WORKING_v120_flapGrade.blend` — **current**, adds the graded flap
* `YemojaDesignArtifacts/weights_clothes_backup_2026-09-04.json` — pre-fix cloth weights
* `YemojaDesignArtifacts/weights_tattoos_backup_2026-09-04.json` — pre-fix tattoo weights
* `YemojaDesignArtifacts/weights_flap_backup_2026-09-04.json` — flap as Fable left it
* `YemojaDesignArtifacts/weights_flap_FINAL_graded_2026-09-04.json` — the adopted blend
* `YemojaDesignArtifacts/anim_review/v119_*.png`, `v120_*.png` — ortho confirmation renders

### 25.6 Method note — framing review renders
Perspective close-ups kept missing the subject. The reliable recipe is an **orthographic**
camera: compute the region's centroid from the evaluated mesh, set
`ortho_scale = 2 x half-height`, place the camera 12 units back along the view direction,
and build `matrix_world` from the basis directly. No lens, no focal-length guessing.
World axes on this rig: **+X = her LEFT, +Z = UP, -Y = the direction she faces.**


---

## 26. Thumb correction + scalp double-transform — v121 (Opus, 2026-09-04)

### 26.1 Baking a hand-made pose correction into an existing clip
Stephanie corrected the left thumb by hand in the viewport (2 joints). A live viewport
edit is NOT in the action — the next `frame_set` evaluates the action and throws it away.
Capture it before anything re-evaluates.

Her values at frame 44:
```
HandThumb2.L  (0.98876, -0.00129, -0.04518, -0.14252)   was (0.99357, 0.11318, 0, 0)
HandThumb3.L  (0.93930, -0.10404,  0.24637, -0.21493)   was (0.91798, 0.35853, 0.06248, -0.15768)
HandThumb1.L  unchanged
```

**Choose the bake method by whether the channel is animated:**

* `HandThumb3.L` was **constant** across all 31 keys -> write her quaternion to every key.
* `HandThumb2.L` was **animated** (w spread 0.0093, x spread 0.0644) -> writing her value
  to every key would flatten the wobble. Instead compute a constant delta in the bone's
  local space and left-multiply every key by it:

```
q_delta = q_her  @  q_action_at_edited_frame.inverted()      # 21.617 deg here
q_key'  = q_delta @ q_key                                    # for all 31 keys
```

This reproduces her pose exactly at the edited frame and carries the animated variation
along unchanged (axis spread 0.0644 -> 0.0602; it shifts slightly because the delta
reorients the wobble axis, which is correct).

Then **de-flip the hemisphere** — after any per-key quaternion maths, walk the keys and
negate any that dot-negative against their predecessor, or the curve takes the long way
round between two keys and the thumb visibly spins.

Verified: exact match at frame 44 (delta 0), loop seam frame 1 vs 121 = 0.
Backup of the original curves: `YemojaDesignArtifacts/anim_thumbL_backup_2026-09-04.json`.

### 26.2 The scalp was double-transformed (same class of bug as the tattoos, section 25)
`Yemoja_Scalp` modifier stack was:
`Shrinkwrap -> Locs_Generator (nodes) -> Hair_Weights (nodes) -> Hair_Material_Split -> Armature`

The Shrinkwrap targets `Yemoja_Body`, and the depsgraph hands it the **already-posed**
body. The Armature modifier at the end of the stack then poses the scalp a second time.
The cap is therefore displaced by (pose applied twice - pose applied once) and sinks into
the skull, dragging the loc roots with it.

Measured, 143 cap verts, signed offset from the head surface:

| state | mean | worst | verts >1 mm inside |
|-------|------|-------|--------------------|
| REST  | -0.0002 | -0.0045 | 0 |
| posed, before fix | -0.0013 to -0.0122 | **-0.1462 (3.2 cm)** | 67-79 |
| posed, after fix  | -0.0002 | -0.0045 | **0** |

Max deviation from rest across the whole loop is now **0.0002 units (0.04 mm)**.

**Fix:** set `pose_position = 'REST'`, `modifier_apply` the Shrinkwrap (baking it into the
143 base verts), restore the pose. The Shrinkwrap is gone from the stack for good.
Base-coordinate backup: `YemojaDesignArtifacts/scalp_basecoords_backup_2026-09-04.json`.

**General rule — worth internalising: a deform modifier that reads another object's
evaluated geometry must never sit in front of the Armature modifier.** It will chase the
posed target and the Armature will then re-apply the pose. Bake it in rest pose instead.
This has now bitten twice on this character (tattoos in section 25, scalp here). Check any
new mesh for Shrinkwrap / Surface Deform / Mesh Deform ahead of the Armature before
trusting it in motion.

### 26.3 The locs' residual head intersection is NOT an animation defect
Full evaluated scalp (5735 verts including generated locs) against the body:
REST 301 verts inside (worst 0.241) vs posed 295-318 (worst 0.198). Essentially unchanged
by the pose, therefore it is generator/modelling overlap in the bind pose, not skinning.
Loc tubes lying against the skull and shoulders will always register some intersection;
it is hidden as long as the outer silhouette stays clean, which it does (see
`anim_review/v121_headback_f31.png`).

### 26.4 Hair rig — what actually exists
* 10 chains, each `mixamorig:Head -> hair_grpNN_0 -> hair_grpNN_1`. **2 joints deep.**
* Bone length ~75-104 armature units = ~0.75-1.04 world units = ~17-23 cm each,
  so ~40 cm of driven loc per chain.
* The generated locs **are** weighted to them (avg 0.04-0.08 per bone over 5735 verts) by
  the `Hair_Weights` geometry-node modifier. The rig is already wired for simulation.
* **0 of the 20 hair bones carry any animation** in `Yemoja_Idle_Loop`. The locs currently
  move only because they are children of `Head`.

### 26.5 Decision: hair moves by Unity physics, not baked animation
Recorded so no future agent re-litigates it.

1. **Humanoid retargeting drops it anyway.** Unity's Humanoid avatar carries only the
   humanoid muscle set. Curves authored on `hair_grp*` are silently discarded on import
   (README section 7). Baking hair into clips would require a Generic rig or a parallel
   Generic clip on a second animator layer.
2. **It is a fighter.** Hair must react to hits, knockback and hitstop. Baked curves
   cannot; a simulation can.
3. **Author once, works forever.** Every future clip — including Fable's unfinished
   attacks — inherits the motion with no extra work.
4. **The cost is trivial.** 20 bones per character, 2-joint chains. Short chains are also
   the stable ones; long chains are what go floaty.

Note for the record: hair motion is **not** a material or shader property. Materials
control appearance only. Motion comes from a bone-chain solver (spring bones) or a cloth
solver, both driven by components on the hair bones.


---

## 27. Head aim + left wrist untwist — v122 (Opus, 2026-09-04)

Two more hand-made corrections from Stephanie, baked with the section 26.1 method.

| bone | what she did | delta | axis |
|---|---|---|---|
| `mixamorig:Head` | pitched down so she faces her opponent instead of upward | **4.83 deg** | 98 % local X (pure pitch) |
| `mixamorig:Hand.L` | untwisted the badly pronated left wrist | **48.83 deg** | 97 % local Y (pure twist) |

Both were animated channels, so both used the delta bake, not a value replacement:
`q_delta = q_hers @ q_action_at_edited_frame.inverted()`, then `q_key' = q_delta @ q_key`
for all 31 keys, then de-flip the hemisphere.

**Why the delta bake provably preserves the motion.** The relative rotation between two
consecutive keys is unchanged by a constant left-multiplication:

```
(q_d q_j)^-1 (q_d q_j+1) = q_j^-1 q_d^-1 q_d q_j+1 = q_j^-1 q_j+1
```

So the idle's motion is mathematically identical, only re-based onto the new orientation.
Do not read the per-channel key spreads to check this — they *will* change (Head w-spread
0.00185 -> 0.00106, Hand.L z-spread 0.0184 -> 0.0342) because the delta reorients the
axis the wobble is expressed about. The spreads moving is expected; the motion is not.

### 27.1 Her wrist call was correct — measured
Region area audit on the body, faces grouped by dominant bone, ratio vs REST:

| region | before her edit | after |
|---|---|---|
| `ForeArm.L` crushed faces (<0.85) | 26 | **21-22** |
| `Hand.L` crushed faces (<0.85) | 7 | **3** |
| stretched faces (>1.30) | 27 / 10 | 27 / 10 (unchanged) |

Untwisting the wrist removed roughly a third of the crushed faces in the forearm and
over half in the hand, with no new stretching. Worth remembering: **a pose that is
anatomically wrong usually shows up as a skinning defect first.** Reach for the pose
before reaching for the weights.

### 27.2 Full-loop verification after both bakes
* 57 animated bones · **0 non-unit quaternions** · **0 hemisphere flips**
* loop seam (every bone, frame 1 vs 121): **1.64e-6**
* floor contact across all 121 frames: **-0.0004 to +0.0018**
* trident butt travel: **0.0021**
* scalp max deviation from rest: **0.00026** (section 26.2 fix still holding)
* tattoo offset from skin: 0.0098 posed vs 0.0099 rest, on every sampled frame
* cloth penetration: 44-46 verts, worst 0.0241 — identical to the pre-existing
  bracelet/anklet/necklace set from section 25.3. The 48.8 deg hand rotation introduced
  **nothing new**.

Backups: `anim_head_backup_2026-09-04.json`, `anim_handL_backup_2026-09-04.json`.
Blend of record: `Backups/Yemoja_WORKING_v122_headWrist.blend`.

### 27.3 Working with a hand-made viewport edit — the procedure
1. **Capture first, in the very first tool call.** A live viewport pose is not in the
   action. Any `frame_set`, any render, any `pose_position` toggle re-evaluates the action
   and destroys it silently — the edit is gone with no error.
2. **Diff every animated bone against the action** at the current frame rather than
   trusting the description of what was changed. On this pass Stephanie said "2 joint
   rotations" on the wrist; only `Hand.L` actually differed. The measurement is the
   source of truth, and saying so lets her re-check if she meant otherwise.
3. **Back up the affected curves to JSON before touching them.**
4. Pick the bake by whether the channel is animated: constant -> replace; animated ->
   delta bake.
5. Verify: exact match at the edited frame, loop seam 0, then re-run the full-loop audit.


---

## 28. Lessons for future sessions — read this before touching anything

Written by Opus at the end of the idle delivery, 2026-09-04. Sections 11-27 record *what*
was done. This section records *how the work went wrong* and what to do instead. Every
item below cost real time on this character.

### 28.A Measurement discipline

**28.A.1 Never "fix" damage you have not proved exists.**
Stephanie reported she had made a mess weight-painting the left elbow. A per-ring weight
audit showed high variance and non-monotonic ring means, which read as damage, so the
ramps were rebuilt. A byte-level diff then proved **all eleven meshes and all weights were
identical to Fable's v115** — zero vertices moved, zero weight differences, no shape keys.
There had never been any damage. The "fix" took the left elbow from 18 to 22 crushed faces
and had to be reverted.

Two lessons, and the second is the more important one:

* **Diff before you repair.** A byte-level comparison against the last known-good file is
  cheap and settles the question in one call. Do it *first*, not after the repair fails.
* **A metric that assumes uniformity will always report intentional asymmetry as damage.**
  The variance was Fable's deliberate flexor/extensor asymmetry (section 23). Before
  trusting any audit, ask what the metric assumes about a *correct* result.

**28.A.2 Check what your measurement is standing on.**
The section 12 conclusion — that the forearm carries pronation worse than the wrist — was
measured on an arm chain that was itself corkscrewed. On a clean chain the opposite holds.
A confident number from a broken setup is worse than no number, because it gets written
down and believed.

**28.A.3 Compare against the alternative, do not just measure the present state.**
The groin flap looked "fixed" when skin-matched: zero penetration. It was only when the
pinned binding was temporarily restored and re-measured that the real trade-off appeared
(zero stretch vs 1.7x stretch). **When changing a binding, measure both bindings under the
same test in the same call.** One number alone cannot tell you whether you improved
anything.

**28.A.4 Take the acceptance bar from work that has already been approved.**
"Is 1.25x stretch acceptable?" has no absolute answer. The shorts and top Stephanie had
already signed off sit at 1.28 and 1.26. That is the bar. Anything at or under it reads as
normal skinned cloth. Invent thresholds only when nothing comparable exists.

**28.A.5 Establish scale before calling a number big or small.**
The character is **7.398 blender units tall = 1.65 m, so 1 unit is about 22 cm**. A 0.02
penetration is 4 mm; a 0.15 displacement is 3.2 cm. Several early panics on this project
were sub-millimetre. Compute this once per session and put it in the notes.

**28.A.6 Distinguish "static fit" from "animation defect" by testing REST.**
Bracelets, anklets and the necklace all showed penetration in the posed audit. They show
the *same* penetration at rest, so they are modelling fit, not skinning, and are out of
scope for an animation pass. **Any residual that does not change between rest and posed is
not an animation problem.** This one test removed most of the noise from the cloth audit.

**28.A.7 Audit every frame, not the keys.**
Fable's own process note warns that the builder's audit passed at the keys twice while
in-betweens failed. Floor contact, penetration and quaternion sanity are all cheap enough
to run across all 121 frames. Do that.

### 28.B Rig and Blender traps specific to this file

**28.B.1 A deform modifier that reads another object's evaluated geometry must never sit
in front of the Armature modifier.** It chases the *posed* target, and the Armature then
applies the pose a second time. This has now bitten twice: the tattoos (section 25.2,
posed offset 0.26 vs 0.021 at rest) and the scalp (section 26.2, cap sinking 3.2 cm into
the skull). **Fix:** switch `pose_position` to `'REST'`, apply/bake the modifier, restore
the pose. **Check any new mesh for Shrinkwrap / Surface Deform / Mesh Deform ahead of the
Armature before trusting it in motion.**

**28.B.2 A garment must carry the weights of the skin it covers.** If a garment and the
skin under it are driven by different bones they separate the moment the pose leaves bind,
and that gap is what reads as tearing. Proximity skin-weight transfer (section 25.2) is
the fix. **Exception: rigid accessories** — bracelets, anklets, bead cords, necklace —
must stay rigidly single-bone bound, and transferring skin weights onto them will shear
them (section 18).

**28.B.3 `limb_ik` in `yemoja_anim_lib.py` is NOT hinge-correct. Do not use it for arms.**
It aims the upper bone's local X at the bend-plane normal, a convention unrelated to where
the bind pose's hinge actually sits — 60.7 degrees off on this rig, which folds elbows
sideways. Use `v115_fixes/apply_pole._limb_ik`. The warning lives in the function's
docstring because **a warning in the code is read and a warning in a document is not.**

**28.B.4 `frame_set` destroys an unkeyed pose.** With an action assigned, changing the
frame re-evaluates it and silently overwrites whatever you just posed; the keying call
then "succeeds" having stored the old values. **Set the frame first, build the pose, then
key.** `key_pose` used to call `frame_set` internally and silently discarded poses for
this reason.

**28.B.5 Rendering also re-evaluates the action** (Fable, section 15.3). Key the pose
before rendering it, or the render will show the pre-edit state and the edit will be gone.

**28.B.6 `CL_*` marker vertex groups carry weight exactly 0.5, not 1.0.** A
`weight > 0.5` filter silently returns an empty set. Test membership, not magnitude.

**28.B.7 Blender 5.x API changes that break older snippets:**
* `Bone.select` moved to `PoseBone.select`.
* `Action.fcurves` no longer exists — actions are layered. Curves live under
  `action.layers[..].strips[..].channelbags[..].fcurves`. Build a
  `{(data_path, array_index): fcurve}` dict once and index into it.

**28.B.8 Quaternion hygiene after any per-key maths.** Re-normalise, then walk the keys and
negate any that dot-negative against the previous one. Without the de-flip the curve takes
the long way round between two keys and the bone visibly spins. Verify with: 0 non-unit
quaternions, 0 hemisphere flips, loop seam ~0.

**28.B.9 Framing review renders — use an orthographic camera.** Perspective close-ups
missed the subject repeatedly on this project (renders of hair and hip when the elbow was
wanted). The reliable recipe: compute the region centroid from the *evaluated* mesh, set
`ortho_scale = 2 x half-height`, place the camera 12-14 units back along the view
direction, and build `matrix_world` from the basis vectors directly. No lens, no
focal-length guessing. **World axes on this rig: +X = her LEFT, +Z = UP, -Y = the
direction she faces.**

**28.B.10 Know what actually survives to Unity.** Humanoid retargeting carries only the
humanoid muscle set. Animation authored on `hair_grp*` or any other non-humanoid bone is
**silently discarded on import** — no warning, it simply does not move. Decide what is
baked and what is simulated *before* authoring, not after (section 26.5).

### 28.C Working with Stephanie's own edits

She poses by hand in the viewport and then asks for the correction to be carried into the
clip. The procedure that works:

1. **Capture in the very first tool call.** A live viewport pose is not in the action. Any
   `frame_set`, render, or `pose_position` toggle destroys it with no error. Read the pose
   values before doing anything else at all.
2. **Diff every animated bone against the action rather than trusting the description.**
   On the wrist pass she said "2 joint rotations"; only `Hand.L` actually differed. The
   measurement is the source of truth — say so plainly so she can re-check if she meant
   otherwise, rather than silently going with either version.
3. **Back the affected curves up to JSON before editing them.**
4. **Pick the bake method by whether the channel is animated.** Constant across all keys ->
   write her value to every key. Animated -> delta bake (`q_delta = q_hers @
   q_action_at_frame.inverted()`, then `q_key' = q_delta @ q_key`), which provably
   preserves the motion because the relative rotation between consecutive keys is invariant
   under constant left-multiplication. Replacing values on an animated channel silently
   flattens the motion out of that joint.
5. **Expect the per-channel key spreads to change** after a delta bake — the delta
   reorients the axis the variation is expressed about. That is not motion loss; do not
   "fix" it (see 28.A.1).
6. **Her instincts about her own character are usually right, and are worth verifying
   rather than assuming.** The wrist untwist removed a third of the crushed faces in the
   forearm and over half in the hand. **A pose that is anatomically wrong shows up as a
   skinning defect first — reach for the pose before reaching for the weights.**

### 28.D Leave no trace — checklist before ending a session

This was got wrong at the end of the idle delivery: the scene was left with the scene
camera pointed at a scratch orthographic camera, 21 scratch cameras littering the Scene
Collection, render resolution at 900x900, transform pivot on Individual Origins, and
viewport overlays switched off in the Layout workspace. Stephanie reported it as "my
viewport navigation is off". **The Blender scene is her working environment, not scratch
space.** Before finishing:

- [ ] `scene.camera` restored to `_review_cam` (hers), and no viewport left looking through
      a scratch camera
- [ ] every scratch camera, light, empty and collection created during the session deleted
- [ ] `render.resolution_x/y` back to **1920 x 1080**, `resolution_percentage` 100
- [ ] `render.engine` back to **CYCLES**; `display.shading.show_cavity` off
- [ ] `render.filepath` not left pointing at a review PNG
- [ ] `tool_settings.transform_pivot_point` back to **MEDIAN_POINT**
- [ ] `overlay.show_overlays` and `overlay.show_bones` **True in every workspace**, not
      just the one you were using — check `bpy.data.screens`, not the active screen
- [ ] `Armature.show_in_front` True
- [ ] the Armature selected and active, `frame_current` inside the clip range
- [ ] scene saved

**Do not silently reset things you did not change.** At the end of this session the 3D
cursor sat at (-0.111, -0.379, 4.303) with a non-zero rotation, and
`preferences.inputs.use_auto_perspective` was on — both plausible causes of "navigation
feels wrong", neither of them ours. Report those and offer; do not quietly overwrite her
settings.
