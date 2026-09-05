"""build_idle_loop.py -- `Yemoja_Idle_Loop`, a 120-frame seamless tidal idle.

Every channel is an ADDITIVE sinusoidal delta on top of frame 1 of
`Yemoja_Idle_MASTER`, with a period that divides 120, so frame 121 reproduces
frame 1 exactly (value AND slope -- a sinusoid has no seam).

Layers (see DEFAULT_PARAMS):
  breath  period 120           spine extend / neck+head counter / clavicle lift / hips rise
  swell   period 120, +30f     hips lateral + yaw, spine tilt, Spine1 counter-tilt
  arm.L   period 120, +15f     cupped hand rises and drifts out
  finger  period 120, +20f     left fingers breathe open, staggered Index->Pinky
  head    period 120, +60f     slow yaw

The three constraints that make it read as a planted deity rather than a
floating one are enforced per key, not approximated:

  * both ankles are pinned to their master world position and both feet keep
    their master world orientation (UpLeg/Leg re-solved with the twist-free
    2-bone solver, knee pole carried with the hip joint);
  * `Hand.R` -- the trident hand -- is pinned in world position AND orientation
    (Arm.R/ForeArm.R re-solved to the fixed wrist, elbow pole carried with the
    shoulder);
  * the arm twist splits from the v115 fixes are re-applied after every
    re-solve (retwist.set_split), so no key silently hands the elbow band back
    the 122 deg it was relieved of.

`Yemoja_Idle_MASTER` is never written to. build() is idempotent: it deletes any
existing `Yemoja_Idle_Loop` first.

    python3 build_idle_loop.py IN.blend OUT.blend
"""

import bpy, os, math, json, importlib.util
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))

ARM_NAME = "Armature"
PFX = "mixamorig:"
MASTER_ACTION = "Yemoja_Idle_MASTER"
LOOP_ACTION = "Yemoja_Idle_Loop"
AXES = {"X": Vector((1.0, 0.0, 0.0)), "Y": Vector((0.0, 1.0, 0.0)), "Z": Vector((0.0, 0.0, 1.0))}

# ------------------------------------------------------------------ params ---
DEFAULT_PARAMS = dict(
    period=120.0, key_step=4, n_frames=121, fps=30,
    scale=2.5,                     # global amplitude multiplier on every layer (1.0 was subliminal on a phone; 2.5 chosen 2026-09-03)

    # phases, in frames, relative to the breath
    breath_phase=0.0, swell_phase=30.0, arm_phase=15.0,
    finger_phase=20.0, head_phase=60.0,

    # 1. breath (deg / armature units)
    spine2_x=-1.2, spine1_x=-0.8, neck_x=1.2, head_x=0.5,
    clav_elev=1.5, clav_retract=0.6, hips_rise=1.0,

    # 2. swell / weight shift
    hips_lat=3.0, hips_yaw=0.4, spine_z=0.8, spine1_z=-0.4,

    # 3. left arm
    lhand_rise=2.0, lhand_lat=0.8,

    # 4. left fingers
    finger_open=3.0, thumb_open=1.5, finger_stagger=8.0,

    # 7. head
    head_turn=0.8,

    # twist splits held at every key (retwist.rel_twist at the elbow)
    twist_L=-122.8, twist_R=42.3,
)

FINGERS = ("Index", "Middle", "Ring", "Pinky")


# ---------------------------------------------------------------- plumbing ---
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
    rt._ym = ym                       # one roll definition everywhere
    ap = _load("apply_pole")
    ap._mods["yemoja_measure"] = ym
    ap._mods["retwist"] = rt
    return ym, rt, ap


def armature():
    return bpy.data.objects[ARM_NAME]


def full(n):
    return n if n.startswith(PFX) or n.startswith("hair_") else PFX + n


def _upd():
    bpy.context.view_layer.update()


def humanoid_bones(A=None):
    """Bones Unity's Humanoid retargeter carries: no hair_grp*, no Eye*."""
    A = A or armature()
    out = []
    for pb in A.pose.bones:
        n = pb.name
        if not n.startswith(PFX):
            continue
        if n.endswith("_end"):
            continue
        if n.startswith(PFX + "Eye"):
            continue
        out.append(n)
    return out


def rot(name, axis, deg):
    """Rotate about an ARMATURE-space axis, on top of the current pose."""
    if abs(deg) < 1e-12:
        return
    pb = armature().pose.bones[full(name)]
    M = pb.bone.matrix_local.to_3x3()
    R = Matrix.Rotation(math.radians(deg), 3, AXES[axis])
    pb.matrix_basis = (M.inverted() @ R @ M).to_4x4() @ pb.matrix_basis


def lrot(name, axis, deg):
    """Rotate about the bone's OWN local axis (finger curl is local +X)."""
    if abs(deg) < 1e-12:
        return
    pb = armature().pose.bones[full(name)]
    pb.matrix_basis = pb.matrix_basis @ Matrix.Rotation(math.radians(deg), 4, AXES[axis])


def loc(name, x=0.0, y=0.0, z=0.0):
    """Translate in armature-space units. Hips only -- Unity ignores the rest."""
    pb = armature().pose.bones[full(name)]
    M = pb.bone.matrix_local.to_3x3()
    pb.matrix_basis.translation += M.inverted() @ Vector((x, y, z))


def lowest_world_z(objnames=("Yemoja_Body",)):
    dg = bpy.context.evaluated_depsgraph_get()
    lo = 1e9
    for nm in objnames:
        ob = bpy.data.objects.get(nm)
        if not ob:
            continue
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ev.matrix_world
        for v in me.vertices:
            z = (mw @ v.co).z
            if z < lo:
                lo = z
        ev.to_mesh_clear()
    return lo


def _ori_deg(m0, m1):
    """Angle between two world 3x3 orientations, in degrees."""
    q = (m1 @ m0.inverted()).to_quaternion()
    return math.degrees(2.0 * math.acos(max(-1.0, min(1.0, abs(q.w)))))


def _restore_matrix(pb, ori3, iters=4):
    """Force a pose bone's world 3x3 back to `ori3`, keeping its head where the
    parent chain put it. Repeated because the setter round-trips through the
    depsgraph."""
    for _ in range(iters):
        head = pb.head.copy()
        pb.matrix = Matrix(((ori3[0][0], ori3[0][1], ori3[0][2], head.x),
                            (ori3[1][0], ori3[1][1], ori3[1][2], head.y),
                            (ori3[2][0], ori3[2][1], ori3[2][2], head.z),
                            (0.0, 0.0, 0.0, 1.0)))
        _upd()
        if _ori_deg(ori3, pb.matrix.to_3x3()) < 1e-7:
            break


# ------------------------------------------------------- master reference ---
def master_reference():
    """Load frame 1 of the master action, detach it, and read every landmark the
    loop is built against. Leaves the armature posed at the master pose with NO
    action assigned."""
    A = armature()
    A.data.pose_position = 'POSE'
    if A.animation_data is None:
        A.animation_data_create()
    act = bpy.data.actions.get(MASTER_ACTION)
    assert act is not None, "no %s in this file" % MASTER_ACTION
    A.animation_data.action = act
    bpy.context.scene.frame_set(1)
    _upd()
    A.animation_data.action = None            # nothing may re-evaluate over our edits
    _upd()

    ym, _, _ = mods()
    ref = dict(basis={}, ankle={}, foot_ori={}, knee_off={}, hip_head={},
               sh_head={}, wrist={}, elbow={}, hand_ori={}, leg_roll={}, off_hinge={})
    for pb in A.pose.bones:
        ref["basis"][pb.name] = (pb.rotation_quaternion.copy(), pb.location.copy())
    for s in ("L", "R"):
        ref["ankle"][s] = A.pose.bones[full("Foot." + s)].head.copy()
        ref["foot_ori"][s] = A.pose.bones[full("Foot." + s)].matrix.to_3x3().copy()
        ref["hip_head"][s] = A.pose.bones[full("UpLeg." + s)].head.copy()
        ref["knee_off"][s] = (A.pose.bones[full("Leg." + s)].head
                              - A.pose.bones[full("UpLeg." + s)].head)
        ref["sh_head"][s] = A.pose.bones[full("Arm." + s)].head.copy()
        ref["wrist"][s] = A.pose.bones[full("ForeArm." + s)].tail.copy()
        ref["elbow"][s] = A.pose.bones[full("ForeArm." + s)].head.copy()
        ref["hand_ori"][s] = A.pose.bones[full("Hand." + s)].matrix.to_3x3().copy()
        # The elbow's out-of-plane swing in the MASTER. Re-solving an arm must not
        # change it: the loop reproduces a pose, it does not re-author one. Before this
        # was captured, re-solving Arm.R moved it 15.7 -> 61.6 deg on every loop frame,
        # rotating the upper-arm skin ~46 deg while the hand stood still.
        ref["off_hinge"][s] = ym.off_hinge(s)

    # The twist-free solver CONSTRUCTS a roll (local X on the hinge); the master
    # thighs and shins carry a few degrees of real axial twist that construction
    # would silently delete, so frame 1 would not equal the master pose. Measure
    # that offset once, as a rotation about each bone's own axis, and re-apply it
    # after every solve -- it moves no joint centre (a bone's tail is on its own
    # local Y), so the ankle pin survives.
    _, _, ap = mods()
    for s in ("L", "R"):
        up, lg = full("UpLeg." + s), full("Leg." + s)
        m_up = A.pose.bones[up].matrix.to_3x3().copy()
        m_lg = A.pose.bones[lg].matrix.to_3x3().copy()
        pole = A.pose.bones[up].head + ref["knee_off"][s]
        ap._limb_ik(A, up, lg, ref["ankle"][s], pole)
        ref["leg_roll"][s] = (_axial_deg(A.pose.bones[up].matrix.to_3x3(), m_up),
                              _axial_deg(A.pose.bones[lg].matrix.to_3x3(), m_lg))
    reset_to_master(ref)
    return ref


def _axial_deg(constructed, target):
    """Signed local-Y angle taking `constructed` to `target` (they differ only in
    roll after a twist-free solve)."""
    q = (constructed.inverted() @ target).to_quaternion()
    if q.w < 0.0:
        q = -q
    return math.degrees(2.0 * math.atan2(q.y, q.w))


def reset_to_master(ref):
    A = armature()
    for n, (q, l) in ref["basis"].items():
        pb = A.pose.bones[n]
        pb.rotation_quaternion = q.copy()
        pb.location = l.copy()
    _upd()


# --------------------------------------------------------------- the pose ---
def _wave(t, phase, period):
    return math.sin(2.0 * math.pi * (t - phase) / period)


def pose_frame(frame, ref, p):
    """Put the rig into the loop pose for `frame`. Assumes no action is assigned."""
    ym, rt, ap = mods()
    A = armature()
    reset_to_master(ref)

    t = float(frame - 1)
    per = p["period"]
    sc = p["scale"]
    b = _wave(t, p["breath_phase"], per)
    sw = _wave(t, p["swell_phase"], per)
    aw = _wave(t, p["arm_phase"], per)
    hw = _wave(t, p["head_phase"], per)

    # --- 1/2. torso: breath + swell -------------------------------------------
    rot("Spine", "Z", p["spine_z"] * sw * sc)
    rot("Spine1", "Z", p["spine1_z"] * sw * sc)
    rot("Spine1", "X", p["spine1_x"] * b * sc)
    rot("Spine2", "X", p["spine2_x"] * b * sc)
    rot("Neck", "X", p["neck_x"] * b * sc)
    rot("Head", "X", p["head_x"] * b * sc)
    rot("Head", "Y", p["head_turn"] * hw * sc)
    rot("Shoulder.L", "Z", p["clav_elev"] * b * sc)
    rot("Shoulder.R", "Z", -p["clav_elev"] * b * sc)
    rot("Shoulder.L", "Y", p["clav_retract"] * b * sc)     # -Y protracts, so +Y retracts
    rot("Shoulder.R", "Y", -p["clav_retract"] * b * sc)
    rot("Hips", "Y", p["hips_yaw"] * sw * sc)
    loc("Hips", p["hips_lat"] * sw * sc, p["hips_rise"] * b * sc, 0.0)
    _upd()

    # --- 6. legs: ankles pinned, feet keep their master orientation ------------
    for s in ("L", "R"):
        up, lg = full("UpLeg." + s), full("Leg." + s)
        pole = A.pose.bones[up].head + ref["knee_off"][s]
        ap._limb_ik(A, up, lg, ref["ankle"][s], pole)
        d_up, d_lg = ref["leg_roll"][s]
        if abs(d_up) > 1e-9 or abs(d_lg) > 1e-9:
            E = A.pose.bones[lg].head.copy()
            W = A.pose.bones[lg].tail.copy()
            hinge = A.pose.bones[lg].matrix.to_3x3().col[0].copy()   # local X = the hinge
            lrot(up, "Y", d_up)                                       # spins thigh, knee fixed
            _upd()
            ap._set_bone_orientation(A, lg, W - E, hinge)             # ankle back on the pin
            lrot(lg, "Y", d_lg)                                       # spins shin, ankle fixed
            _upd()
        _restore_matrix(A.pose.bones[full("Foot." + s)], ref["foot_ori"][s])

    # --- 5. right arm: the trident hand does not move at all -------------------
    arm, fore = full("Arm.R"), full("ForeArm.R")
    pole = A.pose.bones[arm].head + (ref["elbow"]["R"] - ref["sh_head"]["R"])
    ap._limb_ik(A, arm, fore, ref["wrist"]["R"], pole, off_hinge=ref["off_hinge"]["R"])
    _restore_matrix(A.pose.bones[full("Hand.R")], ref["hand_ori"]["R"])
    rt.set_split("R", p["twist_R"], check=False)
    _restore_matrix(A.pose.bones[full("Hand.R")], ref["hand_ori"]["R"])

    # --- 3. left arm: the cupped hand rises with the swell ---------------------
    d = Vector((p["lhand_lat"] * aw * sc, p["lhand_rise"] * aw * sc, 0.0))
    arm, fore = full("Arm.L"), full("ForeArm.L")
    sh = A.pose.bones[arm].head.copy()
    target = sh + (ref["wrist"]["L"] - ref["sh_head"]["L"]) + d
    pole = sh + (ref["elbow"]["L"] - ref["sh_head"]["L"]) + d
    ap._limb_ik(A, arm, fore, target, pole, off_hinge=ref["off_hinge"]["L"])
    _restore_matrix(A.pose.bones[full("Hand.L")], ref["hand_ori"]["L"])
    rt.set_split("L", p["twist_L"], check=False)
    _restore_matrix(A.pose.bones[full("Hand.L")], ref["hand_ori"]["L"])

    # --- 4. left fingers breathe open, staggered so it is water not a machine ---
    for i, fng in enumerate(FINGERS):
        stag = p["finger_stagger"] * i / max(1, len(FINGERS) - 1)
        g = _wave(t, p["finger_phase"] + stag, per)
        for j in (1, 2):
            lrot("Hand%s%d.L" % (fng, j), "X", -p["finger_open"] * g * sc)
    gt = _wave(t, p["finger_phase"], per)
    for j in (1, 2):
        lrot("HandThumb%d.L" % j, "X", -p["thumb_open"] * gt * sc)
    _upd()


# ------------------------------------------------------------------ keying ---
def _channelbag(act, slot):
    if not act.layers:
        return None
    st = act.layers[0].strips[0]
    return st.channelbag(slot)


def set_interpolation(act, slot, mode="BEZIER", handle="AUTO_CLAMPED"):
    """Blender 5 layered-action safe: Action.fcurves is gone."""
    cb = _channelbag(act, slot)
    n = 0
    for fc in cb.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = mode
            kp.handle_left_type = kp.handle_right_type = handle
            n += 1
        fc.update()
    return n


# ------------------------------------------------------------------- build ---
def build(params=DEFAULT_PARAMS):
    """(Re)build `Yemoja_Idle_Loop` on the open file. Returns verification numbers."""
    p = dict(DEFAULT_PARAMS)
    p.update(params or {})
    ym, rt, ap = mods()
    A = armature()
    bpy.context.scene.render.fps = int(p["fps"])

    old = bpy.data.actions.get(LOOP_ACTION)
    if old is not None:
        if A.animation_data and A.animation_data.action is old:
            A.animation_data.action = None
        old.use_fake_user = False
        bpy.data.actions.remove(old)

    ref = master_reference()
    bones = humanoid_bones(A)
    hips = full("Hips")
    frames = list(range(1, int(p["n_frames"]) + 1, int(p["key_step"])))
    if frames[-1] != int(p["n_frames"]):
        frames.append(int(p["n_frames"]))

    # 1. solve every key with no action assigned, stashing the channel values.
    store, twists = {}, {}
    prev = {}
    for f in frames:
        pose_frame(f, ref, p)
        vals = {}
        for n in bones:
            q = A.pose.bones[n].rotation_quaternion.copy()
            if n in prev and q.dot(prev[n]) < 0.0:      # keep the curve continuous
                q = -q
            prev[n] = q
            vals[n] = q
        store[f] = dict(q=vals, hips=A.pose.bones[hips].location.copy())
        twists[f] = {s + "_" + j: round(rt.rel_twist(s, j), 3)
                     for s in ("L", "R") for j in ("elbow", "wrist")}

    # 2. write them into a fresh action.
    act = bpy.data.actions.new(LOOP_ACTION)
    act.use_fake_user = True
    if A.animation_data is None:
        A.animation_data_create()
    A.animation_data.action = act
    for f in frames:
        for n in bones:
            A.pose.bones[n].rotation_quaternion = store[f]["q"][n]
        A.pose.bones[hips].location = store[f]["hips"]
        for n in bones:
            A.pose.bones[n].keyframe_insert("rotation_quaternion", frame=f, group=n)
        A.pose.bones[hips].keyframe_insert("location", frame=f, group=hips)

    slot = A.animation_data.action_slot
    n_kp = set_interpolation(act, slot)
    act.use_frame_range = True
    act.frame_start, act.frame_end = 1.0, float(p["n_frames"])
    act.use_cyclic = True
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 1, int(p["n_frames"])
    bpy.context.scene.frame_set(1)
    _upd()

    out = verify(p)
    out["params"] = {k: v for k, v in p.items()}
    out["n_keys"] = len(frames)
    out["n_keyframe_points"] = n_kp
    out["twists_at_keys"] = twists
    out["twist_worst"] = {
        k: round(max(abs(twists[f][k] - ({"L_elbow": p["twist_L"], "R_elbow": p["twist_R"],
                                          "L_wrist": twists[frames[0]]["L_wrist"],
                                          "R_wrist": twists[frames[0]]["R_wrist"]}[k]))
                     for f in frames), 3)
        for k in ("L_elbow", "R_elbow", "L_wrist", "R_wrist")}
    return out


# ------------------------------------------------------------------ verify ---
def verify(params=DEFAULT_PARAMS, deform_frames=(1, 31, 61, 91), do_deform=True):
    """Evaluate the built action on EVERY frame and report the numbers."""
    p = dict(DEFAULT_PARAMS)
    p.update(params or {})
    ym, rt, ap = mods()
    A = armature()
    A.data.pose_position = 'POSE'
    act = bpy.data.actions[LOOP_ACTION]
    if A.animation_data is None:
        A.animation_data_create()
    A.animation_data.action = act
    slot = A.animation_data.action_slot
    MW = A.matrix_world
    R3 = MW.to_3x3()
    N = int(p["n_frames"])
    bones = humanoid_bones(A)

    def read():
        _upd()
        d = dict(q={n: A.pose.bones[n].rotation_quaternion.copy() for n in bones},
                 hips=A.pose.bones[full("Hips")].location.copy())
        for s in ("L", "R"):
            d["foot_" + s] = MW @ A.pose.bones[full("Foot." + s)].head
            d["footori_" + s] = (R3 @ A.pose.bones[full("Foot." + s)].matrix.to_3x3()).copy()
        d["handR"] = MW @ A.pose.bones[full("Hand.R")].head
        d["handR_ori"] = (R3 @ A.pose.bones[full("Hand.R")].matrix.to_3x3()).copy()
        d["handL"] = MW @ A.pose.bones[full("Hand.L")].head
        return d

    per = {}
    for f in range(1, N + 1):
        bpy.context.scene.frame_set(f)
        per[f] = read()
    f1 = per[1]

    # --- seam --------------------------------------------------------------
    seam_q = max((per[N]["q"][n] - f1["q"][n]).magnitude for n in bones)
    seam_worst = max(bones, key=lambda n: (per[N]["q"][n] - f1["q"][n]).magnitude)
    seam_loc = (per[N]["hips"] - f1["hips"]).length

    def vel(a, b, key, n=None):
        if key == "hips":
            return (per[b]["hips"] - per[a]["hips"]).length
        return (per[b]["q"][n] - per[a]["q"][n]).magnitude

    velo = {"Hips_loc": (vel(1, 2, "hips"), vel(N - 1, N, "hips"))}
    for n in ("Spine", "Spine1", "Spine2"):
        fn = full(n)
        velo[n] = (vel(1, 2, "q", fn), vel(N - 1, N, "q", fn))
    velo_pct = {k: (round(100.0 * abs(v1 - v0) / v0, 3) if v0 > 1e-12 else None)
                for k, (v0, v1) in velo.items()}

    # --- feet / floor / trident hand ---------------------------------------
    feet = {}
    for s in ("L", "R"):
        feet["Foot." + s + "_pos_drift"] = round(
            max((per[f]["foot_" + s] - f1["foot_" + s]).length for f in per), 6)
        feet["Foot." + s + "_ori_drift_deg"] = round(
            max(_ori_deg(f1["footori_" + s], per[f]["footori_" + s]) for f in per), 4)
    hand = dict(
        HandR_pos_drift=round(max((per[f]["handR"] - f1["handR"]).length for f in per), 6),
        HandR_ori_drift_deg=round(max(_ori_deg(f1["handR_ori"], per[f]["handR_ori"])
                                      for f in per), 4),
        HandL_travel=round(max((per[f]["handL"] - f1["handL"]).length for f in per), 6))
    # NOTE: positions above are in BLENDER world units; the tolerances are quoted in
    # armature units (world / 0.01), so convert.
    for k in list(feet):
        if k.endswith("_pos_drift"):
            feet[k + "_armunits"] = round(feet[k] / 0.01, 4)
    hand["HandR_pos_drift_armunits"] = round(hand["HandR_pos_drift"] / 0.01, 4)
    hand["HandL_travel_armunits"] = round(hand["HandL_travel"] / 0.01, 4)

    lz = {}
    for f in range(1, N + 1, 5):
        bpy.context.scene.frame_set(f)
        _upd()
        lz[f] = lowest_world_z()
    bpy.context.scene.frame_set(N)
    _upd()
    lz[N] = lowest_world_z()
    lz1 = lz[1]
    floor = dict(frame1=round(lz1, 8), min=round(min(lz.values()), 8),
                 max=round(max(lz.values()), 8),
                 max_abs_dev_from_frame1=round(max(abs(v - lz1) for v in lz.values()), 8))

    # --- keyed channels ----------------------------------------------------
    cb = _channelbag(act, slot)
    keyed = {}
    for fc in cb.fcurves:
        dp = fc.data_path
        bn = dp.split('"')[1] if '"' in dp else dp
        keyed.setdefault(bn, set()).add(dp.rsplit(".", 1)[-1])
    channels = dict(
        n_bones=len(keyed),
        bones=sorted(ym.short(b) for b in keyed),
        hair_keys=sorted(b for b in keyed if b.startswith("hair_")),
        eye_keys=sorted(b for b in keyed if b.startswith(PFX + "Eye")),
        location_keys=sorted(ym.short(b) for b, c in keyed.items() if "location" in c),
        non_rotation_channels=sorted({c for cs in keyed.values() for c in cs}))

    out = dict(seam=dict(max_quat_delta=seam_q, worst_bone=ym.short(seam_worst),
                         hips_loc_delta=seam_loc),
               velocity_1to2_vs_120to121={k: [round(v[0], 8), round(v[1], 8)]
                                          for k, v in velo.items()},
               velocity_pct_diff=velo_pct,
               feet=feet, floor=floor, hand=hand, channels=channels)

    # --- twists at the keys -------------------------------------------------
    tw = {}
    for f in range(1, N + 1, int(p["key_step"])):
        bpy.context.scene.frame_set(f)
        _upd()
        tw[f] = {s + "_" + j: rt.rel_twist(s, j) for s in ("L", "R") for j in ("elbow", "wrist")}
    out["twist_dev"] = {
        "L_elbow": round(max(abs(v["L_elbow"] - p["twist_L"]) for v in tw.values()), 4),
        "R_elbow": round(max(abs(v["R_elbow"] - p["twist_R"]) for v in tw.values()), 4),
        "L_wrist_range": [round(min(v["L_wrist"] for v in tw.values()), 3),
                          round(max(v["L_wrist"] for v in tw.values()), 3)],
        "R_wrist_range": [round(min(v["R_wrist"] for v in tw.values()), 3),
                          round(max(v["R_wrist"] for v in tw.values()), 3)]}

    # --- deformation --------------------------------------------------------
    if do_deform:
        want = ("band_armpit.L", "band_armpit.R", "band_elbow.L", "band_elbow.R",
                "band_wrist.L", "band_wrist.R", "dom_Shoulder.L", "dom_Shoulder.R",
                "dom_Spine2")
        dfm = {}
        for f in deform_frames:
            bpy.context.scene.frame_set(f)
            _upd()
            a = ym.region_area_audit("Yemoja_Body", regions=want)
            A.data.pose_position = 'POSE'
            bpy.context.scene.frame_set(f)
            _upd()
            dfm[f] = {k: a["regions"][k]["area_ratio"] for k in want}
        base = dfm[deform_frames[0]]
        out["deform"] = dfm
        out["deform_max_dev_from_frame1"] = {
            k: round(max(abs(dfm[f][k] - base[k]) for f in deform_frames), 4) for k in want}

    bpy.context.scene.frame_set(1)
    _upd()
    return out


# --------------------------------------------------------------------- cli ---
def main(argv):
    inp, outp = argv[0], argv[1]
    bpy.ops.wm.open_mainfile(filepath=inp)
    res = build()
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print(json.dumps(res, indent=1, default=str))
    return res


if __name__ == "__main__":
    import sys
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    main(a)
