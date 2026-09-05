# Yemoja — Blender handoff

For whoever picks up modelling work on Yemoja next. Written 2026-08-30 at the end
of the mobile-optimisation / Unity-wiring session.

**Current file:** `Backups/Yemoja_WORKING_v111_hairFuzzBake.blend`
**Status: SHIPPED AND CLOSED.** She is exported, imported, wired and verified in
Unity, and Stephanie is moving to animation. There is no outstanding modelling
work. This document exists so that a future change does not undo something.

Authority documents, in this folder:
- `yemoja_manifest.json` — schema 46, **226 numbered rules**. The real spec. Read
  `read_this_first` and `rules_meta` before touching anything.
- `README_unity_import.md` — Unity side. **Section 5 is the only authoritative
  material→texture table.**
- `README_animation_guidelines.md` — rig limits and authoring rules for animation.
Project-wide standards live one level up in `BlenderTools/`
(`README_rig_conventions.md`, `README_eye_standard.md`) — reference, don't restate.

---

## 1. Where she stands

80 bones, **43,078 triangles**, 10 materials, 13 Unity draw calls, overdraw 3.81x,
alpha-tested share 13.2%. Feet rest exactly on z=0.

| Object | Tris | Verts | Modifiers |
|---|---|---|---|
| `Yemoja_Body` | 14,744 | 7,417 | Armature |
| `Yemoja_Clothes` | 9,426 | 5,741 | Armature |
| `Yemoja_Scalp` | 9,302 | 5,735 | Shrinkwrap, Locs_Generator, Hair_Weights, Hair_Material_Split, Armature |
| `Yemoja_LashCards` | 5,024 | 5,184 | Mirror, Armature |
| `Yemoja_Tattoos` | 1,828 | 1,118 | Conform, Armature |
| `Yemoja_BrowCards_A_R` | 1,360 | 1,530 | Mirror, Armature |
| `Yemoja_Nails` | 880 | 620 | Armature |
| `Yemoja_Fuzz` | 298 | 176 | Armature, Shrinkwrap, Fuzz_Lift, Fuzz_Frizz |
| `Yemoja_Eyeliner` | 216 | 226 | Mirror, Armature |

Scene layout: `Yemoja/Yemoja_Character` (the 9 shipping meshes), `Yemoja/Yemoja_Rig`
(Armature), `Yemoja/Yemoja_Source` (locs curves + `JW_*` instancing sources —
**never exports**), `CAMERAS`, `PREVIEW_LIGHTS`.

---

## 2. THE FIVE THINGS THAT WILL BITE YOU

**1. Changing the hair look means re-baking, not just editing nodes.**
Hair colour, alpha and gloss now live in **baked atlases**, because the node graph
(`CardTint`, mask-green multiply, `RootVal_ramp`, `root_fade`, `is_card`) cannot
travel through FBX. Edit `Yemoja_Hair_mat` and Unity will not change. The bake
procedure and its exact formulas are in `hair_fuzz_bake_2026_08_30` in the
manifest; the atlas is addressed by `_BaseMap_ST` tiling (0.5,1) offset (0.5,0),
packing tip cards into u 0..0.35 and tube bodies into u 0.5..0.947. Same for the
fuzz, whose alpha came from a `hairline_fade` vertex colour.

**2. Do not modify `Locs_Generator`.** Two node groups sit *after* it —
`Hair_Bone_Weights` (writes the 20 hair-bone weights) and `Hair_Material_Split`
(assigns `Yemoja_Hair_Opaque_mat` to tube bodies away from the root). That is the
established pattern: **add a new group after it, never edit it.**
`Hair_Material_Split` selects on `v > 0.05`; loosening that threshold lets faces
with a corner in the root-fade zone go opaque and they lose their fade.

**3. The armature has scale 0.01 and sits at Z 0.14324.** Anything parented to it
must copy `matrix_parent_inverse` from an existing child, or use the armature's
inverted world matrix. Setting `matrix_world` before assigning `.parent` does not
survive. The Z translation is the **floor fix** (rule 225) — it is deliberate,
do not zero it.

**4. Her UVs are mirrored.** Body and clothes share UV space left/right, so
asymmetric skin detail is impossible and any paint near the midline must use the
half-and-mirror method. See `CRITICAL_mirrored_uv_atlas` in the manifest.

**5. Blender's images live inside `Assets/CharacterModels/Yemoja/textures` and are
NOT packed.** The folder was cut from 121 textures to 23. Everything left is
either in the manifest's `material_map` or a Blender source. **Do not delete
textures based on Unity references alone** — you will break the .blend (rule 220).

---

## 3. If you change geometry or the rig

- **Weights:** normalise to 1.0 and **limit to 4 influences** before export.
  Unnormalised weights look fine in Blender and deform differently in Unity
  (rule 224). Current body L/R balance is 1.00085 — check the *ratio*, not the
  magnitude.
- **Bone count:** any change forces a Unity `None → Human` avatar reset plus a
  re-audit of the humanoid map. The auto-mapper has grabbed a hair bone for the
  Jaw slot three times; Yemoja has no jaw bone.
- **Marker parenting:** what breaks it is renaming or restructuring the bones the
  markers hang off — not vertex or bone-count changes per se. Verify anyway; it
  fails with no console message.
- **Deformation:** `stress_poses.json` is a text datablock in the .blend holding
  five reference poses (jumping jack, high kick, deep squat, forward lunge,
  backflip) as target limb directions. Re-apply them after any weight change and
  compare surface-area ratios against section 4 of the animation guidelines.
  That is what makes a before/after comparison meaningful.
- **Known unfixed:** the shoulder collapses ~20% in surface area when the arm
  goes overhead *if the clavicle is not animated with it*. That is an animation
  rule, not a rig fault — see the animation guidelines. Clothes penetrate the body
  on the backflip and high kick.

---

## 4. Export

Follow `README_unity_import.md` section 10 exactly — **the order is load-bearing**.
Apply the tattoo `Conform` in rest pose, then the whole scalp stack, and only
**then** remove the `hair_attach` UV layer (it is a live input to
`Locs_Generator`, not a stray). Then fuzz, then the three Mirrors. Exclude
`Yemoja_Source`. `add_leaf_bones = False` — the rig already has explicit end bones.

Save the applied state as its own file; `Yemoja_EXPORT_PREP_v6.blend` is the
precedent, and it is what to diff against if an import looks wrong.

---

## 5. Deliberate — do not "fix"

Lips ship uncoloured. Brows and lashes are near-white (water goddess). Eyeliner
will not read at fight-camera distance; it is a CharacterSelect asset. The loc UVs
are already perfect axis-aligned strips (rule 216). **1,373 body faces under the
clothes are kept on purpose** — Stephanie ruled out deleting them because the
clothes design may still change. `Yemoja_LocGuides_Invisible` keeps a zero-face
material slot on the scalp; leave it.

---

## 6. Open, and small

- Three scratch text datablocks (`cloth_components`, `garment_panel_vids`,
  `violating_verts`) are working data from earlier sessions and can go.
  `stress_poses.json` and `tattoo_builder.py` must stay.
- The modifier named `Hair_Weights` uses the node group `Hair_Bone_Weights` —
  names differ, same thing.
- Rules 217-226 are not yet in `rules_meta.keyword_index`.
- The pearl beads were decimated 50% on 2026-08-30; 64 of 378 carried deliberate
  darker shading which is now approximate. Stephanie accepted this for performance.
