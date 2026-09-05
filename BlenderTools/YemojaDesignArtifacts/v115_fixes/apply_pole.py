"""Left-elbow pole tuck -- v115 fix step 4.

The authored idle asks ~186 deg of Arm->Hand axial rotation on the left arm, more than a
forearm has, so redistributing it between ForeArm and Hand (retwist.py, step 2) tops the
elbow band out at area ratio 0.774. The only handle that lowers the DEMAND without moving
the hand is the elbow pole: swinging the elbow around the shoulder->wrist line is humeral
rotation, and it re-aims the upper arm's roll frame, which is what the elbow band measures
itself against. Client-approved value: phi = -40 deg on the LEFT arm only.

    phi : right-handed angle about u = (wrist - shoulder).normalized() in ARMATURE space
          (+X = her left, +Y = up, +Z = forward). Negative phi swings the LEFT elbow
          medially and forward -- it tucks against the ribs. At -40 the elbow travels
          32.1 armature units, 0.339 of the upper arm's length.

Fixed for every phi: the shoulder, the WRIST and the Hand's world orientation -- so the
hand, all fingers and anything bone-parented to Hand (the Trident on the right) do not
move at all. Only Arm.<side> and ForeArm.<side> change, and that is asserted here.

    from apply_pole import apply
    apply()                       # side="L", phi=-40, split from the sweep

apply() is idempotent: it stamps Armature["YEMOJA_POLE_FIX"] = {"L": -40.0} and a second
call is a no-op. That guard is not cosmetic -- re-running would rotate an already-rotated
elbow by another phi.

Measured on the idle master pose; see README_animation_guidelines.md section 17.
"""

import bpy, math, os, importlib.util
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
STAMP = "YEMOJA_POLE_FIX"
ACTION_NAME = "Yemoja_Idle_MASTER"

# Winning ForeArm/Hand split per (side, phi), from the pole sweep (section 17). The value is
# the target RELATIVE TWIST AT THE ELBOW in retwist's measure; the wrist takes the rest.
BEST_SPLIT = {("L", -40.0): -122.8}     # -> wrist twist lands on -45.0 deg (inside the +-60 window)

POS_TOL = 1e-5          # world units, for every bone that must not move
ORI_TOL = 1e-8          # hand world orientation

_mods = {}


def _load(name):
    if name not in _mods:
        spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        _mods[name] = m
    return _mods[name]


def mods():
    ym = _load("yemoja_measure")
    rt = _load("retwist")
    rt._ym = ym                       # share one measurement module / one roll definition
    return ym, rt


# --- twist-free 2-bone construction, ported verbatim from yemoja_anim_lib.set_bone_orientation
# --- / limb_ik so this package does not depend on the animation library at run time.
def _set_bone_orientation(A, name, ydir, hinge_dir):
    """local Y along the bone, local X along the joint hinge. No leftover twist."""
    pb = A.pose.bones[name]
    bpy.context.view_layer.update()
    head = pb.head.copy()
    y = Vector(ydir).normalized()
    x = Vector(hinge_dir)
    x = x - y * x.dot(y)
    if x.length < 1e-6:
        x = y.cross(Vector((0, 0, 1)))
        if x.length < 1e-6:
            x = y.cross(Vector((1, 0, 0)))
    x.normalize()
    z = x.cross(y)
    pb.matrix = Matrix(((x.x, y.x, z.x, head.x), (x.y, y.y, z.y, head.y),
                        (x.z, y.z, z.z, head.z), (0.0, 0.0, 0.0, 1.0)))
    bpy.context.view_layer.update()


def _frame3(a, b):
    """Orthonormal 3x3 with columns (a, b orthogonalised against a, their cross)."""
    e1 = Vector(a).normalized()
    e2 = Vector(b) - e1 * Vector(b).dot(e1)
    if e2.length < 1e-8:
        e2 = e1.cross(Vector((0, 1, 0)))
        if e2.length < 1e-8:
            e2 = e1.cross(Vector((1, 0, 0)))
    e2.normalize()
    e3 = e1.cross(e2)
    return Matrix(((e1.x, e2.x, e3.x), (e1.y, e2.y, e3.y), (e1.z, e2.z, e3.z)))


def _elbow_solve(A, b1, b2, target, pole):
    """(S, E, W) for the 2-bone triangle -- joint centres only, no orientation."""
    bpy.context.view_layer.update()
    S = A.pose.bones[b1].head.copy()
    l1 = A.pose.bones[b1].length
    l2 = A.pose.bones[b2].length
    v = Vector(target) - S
    d = max(min(v.length, (l1 + l2) * 0.999), abs(l1 - l2) * 1.001)
    dirv = v.normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pv = Vector(pole) - S
    pv = pv - dirv * pv.dot(dirv)
    if pv.length < 1e-6:
        pv = dirv.cross(Vector((0, 1, 0)))
    pv.normalize()
    return S, S + dirv * a + pv * h, S + dirv * d


def _limb_ik(A, b1, b2, target, pole, off_hinge=0.0, pronation=0.0):
    """2-bone IK in ARMATURE space that keeps the middle joint a HINGE.

    The joint centres come from the same triangle as before -- `pole` still says where
    the elbow goes. What changed is the upper bone's ROLL. The old version aimed the
    upper bone's local X at the bend-plane normal, which is an arbitrary convention: it
    put the posed bend direction on the upper bone's local +Z regardless of where the
    BIND POSE's bend direction sits, and on this rig those differ by 60.7 deg. The
    forearm then folded toward the side of the upper arm -- the "bends like flat paper"
    fault -- because the crease landed where the mesh has no joint.

    Here the upper bone's frame is CONSTRUCTED so that the rest bend direction maps onto
    the posed bend direction, which is exactly yemoja_measure.off_hinge() == 0. The lower
    bone is then placed by rotating the whole rest forearm frame about the anatomical
    hinge axis, so the elbow's relative rotation is hinge flexion and nothing else --
    plus `pronation`, an explicit twist about the FOREARM's own axis, the one axial
    freedom a real forearm has.

        off_hinge : deg of deliberate out-of-plane swing. 0 for anything authored;
                    pass a measured value to REPRODUCE a hand-authored arm (the loop
                    uses it so re-solving the right arm cannot alter Timi's pose).
        pronation : deg about the forearm's own axis, applied last.
    """
    S, E, W = _elbow_solve(A, b1, b2, target, pole)
    Ba = A.data.bones[b1].matrix_local.to_3x3()
    Bf = A.data.bones[b2].matrix_local.to_3x3()
    ua_r = Ba.col[1].normalized()
    fo_r = Bf.col[1].normalized()
    bend_r = (fo_r - ua_r * fo_r.dot(ua_r)).normalized()
    h_r = ua_r.cross(bend_r).normalized()
    b_loc = (Ba.inverted() @ bend_r).normalized()

    ua_p = (E - S).normalized()
    fo_p = (W - E).normalized()
    bend_p = (fo_p - ua_p * fo_p.dot(ua_p))
    bend_p = bend_p.normalized() if bend_p.length > 1e-7 else _frame3(ua_p, Vector((0, 1, 0))).col[1]

    # upper bone: map rest bend -> posed bend (rotated by off_hinge about the bone axis)
    src = Matrix.Rotation(math.radians(off_hinge), 3, Vector((0, 1, 0))) @ b_loc
    Ma = _frame3(ua_p, bend_p) @ _frame3(Vector((0, 1, 0)), src).transposed()
    _set_matrix3(A, b1, Ma)

    # lower bone: the rest forearm frame carried rigidly by the upper bone, then swung
    # about the anatomical hinge axis until it points at W. Nothing else.
    R = Ma @ Ba.inverted()
    h_p = (R @ h_r).normalized()
    f0 = (R @ fo_r).normalized()
    a1 = (f0 - h_p * f0.dot(h_p)).normalized()
    a2 = (fo_p - h_p * fo_p.dot(h_p)).normalized()
    flex = math.atan2(h_p.dot(a1.cross(a2)), a1.dot(a2))
    Mf = Matrix.Rotation(flex, 3, h_p) @ R @ Bf
    got = (Mf.col[1]).normalized()
    if got.angle(fo_p) > 1e-9:                      # exact only when off_hinge == 0
        Mf = got.rotation_difference(fo_p).to_matrix() @ Mf
    if pronation:
        Mf = Mf @ Matrix.Rotation(math.radians(pronation), 3, Vector((0, 1, 0)))
    _set_matrix3(A, b2, Mf)
    return dict(S=S, E=E, W=W, off_hinge=off_hinge, pronation=pronation)


def _set_matrix3(A, name, M3, iters=3):
    """Write an armature-space 3x3 onto a pose bone, keeping its head where the parent
    puts it (these bones are connected, so the head is not ours to move)."""
    pb = A.pose.bones[name]
    for _ in range(iters):
        h = pb.head.copy()
        pb.matrix = Matrix(((M3[0][0], M3[0][1], M3[0][2], h.x),
                            (M3[1][0], M3[1][1], M3[1][2], h.y),
                            (M3[2][0], M3[2][1], M3[2][2], h.z),
                            (0.0, 0.0, 0.0, 1.0)))
        pb.location = Vector((0, 0, 0))
        bpy.context.view_layer.update()
        if max((pb.matrix.to_3x3().col[c] - M3.col[c]).length for c in range(3)) < 1e-9:
            break


def _limb_ik_legacy(A, b1, b2, target, pole):
    """The pre-hinge solver, kept only so old results can be reproduced. Do not use:
    it sets the upper bone's roll from the bend-plane normal, which reads -60.7 deg
    off-hinge on this rig's left arm."""
    bpy.context.view_layer.update()
    S, E, W = _elbow_solve(A, b1, b2, target, pole)
    n = (E - S).cross(W - E)
    hinge = (-n).normalized() if n.length > 1e-8 else (E - S).cross(Vector((0, 1, 0)))
    _set_bone_orientation(A, b1, E - S, hinge)
    _set_bone_orientation(A, b2, W - E, hinge)


# ------------------------------------------------------------------------- apply ---
def stamp(A=None):
    ym, _ = mods()
    A = A or ym.armature()
    return dict(A.get(STAMP) or {})


def apply(side="L", phi=-40.0, forearm_twist=None):
    """Tuck the <side> elbow by phi about the shoulder->wrist line and re-split the twist.

    Returns a log dict. If the stamp already records this side, returns {'already_applied':
    ...} and changes nothing.
    """
    ym, rt = mods()
    A = ym.armature()
    prev = stamp(A)
    if side in prev:
        return dict(already_applied=prev, side=side)

    if forearm_twist is None:
        key = (side, float(phi))
        if key not in BEST_SPLIT:
            raise ValueError("no swept split for %r; pass forearm_twist explicitly" % (key,))
        forearm_twist = BEST_SPLIT[key]

    A.data.pose_position = 'POSE'
    rt.detach_action()
    rt.load_keyed_pose(1)
    snap0 = rt.snapshot()

    arm_b, fore_b = ym.full("Arm." + side), ym.full("ForeArm." + side)
    hand = A.pose.bones[ym.full("Hand." + side)]
    H_world, H_loc = hand.matrix.copy(), hand.location.copy()
    S = A.pose.bones[arm_b].head.copy()
    E0 = A.pose.bones[fore_b].head.copy()
    W = A.pose.bones[fore_b].tail.copy()
    l1 = A.pose.bones[arm_b].length

    u = (W - S).normalized()
    E = S + Matrix.Rotation(math.radians(phi), 3, u) @ (E0 - S)
    _limb_ik(A, arm_b, fore_b, W, E)
    dE = A.pose.bones[fore_b].head - E0
    wrist_moved = (A.pose.bones[fore_b].tail - W).length

    for _ in range(3):                        # restore the hand's world matrix exactly
        hand.matrix = H_world
        bpy.context.view_layer.update()
        if max((hand.matrix.col[c] - H_world.col[c]).length for c in range(4)) < 1e-9:
            break
    hand.location = H_loc
    bpy.context.view_layer.update()

    split = rt.set_split(side, forearm_twist, check=False)

    # Only Arm.<side> and ForeArm.<side> may have moved.
    free = {arm_b, fore_b}
    snap1 = rt.snapshot()
    worst = max(((k, max((v[0] - snap0["ht"][k][0]).length, (v[1] - snap0["ht"][k][1]).length))
                 for k, v in snap1["ht"].items() if k not in free), key=lambda kv: kv[1])
    ori = rt.compare(snap0, snap1)[1]
    assert worst[1] < POS_TOL, "pole tuck moved %s by %.3e world units" % worst
    assert ori < ORI_TOL, "hand orientation moved by %.3e" % ori

    rt.key_arms(1)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    new = dict(prev)
    new[side] = float(phi)
    A[STAMP] = new

    return dict(side=side, phi=float(phi), forearm_twist=float(forearm_twist),
                elbow_twist=round(rt.rel_twist(side, "elbow"), 2),
                wrist_twist=round(rt.rel_twist(side, "wrist"), 2),
                elbow_moved=round(dE.length, 2),
                elbow_moved_frac_upperarm=round(dE.length / l1, 4),
                elbow_delta=[round(v, 2) for v in dE],
                wrist_moved=wrist_moved, locked_max_move=worst[1],
                locked_worst_bone=worst[0], hand_ori_delta=ori, stamp=new)
