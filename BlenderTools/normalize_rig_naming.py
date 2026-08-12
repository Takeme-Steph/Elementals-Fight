"""
normalize_rig_naming.py  -  Elementals-Fight

Converts Mixamo-style bone names to Blender's mirror convention.
Run ONCE on every newly imported rigged model, before any mesh work.

    mixamorig:LeftArm       ->  mixamorig:Arm.L
    mixamorig:RightHand     ->  mixamorig:Hand.R
    mixamorig:Spine2        ->  unchanged (no side)

WHY
---
Blender only treats two bones as a mirrored pair when the side marker is a
recognised SUFFIX (.L/.R, _L/_R, .left/.right) or prefix (L_/R_). Mixamo buries
"Left"/"Right" mid-string, which matches none of them, so these silently fail:

    Mesh > Symmetrize            Select > Select Mirror
    Armature X-Axis Mirror       Vertex Group mirror
    Mirror modifier

Symmetrize is the dangerous one: on Mixamo naming it copies vertex groups
verbatim, so BOTH halves end up weighted to one side's bones. The mesh looks
perfect at rest and only tears apart when posed. This cost a long debugging
session on Yemoja (2026-07-29) - 10,158 vertices ended up on the wrong side.

Renames armature bones AND the vertex groups of every mesh bound to it, so the
armature modifier binding is preserved.

UNITY
-----
Unity's Humanoid avatar mapper handles either convention - its own docs
recommend arm_L / arm_R - so this is safe to export. After re-import, confirm
Rig > Configure shows all bones mapped, and re-parent anything attached to a
bone BY NAME (props, hitbox colliders), since those references use the old names.
"""

import bpy

PREFIX = "mixamorig:"


def to_blender(name):
    """mixamorig:LeftArm -> mixamorig:Arm.L   (returns None if no change needed)"""
    if not name.startswith(PREFIX):
        return None
    body = name[len(PREFIX):]
    if body.startswith("Left"):
        return PREFIX + body[4:] + ".L"
    if body.startswith("Right"):
        return PREFIX + body[5:] + ".R"
    return None


def normalize(armature_name="Armature", verbose=True):
    arm = bpy.data.objects.get(armature_name)
    if not arm or arm.type != 'ARMATURE':
        print("[rig_naming] no armature named '%s'" % armature_name)
        return 0

    pairs = []
    for b in arm.data.bones:
        new = to_blender(b.name)
        if new and new != b.name:
            pairs.append((b.name, new))
    if not pairs:
        print("[rig_naming] already normalized - nothing to do")
        return 0

    meshes = [o for o in bpy.data.objects
              if o.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == arm
                                          for m in o.modifiers)]

    # two-pass via temp names so a rename can never collide with an existing name
    for old, _ in pairs:
        arm.data.bones[old].name = "__tmp__" + old
        for o in meshes:
            g = o.vertex_groups.get(old)
            if g:
                g.name = "__tmp__" + old
    for old, new in pairs:
        arm.data.bones["__tmp__" + old].name = new
        for o in meshes:
            g = o.vertex_groups.get("__tmp__" + old)
            if g:
                g.name = new

    # verify: every sided bone must now have a real counterpart
    names = {b.name for b in arm.data.bones}
    orphans = [n for n in names
               if (n.endswith(".L") or n.endswith(".R"))
               and bpy.utils.flip_name(n) not in names]

    if verbose:
        print("[rig_naming] renamed %d bones on '%s'" % (len(pairs), arm.name))
        print("[rig_naming] vertex groups updated on: %s"
              % ", ".join(o.name for o in meshes))
        if orphans:
            print("[rig_naming] WARNING - sided bones with no counterpart: %s" % orphans)
        else:
            print("[rig_naming] all mirror pairs resolve correctly.")
    return len(pairs)


if __name__ == "__main__":
    normalize()
