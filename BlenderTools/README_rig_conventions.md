# Rig naming convention - Elementals-Fight

## The rule

**Every rigged model imported into this project is renamed to Blender's mirror
convention before any mesh work begins.**

    mixamorig:LeftArm    ->  mixamorig:Arm.L
    mixamorig:RightHand  ->  mixamorig:Hand.R

Run `BlenderTools/normalize_rig_naming.py` once, immediately after import.
It renames armature bones and the vertex groups of every bound mesh together,
so the armature modifier binding stays intact.

## Why

Blender recognises a bone pair as mirrored only when the side marker is a
suffix (`.L`/`.R`, `_L`/`_R`, `.left`/`.right`) or prefix (`L_`/`R_`).
Mixamo's `LeftArm` / `RightArm` format matches none of these, so every
mirror-aware tool silently fails:

- `Mesh > Symmetrize`
- `Select > Select Mirror`
- Armature X-Axis Mirror
- Vertex Group mirror
- Mirror modifier

**Symmetrize is the dangerous one.** With Mixamo naming it copies vertex groups
verbatim instead of flipping them, so both halves of the body end up weighted to
one side's bones. The mesh looks perfect at rest and only tears apart when posed.

This happened to Yemoja on 2026-07-29: 10,158 vertices - her entire left half -
ended up on right-side bones. Diagnosis took hours because every check of the
*mesh* came back clean; the fault was in the weights, and only visible in pose.

## Unity

Unity's Humanoid mapper handles either convention (its own docs recommend
`arm_L` / `arm_R`), so exports are safe.

After re-importing a renamed rig:
1. Check `Rig > Configure` - confirm every bone maps
2. Re-parent anything attached to a bone **by name** - props, hitbox colliders.
   Those references still point at the old names and will silently detach.
