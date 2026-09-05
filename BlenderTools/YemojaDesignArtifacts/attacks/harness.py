"""
Yemoja attack-clip build harness.
Helpers required by SPEC_attacks.md section "Helpers to write first".

Known Blender 5.x API gap: Action.fcurves was removed. Curves now live at
action.layers[*].strips[*].channelbags[*].fcurves. set_interpolation_5x()
below is the replacement for yemoja_anim_lib.set_interpolation().

Second gap, discovered empirically (not documented anywhere): once an
action is reattached to the armature, calling anything that forces a
depsgraph animation evaluation at a frame that is NOT YET keyed on that
action (frame_set() included, and L.key_pose() calls frame_set()
internally) silently overwrites the pose you just built with the
constant-extrapolated value of the nearest existing keyframe, BEFORE the
insert happens. Verified: building a distinct Hips pose at frame 7 with
the action detached, then reattaching and calling L.key_pose(7) directly,
keys back the frame-1 idle pose instead of the built one. So key() below
keys directly via keyframe_insert with an explicit frame= argument and
never calls frame_set while the action is attached and mid-build.
"""
import bpy, os, math, collections
from mathutils import Vector, Matrix, Quaternion
import common as _common

REVIEW_DIR = "/home/claude/work/attacks/review"
PFX = "mixamorig:"


# --------------------------------------------------------------- feet ---
def snapshot_bone(L, name):
    """World head position and world matrix of a single pose bone, from the
    current pose. Generalises snapshot_feet() to any bone -- SPEC_rebuild_v4.md's
    Kick trident-planted rule needs the same technique applied to Hand.R."""
    A = L.armature()
    bpy.context.view_layer.update()
    pb = A.pose.bones[L.full(name)]
    return dict(head=(A.matrix_world @ pb.head).copy(), matrix=(A.matrix_world @ pb.matrix).copy())


def snapshot_feet(L):
    """World head position and world matrix of Foot.L/R, from the current pose."""
    A = L.armature()
    bpy.context.view_layer.update()
    out = {}
    for side in ("L", "R"):
        pb = A.pose.bones[L.full("Foot." + side)]
        out[side] = dict(
            head=(A.matrix_world @ pb.head).copy(),
            matrix=(A.matrix_world @ pb.matrix).copy(),
            knee=(A.matrix_world @ A.pose.bones[L.full("Leg." + side)].head).copy(),
        )
    return out


def clamp_reach(L, b1, b2, target_world, frac=0.95):
    """If target_world is farther from b1's CURRENT head than
    frac * 0.97 * (len(b1)+len(b2)), pull it back along the same ray so
    limb_ik reports ok=True. Returns (clamped_target_world, was_clamped,
    orig_dist_world, budget_world). Direction from the joint is kept
    (so height and strike direction survive), only reach is shortened."""
    A = L.armature()
    bpy.context.view_layer.update()
    S = A.pose.bones[L.full(b1)].head.copy()
    l1 = A.pose.bones[L.full(b1)].length
    l2 = A.pose.bones[L.full(b2)].length
    Sw = A.matrix_world @ S
    Tw = Vector(target_world)
    v = Tw - Sw
    d = v.length
    budget_world = (l1 + l2) * 0.97 * frac / 100.0
    if d <= budget_world:
        return Tw, False, d, budget_world
    Tw2 = Sw + v.normalized() * budget_world
    return Tw2, True, d, budget_world


def pin_foot(L, side, snap, knee_push=0.4, forward=(0.0, -1.0, 0.0)):
    """Re-solve the leg so Foot.<side> lands back on its snapshot world
    position and orientation, regardless of how the Hips moved. Knee hint
    = the snapshot knee position pushed `knee_push` world units toward
    `forward` (world -Y, i.e. toward the opponent -- matches the idle
    stance's own forward convention)."""
    A = L.armature()
    s = snap[side]
    knee_hint = s["knee"] + Vector(forward) * knee_push
    ok = L.leg_ik(side, s["head"], knee_hint)
    pb = A.pose.bones[L.full("Foot." + side)]
    pb.matrix = A.matrix_world.inverted() @ s["matrix"]
    bpy.context.view_layer.update()
    err = ((A.matrix_world @ pb.head) - s["head"]).length
    assert err < 0.005, "pin_foot(%s) ankle error %.5f >= 0.005" % (side, err)
    return ok, err


def pivot_support_foot(L, side, snap, pivot_deg, knee_push=0.4, forward=(0.0, -1.0, 0.0)):
    """HardKick v2: the support foot pivots on the spot as the hips turn.

    v1 (reverted) applied a rotation to Foot.<side> ALONE, on top of a
    knee_hint that was never rotated -- the knee solve stayed pointed the
    old direction while the foot's orientation was cranked to a new one,
    and the mismatch landed entirely on Foot's own twist (measured -34 deg
    at hip yaw 55, -43 deg at yaw 70; budget 20). This version rotates the
    WHOLE leg -- the knee hint AND the foot's final orientation -- about
    the world-vertical axis through the idle ankle, so the knee solve and
    the forced foot orientation agree and Foot's twist relative to Leg
    stays near idle.

        R  = rotation(world Z, pivot_deg) about the idle ankle (snap head)
        ankle      = idle ankle, unchanged (it's ON the pivot axis)
        knee_hint  = R applied to the (push-forward) idle knee hint,
                     rotated about that same ankle-centered axis
        leg_ik(side, ankle, knee_hint)
        Foot.<side>.matrix = R (rotation only) @ idle Foot matrix,
                     translation left as whatever leg_ik put there (a
                     bone connected to its parent ignores a translation
                     set through .matrix anyway -- see pin_foot).

    Returns (leg_ik ok, ankle position error, Foot twist deg)."""
    import math
    A = L.armature()
    s = snap[side]
    pivot = s["head"]                                    # world, on the axis
    idle_knee_hint = s["knee"] + Vector(forward) * knee_push
    Rw = Matrix.Rotation(math.radians(pivot_deg), 3, Vector((0.0, 0.0, 1.0)))
    knee_hint = pivot + Rw @ (idle_knee_hint - pivot)
    ok = L.leg_ik(side, pivot, knee_hint)
    bpy.context.view_layer.update()
    pb = A.pose.bones[L.full("Foot." + side)]
    idle_rot = s["matrix"].to_3x3()
    new_rot = Rw @ idle_rot
    cur_pos = (A.matrix_world @ pb.matrix).translation
    new_world = new_rot.to_4x4()
    new_world.translation = cur_pos
    pb.matrix = A.matrix_world.inverted() @ new_world
    bpy.context.view_layer.update()
    err = ((A.matrix_world @ pb.head) - pivot).length
    return ok, err


def seg_seg_dist(p1, p2, p3, p4):
    """Minimum distance between segments p1-p2 and p3-p4 (world points)."""
    d1 = p2 - p1
    d2 = p4 - p3
    r = p1 - p3
    a = d1.dot(d1); e = d2.dot(d2); f = d2.dot(r)
    if a <= 1e-12 and e <= 1e-12:
        return (p1 - p3).length
    if a <= 1e-12:
        s_ = 0.0; t_ = min(1.0, max(0.0, f / e))
    else:
        c = d1.dot(r)
        if e <= 1e-12:
            t_ = 0.0; s_ = min(1.0, max(0.0, -c / a))
        else:
            b = d1.dot(d2); denom = a * e - b * b
            s_ = min(1.0, max(0.0, (b * f - c * e) / denom)) if denom > 1e-12 else 0.0
            t_ = (b * s_ + f) / e
            if t_ < 0.0:
                t_ = 0.0; s_ = min(1.0, max(0.0, -c / a))
            elif t_ > 1.0:
                t_ = 1.0; s_ = min(1.0, max(0.0, (b - c) / a))
    cp1 = p1 + d1 * s_
    cp2 = p3 + d2 * t_
    return (cp1 - cp2).length


# -------------------------------------------------------------- audit ---
PFX = "mixamorig:"
_REST_CACHE = {}   # objname -> (face_dom[list of bone names], rest_areas[list])
_REST_CACHE_MOD_STATE = None   # (VERIFY_attacks.md 10c) which preview_mode()
# state the cache was built under, so a caller who audits BEFORE flipping
# preview mode (Scalp/Tattoo shrinkwrap on) doesn't silently get a rest
# baseline captured under the WRONG modifier stack with no error. Every
# audit()/trident_penetration() call goes through _rest_cache(), which
# checks this against the object's current modifier show_viewport state and
# clears the cache on a mismatch instead of trusting a stale one.


def _mod_state(objnames=("Yemoja_Scalp", "Yemoja_Tattoos")):
    out = []
    for nm in objnames:
        ob = bpy.data.objects.get(nm)
        if ob is None:
            continue
        out.append((nm, ob.hide_viewport))
        for m in ob.modifiers:
            out.append((nm, m.name, m.show_viewport))
    return tuple(out)


def _save_pose(A):
    return {pb.name: pb.matrix_basis.copy() for pb in A.pose.bones}


def _restore_pose(A, saved):
    for n, m in saved.items():
        A.pose.bones[n].matrix_basis = m
    bpy.context.view_layer.update()


def _face_dominant_bones(ob):
    gn = {g.index: g.name for g in ob.vertex_groups}
    vert_w = []
    for v in ob.data.vertices:
        vert_w.append([(gn[ge.group], ge.weight) for ge in v.groups
                        if gn[ge.group].startswith(PFX)])
    dom = []
    for p in ob.data.polygons:
        sums = {}
        for vi in p.vertices:
            for bn, w in vert_w[vi]:
                sums[bn] = sums.get(bn, 0.0) + w
        dom.append(max(sums, key=sums.get) if sums else None)
    return dom


def _eval_face_areas(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    areas = [p.area for p in me.polygons]
    ev.to_mesh_clear()
    return areas


def _rest_cache(L, objname):
    global _REST_CACHE_MOD_STATE
    cur_state = _mod_state()
    if _REST_CACHE_MOD_STATE is not None and cur_state != _REST_CACHE_MOD_STATE:
        _REST_CACHE.clear()
    _REST_CACHE_MOD_STATE = cur_state
    hit = _REST_CACHE.get(objname)
    if hit is not None:
        return hit
    A = L.armature()
    ob = bpy.data.objects[objname]
    saved = _save_pose(A)
    L.clear_pose()
    dom = _face_dominant_bones(ob)
    areas = _eval_face_areas(ob)
    _restore_pose(A, saved)
    _REST_CACHE[objname] = (dom, areas)
    return _REST_CACHE[objname]


# Right-hand finger bones are exempt (Stephanie's hand-authored grip, README 14).
def _exempt(bone):
    return bone is not None and bone.startswith(PFX + "Hand") and bone.endswith(".R") \
        and any(f in bone for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"))


def audit(L, objnames=("Yemoja_Body", "Yemoja_Clothes")):
    """Per-dominant-bone area ratio (posed/rest) and crushed-face count
    (ratio < 0.5) for each mesh in objnames, at the CURRENT pose.
    Returns (rows, below_rows, worst5) where rows is every region with
    >=1 face, below_rows is rows with ratio < 0.95 (finger bones on the
    right hand excluded), worst5 is the 5 lowest ratios overall."""
    rows = []
    for objname in objnames:
        dom, rest_areas = _rest_cache(L, objname)
        ob = bpy.data.objects[objname]
        pose_areas = _eval_face_areas(ob)
        per = {}   # bone -> [rest_sum, pose_sum, n, crushed]
        for bn, ra, pa in zip(dom, rest_areas, pose_areas):
            if bn is None:
                continue
            e = per.setdefault(bn, [0.0, 0.0, 0, 0])
            e[0] += ra; e[1] += pa; e[2] += 1
            if ra > 1e-9 and (pa / ra) < 0.5:
                e[3] += 1
        for bn, (ra, pa, n, crushed) in per.items():
            ratio = (pa / ra) if ra > 1e-9 else 1.0
            rows.append(dict(mesh=objname, bone=bn[len(PFX):], ratio=ratio,
                              crushed=crushed, n=n, exempt=_exempt(bn)))
    rows.sort(key=lambda r: r["ratio"])
    below = [r for r in rows if r["ratio"] < 0.95 and not r["exempt"]]
    worst5 = rows[:5]
    return rows, below, worst5


# ------------------------------------------------------------ twist/z ---
def twist_checks(L):
    out = {}
    for n in ("Hand.L", "Hand.R", "Foot.L", "Foot.R"):
        out[n] = _common.twist_deg(L, n)
    return out


def world_delta_to_armature(world_xyz):
    """A world-space displacement -> the (x,y,z) argument loc() expects.

    BUG FIXED 2026-09-03: the original version here returned
    tuple(c*100.0 for c in world_xyz) on the claim that "no rotation [applies]
    between the two frames, only the 0.01 object scale". That is false --
    per SPEC_attacks.md and yemoja_anim_lib's own header, armature space is
    +X her left / +Y up / +Z forward, while world space has +Z up and she
    faces world -Y. So armature +Y (up) IS world +Z, and armature +Z
    (forward) IS world -Y: a 90-degree axis permutation, not identity.
    Verified empirically (scratch_axis.py / scratch_axis2.py in this dir):
    loc("Hips", *old_fn((0,0.1,0))) moved the Hips by world (0,0,+0.1), not
    (0,0.1,0); loc("Hips", *old_fn((0,0,0.1))) moved it by world (0,-0.1,0).
    The old function silently mis-happed every Hips move with a nonzero
    world Y or Z component -- e.g. HardPunch f7's "Hips move
    (-0.15,+0.20,-0.10)" would have come out as a RISE of 0.20 instead of a
    drop of 0.10. Fixed mapping (armature args = world x, world z, -world y),
    reverse-solved from the measured behaviour above and confirmed to
    round-trip exactly for arbitrary (x,y,z)."""
    wx, wy, wz = world_xyz
    return (wx * 100.0, wz * 100.0, -wy * 100.0)


def _seg_point_dist(p, a, b):
    ab = b - a
    t = (p - a).dot(ab) / max(ab.length_squared, 1e-12)
    t = max(0.0, min(1.0, t))
    return (p - (a + ab * t)).length


def _vertex_dominant_bones(ob):
    """Per-VERTEX dominant bone (not per-face) -- needed for the
    nearest-vertex-inside-a-run lookup in trident_penetration()."""
    gn = {g.index: g.name for g in ob.vertex_groups}
    out = []
    for v in ob.data.vertices:
        s = {}
        for ge in v.groups:
            n = gn[ge.group]
            if n.startswith(PFX):
                s[n] = s.get(n, 0.0) + ge.weight
        out.append(max(s, key=s.get) if s else None)
    return out


def _build_bvh(ob):
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); me = ev.to_mesh(); mw = ev.matrix_world
    vs = [mw @ v.co for v in me.vertices]
    ps = [list(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    return BVHTree.FromPolygons(vs, ps, all_triangles=False), vs


def _ray_inside(bvh, p, d):
    n = 0; o = p.copy()
    for _ in range(80):
        h = bvh.ray_cast(o + d * 1e-4, d)
        if h[0] is None:
            break
        n += 1; o = h[0]
    return n % 2 == 1


_PEN_DIRS = [Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)),
             Vector((0.577, 0.577, 0.577)), Vector((-0.577, 0.577, -0.577))]


def _robust_inside(bvh, p):
    return sum(1 for d in _PEN_DIRS if _ray_inside(bvh, p, d)) >= 3


def trident_penetration(L, objname="Yemoja_Body", n_samples=201):
    """SIGNED inside/outside test along the trident shaft (5-direction BVH
    ray-parity, VERIFY_attacks.md section 8's method), replacing the old
    trident_clearance().

    trident_clearance() was an UNSIGNED nearest-vertex distance. It could
    never tell a shaft that is fully buried inside a limb from one that is
    merely close to the surface -- both report a small positive number. v2
    shipped with the shaft passing through the shin at HardKick f16 (0.65
    world units of shaft actually inside the mesh) while trident_clearance
    reported a passing-looking 0.1254, because the nearest SURFACE vertex to
    a point in the middle of a shin is naturally ~0.13 away on a mesh this
    coarse. That is the bug VERIFY_attacks.md section 8 found; this function
    is its fix, deleted-and-replaced per SPEC_fix_v3.md item 13.

    Returns a list of (s0, s1, nearest_bone, nearest_dist) tuples, one per
    contiguous run of the shaft parameter s in [0,1] where the sample point
    tests INSIDE the evaluated body mesh. A run whose nearest_bone is
    Hand.R/ForeArm.R or one of its finger bones is the expected grip contact
    (fingers wrap the shaft); any other bone name is real penetration. An
    empty list means the shaft never enters the body mesh at all."""
    b, t = L.trident_ends()
    ob = bpy.data.objects[objname]
    bvh, vs = _build_bvh(ob)
    vdom = _vertex_dominant_bones(ob)
    runs = []
    cur = None
    for i in range(n_samples):
        s = i / (n_samples - 1)
        p = b + (t - b) * s
        ins = _robust_inside(bvh, p)
        if ins and cur is None:
            cur = [s, s]
        elif ins:
            cur[1] = s
        elif cur is not None:
            runs.append(cur); cur = None
    if cur:
        runs.append(cur)
    out = []
    for r in runs:
        mid = b + (t - b) * ((r[0] + r[1]) / 2.0)
        bi = min(range(len(vs)), key=lambda i: (vs[i] - mid).length_squared)
        bone = (vdom[bi] or "?")
        bone = bone[len(PFX):] if bone.startswith(PFX) else bone
        dist = (vs[bi] - mid).length
        out.append((round(r[0], 3), round(r[1], 3), bone, round(dist, 4)))
    return out


_GRIP_BONE_NAMES = {"Hand.R", "ForeArm.R"} | set(
    "Hand%s%d.R" % (f, j) for f in ("Thumb", "Index", "Middle", "Ring", "Pinky") for j in (1, 2, 3)
)


def trident_penetration_bad(L, objname="Yemoja_Body", n_samples=201):
    """trident_penetration() filtered to runs that are NOT the expected grip
    contact -- i.e. real penetration into a limb/torso the shaft should
    clear. Empty list = clean."""
    return [r for r in trident_penetration(L, objname, n_samples) if r[2] not in _GRIP_BONE_NAMES]


def lowest_world_z_excluding(L, objname, exclude_bones):
    """Like L.lowest_world_z but ignores vertices whose dominant bone is in
    exclude_bones (e.g. the kicking foot, which is meant to be off the
    ground)."""
    dom, _ = _rest_cache(L, objname)
    ob = bpy.data.objects[objname]
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); me = ev.to_mesh(); mw = ev.matrix_world
    excl_full = set(L.full(b) for b in exclude_bones)
    excluded_vidx = set()
    for p, bn in zip(ob.data.polygons, dom):
        if bn in excl_full:
            excluded_vidx.update(p.vertices)
    lo = 1e9
    for v in me.vertices:
        if v.index in excluded_vidx:
            continue
        z = (mw @ v.co).z
        if z < lo:
            lo = z
    ev.to_mesh_clear()
    return lo


# ---------------------------------------------------------- interpolation ---
def _all_channelbags(action):
    """VERIFY_attacks.md 10b: action.layers[*].strips[*].channelbags is the
    5.0 shape and was unguarded. Falls back to strip.channelbag(slot) per
    action slot (the shape VERIFY guessed 5.2 might use) so a future Blender
    raises somewhere obvious in this one function instead of mid-build with
    some actions already written, rather than pretending to support a shape
    that has never actually been run against."""
    out = []
    for lyr in action.layers:
        for st in lyr.strips:
            cbs = getattr(st, "channelbags", None)
            if cbs is not None:
                out.extend(cbs)
                continue
            get_cb = getattr(st, "channelbag", None)
            if get_cb is None:
                continue
            for slot in getattr(action, "slots", []):
                cb = None
                try:
                    cb = get_cb(slot)
                except TypeError:
                    try:
                        cb = get_cb(slot, ensure=False)
                    except Exception:
                        cb = None
                if cb is not None:
                    out.append(cb)
    return out


def set_interpolation_5x(action, mode="BEZIER", handle="AUTO_CLAMPED"):
    for cb in _all_channelbags(action):
        for fc in cb.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = mode
                kp.handle_left_type = kp.handle_right_type = handle


def _clear_action(action):
    for cb in _all_channelbags(action):
        for fc in list(cb.fcurves):
            cb.fcurves.remove(fc)


# --------------------------------------------------------------- keying ---
def key(L, frame):
    """Key every humanoid bone's rotation plus Hips location at `frame`,
    reading whatever pose is CURRENTLY on the rig. Deliberately does not
    call L.key_pose()/frame_set() -- see module docstring. Used by
    enforce_pins() (which needs to key a specific already-posed frame, not
    a freshly-snapshotted one); build_clip() uses key_from_snapshot()
    instead -- see that function for why."""
    A = L.armature()
    bones = L.humanoid_bones(A)
    for n in bones:
        A.pose.bones[n].keyframe_insert("rotation_quaternion", frame=frame, group=n)
    A.pose.bones[L.full("Hips")].keyframe_insert("location", frame=frame, group=L.full("Hips"))


def _snapshot_pose(L):
    """{bone_name: matrix_basis} for every humanoid bone, read RIGHT NOW."""
    A = L.armature()
    return {n: A.pose.bones[n].matrix_basis.copy() for n in L.humanoid_bones(A)}


def key_from_snapshot(L, frame, snap):
    """Key `frame` from a matrix_basis snapshot dict (from _snapshot_pose),
    not from whatever is currently live on the rig (VERIFY_attacks.md 10a:
    build_clip used to reattach the action and then call keyframe_insert
    231 times trusting that nothing re-evaluates the animation in between
    and silently overwrites the hand-built pose first -- true in 5.0.1, not
    guaranteed. This re-applies the snapshot's matrix_basis to every bone
    immediately before inserting, so even if reattaching the action did
    something to the live pose, the values actually keyed are the ones the
    pose-building function produced, not whatever the rig happened to show
    a moment later).

    BUG FIXED 2026-09-03: this used to call bpy.context.view_layer.update()
    between the matrix_basis loop and the keyframe_insert loop, WHILE the
    action was already reattached (build_clip reattaches right before
    calling this). That is exactly "calling anything that forces a
    depsgraph animation evaluation at a frame that is NOT YET keyed on
    that action" -- the module docstring's own documented hazard -- and
    for every key after the clip's first, it did: evaluate the
    just-reattached, still-mostly-empty action at the current frame,
    which (only the earlier keyframes existing so far, CONSTANT
    extrapolation) silently OVERWROTE the just-set matrix_basis with the
    nearest earlier keyframe's value, one call before keyframe_insert
    would have captured it. Measured effect: every key of every clip
    after the first collapsed to the first key's pose (verified via raw
    fcurve keyframe_points, not evaluation -- e.g. HardKick's Hips.location
    and Spine1/Hand.R.rotation_quaternion were bit-identical across
    f1/6/12/16/22/28) -- the actions rendered/reported as if the poses
    differed only because build-time asserts and report()'s own
    frame_set() reads happened against LIVE pose state, which the bug
    never touched; only the KEYED data was silently frozen at frame 1.
    Fix: matrix_basis's setter synchronously recomputes
    rotation_quaternion/location (verified empirically -- no depsgraph
    evaluation needed), so the update() call is both unnecessary and the
    one thing here that was unsafe. Removed."""
    A = L.armature()
    for n, m in snap.items():
        A.pose.bones[n].matrix_basis = m
    for n in snap:
        A.pose.bones[n].keyframe_insert("rotation_quaternion", frame=frame, group=n)
    A.pose.bones[L.full("Hips")].keyframe_insert("location", frame=frame, group=L.full("Hips"))


def build_clip(L, apply_json_pose, name, keys):
    """keys: ordered [(frame, fn), ...]. fn() builds the pose from a fresh
    idle master (apply_json_pose is called for you first). Keys every
    humanoid bone at every listed frame; frames not listed interpolate."""
    A = L.armature()
    act = L.get_action(name)
    _clear_action(act)
    for frame, fn in keys:
        A.animation_data.action = None          # detach: safe to move the frame / build
        bpy.context.scene.frame_set(frame)
        apply_json_pose(L)
        fn()
        bpy.context.view_layer.update()
        snap = _snapshot_pose(L)                # capture while still detached -- see key_from_snapshot
        A.animation_data.action = act            # reattach only to key -- no frame_set after this
        key_from_snapshot(L, frame, snap)
    set_interpolation_5x(act)
    return act


# ------------------------------------------------ quaternion hemisphere ---
def fix_quaternion_hemispheres(action):
    """SPEC_fix_v3.md item 1 / VERIFY_attacks.md section 0: Blender
    interpolates rotation_quaternion component-wise. When two consecutive
    keys on the same bone land in opposite quaternion hemispheres (their
    4-vector dot product is negative), that represents the SAME rotation
    but interpolation takes the long way round -- this is what sent
    HardKick's kicking ankle 2+ world units backward between f6 and f12,
    and swung HardPunch's trident wide between f7 and f12 (VERIFY section
    0). Fix: walk every bone's 4 rotation_quaternion curves in ascending
    keyframe order; whenever a key's quaternion has negative dot with the
    PREVIOUS key's, negate the whole quaternion (all 4 components) at that
    key -- q and -q represent the identical rotation, so this changes
    nothing about any keyed pose, only which hemisphere-continuous path
    interpolation takes between them.
    Returns (n_flips, [(bone, frame), ...]) for reporting."""
    n_flipped = 0
    flips = []
    for cb in _all_channelbags(action):
        groups = collections.defaultdict(dict)
        for fc in cb.fcurves:
            if fc.data_path.endswith("rotation_quaternion"):
                groups[fc.data_path][fc.array_index] = fc
        for path, comps in groups.items():
            if len(comps) != 4:
                continue
            curves = [comps[i] for i in range(4)]
            n = len(curves[0].keyframe_points)
            prev = None
            for k in range(n):
                cur = [curves[i].keyframe_points[k].co[1] for i in range(4)]
                frame = curves[0].keyframe_points[k].co[0]
                if prev is not None:
                    dot = sum(a * b for a, b in zip(prev, cur))
                    if dot < 0:
                        for i in range(4):
                            kp = curves[i].keyframe_points[k]
                            kp.co[1] = -kp.co[1]
                            hl = kp.handle_left; hl[1] = -hl[1]; kp.handle_left = hl
                            hr = kp.handle_right; hr[1] = -hr[1]; kp.handle_right = hr
                        cur = [-c for c in cur]
                        n_flipped += 1
                        bone = path.split('"')[1] if '"' in path else path
                        flips.append((bone[len(PFX):] if bone.startswith(PFX) else bone, frame))
                prev = cur
            for c in curves:
                c.update()
    return n_flipped, flips


_TWIST_BUDGET = {"Hand.L": 30.0, "Hand.R": 30.0, "Foot.L": 20.0, "Foot.R": 20.0}


def fix_twist_overshoot(L, action, bones=("Hand.L", "Hand.R", "Foot.L", "Foot.R"),
                         budget=None, max_iter=4):
    """Found running eval_all.py's per-frame arc scan (the general mechanism
    item 1 asks for), on top of fix_quaternion_hemispheres and enforce_pins:
    two DIFFERENT ways a bone's twist can exceed budget on a frame nobody
    explicitly built, neither of which is a hemisphere-sign flip (dot >= 0
    at every key already, post fix_quaternion_hemispheres):

    1. A large free-spin difference between two individually-fine keys (e.g.
       HardPunch's Hand.R hint differs ~150deg between the f7 windup and f12
       thrust -- BUILD_NOTES documents both were swept independently for
       their OWN twist/clearance budget, not for closeness to each other).
       Even a from-scratch Quaternion.slerp between the two keys' actual
       quaternions sweeps a large twist somewhere in the middle when this
       happens (verified: q7.dot(q12)=0.053, ~174 deg apart as unit
       quaternions) -- interpolation METHOD isn't the cause, the two
       endpoints genuinely require a big spin to connect. HardPunch's f9
       spike (-87.3 deg) was this case and has its own targeted fix in
       attacks_build.py's _hardpunch_hand_twist_check_fix() (which knows
       the actual hint values, so it rebuilds the pose correctly instead of
       just interpolating raw numbers).
    2. Plain Bezier/AUTO_CLAMPED overshoot on a long (5+ frame) gap between
       two ordinary keys, even though AUTO_CLAMPED is supposed to prevent a
       curve from overshooting past its neighbouring keyframe VALUES at a
       local extremum -- measured on HardKick's Hand.R between the f22 key
       (26.3 deg) and the f28 idle key (29.9 deg): the curve overshoots to
       30.3 deg at f25 before easing back down to 29.9 at f28, 0.3 deg over
       the 30 deg budget on two plain interpolated frames with no posing
       reason behind them at all.

    This function is the general backstop for case 2 (and would also catch
    case 1 if a clip-specific fix like HardPunch's didn't already run first
    -- run it AFTER any such targeted fixes, which take precedence since
    they know the actual intended pose, not just curve numbers). For each
    named bone, repeatedly finds the worst-violating NON-KEYED frame between
    two consecutive existing keyframes and inserts a new keyframe there
    using Quaternion.slerp between those two keys' own quaternions at the
    matching fractional position -- shortening the gap is what removes the
    overshoot (a slerp between two already-close, hemisphere-matched
    quaternions has nothing to overshoot). Does not touch any other bone's
    POSE, but (SPEC_fix_v5.md item 2) it now keys the FULL humanoid set +
    Hips location at the fixed frame from a normalised snapshot -- the
    original version inserted a keyframe on ONLY this bone's own 4 curves
    directly (`curves[ci].keyframe_points.insert(...)`), which is exactly
    the partial-keying pattern VERIFY_attacks_v4.md found (Hand.L/Hand.R/
    Foot.L/Foot.R keyed at frames no other bone shares). Returns
    [(bone, frame, twist_before), ...] for reporting."""
    budget = budget or _TWIST_BUDGET
    A = L.armature()
    act_ref = action
    fixed = []
    for cb in _all_channelbags(action):
        for bone in bones:
            full = L.full(bone)
            path = 'pose.bones["%s"].rotation_quaternion' % full
            curves = {}
            for fc in cb.fcurves:
                if fc.data_path == path:
                    curves[fc.array_index] = fc
            if len(curves) != 4:
                continue
            curves = [curves[i] for i in range(4)]
            lim = budget.get(bone, 30.0)
            for _pass in range(max_iter):
                kps = sorted(curves[0].keyframe_points, key=lambda k: k.co[0])
                frames = [round(k.co[0]) for k in kps]
                worst = None
                for i in range(len(frames) - 1):
                    f0, f1 = frames[i], frames[i + 1]
                    if f1 - f0 < 2:
                        continue
                    for f in range(f0 + 1, f1):
                        w = curves[0].evaluate(f); x = curves[1].evaluate(f)
                        y = curves[2].evaluate(f); z = curves[3].evaluate(f)
                        n = math.sqrt(w * w + x * x + y * y + z * z)
                        if n < 1e-9:
                            continue
                        w, x, y, z = w / n, x / n, y / n, z / n
                        tw = math.degrees(2 * math.atan2(y, w))
                        if abs(tw) > lim and (worst is None or abs(tw) > abs(worst[1])):
                            worst = (f, tw, i)
                if worst is None:
                    break
                f, tw, i = worst
                f0, f1 = frames[i], frames[i + 1]
                q0 = Quaternion((curves[0].keyframe_points[i].co[1], curves[1].keyframe_points[i].co[1],
                                 curves[2].keyframe_points[i].co[1], curves[3].keyframe_points[i].co[1]))
                q1 = Quaternion((curves[0].keyframe_points[i + 1].co[1], curves[1].keyframe_points[i + 1].co[1],
                                 curves[2].keyframe_points[i + 1].co[1], curves[3].keyframe_points[i + 1].co[1]))
                q0.normalize(); q1.normalize()
                t = (f - f0) / float(f1 - f0)
                qf = q0.slerp(q1, t)
                # Evaluate the FULL pose at f (action still attached -- safe,
                # every real key already exists), then override just this
                # bone's rotation and key the whole snapshot.
                A.animation_data.action = act_ref
                bpy.context.scene.frame_set(f)
                bpy.context.view_layer.update()
                A.animation_data.action = None
                A.pose.bones[full].matrix_basis = qf.to_matrix().to_4x4()
                bpy.context.view_layer.update()
                snap = _snapshot_pose(L)
                A.animation_data.action = act_ref
                key_from_snapshot(L, f, snap)
                fixed.append((bone, f, tw))
    set_interpolation_5x(action)
    return fixed


def add_arc_breakdowns(L, action, bones=("Foot.L", "Foot.R", "Hand.L", "Hand.R"),
                        max_step=0.9, max_iter=8):
    """SPEC_fix_v3 item 1's arc check: 'the per-frame step of any ankle or
    wrist must be < 0.9 world units' at EVERY frame, not only keys.
    fix_quaternion_hemispheres removes the hemisphere-driven blowups
    VERIFY_attacks.md measured (2.5-2.7 world unit jumps on HardKick's
    Foot.R before that fix) -- but on gaps that are still fairly wide (a
    few real keys bracketing a large hip-yaw sweep, e.g. HardKick f16->f22
    or Kick's kicking-foot arc) the surviving, hemisphere-correct
    interpolation can still take a single step at or above the 0.9 budget.
    Pure re-sampling: no pose is changed, every value already keyed is
    untouched -- walks each named bone's world head position frame by
    frame, and wherever the step from f-1 to f is >= max_step, bisects the
    REAL/breakdown key pair bracketing that step and inserts a new
    breakdown key at the midpoint, snapshotting whatever pose the
    already-correct, already-smooth curve shows there (key_from_snapshot,
    same capture-not-correct mechanism enforce_pins uses for its own
    breakdown inserts). Two smaller steps replace one large one; repeated
    to a cap, this reliably drives the max step under budget in a few
    passes without altering any existing pose. Returns the sorted list of
    frames added (folded into the clip's breakdown-frame report)."""
    A = L.armature()
    added = []
    for _pass in range(max_iter):
        A.animation_data.action = action
        f0i, f1i = int(round(action.frame_range[0])), int(round(action.frame_range[1]))
        positions = {}
        for b in bones:
            pos = []
            for f in range(f0i, f1i + 1):
                bpy.context.scene.frame_set(f)
                bpy.context.view_layer.update()
                pb = A.pose.bones[L.full(b)]
                pos.append((f, (A.matrix_world @ pb.head).copy()))
            positions[b] = pos
        worst = None
        for b in bones:
            seq = positions[b]
            for i in range(1, len(seq)):
                fprev, pprev = seq[i - 1]
                fcur, pcur = seq[i]
                d = (pcur - pprev).length
                # Only a subdividable violation (its two frames are not
                # already adjacent integers) can be improved by inserting a
                # breakdown key between them -- an adjacent-frame violation
                # is a real one-frame pose change with no room to bisect,
                # so it must not block the search for OTHER, fixable
                # violations (bug found here: the original version picked
                # the single largest-d violation regardless of whether it
                # was subdividable, and broke out of the whole pass the
                # first time that happened to be an adjacent pair --
                # silently abandoning every other bone's fixable violation
                # in the same pass).
                if fcur - fprev < 2:
                    continue
                if d >= max_step and (worst is None or d > worst[0]):
                    worst = (d, b, fprev, fcur)
        if worst is None:
            break
        d, b, fprev, fcur = worst
        fmid = (fprev + fcur) // 2
        bpy.context.scene.frame_set(fmid)
        bpy.context.view_layer.update()
        snap = _snapshot_pose(L)
        key_from_snapshot(L, fmid, snap)
        set_interpolation_5x(action)
        added.append(fmid)
    return sorted(set(added))


def fix_ankle_arc_monotonic(L, action, bone, side_leg, f0, f1, axis="y", sign=-1,
                             knee_hint_bone="Leg.R", tol=0.01, max_iter=6):
    """SPEC_fix_v3 item 1's other arc requirement: HardKick's right ankle
    must move monotonically forward-and-up from f6 to f12 (no frame with
    world y greater than the previous frame's). fix_quaternion_hemispheres
    removed the hemisphere-driven reversal VERIFY_attacks.md measured (the
    foot travelling BEHIND her, f8, and 2+ world-unit snaps, f19-f20) --
    what is left after that fix is much smaller (order 0.2-0.4 world units)
    but is a GENUINE property of the two literal SPEC_hardkick_v2.md
    endpoints at f6/f12 (ankle (-1.15,0.80,2.2) -> (0.10,-1.60,5.85)):
    the natural IK arc between them reaches further forward (more negative
    y) around f9-f10 than f12's own less-forward target, so any path
    connecting them exactly must ease back in y approaching f12 -- not an
    interpolation artifact. This clamps that residual dip: at each frame
    strictly between f0 and f1 where `axis` has moved the WRONG way
    (world y greater than the previous already-fixed frame's, when
    sign=-1 means 'forward' is decreasing y), it re-solves ONLY the named
    leg (leg_ik, keeping the knee hint direction from the frame's own
    current pose) to a target with that axis clamped back to the previous
    frame's value, leaving the other two axes as the curve already had
    them, then re-keys the WHOLE pose (key_from_snapshot) as a breakdown.
    Processes frames in increasing order so each fix's 'previous frame' is
    itself already monotonic. Returns the sorted list of frames touched."""
    A = L.armature()
    A.animation_data.action = action
    touched = []
    prev_val = None
    for f in range(f0, f1 + 1):
        fixed_here = False
        for _iter in range(max_iter):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            pb = A.pose.bones[L.full(bone)]
            pos = A.matrix_world @ pb.head
            val = getattr(pos, axis)
            if prev_val is None or sign * (val - prev_val) >= -tol:
                break
            # violation: clamp this axis back to prev_val (minus a hair of
            # margin) and re-solve the leg with the other two axes as-is.
            clamped = prev_val - sign * tol
            tgt = Vector(pos)
            setattr(tgt, axis, clamped)
            kpb = A.pose.bones[L.full(knee_hint_bone)]
            hint = (A.matrix_world @ kpb.head).copy()
            L.leg_ik(side_leg, tgt, hint)
            bpy.context.view_layer.update()
            fixed_here = True
        if fixed_here:
            snap = _snapshot_pose(L)
            key_from_snapshot(L, f, snap)
            set_interpolation_5x(action)
            touched.append(f)
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
        pos = A.matrix_world @ A.pose.bones[L.full(bone)].head
        prev_val = getattr(pos, axis)
    return touched


# ------------------------------------------------------- between-key pins ---
def enforce_pins(L, name, fix_frame, check_frame, max_iter=6):
    """SPEC_fix_v3.md item 4. Keys only fix the poses AT keys -- Blender's
    quaternion/linear interpolation between them can still let a pinned
    support ankle drift or the lowest body z sink below the floor on
    frames nobody looked at (VERIFY_attacks.md section 4: e.g. HardPunch
    floor sink -0.0098 @f9, rear foot skate 0.144 @f10, neither a keyed
    frame). This scans every frame of `name`'s action; wherever
    `check_frame(f)` reports a violation, it captures the CURRENT
    interpolated pose (the action is briefly detached so it's safe to
    modify -- same reasoning as build_clip), calls `fix_frame(f, detail)`
    to correct it in place (clip-specific: full pin_foot-style re-solve
    for Punch/HardPunch's static support foot, settle_floor-style for
    Kick, position-only leg_ik reposition for HardKick's pivoting support
    foot -- built by the caller in attacks_build.py, not here), and keys
    ALL humanoid bones at that frame via key() so the correction becomes
    a real breakdown keyframe rather than a one-frame fix nothing else
    knows about. Repeats (fixing on frame N can perturb the interpolation
    into frame N-1/N+1) up to `max_iter` passes, stopping early once a
    full pass fixes nothing.

    check_frame(f) -> (violated: bool, detail) -- detail is whatever
    fix_frame needs (e.g. which side, the target point); may be None.
    fix_frame(f, detail) -> bool -- True if it actually changed the pose
    (a breakdown key should be added), False if there was nothing to do
    after all.

    Returns (sorted list of frames a breakdown key was added at,
    [n_fixed per pass])."""
    A = L.armature()
    act = bpy.data.actions[name]
    added = set()
    pass_counts = []
    for _pass in range(max_iter):
        A.animation_data.action = act
        f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
        n_fixed = 0
        for f in range(f0, f1 + 1):
            A.animation_data.action = act
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            violated, detail = check_frame(f)
            if not violated:
                continue
            A.animation_data.action = None       # detach: safe to rebuild this frame's pose
            changed = fix_frame(f, detail)
            bpy.context.view_layer.update()
            # SPEC_fix_v5.md item 2: key the FULL humanoid set from a
            # matrix_basis snapshot (guaranteed unit-quaternion, since it's
            # read straight off each bone's own posed matrix), never the old
            # key(L,f) -- which used native keyframe_insert reading
            # pose.bones[n].rotation_quaternion directly, and VERIFY_attacks_v4
            # found that property can still reflect a stale/un-normalised
            # ANIMATED (interpolated-channel) value at the instant the action
            # is reattached, not the value fix_frame() just set -- the exact
            # "|q|=0.735, component-wise midpoint" signature it measured, plus
            # the partial-keying this same swap fixes structurally (a full
            # snapshot always covers all 57 bones, never a subset).
            snap = _snapshot_pose(L)               # captured DETACHED, still current
            A.animation_data.action = act          # reattach only to key
            if changed:
                key_from_snapshot(L, f, snap)
                added.add(f)
                n_fixed += 1
        pass_counts.append(n_fixed)
        if n_fixed == 0:
            break
    set_interpolation_5x(act)
    return sorted(added), pass_counts


# ------------------------------------------------- generic hinge solves ---
def bisect_lrot(L, bone, axis, metric_fn, target, lo=-90.0, hi=90.0, iters=30):
    """Find the local-`axis` rotation (deg) of `bone` (on top of its CURRENT
    matrix_basis) that makes metric_fn() == target, by bisection. metric_fn
    must be monotonic in the rotation angle over [lo,hi] -- if it isn't
    (lo/hi don't bracket a sign change), falls back to a coarse 3-degree
    scan for the closest match. Leaves the bone at the solved angle and
    returns it."""
    A = L.armature()
    pb = A.pose.bones[L.full(bone)]
    base = pb.matrix_basis.copy()

    def set_ang(ang):
        pb.matrix_basis = base.copy()
        L.lrot(bone, axis, ang)
        bpy.context.view_layer.update()

    set_ang(lo); dlo = metric_fn() - target
    set_ang(hi); dhi = metric_fn() - target
    if dlo == 0:
        set_ang(lo); return lo
    if dhi == 0:
        set_ang(hi); return hi
    if (dlo > 0) == (dhi > 0):
        best_ang, best_err = lo, abs(dlo)
        a = lo
        while a <= hi:
            set_ang(a); e = abs(metric_fn() - target)
            if e < best_err:
                best_err, best_ang = e, a
            a += 3.0
        set_ang(best_ang)
        return best_ang
    for _ in range(iters):
        mid = (lo + hi) / 2.0
        set_ang(mid); dmid = metric_fn() - target
        if (dmid > 0) == (dlo > 0):
            lo, dlo = mid, dmid
        else:
            hi, dhi = mid, dmid
    ang = (lo + hi) / 2.0
    set_ang(ang)
    return ang


def toe_shin_angle(L, side):
    """Angle (deg) between the shin (Leg.<side> head -> Foot.<side> head)
    and the toe (Foot.<side> head -> ToeBase.<side> tail), world space."""
    A = L.armature()
    bpy.context.view_layer.update()
    leg_h = A.matrix_world @ A.pose.bones[L.full("Leg." + side)].head
    foot_h = A.matrix_world @ A.pose.bones[L.full("Foot." + side)].head
    toe_t = A.matrix_world @ A.pose.bones[L.full("ToeBase." + side)].tail
    shin = (foot_h - leg_h).normalized()
    toe_v = (toe_t - foot_h).normalized()
    dot = max(-1.0, min(1.0, shin.dot(toe_v)))
    return math.degrees(math.acos(dot))


def articulate_toe(L, side, target_angle_deg):
    """Bisect Foot.<side>'s local-X rotation until toe_shin_angle hits
    target_angle_deg. Used for Kick f7's ball-of-foot-leading impact and
    (paired with a local-Y detwist below) HardKick's plantarflex fixes."""
    return bisect_lrot(L, "Foot." + side, "X", lambda: toe_shin_angle(L, side), target_angle_deg)


def plantarflex_and_detwist(L, side, target_toe_angle=10.0, target_twist=0.0):
    """HardKick f12/f16 (SPEC_fix_v3.md item 6): leg_ik only orients
    UpLeg/Leg, leaving Foot/ToeBase at their idle-relative angle, which
    reads badly once the shin has swung to head height. A local-X pitch
    fixes the toe angle but pushes the foot's OWN twist (measured relative
    to its parent Leg bone, which at these extreme leg poses is already
    non-zero) over budget; a local-Y spin on top cancels the twist back out
    (twist is ~1:1 sensitive to local Y, which barely moves the toe angle).
    Bisects X first, then Y on top of the X result. Returns (ang_x, ang_y)."""
    ang_x = bisect_lrot(L, "Foot." + side, "X", lambda: toe_shin_angle(L, side), target_toe_angle)
    ang_y = bisect_lrot(L, "Foot." + side, "Y", lambda: _common.twist_deg(L, "Foot." + side), target_twist)
    return ang_x, ang_y


def flatten_foot(L, side, target_drop):
    """Bisect Foot.<side>'s local-X rotation until (ToeBase tail z - Foot
    head z) == target_drop -- used to make a foot sole-flat by matching the
    OTHER (already-flat) foot's own idle ankle-to-toe height drop."""
    def metric():
        A = L.armature()
        head = A.matrix_world @ A.pose.bones[L.full("Foot." + side)].head
        tail = A.matrix_world @ A.pose.bones[L.full("ToeBase." + side)].tail
        return tail.z - head.z
    return bisect_lrot(L, "Foot." + side, "X", metric, target_drop)


def settle_floor(L, ankle_target, side, target_drop, knee_hint, tol=0.005, max_iter=10, exclude=None):
    """SPEC_fix_v3.md item 3 (Kick: plant her). Solve `side` leg to the
    WORLD `ankle_target`, flatten the foot to `target_drop` (see
    flatten_foot), then iterate until the support foot's own sole sits
    within `tol` of the floor (z=0). Returns (ok, foot_pitch_deg,
    lowest_z).

    BUG FIXED 2026-09-04 (fix round v3, found verifying item 3, not itself
    a spec item): the original version iterated HIPS z, re-solving `side`'s
    leg_ik to the SAME FIXED ankle_target every pass. leg_ik always places
    the ankle at exactly ankle_target in WORLD space regardless of where
    Hips sits -- solving to a fixed absolute target is the whole point of
    pin-style IK -- so moving Hips and re-solving to the identical target
    changed nothing about the support foot's own height; only whatever OTHER
    geometry happened to be dragged along with the Hips shift (in practice,
    the not-yet-posed kicking leg -- see attacks_build.KICK_LEG_L_FULL)
    could move, and once that was excluded too, the loop just sat at a
    stable, wrong answer. Measured: Kick f7's support foot converged (self-
    consistently, but incorrectly) to lowest-excl-kicking-foot 0.0457 for
    every iteration from the 3rd through the 10th -- 9x the +0.005 upper
    tolerance actually wanted here, unmoving, because nothing was actually
    happening to it. The one DOF that genuinely determines the support
    sole's height, once the foot is flattened, is ankle_target.z itself
    (the initial value is only an ESTIMATE -- idle ankle height minus the
    idle-stance's own raised-heel offset -- and flatten_foot's rotation
    doesn't reproduce that estimate exactly). Fixed: iterate ankle_target's
    OWN z (subtract the measured excess each pass, re-solve+reflatten to the
    ADJUSTED target) instead of Hips alone -- but ankle-only movement isn't
    enough either: Kick f7 additionally rotates Hips (X -6, on top of the
    chamber/impact Y yaw) before calling this, which independently pushes
    the support leg toward the edge of its own max reach (measured d at
    ~99.9% of (l1+l2) even at the FIRST, unadjusted target) -- lowering
    ONLY the ankle target from there increases hip-to-target distance and
    immediately clamps (leg_ik ok=False), so each pass barely moves the
    sole (measured: 10 iterations only crept from 0.070 to 0.035, nowhere
    near the 0.005 tolerance). Fixed properly: drop Hips AND the ankle
    target (AND the knee hint, so the pole direction doesn't drift) by the
    SAME `-lo` each pass. That keeps the hip-to-target distance, hence
    reachability, unchanged (translating the whole leg assembly rigidly),
    while the ABSOLUTE ankle/sole height genuinely drops by `lo` per pass
    -- converges in 1-2 iterations even when the leg is already near full
    stretch. x/y of both Hips and the target are untouched, matching the
    spec's "keep the ankle x/y at idle" -- only z moves on both ends
    together.

    `exclude`, if given, is a bone-name list passed to
    lowest_world_z_excluding instead of using the plain (whole-body)
    lowest_world_z -- must cover the WHOLE other leg, not just its
    foot/toe, whenever that leg (e.g. Kick's kicking leg) is still at its
    unposed idle orientation when this runs: excluding only the foot/toe
    leaves the idle-shaped shin/thigh, hanging from whatever Hips this
    call is given, as the apparent "lowest point", which has nothing to do
    with `side`'s own sole and would silently corrupt convergence."""
    def _lo():
        if exclude:
            return lowest_world_z_excluding(L, "Yemoja_Body", exclude)
        return L.lowest_world_z()
    tgt = Vector(ankle_target)
    hint = Vector(knee_hint)
    ok = L.leg_ik(side, tgt, hint)
    ang = flatten_foot(L, side, target_drop)
    bpy.context.view_layer.update()
    lo = _lo()
    for _ in range(max_iter):
        if abs(lo) < tol:
            break
        dx, dy, dz = world_delta_to_armature((0.0, 0.0, -lo))
        L.loc("Hips", dx, dy, dz)
        tgt = Vector((tgt.x, tgt.y, tgt.z - lo))
        hint = Vector((hint.x, hint.y, hint.z - lo))
        ok = L.leg_ik(side, tgt, hint)
        ang = flatten_foot(L, side, target_drop)
        bpy.context.view_layer.update()
        lo = _lo()
    return ok, ang, lo


def chest_local_target(L, forward, down, left, bone="Spine2"):
    """SPEC_fix_v3.md item 8: a world-space point offset from `bone`'s
    CURRENT POSED origin along ITS OWN current forward/up/left axes (bone
    local +Z/+Y/+X respectively, matching lean.py's own "chest fwd"
    convention and the rig-wide +X=her-left/+Y=up/+Z=forward axis
    labelling) -- so a target specified this way tracks the chest as the
    hips yaw 75-95 deg during HardKick, instead of sitting at a fixed WORLD
    point the real chest has rotated away from."""
    A = L.armature()
    bpy.context.view_layer.update()
    pb = A.pose.bones[L.full(bone)]
    R = A.matrix_world.to_3x3() @ pb.matrix.to_3x3()
    fwd = (R @ Vector((0, 0, 1))).normalized()
    up = (R @ Vector((0, 1, 0))).normalized()
    lft = (R @ Vector((1, 0, 0))).normalized()
    origin = A.matrix_world @ pb.head
    return origin + fwd * forward - up * down + lft * left


def head_angle_to_z(L):
    """Angle (deg) between the Head bone's current world forward direction
    (its local +Z, matching the rig-wide forward convention) and armature
    +Z expressed in world space -- VERIFY_attacks.md section 9's "face
    keeps pointing +Z" measurement."""
    A = L.armature()
    bpy.context.view_layer.update()
    arm_z_world = (A.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()
    head_fwd = (A.matrix_world.to_3x3() @ (A.pose.bones[L.full("Head")].matrix.to_3x3() @ Vector((0, 0, 1)))).normalized()
    dot = max(-1.0, min(1.0, head_fwd.dot(arm_z_world)))
    return math.degrees(math.acos(dot))


def aim_head(L, target_angle=0.0, tol=0.5, max_iter=6):
    """Rotate Neck then Head so head_angle_to_z lands within `tol` of
    `target_angle` -- used at every key to keep the face on the opponent
    (SPEC_fix_v3.md item 5). Only target_angle=0.0 (every call site in this
    file) is supported; the correction aims the head's local +Z exactly at
    armature +Z in world space.

    BUG FIXED 2026-09-03: the original version bisected an EXTRA local-Y
    rotation (a twist about the bone's own length axis). Measured at
    HardKick f12 (hip yaw 75, spine lean -20/-10/-15): sweeping that extra
    Y from -30 to +30 only ever made head_angle_to_z WORSE in both
    directions (23.7 at 0 -> 36.6 at -30, 38.5 at +30) -- local Y is
    essentially the axis the deviation is symmetric about here, not a
    corrective DOF, once the parent chain's own large X/Z rotations have
    reoriented the bone's local frame. (Local X, "nod", turned out to be
    far more effective empirically -- 6.4 deg at X=+30 -- but guessing
    the right single local axis per pose is exactly the kind of fragility
    this rewrite avoids.) Replaced with a direct analytic solve: measure
    the WORLD-space rotation that takes the bone's current forward
    direction onto armature +Z, and apply it as a WORLD-space rotation
    (not a local-axis one) -- correct regardless of how the parent chain
    has reoriented the bone's own local frame. Split across Neck (half the
    needed angle) then Head (whatever remains after Neck's own share
    changes Head's effective world orientation, re-measured before
    applying) so the correction visibly comes from the whole neck, not a
    single bone snapping."""
    cur = head_angle_to_z(L)
    if abs(cur - target_angle) <= tol:
        return 0.0, 0.0
    A = L.armature()
    bpy.context.view_layer.update()
    target_world = (A.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()

    def correct(bone, frac):
        pb = A.pose.bones[L.full(bone)]
        bpy.context.view_layer.update()
        cur_world_rot = A.matrix_world.to_3x3() @ pb.matrix.to_3x3()
        cur_fwd = (cur_world_rot @ Vector((0, 0, 1))).normalized()
        dot = max(-1.0, min(1.0, cur_fwd.dot(target_world)))
        ang = math.degrees(math.acos(dot))
        axis = cur_fwd.cross(target_world)
        if ang < 1e-4 or axis.length < 1e-8:
            return 0.0
        axis.normalize()
        applied = ang * frac
        Rw = Matrix.Rotation(math.radians(applied), 3, axis)
        new_world_rot = Rw @ cur_world_rot
        cur_pos = (A.matrix_world @ pb.matrix).translation
        new_world = new_world_rot.to_4x4()
        new_world.translation = cur_pos
        pb.matrix = A.matrix_world.inverted() @ new_world
        bpy.context.view_layer.update()
        return applied

    a1 = correct("Neck", 0.5)
    a2 = correct("Head", 1.0)   # whatever remains after Neck's share
    return a1, a2


# ---------------------------------------------------------- safe render ---
def safe_review(L, tag, **kwargs):
    """VERIFY_attacks.md 10d: L.review() mutates render engine, resolution,
    camera, workbench shading and the Yemoja_Source layer-collection
    exclude flag with no try/finally -- an exception mid-render leaves the
    scene in silhouette mode. Cannot edit yemoja_anim_lib.py (shared
    project file), so wrap it here: snapshot the same state review() itself
    saves, and restore it in a finally regardless of what review() does."""
    sc = bpy.context.scene
    sav = dict(engine=sc.render.engine, cam=sc.camera, rx=sc.render.resolution_x,
               ry=sc.render.resolution_y, fp=sc.render.filepath)
    sh = sc.display.shading
    shs = (sh.light, sh.color_type, tuple(sh.single_color), sh.show_cavity)
    lc = bpy.context.view_layer.layer_collection.children.get("Yemoja")
    src = lc.children.get("Yemoja_Source") if lc else None
    src_excl = src.exclude if src is not None else None
    try:
        return L.review(tag, **kwargs)
    finally:
        sc.render.engine = sav["engine"]; sc.camera = sav["cam"]
        sc.render.resolution_x, sc.render.resolution_y = sav["rx"], sav["ry"]
        sc.render.filepath = sav["fp"]
        sh.light, sh.color_type, sh.single_color, sh.show_cavity = shs
        if src is not None:
            src.exclude = src_excl


# --------------------------------------------------------------- report ---
def idle_master_delta_deg(L, apply_json_pose):
    """Max bone quaternion angle difference (deg) between the CURRENT pose
    and a freshly-applied idle master."""
    A = L.armature()
    cur = {pb.name: pb.matrix_basis.to_quaternion().copy() for pb in A.pose.bones}
    apply_json_pose(L)
    worst = 0.0
    for pb in A.pose.bones:
        q1 = cur[pb.name]; q2 = pb.matrix_basis.to_quaternion()
        d = q1.rotation_difference(q2)
        ang = math.degrees(d.angle)
        if ang > worst:
            worst = ang
    return worst


def report(L, apply_json_pose, name, frames, support_sides=("L", "R"),
           idle_snap=None, kicking_exclude=None, first_last_are_idle=True):
    """Scrub the built action to each frame in `frames`, run audit() +
    safe_review() + twist checks + support-foot error + head angle +
    trident penetration, and write review/{name}_report.md. Returns the
    list of per-frame result dicts. VERIFY_attacks.md section 4: the
    excluded floor region is now named explicitly in the report instead of
    the un-labelled 0.0715 that read as "body floor clean" when it was
    actually the support foot's own raised idle height."""
    A = L.armature()
    act = bpy.data.actions[name]
    A.animation_data.action = act
    rows_out = []
    for frame in frames:
        bpy.context.scene.frame_set(frame)   # safe: action fully keyed now
        L.attach_trident()
        _, below, worst5 = audit(L)
        tw = twist_checks(L)
        if kicking_exclude:
            lo_z = lowest_world_z_excluding(L, "Yemoja_Body", kicking_exclude)
        else:
            lo_z = L.lowest_world_z(("Yemoja_Body",))
        support_err = 0.0
        if idle_snap is not None:
            for side in support_sides:
                pb = A.pose.bones[L.full("Foot." + side)]
                e = ((A.matrix_world @ pb.head) - idle_snap[side]["head"]).length
                support_err = max(support_err, e)
        idle_delta = None
        if first_last_are_idle and (frame == frames[0] or frame == frames[-1]):
            idle_delta = idle_master_delta_deg(L, apply_json_pose)
            bpy.context.scene.frame_set(frame)
            A.animation_data.action = act
        head_ang = head_angle_to_z(L)
        pen_bad = trident_penetration_bad(L)
        paths = safe_review(L, "%s_f%d" % (name, frame))
        rows_out.append(dict(frame=frame, below=below, worst5=worst5, twist=tw,
                              lowest_z=lo_z, lowest_z_excl=list(kicking_exclude) if kicking_exclude else None,
                              support_err=support_err, idle_delta=idle_delta,
                              head_angle=head_ang, pen=pen_bad, paths=paths))
    _write_report_md(name, rows_out)
    return rows_out


def _write_report_md(name, rows_out):
    path = os.path.join(REVIEW_DIR, "%s_report.md" % name)
    lines = ["# %s -- build report\n" % name, ""]
    excl = rows_out[0]["lowest_z_excl"] if rows_out else None
    lowz_label = "lowest body z (excl. %s)" % ", ".join(excl) if excl else "lowest body z"
    lines.append("| frame | worst region | ratio | crushed | Hand.L/R twist | Foot.L/R twist | %s | support-foot err | head-to-+Z | trident penetration | idle delta (deg) |" % lowz_label)
    lines.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for r in rows_out:
        w = r["worst5"][0] if r["worst5"] else None
        wname = "%s/%s" % (w["mesh"], w["bone"]) if w else "-"
        wratio = "%.3f" % w["ratio"] if w else "-"
        wcrush = w["crushed"] if w else "-"
        tw = r["twist"]
        hand_tw = "%.1f / %.1f" % (tw.get("Hand.L") or 0.0, tw.get("Hand.R") or 0.0)
        foot_tw = "%.1f / %.1f" % (tw.get("Foot.L") or 0.0, tw.get("Foot.R") or 0.0)
        idle = ("%.4f" % r["idle_delta"]) if r["idle_delta"] is not None else "-"
        pen = ("%d run(s): %s" % (len(r["pen"]), r["pen"])) if r["pen"] else "clean"
        lines.append("| %d | %s | %s | %s | %s | %s | %.4f | %.4f | %.1f | %s | %s |" %
                      (r["frame"], wname, wratio, wcrush, hand_tw, foot_tw,
                       r["lowest_z"], r["support_err"], r["head_angle"], pen, idle))
    lines.append("")
    lines.append("## Regions below 0.95 (finger bones on the right hand exempt)\n")
    for r in rows_out:
        if r["below"]:
            lines.append("**frame %d**" % r["frame"])
            for b in r["below"]:
                lines.append("- %s/%s ratio %.3f crushed %d/%d" %
                              (b["mesh"], b["bone"], b["ratio"], b["crushed"], b["n"]))
        else:
            lines.append("**frame %d**: none" % r["frame"])
    lines.append("")
    lines.append("## Worst 5 regions per frame\n")
    for r in rows_out:
        lines.append("**frame %d**" % r["frame"])
        for w in r["worst5"]:
            lines.append("- %s/%s ratio %.3f crushed %d/%d" %
                          (w["mesh"], w["bone"], w["ratio"], w["crushed"], w["n"]))
    os.makedirs(REVIEW_DIR, exist_ok=True)
    open(path, "w").write("\n".join(lines) + "\n")
    return path


# ============================================================= v4 rebuild ===
# SPEC_rebuild_v4.md: the model of record changed (v115_idleWeights, idle
# pose "A3", new mesh, new trident offset). Targets written against the old
# (v114) idle must be re-derived, not pasted, and every SOLVED arm must be
# hinge-correct (README 22: v115_fixes/apply_pole._limb_ik, off_hinge < 5deg)
# instead of yemoja_anim_lib's own limb_ik (which is off-hinge by 60.7deg on
# this rig's left arm -- see that function's docstring). Everything below is
# new for v4 and does not affect any v3 code path above.

V115_FIXES_DIR = "/home/claude/work/attacks/v115_fixes"
_V4_MODS = {}


def v4_mods(fixes_dir=None):
    """Lazily load v115_fixes/apply_pole.py and yemoja_measure.py (plain
    scripts, not a package -- loaded the same way apply_pole.py itself loads
    them). Returns (apply_pole_module, yemoja_measure_module), cached after
    the first call for whichever fixes_dir was used first."""
    d = fixes_dir or V115_FIXES_DIR
    if d not in _V4_MODS:
        import importlib.util as _ilu
        mods = {}
        for name in ("yemoja_measure", "apply_pole"):
            spec = _ilu.spec_from_file_location(name, os.path.join(d, name + ".py"))
            m = _ilu.module_from_spec(spec)
            spec.loader.exec_module(m)
            mods[name] = m
        _V4_MODS[d] = (mods["apply_pole"], mods["yemoja_measure"])
    return _V4_MODS[d]


# Frozen v114 idle landmarks (Yemoja_Idle_MASTER frame 1, world space),
# measured directly off Yemoja_WORKING_v114_idleClean.blend -- this file is
# read-only and never changes, so these are safe to freeze rather than
# re-measure every run. Source of every SPEC_fix_v3/SPEC_attacks.md/
# SPEC_hardkick_v2.md world-space target this round re-derives.
V114_LANDMARKS = {
    "Arm.L": Vector((0.58694, 0.34174, 5.99275)),
    "Arm.R": Vector((-0.43221, 0.64831, 5.91916)),
    "Hips":  Vector((-0.00437, 0.29741, 4.52707)),
}

_V4_DELTAS = {}


def v4_deltas(L):
    """(shoulder_delta {"L":Vector,"R":Vector}, hips_delta Vector): the world-
    space shift from the v114 idle's landmarks to THIS loaded file's own
    Yemoja_Idle_MASTER frame-1 landmarks, per SPEC_rebuild_v4.md's
    'new_idle_landmark + (old_target - old_idle_landmark)' rule. Computed
    once per armature object (cached on id(A)) by reading the CURRENT file's
    own idle action -- never hardcoded for the new side, so this keeps
    working if the model of record moves again."""
    A = L.armature()
    key = id(A)
    if key not in _V4_DELTAS:
        act = A.animation_data.action if A.animation_data else None
        prev_frame = bpy.context.scene.frame_current
        A.animation_data_create()
        A.animation_data.action = bpy.data.actions["Yemoja_Idle_MASTER"]
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()

        def w(name):
            pb = A.pose.bones[L.full(name)]
            return (A.matrix_world @ pb.head).copy()

        shoulder = {s: w("Arm." + s) - V114_LANDMARKS["Arm." + s] for s in ("L", "R")}
        hips = w("Hips") - V114_LANDMARKS["Hips"]
        A.animation_data.action = act
        bpy.context.scene.frame_set(prev_frame)
        bpy.context.view_layer.update()
        _V4_DELTAS[key] = (shoulder, hips)
    return _V4_DELTAS[key]


def retarget_shoulder(L, side, old_world):
    """old_world (a v114-idle-relative world target: a fist, guard hand, or
    elbow pole) -> the equivalent target against THIS file's own idle,
    keeping the same offset from the same-side shoulder joint (Arm.<side>
    head), per SPEC_rebuild_v4.md."""
    shoulder, _ = v4_deltas(L)
    return Vector(old_world) + shoulder[side]


def retarget_hips(L, old_world):
    """old_world (a v114-idle-relative world target: an ankle or knee hint)
    -> the equivalent target against THIS file's own idle, keeping the same
    offset from Hips, per SPEC_rebuild_v4.md."""
    _, hips = v4_deltas(L)
    return Vector(old_world) + hips


def arm_ik_hinge(L, side, wrist_world, elbow_pole_world, off_hinge=0.0,
                  pronation=0.0, hinge_tol=5.0, fixes_dir=None):
    """README 22 / SPEC_rebuild_v4.md: hinge-correct arm IK. Wraps
    v115_fixes.apply_pole._limb_ik (which constructs the upper bone's roll
    from the REST bend direction, keeping the elbow a true hinge) instead of
    yemoja_anim_lib's own limb_ik/arm_ik (aims local X at the bend-plane
    normal, 60.7deg off this rig's true left-elbow hinge). wrist_world/
    elbow_pole_world are WORLD-space points, same convention as the old
    L.arm_ik, already retargeted by the caller if they came from an old
    v114-relative number (retarget_shoulder) -- this function does no
    retargeting itself, so it is also correct for already-current-frame
    targets (e.g. chest_local_target's output).

    Returns (ok, off_hinge_deg): ok is reachability (same 99.9%-of-max-
    reach convention as the old solver); off_hinge_deg is
    yemoja_measure.off_hinge(side) measured immediately after solving.
    Asserts |off_hinge_deg| <= hinge_tol (the spec's own hard budget for any
    arm this pipeline solves) unless hinge_tol is None."""
    _apply_pole, ym = v4_mods(fixes_dir)
    A = L.armature()
    b1, b2 = L.full("Arm." + side), L.full("ForeArm." + side)
    bpy.context.view_layer.update()
    S = A.pose.bones[b1].head.copy()
    l1 = A.pose.bones[b1].length
    l2 = A.pose.bones[b2].length
    T = L.w2a(wrist_world)
    P = L.w2a(elbow_pole_world)
    ok = (T - S).length <= (l1 + l2) * 0.999
    _apply_pole._limb_ik(A, b1, b2, T, P, off_hinge=off_hinge, pronation=pronation)
    bpy.context.view_layer.update()
    measured = ym.off_hinge(side)
    if hinge_tol is not None:
        assert abs(measured) <= hinge_tol, (
            "off_hinge %.2f exceeds %.1f budget on %s (side=%s)" %
            (measured, hinge_tol, side, side))
    return ok, measured


def apply_idle_action(L, action_name="Yemoja_Idle_MASTER"):
    """Reset the rig to the idle pose read from the ACTION (not a JSON
    snapshot) -- SPEC_rebuild_v4.md: 'the authority is the action itself'.
    Snapshots matrix_basis at frame 1 of `action_name`, detaches the action,
    then re-applies the snapshot directly (mirrors what
    common.apply_json_pose leaves the rig in: a fully posed armature with no
    action attached, ready for build_clip to attach a fresh one). Found and
    used because pose_idle_master_2026-09-04_v115_A3.json does NOT match
    Yemoja_Idle_MASTER frame 1 exactly on this file (measured: Hips loc
    differs by 7.36 armature units, HandPinky2.L quaternion by 7.46deg) --
    the spec anticipates this ('check it equals the JSON') and the action is
    the one named as authoritative."""
    A = L.armature()
    A.animation_data_create()
    A.animation_data.action = bpy.data.actions[action_name]
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    snap = {pb.name: pb.matrix_basis.copy() for pb in A.pose.bones}
    A.animation_data.action = None
    for n, m in snap.items():
        A.pose.bones[n].matrix_basis = m
    bpy.context.view_layer.update()


# ================================================================ v5 fixes ===
# ------------------------------------------------------------ item 4: clavicle
def arm_target_rise_deg(L, side, wrist_world):
    """SPEC_fix_v5.md item 4: degrees the point `wrist_world` sits above
    Arm.<side>'s CURRENT (pre-elevation -- call this before rotating
    Shoulder.<side> for the frame being built) pose-space head, i.e. the
    angle above horizontal of the ray from the shoulder socket to the arm
    target. 0 if the target is at or below shoulder height. This is the
    "arm's rise above shoulder height" the clavicle formula is 1/3 of."""
    A = L.armature()
    bpy.context.view_layer.update()
    shoulder_w = A.matrix_world @ A.pose.bones[L.full("Arm." + side)].head
    d = Vector(wrist_world) - shoulder_w
    horiz = math.hypot(d.x, d.y)
    ang = math.degrees(math.atan2(d.z, horiz))
    return max(0.0, ang)


def clavicle_elevation_deg(L, side, wrist_world, cap=30.0):
    """SPEC_fix_v5.md item 4: min(cap, 1/3 of the arm's rise above shoulder
    height, 0 if the arm is not above shoulder height). UNSIGNED -- the
    caller applies the sign this rig's own convention needs (Shoulder.L
    elevates on +Z, Shoulder.R on -Z, per every existing pose-building
    function in attacks_build.py)."""
    return min(cap, arm_target_rise_deg(L, side, wrist_world) / 3.0)


# ------------------------------------------------- item 1: trident vs. body ---
def _leg_seg_clearance(L, shaft_b, shaft_t, side="R"):
    """Min distance from the shaft segment to the UpLeg.<side> and
    Leg.<side> bone segments (world space), SPEC_fix_v5.md item 1's own
    0.25 floor."""
    A = L.armature()
    up_h = A.matrix_world @ A.pose.bones[L.full("UpLeg." + side)].head
    up_t = A.matrix_world @ A.pose.bones[L.full("UpLeg." + side)].tail
    lg_h = A.matrix_world @ A.pose.bones[L.full("Leg." + side)].head
    lg_t = A.matrix_world @ A.pose.bones[L.full("Leg." + side)].tail
    return min(seg_seg_dist(shaft_b, shaft_t, up_h, up_t),
               seg_seg_dist(shaft_b, shaft_t, lg_h, lg_t))


def trident_frame_violation(L, PEN, BODY, n_samples=201, leg_side="R"):
    """SPEC_fix_v5.md item 1's per-frame check, at whatever frame/pose is
    CURRENT: (violated, (bad_runs, leg_clearance)). `PEN` is the imported
    verify/pen module (caller's job -- see enforce_trident_clear); bad_runs
    is PEN.trident_shaft_runs() filtered to non-grip bones."""
    b, t = L.trident_ends()
    runs = PEN.trident_shaft_runs(L, BODY, n_samples=n_samples)
    bad = [r for r in runs if r[2] not in _GRIP_BONE_NAMES]
    leg_d = _leg_seg_clearance(L, b, t, leg_side)
    return (bool(bad) or leg_d < 0.25), (bad, leg_d)


def enforce_trident_clear(L, name, key_frames, PEN, BODY, leg_side="R",
                           max_iter=8, n_samples=201):
    """SPEC_fix_v5.md item 1. `key_frames`: the clip's own REAL/named key
    frames (never touched -- only the interpolated frames between them are
    corrected). At every violating interior frame: SLERP the two bracketing
    named keys' own shaft directions (measured at THOSE keys, in world
    space) to this frame's fractional position between them, re-orient
    Hand.R to that direction with orient_hand_for_shaft (keeping Hand.R's
    position and roughly its current back-of-hand spin as the free-DOF
    hint), re-attach the trident, and key the FULL humanoid set. Iterates
    to a fixed point (cap max_iter passes). Returns (sorted frames a
    breakdown was added/updated at, [(frame, (bad_runs, leg_clearance)) for
    any frame still violating after the cap])."""
    A = L.armature()
    act = bpy.data.actions[name]
    key_frames = sorted(set(key_frames))

    def shaft_dir_at(f):
        A.animation_data.action = act
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        b, t = L.trident_ends()
        return (t - b).normalized()

    def bracket(f):
        lo_c = [k for k in key_frames if k <= f]
        hi_c = [k for k in key_frames if k >= f]
        lo = lo_c[-1] if lo_c else key_frames[0]
        hi = hi_c[0] if hi_c else key_frames[-1]
        if lo == hi:
            return lo, hi, 0.0
        return lo, hi, (f - lo) / float(hi - lo)

    added = set()
    for _pass in range(max_iter):
        n_fixed = 0
        for f in range(key_frames[0], key_frames[-1] + 1):
            if f in key_frames:
                continue
            A.animation_data.action = act
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            violated, detail = trident_frame_violation(L, PEN, BODY, n_samples, leg_side)
            if not violated:
                continue
            lo, hi, s = bracket(f)
            d_lo = shaft_dir_at(lo)
            d_hi = shaft_dir_at(hi)
            A.animation_data.action = act
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            dot = max(-1.0, min(1.0, d_lo.dot(d_hi)))
            theta = math.acos(dot)
            if theta < 1e-6:
                d = d_lo.copy()
            else:
                st = math.sin(theta)
                d = (d_lo * math.sin((1 - s) * theta) + d_hi * math.sin(s * theta)) / st
                d.normalize()
            A.animation_data.action = None    # detach: safe to rebuild this frame's pose
            hand_hint = A.pose.bones[L.full("Hand.R")].matrix.to_3x3() @ Vector((0, 1, 0))
            L.orient_hand_for_shaft(d, hand_hint)
            bpy.context.view_layer.update()
            L.attach_trident()
            snap = _snapshot_pose(L)
            A.animation_data.action = act
            key_from_snapshot(L, f, snap)
            added.add(f)
            n_fixed += 1
        if n_fixed == 0:
            break
    set_interpolation_5x(act)

    still_bad = []
    for f in range(key_frames[0], key_frames[-1] + 1):
        A.animation_data.action = act
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        violated, detail = trident_frame_violation(L, PEN, BODY, n_samples, leg_side)
        if violated:
            still_bad.append((f, detail))
    return sorted(added), still_bad


# --------------------------------------------- item 3: monotonic apex arcs ---
def apex_breakdowns(L, LEG_fn, name, side, frame_fracs, ankle_lo, ankle_hi,
                     knee_lo, knee_hi):
    """SPEC_fix_v5.md item 3. frame_fracs: {frame: t in (0,1)}. At each,
    re-solves ONLY the kicking leg via LEG_fn(side, lerp(ankle_lo,ankle_hi,t),
    lerp(knee_lo,knee_hi,t)) -- `ankle_lo/hi`/`knee_lo/hi` are the SAME
    (un-retargeted) world points the two bracketing real keys themselves
    used, so LEG_fn (attacks_build.py's own LEG() wrapper) retargets them
    identically -- leaving every other bone at whatever the existing
    Bezier curve already produced at that frame, then keys the FULL
    humanoid set. Returns the sorted list of frames touched."""
    A = L.armature()
    act = bpy.data.actions[name]
    added = []
    for f, t in sorted(frame_fracs.items()):
        A.animation_data.action = act
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        A.animation_data.action = None
        ankle = ankle_lo.lerp(ankle_hi, t)
        knee = knee_lo.lerp(knee_hi, t)
        LEG_fn(side, ankle, knee)
        bpy.context.view_layer.update()
        snap = _snapshot_pose(L)
        A.animation_data.action = act
        key_from_snapshot(L, f, snap)
        added.append(f)
    set_interpolation_5x(act)
    return added


# ------------------------------------------- item 8: spread support settle ---
def settle_spread_breakdowns(L, name, settle_fn, frames):
    """SPEC_fix_v5.md item 8. At each frame in `frames` (already an
    interior, already-interpolated frame of the clip): re-run `settle_fn`
    (a zero-arg closure -- e.g. Kick's settle_R()) against the pose
    CURRENTLY interpolated at that frame (not a fresh idle), so the floor
    settle is corrected incrementally along the existing curve instead of
    landing in one single-frame step, then key the FULL humanoid set.
    Caller picks `frames` as the interior points to spread the settle
    across (e.g. f2,f3 before the f4 key; f13,f15 before/after f16)."""
    A = L.armature()
    act = bpy.data.actions[name]
    added = []
    for f in sorted(frames):
        A.animation_data.action = act
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        A.animation_data.action = None
        settle_fn()
        bpy.context.view_layer.update()
        snap = _snapshot_pose(L)
        A.animation_data.action = act
        key_from_snapshot(L, f, snap)
        added.append(f)
    set_interpolation_5x(act)
    return added


# --------------------------------------- item 6: late per-key re-corrections ---
def final_key_correction(L, name, corrections):
    """SPEC_fix_v5.md item 6 (and the general item-2 hazard behind it):
    every OTHER per-action pass (enforce_pins, fix_quaternion_hemispheres,
    fix_twist_overshoot, enforce_trident_clear) runs after a key is first
    built and can perturb bones a build-time assert already validated at
    that exact key (VERIFY_attacks_v4.md's HardPunch head f12/f15 and
    HardKick toe f12/f16 numbers are both this -- correct when the pose
    function itself asserted, drifted afterwards). This runs LAST: at each
    `corrections` frame (already a real key of the action), evaluates the
    already-fixed pose, detaches, applies the given zero-arg closures (each
    a small in-place correction -- H.aim_head, H.plantarflex_and_detwist,
    etc.), and keys the FULL humanoid set, overwriting whatever that key
    held before with the corrected value."""
    A = L.armature()
    act = bpy.data.actions[name]
    for frame, fns in corrections.items():
        A.animation_data.action = act
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        A.animation_data.action = None
        for fn in fns:
            fn()
        bpy.context.view_layer.update()
        snap = _snapshot_pose(L)
        A.animation_data.action = act
        key_from_snapshot(L, frame, snap)
    set_interpolation_5x(act)


# ------------------------------------- item 5: Arm.R/Shoulder.R deformation ---
def sweep_arm_pole_and_hint(L, ARM_fn, side, wrist_world, base_pole_world,
                             shaft, hint_base, hinge_tol=5.0, twist_budget=30.0,
                             pole_angles=tuple(range(-60, 61, 10)),
                             hint_angles=tuple(range(0, 360, 15)),
                             n_samples=151):
    """SPEC_fix_v5.md item 5: jointly sweep the trident-hold pole (rotated
    about the shoulder->wrist axis, Rodrigues) and the orient_hand_for_shaft
    free hand-Y hint (rotated about the shaft axis) for Arm.<side>/
    Shoulder.<side> (Yemoja_Body) deformation ratio >= 0.90 WITH Hand.R
    twist <= twist_budget and off_hinge < hinge_tol -- all three checked
    TOGETHER for every (pole, hint) pair, not pole-then-hint sequentially:
    an early version of this picked the pole purely for deformation ratio,
    which could (and did, measured on HardKick f22) land on a pole whose
    hint-sweep twist-clean window only exists past the 30deg budget, even
    though a nearby pole had both a fine ratio AND an easy low-twist clean
    hint available. Full grid: for every pole angle, sweeps every hint
    angle; scores each (pole,hint) combo by (meets_all_three, worst_ratio,
    -|twist|) so a combo meeting every budget always outranks one that
    doesn't, and among those ties breaks toward the highest ratio then the
    lowest twist. If nothing meets all three, retries once with the hand
    target raised 0.15 (spec's own escape hatch) and keeps whichever grid
    (raised or not) has the better best-combo score; the actual return is
    always the single best (pole,hint) found, full grids included for
    BUILD_NOTES."""
    A = L.armature()
    _apply_pole, ym = v4_mods()
    shoulder_w = A.matrix_world @ A.pose.bones[L.full("Arm." + side)].head
    axis = (Vector(wrist_world) - shoulder_w)
    if axis.length < 1e-6:
        axis = Vector((0, 0, 1))
    axis.normalize()
    base_pole_rel = Vector(base_pole_world) - shoulder_w
    shaft_v = Vector(shaft).normalized()

    def worst_ratio():
        rows, _, _ = audit(L, objnames=("Yemoja_Body",))
        want = {"Arm." + side: None, "Shoulder." + side: None}
        for r in rows:
            if r["bone"] in want:
                want[r["bone"]] = r["ratio"]
        vals = [v for v in want.values() if v is not None]
        return (min(vals) if vals else 0.0), want

    def grid(wrist):
        out = []
        for pang in pole_angles:
            M = Matrix.Rotation(math.radians(pang), 4, axis)
            pole = shoulder_w + (M.to_3x3() @ base_pole_rel)
            reach_ok = ARM_fn(side, wrist, pole)
            bpy.context.view_layer.update()
            hinge = ym.off_hinge(side)
            wr, detail = worst_ratio()
            for hang in hint_angles:
                Mh = Matrix.Rotation(math.radians(hang), 3, shaft_v)
                hint = Mh @ Vector(hint_base)
                L.orient_hand_for_shaft(shaft_v, hint)
                L.apply_captured_grip("R")
                bpy.context.view_layer.update()
                twist = _common.twist_deg(L, "Hand.R")
                bad = trident_penetration_bad(L, n_samples=n_samples)
                meets = (reach_ok and wr >= 0.90 and abs(hinge) < hinge_tol
                         and abs(twist) <= twist_budget and not bad)
                out.append(dict(pole_ang=pang, pole=tuple(pole), hinge=hinge,
                                 reach_ok=reach_ok,
                                 worst_ratio=wr, ratio_detail=detail,
                                 hint_ang=hang, hint=tuple(hint), twist=twist,
                                 n_bad=len(bad), meets_all=meets))
        return out

    def score(c):
        # Lexicographic: reachability, then the THREE hard limits this rig
        # enforces everywhere else (hinge<tol -- arm_ik_hinge itself
        # asserts this; twist<=budget; zero non-grip penetration) as one
        # combined "hard-clean" flag, THEN (only among hard-clean
        # candidates, or as a tiebreak among not-hard-clean ones) the
        # deformation ratio, THEN least twist. An earlier version scored
        # worst_ratio ahead of the hard twist/hinge limits and could (did,
        # HardKick f22: measured 66.6deg twist) pick a high-ratio point
        # whose twist was nowhere near budget over a nearby point that
        # cleared all three -- this ordering can't do that: hard-clean
        # always outranks a higher ratio that isn't.
        hinge_ok = abs(c["hinge"]) < hinge_tol
        twist_ok = abs(c["twist"]) <= twist_budget
        pen_ok = not c["n_bad"]
        hard_ok = hinge_ok and twist_ok and pen_ok
        meets_three = c["worst_ratio"] >= 0.90 and hard_ok
        return (c["reach_ok"], meets_three, hard_ok, c["worst_ratio"], -abs(c["twist"]))

    cand = grid(wrist_world)
    best = max(cand, key=score)
    raised = False
    if not best["meets_all"]:
        raised_wrist = Vector(wrist_world) + Vector((0, 0, 0.15))
        cand2 = grid(raised_wrist)
        best2 = max(cand2, key=score)
        if score(best2) > score(best):
            best, cand, raised, wrist_world = best2, cand2, True, raised_wrist

    # Leave the rig posed at the chosen combo.
    final_ok = ARM_fn(side, wrist_world, Vector(best["pole"]))
    bpy.context.view_layer.update()
    L.orient_hand_for_shaft(shaft_v, Vector(best["hint"]))
    L.apply_captured_grip("R")
    L.attach_trident()
    bpy.context.view_layer.update()

    return dict(wrist_world=tuple(wrist_world), raised_hand=raised, ok=final_ok,
                chosen=best, n_candidates=len(cand),
                n_meeting_all=sum(1 for c in cand if c["meets_all"]),
                final_off_hinge=ym.off_hinge(side),
                final_twist=_common.twist_deg(L, "Hand.R"),
                final_n_bad=len(trident_penetration_bad(L, n_samples=n_samples)),
                final_worst_ratio=worst_ratio()[0])
