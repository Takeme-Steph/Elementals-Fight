# Yemoja — Unity import guideline

For the Cowork agent handling the Unity side. **Yemoja-specific.**
Written 2026-08-30 against `Backups/Yemoja_WORKING_v107_merged.blend`.

Project-wide standards live in `BlenderTools/README_rig_conventions.md` and
`BlenderTools/README_eye_standard.md`. Reference those, do not restate them.
Animation authoring rules: `YemojaDesignArtifacts/README_animation_guidelines.md`.
Full asset spec: `YemojaDesignArtifacts/yemoja_manifest.json`.

Prior import history is in `claude/yemoja-v2..v5-import-record.md`. Read the v4
record before touching the rig — its avatar-reset rule applies to this delivery.

---

## 1. What changed since v5 — read this first

| Area | Change | Consequence for Unity |
|---|---|---|
| **Rig** | **94 -> 80 bones.** 34 stale `hair_loc*` replaced by 20 `hair_grp*`. | **Avatar reset REQUIRED.** See section 3. |
| **Floor** | Rest mesh was 0.1432 Blender units **below** z=0; now sits exactly on it. | Toe clearance should improve by ~0.03 source units. See section 6. |
| **Weights** | All meshes normalised to 1.0, **max 4 influences**. | `Yemoja_Clothes` previously shipped 96 five-influence vertices. Deformation in Unity will now match Blender. |
| **Clavicle** | `Spine2` bleed trimmed out of the shoulder region. | No action, but see section 7 on shoulder ranges. |
| **New mesh** | `Yemoja_Tattoos` decal, own UV, own material. | +1 material, +1 renderer. Two runtime floats, section 5. |
| **Hair/eyes** | 20 hair bones; eye emission mask. | Hair bones are **not** humanoid — see section 3. |
| **Culling** | Backface culling set in Blender. | **Does not export.** You must set Render Face. Section 4. |
| **Merges** | Lash cards and eyeliner each merged to one object. | 14 -> 12 draw calls. |
| **Hair baked** | Node-graph hair colour/alpha/gloss baked to flat PBR maps. | See section 5a. Uses `_BaseMap_ST` tiling (0.5,1) offset (0.5,0). |
| **Fuzz baked** | Hairline alpha baked from a vertex colour to a tiling map. | Fuzz is now **Transparent**, see section 5b. |
| **Hair split** | Hair separated into an **opaque** body material and an alpha-tested tip/root material. | +1 material, +1 draw call (13). Alpha-tested share 48.4% -> **13.2%**. Section 4. |
| **Clothes** | Hip pearl strands and beads decimated (decorations only, garments untouched). | 13,154 -> **9,426** triangles. No material or UV change. |

---

## 2. Budget as shipped

- **43,078 triangles**, 27,747 vertices (Blender count; Unity will be higher after
  UV/normal seam splitting), **13 draw calls**, 9 renderers, **10 materials**,
  **80 bones**.
- Overdraw at a framed side view, 1080x1080, silhouette 62,818 px: **3.81x** with
  backface culling, 7.26x without.
- **Alpha-tested fragments are 13.2% of the total**, down from 48.4% before the
  hair split. This is the number that matters most on tile-based mobile GPUs:
  alpha-tested fragments defeat early-depth rejection, because the GPU cannot
  know a fragment is discarded until it has sampled the texture and run the
  shader. Opaque fragments reject cheaply and can occlude what is behind them.

Note the fragment *count* barely moved when the hair was split, and again when
the clothes were decimated. That is expected — the same pixels are still shaded.
The hair split changed the cost per fragment; the clothes work saved vertex and
skinning cost. Do not read a flat overdraw number as "nothing happened".

Object list, triangles / vertices: `Yemoja_Body` 14,744 / 7,417 |
`Yemoja_Clothes` 9,426 / 5,741 | `Yemoja_Scalp` 9,302 / 5,735 |
`Yemoja_LashCards` 5,024 / 5,184 | `Yemoja_Tattoos` 1,828 / 1,118 |
`Yemoja_BrowCards_A_R` 1,360 / 1,530 | `Yemoja_Nails` 880 / 620 |
`Yemoja_Fuzz` 298 / 176 | `Yemoja_Eyeliner` 216 / 226.

### Texture memory — the biggest remaining mobile cost, and it is yours to fix

17 textures, **26.5 megapixels total**. Six of them are 2048x2048 and account for
**95% of that**: skin base colour, skin normal, skin metallic/smoothness, and the
same three for clothes.

| Encoding | Memory (one character) |
|---|---|
| RGBA32 uncompressed | **101 MB** |
| ASTC 6x6 | 9.0 MB |
| ASTC 6x6 + mipmaps | **12.0 MB** |

Two fighters on screen is roughly **24 MB** of texture before the stage, UI or
VFX. That is the single largest number in this whole budget and **it is fixed
entirely by Unity import settings, not by changing any asset**:

- Set **Max Size 1024** on the six 2048 maps for the mobile build. That is a 4x
  cut, to about **3 MB per character**. At fight-camera distance she covers a
  small fraction of the screen; 2048 skin detail cannot resolve there.
- Keep 2048 only if CharacterSelect close-ups demand it — and if so, use a
  platform override so the mobile build still ships 1024.
- Ensure **Generate Mip Maps ON** and **compression ASTC** for Android/iOS.
  Uncompressed 2048 maps are the default failure mode and cost 11x more.

Everything else is small: the next largest textures are a 512 eye emission, a 512
tattoo atlas, and a handful of 256 and 128 maps.

### Considered and deliberately rejected
1,373 body faces (9.3% of body triangles, **29.3% of body surface area**) are
permanently hidden under the clothes. Deleting them would cut triangles *and*
fragments. **Stephanie has ruled this out on purpose**: the clothes design may
still change, and regenerating deleted body faces afterwards is expensive. Do not
propose it again.

---

## 3. The rig — avatar reset is mandatory

Bone count changed, so per the **v4 rule** the preserved `.meta` will pin a stale
`humanDescription.skeleton[]` and the avatar will silently flip to
`isHuman = false`. Sequence:

1. Overwrite the FBX **in place** (preserves GUID, prefabs and roster arrays).
2. `animationType = None` -> reimport.
3. `animationType = Human`, `avatarSetup = CreateFromThisModel` -> reimport.
4. **Re-audit the humanoid map.** The reset discards hand corrections along with
   the stale data.

Then verify, because none of these fail loudly:

- `isHuman == true` and `Armature` present in `skeleton[]`.
- **Jaw slot bound to nothing.** Yemoja has no jaw bone. The auto-mapper has
  grabbed a hair bone for it **twice** (`hair_loc16_0` on v2 and v4). It will now
  reach for a `hair_grp*` bone. Remove the entry.
- Both hands' 15 finger bones mapped. `.L` has defeated the auto-mapper before.
- `Eye.L` / `Eye.R` bound to the real eye bones, not hair.
- **Marker parenting**: 12 `H_*`/`A_*` colliders plus the trident. The bones they
  hang off (`Head`, `Hand.L/R`, `Spine`, `Leg.L/R`) are **unchanged in name**, so
  the narrowed v4 rule predicts they survive — but 14 bones were removed from
  under `Head`, which can perturb transform fileIDs. **Verify, do not assume.**
  It fails silently by re-parenting to the prefab root.

### Hair bones do not retarget
The 20 `hair_grp*` bones are not humanoid bones. Unity Humanoid clips carry only
the humanoid muscle set, so **animation keyed onto hair bones is dropped on
import**. Hair motion must be a Unity-side runtime spring/jiggle system driven
off head motion. This is stated in the animation guidelines too.

---

## 4. Render Face — the single biggest performance item

Blender's `use_backface_culling` is a Blender-only property. **FBX does not carry
it.** The 47.5% fragment saving only lands when these are set in URP:

| Material | Render Face |
|---|---|
| `Yemoja_Body_mat` | **Front** |
| `Yemoja_Clothes_mat` | **Front** |
| `Yemoja_Eye_mat` | **Front** |
| `Yemoja_Eyeliner_mat` | **Front** |
| `Yemoja_Hair_Opaque_mat` | **Front** |
| `Yemoja_Jewelry_mat` | **Front** |
| `Yemoja_Tattoos_mat` | **Front** |
| `Yemoja_Hair_mat` | **Both** |
| `Yemoja_Fuzz_mat` | **Both** |
| `Yemoja_Lashes_mat` | **Both** |

**`Yemoja_Hair_mat` must be Both, and this was got wrong once.** It carries the
184 tip-card triangles, which are single-sided planes. Set to Front, a card is
visible from one side only — so in a mirror match the fighter facing one way
shows her tips and the fighter facing the other way loses them entirely.
`FaceOpponent` rotates rather than mirroring scale, so winding is *not* the
cause; single-sided cards are. Only 920 triangles are affected, so the cost is
negligible.

Do not generalise "hair is closed tubes, culling is safe" — that was measured
across hair as a whole *before* the material split separated cards from tubes.

### The two hair materials
`Yemoja_Hair_Opaque_mat` (5,704 triangles, 81% of hair area) is the loc tube
bodies. It has **no alpha at all** — set Surface Type **Opaque**, alpha clipping
**off**. `Yemoja_Hair_mat` (920 triangles) is the tip cards plus the root fade
band and **must keep alpha clipping** (threshold 0.5).

Getting this backwards — leaving the opaque material alpha-clipped — silently
forfeits the entire benefit of the split. The two materials are otherwise
identical, including textures.

Fuzz and Lashes are genuine two-sided cards. Culling them would save a further
1.3% and break them — not worth it.

`Yemoja_Nails` uses `Yemoja_Jewelry_mat` and has been rendering culled all along,
so its two-shell construction is proven safe under culling.

---

## 5. Materials -> textures: the authoritative table

**Read this before changing any texture assignment.** The absence of this table
is why a whole import cycle ran against superseded maps: the clothes gradient,
the silver ornament and the pale hair all existed in the project and were never
wired, while Unity kept pointing at older files. If a map is not in this table,
it should not be assigned to anything.

| Material | Base | Metallic/Smoothness | Normal | Emission |
|---|---|---|---|---|
| `Yemoja_Body_mat` | `Yemoja_Color_SkinGrade_v17_navelMirrored` | `Yemoja_MetallicSmoothness_v10_pores` | `Yemoja_Normal_v11_navelMirrored` | — |
| `Yemoja_Eye_mat` | same skin atlas as Body | same | same | `Yemoja_Eye_Emission_v2_soft` |
| `Yemoja_Clothes_mat` | `Clothes_BaseColor_Ocean6_gradient` | `Clothes_MetallicSmoothness_v4_silverOrnament` | `Clothes_Normal_v2_seam` | — |
| `Yemoja_Jewelry_mat` | `Yemoja_Jewelry_BaseColor_v3_nails` | `Yemoja_Jewelry_MetallicSmoothness_v2_nails` | `Yemoja_Jewelry_Normal_v1` | `Yemoja_Jewelry_Emission_v1` @ 0.45 |
| `Yemoja_Hair_mat` | `Yemoja_Hair_Baked_BaseColor_v2` | `Yemoja_Hair_Baked_MetallicSmoothness_v2` | `Yemoja_Hair_Baked_Normal_v2` | base map @ 0.2 |
| `Yemoja_Hair_Opaque_mat` | same three baked maps | same | same | base map @ 0.2 |
| `Yemoja_Fuzz_mat` | `Yemoja_Fuzz_Baked_BaseColor_v1` | none, `_Smoothness` 0.22 | `Yemoja_Fuzz_CoilNormal_v3_wisp` | flat 0.2 |
| `Yemoja_Lashes_mat` | `lash_strand_white4` | — | — | — |
| `Yemoja_Tattoos_mat` | `Yemoja_Tattoo_Baked_512` | — | — | `RIG_tattoo_glow`, default 0 |
| `Yemoja_Eyeliner_mat` | none, flat `_BaseColor` (0.275, 0.552, 0.701) | — | — | — |

`Yemoja_Jewelry_mat` had **no textures at all** until 2026-08-30 — flat colour at
`_Metallic` 1.0 / `_Smoothness` 0.8, i.e. a mirror, which rendered as royal blue
because it was reflecting the skybox. If the jewellery ever looks like chrome
again, check the maps are still assigned before touching anything else.

**`SetTexture` does not enable the matching shader keyword.** `_METALLICSPECGLOSSMAP`,
`_NORMALMAP`, `_EMISSION` and `_DETAIL_MULX2` must be enabled explicitly when
assigning by script. Always read back `material.shaderKeywords` to verify.

### 5a. The baked hair maps
Blender drives hair colour and alpha from a node graph — `CardTint`, a mask-green
multiply, a `RootVal_ramp` off `root_arclen`, and `root_fade` / `is_card` float
attributes. **None of that survives FBX.** It has been baked into three
1024x1024 maps (9x supersampled):

- `Yemoja_Hair_Baked_BaseColor_v2` — RGB final albedo, **A = root fade x tip-card shape**
- `Yemoja_Hair_Baked_MetallicSmoothness_v2` — RGB 0, A = smoothness (mean 0.346), sRGB OFF
- `Yemoja_Hair_Baked_Normal_v2` — wisp normal at native 12.47 x 3.52 tiling, strength 0.8

**Critical:** these use `_BaseMap_ST` **tiling (0.5, 1), offset (0.5, 0)**. The
atlas packs the tip cards into u 0..0.35 and the tube bodies into u 0.5..0.947,
which the ST transform addresses as `u * 0.5 + 0.5`. Reset that tiling and the
hair samples the wrong half of the atlas. URP applies `_BaseMap_ST` to the
metallic and normal maps too, which is why all three share the atlas.

Fidelity: alpha-clip decisions agree with Blender **100%** across 19,872 sampled
corners. Base colour differs by 0.034 mean and smoothness by 0.072 — the mask
and wisp tile 12.47x across U, so exact reproduction would need ~3,200 texels of
atlas width. These are noise-breakup patterns, so the statistics match (mean
smoothness lands exactly on 0.346) even though individual texels do not.
`root_arclen` is approximated as `3.2113 x v`, costing 0.7% mean brightness error.

### 5b. The baked fuzz map
`Yemoja_Fuzz_mat` alpha came from a **`hairline_fade` vertex colour**, which URP
Lit cannot read (rule 59). Baked to `Yemoja_Fuzz_Baked_BaseColor_v1` (256x256,
tiling, REPEAT) as `clamp(fade^0.5 + (coilNormal.a - 0.5) x 0.35, 0, 1)`. The
`fuzz_uv` layer is single-valued under mod-1 wrapping, so this tiles correctly
with **no UV transform** — leave `_BaseMap_ST` at identity here. RGB is a flat
constant (`MASTER_flat` x `MASTER_dark` 0.52 x `rootval` 0.75).

**Fuzz is Transparent, not alpha-clipped.** Blender renders it dithered, and the
alpha is a soft fade; clipping produced solid quads. Queue 3000, ZWrite off,
shadow pass off. Unity resolves this to premultiplied blending
(`_ALPHAPREMULTIPLY_ON`, SrcBlend One), which is equivalent because URP
premultiplies inside the shader. If the fuzz looks washed out or too bright,
force SrcBlend to SrcAlpha and clear that keyword.

### Runtime parameters
- `RIG_tattoo_glow` — default **0.0**
- `RIG_eye_glow` — default **0.9375**, tuned blind. Cycles showed almost no
  change across 0.0-1.5 because the perceived strength comes from **bloom**,
  which Cycles does not apply. Tune against URP's post-processing volume.

### Texture import settings
Any packed data map needs **sRGB off**; any normal map needs **Normal Map type**.
This project has shipped these wrong four times. Check on arrival.

**Do not "fix" these — deliberate:** lips ship uncoloured; brows and lashes are
near-white; eyeliner will not read at fight-camera distance (it is a
CharacterSelect / DisplayModels asset).

**Must not reach Unity:** `Yemoja_LocGuides_Invisible` and the `Yemoja_Source`
collection (locs curves + five `JW_*` instancing sources).

### Texture folder hygiene
The folder was cut from 121 textures to 23 on 2026-08-30 (98 files, 85 MB
deleted). **Everything left is either in the table above or a Blender source.**
Note that **Blender's images live inside `Assets/CharacterModels/Yemoja/textures`
and are not packed**, so deleting a file Unity does not reference can still break
the .blend. Always exclude Blender-referenced files from any cleanup.

## 6. Scale and floor

**Measure, never assume.** Import at defaults, measure the resulting height, then
`globalScale = 1.80 / measured`. Do not reuse v2's `0.23985990` — the mesh has
changed since. Leave `useFileScale = true`; flipping it to false also drops the
file's cm->m factor, which is the documented 100x trap.

Targets: **1.80000** source height, **3.4500** in-game at the prefab's existing
1.91667 root scale.

**Floor:** her feet now rest on z = 0 exactly. Every import record from v2 to v5
noted toe clearance of "-0.03 to -0.39" and attributed it to pose lean; the -0.03
end was actually a constant offset baked into the rest mesh, now removed. Expect
the measured range to shift up by ~0.03 source units. If it does not, something
else is going on and it is worth chasing rather than repeating the old note.

The `Armature` object now carries a **Z translation of 0.14324** rather than
sitting at the origin. That is intentional, not a mistake to correct.

---

## 7. Verify and report back

- **Shoulder muscle ranges.** Unity clamps humanoid rotations per muscle, and
  shoulder defaults are tight. The animation plan depends on ~30 deg of clavicle
  elevation surviving retargeting. Read `humanDescription.human[]` limits for the
  `LeftShoulder` / `RightShoulder` slots and report the numbers back to the
  Blender side — this was not checkable when the guidelines were written.
- **Console at Log level, not just errors and warnings.** `Rig Error: Avatar
  creation failed` is logged as a **Log**. A console filtered to errors and
  warnings is not a clean console. Expect ~6 "self-intersecting Hair polygon"
  Log entries; those are known, not a regression.

---

## 8. Still open on the Unity side — unchanged, and not caused by this delivery

- **Animation events** (`PerformAttack` / `StopAttacking` / `EndHit`) absent from
  all 8 clips. **Her attacks still deal no damage.** Open since v2 and by a wide
  margin the biggest remaining gap.
- Root `BoxCollider` and all hitbox sizes still date from pre-resculpt
  proportions.
- `CharacterPhysics.groundLayerMask` is still 0 (nothing) on Yemoja.
- The 8 current clips are **Mixamo placeholders** used for gameplay work. Custom
  animations are planned; see the animation guidelines.

## 9. Regression test that matters

Body-vs-clothes tearing during the asymmetric attacks (`SwordAndShieldAttack`,
`SwordAndShieldSlash`). A static pose looks fine even when skin weights are
wrong — that is exactly how the v2 weight bug went unnoticed.

---

## 11. The delivered export

**File:** `BlenderTools/_export/Yemoja_v6.fbx` (2.38 MB), written 2026-08-30.
**Source:** `Backups/Yemoja_WORKING_v110_clothesOpt.blend`.
**Export-prep state (all modifiers applied):** `Backups/Yemoja_EXPORT_PREP_v6.blend`
— keep this; it is what the FBX actually contains, and it is the file to diff
against if anything imports strangely.

Exported in **rest pose**, with the section-10 sequence followed in order. All
applies verified: tattoo `Conform` baked, scalp stack realised (20 `hair_grp*`
vertex groups now carry real weights, sum 1.0, max 4 influences, 0 unweighted),
`hair_attach` removed **after** the generator ran, fuzz shrinkwrap and both
displaces baked, all three Mirror modifiers applied. Only the Armature modifier
remains on each mesh, which is correct. Triangle count held at 43,078 throughout.

### FBX settings used
```
object_types        = {ARMATURE, MESH}      use_mesh_modifiers   = True
add_leaf_bones      = False                 bake_anim            = False
primary_bone_axis   = Y                     secondary_bone_axis  = X
armature_nodetype   = NULL                  mesh_smooth_type     = FACE
axis_forward        = -Z                    axis_up              = Y
apply_scale_options = FBX_SCALE_NONE        global_scale         = 1.0
path_mode = AUTO, embed_textures = False, use_tspace = False
```
`add_leaf_bones = False` matters: the rig already carries explicit end bones
(`Toe_End_end`, `HeadTop_End_end`), and leaving it on would add a second set.
`use_tspace = False` because Unity generates its own tangents.

**Not exported:** the `Yemoja_Source` collection (`Yemoja_Locs_Curves` and the
five `JW_*` jewellery instancing sources), all cameras and lights. The scene is
now organised so that everything non-shipping lives under `Yemoja_Source` — a
single collection to exclude rather than a list of object names.

### Known, expected, not a fault
`Yemoja_Scalp` carries a **`Yemoja_LocGuides_Invisible` material slot with zero
faces**, so it will import as an empty submesh needing a material assignment.
This is deliberate and Stephanie has asked for it to be left alone — it comes
from the `Locs_Generator` node tree, which is not to be modified. Assign anything
to that slot; nothing renders from it.


---

## 12. Animation delivery — the Idle clip (Opus, 2026-09-04)

Handoff to the Unity importer session. **The character mesh in this section is a
separate decision from the clip — read 12.4 before deciding to touch it.**

### 12.1 The file

**`BlenderTools/_export/Yemoja@Idle.fbx`** (1,100,172 bytes, written 2026-09-04)
**Source:** `Backups/Yemoja_WORKING_v122_headWrist.blend`

* **Animation only — contains no geometry.** Verified: 0 `Geometry` nodes in the FBX.
* 80 bones (60 `mixamorig:*` + 20 `hair_grp*`) — identical hierarchy to `Yemoja_v6.fbx`.
* 1 AnimationStack / 1 AnimationLayer, 244 curve nodes, 974 curves.
* **Frames 1-121 at 30 fps = exactly 4.000 s.** Frame 121 duplicates frame 1; that is
  intentional and is what makes the loop seamless. **Set Loop Time on.**
* The `@` in the filename is the Unity clip-association convention — keep it.

FBX settings used (deliberately identical to the `Yemoja_v6.fbx` mesh export in section
11, so the two skeletons match bit for bit):
```
object_types      = {ARMATURE}        add_leaf_bones    = False
primary_bone_axis = Y                 secondary_bone_axis = X
armature_nodetype = NULL              axis_forward = -Z, axis_up = Y
apply_scale_options = FBX_SCALE_NONE  global_scale = 1.0
bake_anim = True, bake_anim_step = 1.0, bake_anim_simplify_factor = 0.0
bake_anim_force_startend_keying = True, bake_anim_use_all_actions = False
```
`bake_anim_simplify_factor = 0.0` is deliberate — no curve decimation, so the clip Unity
receives is exactly the clip that was audited.

### 12.2 Import settings — non-negotiable

* `animationType = **Human**`
* `avatarSetup = **CreateFromThisModel**` — **never `CopyFromOther`.** That skips
  proportional retargeting and sank the character half underground on the first attempt
  (animation guidelines section 7).
* **Loop Time = on**, Loop Pose = off (the clip is already seam-exact, 1.64e-6; letting
  Unity re-blend the pose would only add error).
* Root Motion: leave off. The idle has none — floor contact stays within
  -0.0004 to +0.0018 source units across all 121 frames.
* Rename the imported clip to **`Idle`** so it drops into the existing `YemojaAnimCTRL`
  Idle state in place of the Mixamo placeholder.

### 12.3 Considerations

1. **The 20 `hair_grp*` curves in this FBX will be discarded on import.** Humanoid
   retargeting carries only the humanoid muscle set. They are baked into the file because
   `bake_anim_use_all_bones = True` exports every bone; they are all constant anyway
   (0 of the 20 hair bones carry any authored motion). **This is expected, not a fault.**
   Do not switch the clip to Generic to keep them.
2. **Hair motion is a Unity-side job, by decision, not an omission.** See animation
   guidelines section 26.5 for the full reasoning. Summary of what you are inheriting:
   * 10 chains, each `mixamorig:Head -> hair_grpNN_0 -> hair_grpNN_1`. **2 joints deep**,
     bone length ~0.17-0.23 m, so ~0.40 m of driven loc per chain.
   * The generated locs **are** genuinely weighted to them — average 0.04-0.08 influence
     per bone across 5,735 verts, assigned by the `Hair_Weights` geometry-node modifier
     and already baked into `Yemoja_v6.fbx`.
   * Therefore the rig is ready for a spring-bone / cloth solver with no Blender work.
   * **Budget colliders time, not solver time.** 20 bones x 2 fighters = 40 short chains
     is negligible CPU. Without capsule colliders on head/neck/chest/shoulders the locs
     will swing straight through her — that is where the setup effort actually goes.
   * Stephanie asked whether a *material* could do this. It cannot: materials and shaders
     control appearance only. Motion needs components on the bones.
3. **Animation events are still missing and still mandatory** (animation guidelines
   section 7). The Idle clip needs none, but `PerformAttack` / `StopAttacking` / `EndHit`
   remain absent on the attack and hit clips and remain the reason her attacks deal no
   damage.
4. The **trident** is already a direct child of `mixamorig:Hand.R` in both
   `Yemoja.prefab` and `YemojaDisplay.prefab`, with the `A_Trident` capsule collider under
   it. It follows the hand and needs nothing from this delivery. Grip was verified stable:
   butt travel 0.0021 source units across the whole loop.

### 12.4 The mesh question — SUPERSEDED BY SECTION 13

> **This section is out of date. The mesh has since been re-exported as
> `Yemoja_v7.fbx` and the avatar-reset claim below was wrong — the bone set is
> unchanged, so no reset is required. Read section 13 instead. The section is kept
> here only so the reasoning trail is visible.**


`Assets/CharacterModels/Yemoja/models/Yemoja_v2.fbx` is **byte-identical to
`_export/Yemoja_v6.fbx`** — Unity is running the v6 mesh under the v2 filename.

Since v6 was exported, the following mesh/weight work has landed in Blender and is
**not in Unity**:

| change | where documented |
|---|---|
| Fable's V3/V4 elbow loop cuts + anatomy sculpt | animation guidelines sections 22-23 |
| Fable's girdle and breast weight passes | sections 16-17 |
| Bracelet rigid re-bind (shear fix) | section 18 |
| Top / shorts skin-weight transfer | section 25.2 |
| Tattoo shrinkwrap bake + skin-weight transfer | section 25.2 |
| Groin flap graded bind | section 25.4 |
| **Scalp shrinkwrap bake (double-transform fix)** | section 26.2 |

Consequences if the mesh is NOT re-exported: the idle will play correctly, but the top
will tear at her left side, the shorts will tear between the thighs, the arm tattoos will
clip, and **the scalp will sink up to 3.2 cm into her skull** — the artifacts Stephanie
reported are all mesh-side, and this clip alone does not fix any of them.

Consequence if it IS re-exported: **the avatar reset in section 3 becomes mandatory
again** and the full humanoid re-audit must be repeated.

**This is Stephanie's call, not the importer agent's.** Ask her before re-exporting the
mesh. If she says go, the export-prep sequence is section 10 of the animation guidelines
and the scalp Shrinkwrap no longer needs applying at export time — it is already baked
into the base mesh as of v121.


---

## 13. Mesh delivery — `Yemoja_v7.fbx` (2026-09-04). Supersedes 12.4

**File:** `BlenderTools/_export/Yemoja_v7.fbx` (2,421,164 bytes)
**Export-prep state:** `Backups/Yemoja_EXPORT_PREP_v7.blend` — this is what the FBX
actually contains; diff against it if anything imports strangely.
**Source:** `Backups/Yemoja_WORKING_v122_headWrist.blend`

### 13.1 The avatar reset is NOT required this time — measured, not assumed

Section 12.4 said a mesh re-export makes the section 3 avatar reset mandatory. **That was
wrong, and the correction matters.** The section 3 rule exists because v5 -> v6 changed
the rig from 94 bones to 80. This delivery changes **no bones at all**:

```
current rig bone set vs Yemoja_v6.fbx bone set
  80 bones in each · in current not in v6: []  · in v6 not in current: []
```

Same 60 `mixamorig:*` + 20 `hair_grp*`, same names, same hierarchy, exported with byte-
identical FBX axis and scale settings. **The existing avatar should map without a reset.**
Verify the humanoid mapping after import as you would anyway, but do not pre-emptively
reset it and redo the whole audit — this is a mesh-and-weights delivery only.

### 13.2 What is in it that was not in v6

| change | reference |
|---|---|
| Fable's V3/V4 elbow loop cuts and anatomy sculpt | anim guidelines 22-23 |
| Fable's girdle and breast weight passes | 16-17 |
| Bracelet rigid re-bind (shear fix) | 18 |
| Top + shorts skin-weight transfer — **fixes the left-side and inner-thigh tearing** | 25.2 |
| Tattoo shrinkwrap bake + skin-weight transfer — **fixes the arm tattoo clipping** | 25.2 |
| Groin flap graded bind | 25.4 |
| Scalp shrinkwrap bake — **fixes the scalp sinking 3.2 cm into the skull** | 26.2 |
| Weight hygiene, see 13.4 | this section |

### 13.3 Budget delta

**43,462 triangles**, up from 43,078 (+384, +0.9 %) — entirely Fable's elbow loop cuts.
9 meshes, 9 renderers, **10 real materials** (11 slots; `Yemoja_LocGuides_Invisible` is the
known zero-face slot from section 11 — still deliberate, still leave it alone). Unchanged:
draw calls, UV layout, texture set, material assignments, alpha-tested fragment share.

Per-mesh triangles: Body 15,128 · Scalp 9,302 · Clothes 9,426 · LashCards 5,024 ·
Tattoos 1,828 · BrowCards 1,360 · Nails 880 · Fuzz 298 · Eyeliner 216.

### 13.4 Weight hygiene fixed during prep — read this one

Export-sequence step 5 (`sum 1.0`, `max 4 influences`) was **violated in the working file**
and would have shipped:

* `Yemoja_Body` — **238 vertices carried 5 influences.** These came in with Fable's elbow
  re-weighting. This is the dangerous class: Unity clamps to 4 influences **and then**
  normalises, so a 5-influence vertex deforms *differently* in Unity than in Blender and
  nothing reports it. Fixed by dropping the smallest influence and renormalising — which
  is exactly what Unity would have done, so the two now agree. **The dropped 5th influence
  had weight 0.0 in all 238 cases**, so the deformation is bit-identical; the risk was
  latent, not active.
* `Yemoja_Clothes` — 4,479 vertices summed to as little as **0.4847**.
* `Yemoja_Fuzz` — 124 vertices summed to 0.5.

Both of the latter had 4 or fewer influences, so Unity's normalisation would have produced
the correct ratios anyway; they were cosmetically wrong rather than broken. All three are
now clean.

**Post-prep verification, all 9 shipping meshes:** 0 unweighted vertices, 0 vertices whose
weights do not sum to 1.0, 0 vertices over 4 influences, exactly 1 armature.

### 13.5 Export prep actually performed (animation guidelines section 10 order)

1. Tattoo `Conform` shrinkwrap — **already baked in the working file** (v119), nothing to do.
2. Scalp `Shrinkwrap` — **already baked in the working file** (v121, the double-transform
   fix). Then `Locs_Generator`, `Hair_Weights` and `Hair_Material_Split` applied in order:
   143 -> 5,735 verts, 22 real vertex groups (20 hair bones + Head + Neck).
3. `hair_attach` UV removed **only after** step 2, per the section 10 warning.
4. `Yemoja_Fuzz` Shrinkwrap + `Fuzz_Lift` + `Fuzz_Frizz` applied.
5. Mirror modifiers applied: BrowCards 765 -> 1,530, Eyeliner 113 -> 226, LashCards
   2,592 -> 5,184.
6. Weight clamp + normalise (13.4).
7. Exported in **REST pose**. Only the Armature modifier remains on each mesh, which is
   correct.

FBX settings identical to section 11, with `bake_anim = False`. Verified in the written
file: 80 bones, 0 `AnimationStack` nodes, all 9 meshes present, **no Trident, no cameras,
no jewellery source objects**.

### 13.6 Import order

1. Import `Yemoja_v7.fbx` first and let the humanoid mapping settle (13.1 — expect no
   reset).
2. Then `Yemoja@Idle.fbx` with `avatarSetup = CreateFromThisModel`, Loop Time on.
3. Rename the clip to `Idle`, drop it into the `YemojaAnimCTRL` Idle state.
4. `Yemoja_v2.fbx` in `Assets/CharacterModels/Yemoja/models/` is the file Unity currently
   references and it is byte-identical to the old `Yemoja_v6.fbx`. Decide with Stephanie
   whether v7 replaces it **in place under the same filename** (keeps every GUID, material
   and prefab reference intact — this is what was done when the trident shaft was slimmed)
   or lands as a new asset. Replacing in place is far less disruptive; the two prefabs
   `Yemoja.prefab` and `YemojaDisplay.prefab` both point at it, trident and collider
   included.
