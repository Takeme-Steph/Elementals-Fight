"""
retwist.py -- redistribute axial twist between ForeArm and Hand without moving anything.

The Yemoja rig has no twist bones, so the whole relative roll across a joint is absorbed
by one LBS ring, which collapses by cos(theta/2). Frame 1 of `Yemoja_Idle_MASTER` puts
128 deg across the LEFT elbow and only 30 deg across the left wrist; the elbow band pays
for it (area ratio 0.753, 9/50 faces crushed). Nothing about the *silhouette* forces that
split: rotating ForeArm about its own axis moves no joint centre at all, and the hand's
world orientation can be restored exactly by counter-rotating Hand. The only observable
consequence is where the twist is absorbed.

    set_split(side, forearm_twist_deg)

`forearm_twist_deg` is the TARGET relative twist at the elbow, in the same measure as
yemoja_measure.pose_twist_table()['geometric_roll'][...]['rel_twist_wrapped_deg']
(geometric roll of ForeArm minus that of Arm, wrapped to (-180, 180]). The wrist's
relative twist moves by exactly the opposite amount, because the total roll from Arm to
Hand is fixed by the (unchanged) hand orientation.

Every call asserts:
  * every bone's world head and tail is unchanged  (< POS_TOL)
  * both Hand bones' world 3x3 orientation is unchanged (< ORI_TOL)

Usage (each python3 run is a fresh process):

    import bpy, os, importlib.util
    bpy.ops.wm.open_mainfile(filepath="/home/claude/yemoja/v115_base.blend")
    ym = _load("/home/claude/yemoja/yemoja_measure.py")
    rt = _load("/home/claude/yemoja/fix_twist/retwist.py")
    rt.detach_action()                  # so frame_set cannot overwrite the edit
    rt.load_keyed_pose()                # re-apply frame 1 before each candidate
    rt.set_split("L", 80.0)

Note: during a sweep the armature's action MUST be detached (detach_action), because
yemoja_measure.set_pose_state() calls frame_set(), which would re-evaluate the action and
throw the edit away. reattach_action() puts it back for keying/saving.
"""

import bpy, os, math, importlib.util
from mathutils import Matrix, Quaternion, Vector

ARM_NAME = "Armature"
PFX = "mixamorig:"
ACTION_NAME = "Yemoja_Idle_MASTER"
POS_TOL = 1e-4
ORI_TOL = 1e-4

_ym = None
_detached = None          # the action removed by detach_action()
_twist_sign = {}          # side -> +1/-1, sign of d(rel twist)/d(local Y rotation)


# ----------------------------------------------------------------- plumbing ---
def _load(path, name="mod"):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def ym():
    """The measurement module, loaded lazily so its roll measure is the single source."""
    global _ym
    if _ym is None:
        _ym = _load(os.path.join(os.path.dirname(os.path.abspath(__file__)), "yemoja_measure.py"), "yemoja_measure")
    return _ym


def armature():
    return bpy.data.objects[ARM_NAME]


def full(n):
    return n if n.startswith(PFX) else PFX + n


def _upd():
    bpy.context.view_layer.update()


# --------------------------------------------------------- action management ---
def detach_action():
    """Remove the action so scene.frame_set() cannot overwrite pose edits. Idempotent."""
    global _detached
    A = armature()
    if A.animation_data and A.animation_data.action:
        _detached = A.animation_data.action
        A.animation_data.action = None
    return _detached


def reattach_action():
    A = armature()
    act = _detached or bpy.data.actions.get(ACTION_NAME)
    if A.animation_data is None:
        A.animation_data_create()
    A.animation_data.action = act
    return act


def load_keyed_pose(frame=1):
    """Re-apply the authored pose from the action, then detach again. Call before each
    sweep candidate so candidates never accumulate."""
    A = armature()
    A.data.pose_position = 'POSE'
    act = _detached or bpy.data.actions.get(ACTION_NAME)
    if A.animation_data is None:
        A.animation_data_create()
    A.animation_data.action = act
    bpy.context.scene.frame_set(frame)
    _upd()
    detach_action()
    _upd()
    return act


# ------------------------------------------------------------- measurements ---
def rel_twist(side, joint):
    """Wrapped relative geometric twist at 'elbow' (ForeArm vs Arm) or 'wrist' (Hand vs
    ForeArm), in degrees -- identical measure to pose_twist_table's rel_twist_wrapped."""
    m = ym()
    child, parent = {"elbow": ("ForeArm", "Arm"), "wrist": ("Hand", "ForeArm")}[joint]
    return m._wrap180(m._roll_deg(child + "." + side) - m._roll_deg(parent + "." + side))


def snapshot():
    """World head/tail of every bone + world 3x3 of both hands: the invariants."""
    A = armature()
    MW = A.matrix_world
    ht = {}
    for pb in A.pose.bones:
        ht[pb.name] = (MW @ pb.head, MW @ pb.tail)
    ori = {}
    for s in ("L", "R"):
        ori[s] = (MW.to_3x3() @ A.pose.bones[full("Hand." + s)].matrix.to_3x3()).copy()
    return dict(ht=ht, ori=ori)


def compare(a, b):
    """(max head/tail world displacement, max hand orientation column deviation)."""
    dp = 0.0
    for k, (h0, t0) in a["ht"].items():
        h1, t1 = b["ht"][k]
        dp = max(dp, (h1 - h0).length, (t1 - t0).length)
    do = 0.0
    for s, m0 in a["ori"].items():
        m1 = b["ori"][s]
        for c in range(3):
            do = max(do, (m1.col[c] - m0.col[c]).length)
    return dp, do


# ------------------------------------------------------------------- the fix ---
def _rot_forearm_local(side, deg):
    """Post-multiply ForeArm's matrix_basis by a rotation about its own local Y (= its own
    bone axis, origin at its head). Moves no joint centre; drags Hand along."""
    pb = armature().pose.bones[full("ForeArm." + side)]
    pb.matrix_basis = pb.matrix_basis @ Quaternion(Vector((0, 1, 0)),
                                                   math.radians(deg)).to_matrix().to_4x4()
    _upd()


def _twist_dir(side):
    """+1 if a positive local-Y rotation of ForeArm raises rel_twist(elbow), else -1."""
    if side not in _twist_sign:
        before = rel_twist(side, "elbow")
        _rot_forearm_local(side, 1.0)
        after = rel_twist(side, "elbow")
        _rot_forearm_local(side, -1.0)
        _twist_sign[side] = 1 if ym()._wrap180(after - before) > 0 else -1
    return _twist_sign[side]


def set_split(side, forearm_twist_deg, tol=1e-6, check=True):
    """Set the relative twist at the `side` elbow to forearm_twist_deg, keeping every joint
    centre and the hand's world orientation numerically fixed.

    Returns a dict with the achieved elbow/wrist twists and the invariant residuals.
    Raises AssertionError if the silhouette moved by more than POS_TOL / ORI_TOL.
    """
    A = armature()
    assert A.data.pose_position == 'POSE', "set_split needs pose_position == 'POSE'"
    m = ym()
    before = snapshot()
    # The elbow is a HINGE. Rotating ForeArm about its own Y is pronation and is allowed;
    # anything that swung the forearm out of its flexion plane would not be. off_hinge is
    # pinned here so a future edit to _rot_forearm_local cannot quietly reintroduce the
    # "bends like flat paper" fault -- that is exactly how it got in.
    oh0 = m.off_hinge(side)
    elbow0, wrist0 = rel_twist(side, "elbow"), rel_twist(side, "wrist")

    hand = A.pose.bones[full("Hand." + side)]
    H_world = hand.matrix.copy()                       # armature-space pose matrix
    H_loc = hand.location.copy()                       # must not drift: the head cannot move

    delta = m._wrap180(forearm_twist_deg - elbow0)
    _rot_forearm_local(side, _twist_dir(side) * delta)

    # Restore the hand exactly. Setting pose_bone.matrix decomposes into matrix_basis /
    # rotation_quaternion for us; the extra passes mop up depsgraph ordering.
    for _ in range(3):
        hand.matrix = H_world
        _upd()
        if max((hand.matrix.col[c] - H_world.col[c]).length for c in range(4)) < 1e-9:
            break
    # The matrix setter decomposes into loc+rot; only the rotation is ours to change, and
    # Hand's head is pinned to ForeArm's tail regardless, so drop the location residue
    # (~5e-4 bone units) rather than carry an unkeyed channel offset into the action.
    hand.location = H_loc
    _upd()

    after = snapshot()
    dp, do = compare(before, after)
    oh1 = m.off_hinge(side)
    elbow1, wrist1 = rel_twist(side, "elbow"), rel_twist(side, "wrist")
    res = dict(side=side, target=round(forearm_twist_deg, 4),
               off_hinge_before=round(oh0, 3), off_hinge_after=round(oh1, 3),
               elbow_before=round(elbow0, 3), elbow_after=round(elbow1, 3),
               wrist_before=round(wrist0, 3), wrist_after=round(wrist1, 3),
               applied_local_deg=round(_twist_dir(side) * delta, 4),
               max_pos_delta=dp, max_hand_ori_delta=do,
               hand_basis=[round(v, 6) for v in hand.matrix_basis.to_quaternion()])
    assert abs(m._wrap180(oh1 - oh0)) < 1e-3, \
        "set_split moved the elbow off its hinge by %.3f deg -- twist must go about the " \
        "FOREARM axis, never across the elbow (%s)" % (oh1 - oh0, res)
    if check:
        assert dp < POS_TOL, "bone positions moved by %.3e (%s)" % (dp, res)
        assert do < ORI_TOL, "hand orientation moved by %.3e (%s)" % (do, res)
        assert abs(m._wrap180(elbow1 - forearm_twist_deg)) < 1e-3, \
            "elbow twist did not reach target: %s" % res
    return res


def arm_pose_json():
    """{bone: {'q':[w,x,y,z], 'loc':[x,y,z]}} for the 8 arm bones -- pose_idle_master schema."""
    A = armature()
    out = {}
    for b in ("Shoulder", "Arm", "ForeArm", "Hand"):
        for s in ("L", "R"):
            pb = A.pose.bones[full(b + "." + s)]
            q = pb.matrix_basis.to_quaternion()
            out[full(b + "." + s)] = dict(q=[q.w, q.x, q.y, q.z], loc=list(pb.location))
    return out


def key_arms(frame=1):
    """Key rotation_quaternion on Arm/ForeArm/Hand of both sides into ACTION_NAME.

    Order matters: reattaching the action and calling frame_set() would re-evaluate the OLD
    keys over the edit, so the live channel values are stashed first and written back after
    the action is reattached and the frame is set. Then keyframe_insert overwrites the keys.
    """
    A = armature()
    names = [full(b + "." + s) for s in ("L", "R") for b in ("Arm", "ForeArm", "Hand")]
    keep = {n: (A.pose.bones[n].rotation_quaternion.copy(),
                A.pose.bones[n].location.copy()) for n in names}
    reattach_action()
    bpy.context.scene.frame_set(frame)
    for n in names:                                   # undo the action's re-evaluation
        A.pose.bones[n].rotation_quaternion = keep[n][0]
        A.pose.bones[n].location = keep[n][1]
    _upd()
    for n in names:
        A.pose.bones[n].keyframe_insert("rotation_quaternion", frame=frame, group=n)
    _upd()
    return names
