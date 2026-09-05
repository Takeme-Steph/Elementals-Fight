"""
author_idle_pose.py -- re-author Yemoja's idle ARM pose (v2).

The v1 idle "gums the body": the left upper arm is pressed into the ribs, the elbow is
tucked, the cupped hand is held close in front of the sternum with the palm twisted up.
That costs ~186 deg of Arm->Hand axial roll -- more than a forearm has -- and crushes the
armpit. This module throws the four arm chains away and CONSTRUCTS new ones:

    * wrist targets and elbow poles are stated in units of the upper-arm length U,
      measured from the shoulder joint S = Arm.<side> head, in ARMATURE space
      (+X = her left, +Y = up, +Z = forward, toward the opponent);
    * each arm is solved with the twist-free 2-bone IK from apply_pole (`_limb_ik`);
    * each hand's orientation is CONSTRUCTED from two explicit directions -- where the
      fingers point and where the palm faces -- not rotated into place by eye. The two
      hand-local axes those correspond to are measured off the REST mesh/skeleton
      (fingertip minus wrist for the finger axis; the direction a finger bone's tail
      travels under a positive local-X curl for the palm normal), so the numbers are the
      rig's, not a guess;
    * the residual axial roll is then split between forearm and wrist with
      retwist.set_split to a budget of |elbow| <= 90 deg and |wrist| <= 40 deg, and if the
      budget cannot be met the ELBOW POLE is swung about the shoulder->wrist line
      (humeral rotation) until it can -- that lowers the demand instead of hiding it;
    * clavicles follow the section-3 rule: Shoulder elevation ~ 1/3 of the arm's
      elevation above rest, plus 8-12 deg of protraction on the side whose arm leads.

The RIGHT hand is not free: the trident is bone-parented to Hand.R with Unity's exact
offset, its butt has to reach the ground and its shaft has to stand near vertical. Its
HEIGHT is therefore solved from the trident, not chosen -- and that height is 42 armature
units below the v1 hand, which makes the v1 Hand.R orientation unusable (the arm now
hangs, so freezing it would ask the wrist for a 98 deg break). Hand.R is instead
re-derived with orient_hand_for_shaft(): the shaft -- a fixed direction in hand-local
space -- is aimed near vertical, and the one remaining degree of freedom, the spin about
the shaft, is spent making the hand continue the forearm. The authored finger curls are
hand-local, so the grip on the shaft is untouched either way.

Nothing here is keyed. `author()` leaves the pose live on the open file; `key_as()`
writes it into a named action. Fingers, legs, hips, spine and neck are never touched.

    import author_idle_pose as ap
    ap.author("A")                    # or "B" / "C"
    ap.key_as("Yemoja_Idle_MASTER_A")
"""

import bpy, math, os, importlib.util
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ARM_NAME = "Armature"
PFX = "mixamorig:"
MASTER = "Yemoja_Idle_MASTER"

DEFAULT_BUDGET = dict(shoulder=85.0, elbow=90.0, wrist=40.0)
TWIST_BUDGET = dict(DEFAULT_BUDGET)            # swapped per candidate inside author()
POLE_SWEEP = tuple(range(-80, 85, 5))          # degrees about the shoulder->wrist line

_mods = {}


def _load(name, path=None):
    """Import a sibling module by path. Looks next to this file first, then in the
    v115_fixes package, then in the project root -- so the same file works from either
    copy without a sys.path dance."""
    if name not in _mods:
        cands = [path] if path else [
            os.path.join(HERE, name + ".py"),
            os.path.join(os.path.dirname(HERE), "v115_fixes", name + ".py"),
            "/home/claude/yemoja/v115_fixes/" + name + ".py",
            os.path.join(os.path.dirname(HERE), name + ".py"),
        ]
        path = next((p for p in cands if p and os.path.exists(p)), cands[0])
        spec = importlib.util.spec_from_file_location(name, path)
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        _mods[name] = m
    return _mods[name]


def mods():
    """(yemoja_measure, retwist, apply_pole, yemoja_anim_lib) -- siblings of this file."""
    ym = _load("yemoja_measure")
    rt = _load("retwist"); rt._ym = ym          # one roll definition for the whole stack
    ap = _load("apply_pole"); ap._mods["yemoja_measure"] = ym; ap._mods["retwist"] = rt
    al = _load("yemoja_anim_lib", os.path.join(os.path.dirname(HERE), "yemoja_anim_lib.py"))
    return ym, rt, ap, al


# --------------------------------------------------------------------- helpers ---
def A():
    return bpy.data.objects[ARM_NAME]


def full(n):
    return n if n.startswith(PFX) else PFX + n


def _upd():
    bpy.context.view_layer.update()


def _sgn(side):
    """+1 on the left: 'out' is +X for her left arm, -X for her right."""
    return 1.0 if side == "L" else -1.0


def arm_lengths():
    """(U, F) -- upper-arm and forearm length in armature units, from the rest skeleton."""
    a = A().data.bones
    U = (a[full("Arm.L")].tail_local - a[full("Arm.L")].head_local).length
    F = (a[full("ForeArm.L")].tail_local - a[full("ForeArm.L")].head_local).length
    return U, F


def hand_axes(side):
    """(finger_dir, palm_normal) as unit vectors in HAND-BONE-LOCAL coordinates.

    Measured on the REST skeleton, not assumed:
      finger_dir  = middle-fingertip minus wrist;
      palm_normal = the direction a finger bone's tail travels under a positive rotation
                    about its own local +X (which section 13 gives as the curl axis) --
                    the fingers curl toward the palm, so that velocity IS the outward
                    palm normal. Averaged over the twelve non-thumb finger bones.
    Returned orthonormalised against each other (the raw pair is ~19 deg from square,
    because curling has a component along the finger).
    """
    arm = A().data.bones
    hb = arm[full("Hand." + side)]
    Bi = hb.matrix_local.to_3x3().inverted()
    f = (Bi @ (arm[full("HandMiddle3." + side)].tail_local - hb.head_local)).normalized()
    acc = Vector((0, 0, 0))
    for fng in ("Index", "Middle", "Ring", "Pinky"):
        for j in (1, 2, 3):
            fb = arm[full("Hand%s%d.%s" % (fng, j, side))]
            x = fb.matrix_local.to_3x3().col[0].normalized()
            acc += x.cross(fb.tail_local - fb.head_local)
    p = (Bi @ acc).normalized()
    p = (p - f * p.dot(f)).normalized()
    return f, p


def _frame(a, b):
    """Orthonormal 3x3 whose columns are (a, b-orthogonalised, their cross)."""
    e1 = Vector(a).normalized()
    e2 = Vector(b) - e1 * Vector(b).dot(e1)
    if e2.length < 1e-8:
        e2 = e1.cross(Vector((0, 1, 0)))
        if e2.length < 1e-8:
            e2 = e1.cross(Vector((1, 0, 0)))
    e2.normalize()
    e3 = e1.cross(e2)
    return Matrix(((e1.x, e2.x, e3.x), (e1.y, e2.y, e3.y), (e1.z, e2.z, e3.z)))


def set_hand_by_construction(side, palm_normal_arm, finger_dir_arm, roll_deg=0.0):
    """Orient Hand.<side> from two explicit directions, in armature space.

    The PALM NORMAL is the primary constraint -- 'does the palm read as up' is the thing
    the client is judging -- and the finger direction is the secondary one, orthogonalised
    against it. `roll_deg` then tips the cup about the finger axis (positive = the thumb
    edge lifts on the left hand); the finger direction does not move, so roll_deg is a
    clean 1:1 handle on the axial roll the wrist has to carry.

    This is set_bone_orientation's idea -- construct the frame, never shortest-arc into it
    -- stated in anatomy rather than in bone axes.
    """
    f_loc, p_loc = hand_axes(side)
    f = Vector(finger_dir_arm).normalized()
    p = Vector(palm_normal_arm).normalized()
    f = f - p * f.dot(p)
    if f.length < 1e-8:
        f = p.cross(Vector((1, 0, 0)))
    f.normalize()
    if roll_deg:
        p = (Matrix.Rotation(math.radians(roll_deg), 3, f) @ p).normalized()
    M = _frame(p, f) @ _frame(p_loc, f_loc).transposed()
    pb = A().pose.bones[full("Hand." + side)]
    _set_matrix3(pb, M)
    return M


def _set_matrix3(pb, M3):
    """Write a world(armature)-space 3x3 onto a pose bone, keeping its head where the
    parent puts it. Repeated because setting .matrix decomposes through the parent."""
    for _ in range(3):
        h = pb.head.copy()
        pb.matrix = Matrix(((M3[0][0], M3[0][1], M3[0][2], h.x),
                            (M3[1][0], M3[1][1], M3[1][2], h.y),
                            (M3[2][0], M3[2][1], M3[2][2], h.z),
                            (0.0, 0.0, 0.0, 1.0)))
        pb.location = Vector((0, 0, 0))          # connected bone: head is not ours to move
        _upd()
        if max((pb.matrix.to_3x3().col[c] - M3.col[c]).length for c in range(3)) < 1e-9:
            break


def zero_arms(sides=("L", "R")):
    """matrix_basis = identity on the named sides' clavicle, upper arm, forearm and hand.
    Fingers, spine, legs, head are left exactly as authored."""
    a = A()
    for s in sides:
        for b in ("Shoulder", "Arm", "ForeArm", "Hand"):
            a.pose.bones[full(b + "." + s)].matrix_basis = Matrix.Identity(4)
    _upd()


# ------------------------------------------------------------------ clavicles ---
def _elev_prot(side, name, posed):
    """(elevation, protraction) of a bone in degrees, the same measure pose_twist_table
    uses: elevation = asin(y), protraction = atan2(z, x * side_sign)."""
    a = A()
    if posed:
        pb = a.pose.bones[full(name)]
        v = (pb.tail - pb.head).normalized()
    else:
        b = a.data.bones[full(name)]
        v = (b.tail_local - b.head_local).normalized()
    return (math.degrees(math.asin(max(-1.0, min(1.0, v.y)))),
            math.degrees(math.atan2(v.z, v.x * _sgn(side))))


def set_clavicle(side, d_elev, d_prot, iters=6):
    """Rotate Shoulder.<side> to the requested elevation / protraction CHANGE from rest.

    Section 13's axis table (`Shoulder.L +Z` / `Shoulder.R -Z` elevate, `Shoulder.L -Y` /
    `Shoulder.R +Y` protract) gives the two armature-space axes; the amounts are solved by
    measurement rather than arithmetic, because the two rotations do not commute and the
    clavicle does not lie on either axis.
    """
    a = A(); pb = a.pose.bones[full("Shoulder." + side)]
    pb.matrix_basis = Matrix.Identity(4); _upd()
    e0, p0 = _elev_prot(side, "Shoulder." + side, False)
    kz = d_elev * _sgn(side)                     # +Z elevates on the left, -Z on the right
    ky = -d_prot * _sgn(side)                    # -Y protracts on the left, +Y on the right
    Bl = a.data.bones[full("Shoulder." + side)].matrix_local.to_3x3()
    for _ in range(iters):
        R = (Matrix.Rotation(math.radians(ky), 3, Vector((0, 1, 0))) @
             Matrix.Rotation(math.radians(kz), 3, Vector((0, 0, 1))))
        pb.matrix_basis = (Bl.inverted() @ R @ Bl).to_4x4()
        _upd()
        e1, p1 = _elev_prot(side, "Shoulder." + side, True)
        de, dp = (e1 - e0) - d_elev, (p1 - p0) - d_prot
        if abs(de) < 0.02 and abs(dp) < 0.02:
            break
        kz -= de * _sgn(side) * 0.9
        ky += dp * _sgn(side) * 0.9
    e1, p1 = _elev_prot(side, "Shoulder." + side, True)
    return dict(side=side, target_d_elev=round(d_elev, 2), target_d_prot=round(d_prot, 2),
                got_d_elev=round(e1 - e0, 2), got_d_prot=round(p1 - p0, 2),
                rest_elev=round(e0, 2), pose_elev=round(e1, 2))


# ----------------------------------------------------------------- arm solving ---
def clavicle_roll(side, deg):
    """Rotate Shoulder.<side> about its OWN axis. Its tail lies on that axis, so the
    shoulder joint does not move and -- once the arm is re-solved with _limb_ik, which
    sets the upper arm's frame absolutely -- neither does the elbow, the wrist or the
    hand. All it changes is where the roll is BOOKED: 1 deg here is 1 deg off the
    glenohumeral twist and 1 deg onto the clavicle/scapula band. A real clavicle rotates
    axially by tens of degrees as the arm works; this rig has to spend that somewhere."""
    pb = A().pose.bones[full("Shoulder." + side)]
    pb.matrix_basis = pb.matrix_basis @ Matrix.Rotation(math.radians(deg), 4,
                                                        Vector((0, 1, 0)))
    _upd()


def _aim_shoulder_twist(side, target, resolve_arm, iters=3):
    """Set the glenohumeral twist to `target` by rolling the clavicle, re-solving the arm
    after each step so no joint centre moves. Returns the roll spent."""
    spent = 0.0
    for _ in range(iters):
        s = shoulder_twist(side)
        d = s - target
        if abs(d) < 0.05:
            break
        clavicle_roll(side, d)
        resolve_arm()
        if abs(shoulder_twist(side) - target) > abs(d):   # wrong sign: undo and flip
            clavicle_roll(side, -2 * d)
            resolve_arm()
            spent -= d
        else:
            spent += d
    return spent


def _pole_point(S, U, out, up, fwd, side, wrist=None, phi=0.0):
    """The elbow hint, in U-multiples from the shoulder. `phi` then swings it about the
    shoulder->wrist line -- humeral rotation, which re-aims the upper arm's roll frame
    while leaving the elbow on the same circle, so it buys twist for very little
    silhouette. Authored per candidate rather than left to the automatic sweep."""
    P = S + Vector((_sgn(side) * out * U, up * U, fwd * U))
    if phi and wrist is not None:
        u = (Vector(wrist) - S).normalized()
        P = S + Matrix.Rotation(math.radians(phi), 3, u) @ (P - S)
    return P


MAX_WRIST_DEV = 55.0        # hard stop: the hand may not deviate further from the forearm


def _solve_one_arm(side, wrist, pole, palm_normal=None, finger_dir=None, roll_deg=0.0,
                   hand_matrix=None, shaft_dir=None):
    """_limb_ik the chain onto `wrist` with elbow hint `pole`, then orient the hand.

    If the constructed hand deviates more than MAX_WRIST_DEV from the forearm the whole
    hand frame is swung back -- a broken wrist is worse than an imperfect palm angle.
    """
    ym, rt, ap, al = mods()
    a = A()
    S = a.pose.bones[full("Arm." + side)].head.copy()
    reach = (Vector(wrist) - S).length
    U, F = arm_lengths()
    ap._limb_ik(a, full("Arm." + side), full("ForeArm." + side), Vector(wrist), Vector(pole))
    _upd()
    E = a.pose.bones[full("ForeArm." + side)].head.copy()
    W = a.pose.bones[full("ForeArm." + side)].tail.copy()
    fa = (W - E).normalized()
    pn = fd = None
    hb = a.pose.bones[full("Hand." + side)]
    if shaft_dir is not None:
        # The trident's shaft is a FIXED direction in Hand.R-local space (Unity's offset),
        # so aiming it leaves exactly one degree of freedom: the spin about the shaft.
        # Spend it on the wrist -- the hand axis follows the forearm, so the grip is a
        # straight wrist rather than the 98 deg break that freezing the v1 hand
        # orientation would need once the arm hangs down to a planted butt.
        al.orient_hand_for_shaft(Vector(shaft_dir).normalized(), fa)
    elif hand_matrix is not None:
        _set_matrix3(hb, hand_matrix)
    else:
        pn, fd = Vector(palm_normal).normalized(), Vector(finger_dir).normalized()
        set_hand_by_construction(side, pn, fd, roll_deg)
        f_loc, _p = hand_axes(side)
        got = (hb.matrix.to_3x3() @ f_loc).normalized()
        dev = math.degrees(got.angle(fa))
        if dev > MAX_WRIST_DEV:
            axis = fa.cross(got)
            if axis.length > 1e-8:
                Rb = Matrix.Rotation(math.radians(dev - MAX_WRIST_DEV), 3,
                                     -axis.normalized())
                _set_matrix3(hb, Rb @ hb.matrix.to_3x3())
    _upd()
    f_loc, p_loc = hand_axes(side)
    M3 = hb.matrix.to_3x3()
    got_f = (M3 @ f_loc).normalized(); got_p = (M3 @ p_loc).normalized()
    return dict(side=side, shoulder=[round(v, 2) for v in S],
                elbow=[round(v, 2) for v in E], wrist=[round(v, 2) for v in W],
                wrist_error=round((W - Vector(wrist)).length, 4),
                reach_needed=round(reach, 2), reach_max=round(U + F, 2),
                reach_frac=round(reach / (U + F), 4),
                elbow_included_deg=round(180 - math.degrees((E - S).angle(W - E)), 1),
                forearm_dir=[round(v, 3) for v in fa],
                finger_dir=[round(v, 3) for v in got_f],
                palm_normal=[round(v, 3) for v in got_p],
                palm_up_deg=round(math.degrees(got_p.angle(Vector((0, 1, 0)))), 1),
                wrist_dev_deg=round(math.degrees(got_f.angle(fa)), 1),
                # For a hand closed round a shaft the fingertips curl back into the palm,
                # so finger-vs-forearm says nothing; the hand BONE axis is the wrist angle.
                hand_axis_dev_deg=round(math.degrees(
                    (hb.tail - hb.head).normalized().angle(fa)), 1))


def shoulder_twist(side):
    """Glenohumeral axial roll: roll(Arm) - roll(Shoulder), same measure as retwist's."""
    ym = _load("yemoja_measure")
    return ym._wrap180(ym._roll_deg("Arm." + side) - ym._roll_deg("Shoulder." + side))


def _total_twist(side):
    """Arm->Hand roll: what set_split can move between the elbow and the wrist."""
    _, rt, _, _ = mods()
    ym = _load("yemoja_measure")
    return ym._wrap180(rt.rel_twist(side, "elbow") + rt.rel_twist(side, "wrist"))


def _budget_split(T):
    """(elbow_target, wrist_target) inside the budget, or None if T cannot be split."""
    e = max(-TWIST_BUDGET["elbow"], min(TWIST_BUDGET["elbow"], T))
    w = T - e
    return (e, w) if abs(w) <= TWIST_BUDGET["wrist"] else None


def _load_of(S, T2):
    """Worst normalised joint load for a candidate pole angle.

    The whole roll from clavicle to hand, S + E + W, is INVARIANT: the elbow pole moves
    it between the shoulder and the elbow, set_split moves it between the elbow and the
    wrist, and neither moves a joint centre. So the only real question is which joint
    pays. The first version of this budgeted E and W alone, drove the pole to whatever
    minimised them -- and quietly dumped 145 deg into Arm.R, which crushed band_shoulder.R
    to 0.69 (from 0.99 in v1). Budgeting all three is not optional.
    """
    e = max(-TWIST_BUDGET["elbow"], min(TWIST_BUDGET["elbow"], T2))
    w = T2 - e
    return (max(abs(S) / TWIST_BUDGET["shoulder"], abs(e) / TWIST_BUDGET["elbow"],
                abs(w) / TWIST_BUDGET["wrist"]), e, w)


def _apply_twist_budget(side, resolve, tol=0.03, force_elbow=None, sweep=None):
    """Choose the elbow pole that spreads the roll best, then split what is left.

    `resolve(phi)` rebuilds the arm with the elbow pole swung phi degrees about the
    shoulder->wrist line -- humeral rotation, which moves no joint centre and (on a nearly
    straight arm) barely moves the elbow. The whole sweep is measured, the phi with the
    lowest worst-joint load wins, and ties within `tol` go to the smallest |phi| so the
    elbow stays as near the authored placement as the anatomy allows.
    """
    ym, rt, _, _ = mods()
    swept = []
    for phi in (POLE_SWEEP if sweep is None else sweep):
        resolve(float(phi))
        S, T2 = shoulder_twist(side), _total_twist(side)
        swept.append((float(phi), round(S, 1), round(T2, 1), round(_load_of(S, T2)[0], 3)))
    best = min(s[3] for s in swept)
    ok = [s for s in swept if s[3] <= best + tol]
    phi_used = min(ok, key=lambda s: abs(s[0]))[0]
    log = resolve(phi_used)
    S, T = shoulder_twist(side), _total_twist(side)
    load, e, w = _load_of(S, T)
    if force_elbow is not None:                  # authored split, when the budget cannot
        e, w = float(force_elbow), T - float(force_elbow)   # be met and the trade is ours
    rt.set_split(side, e, check=False)
    return dict(side=side, arm_to_hand_deg=round(T, 2), shoulder_deg=round(S, 2),
                elbow_deg=round(rt.rel_twist(side, "elbow"), 2),
                wrist_deg=round(rt.rel_twist(side, "wrist"), 2),
                clavicle_to_hand_deg=round(S + T, 2),
                worst_joint_load=round(load, 3), in_budget=bool(load <= 1.0),
                pole_phi_deg=phi_used, budget=dict(TWIST_BUDGET),
                pole_sweep=[dict(phi=s[0], shoulder=s[1], arm_to_hand=s[2], load=s[3])
                            for s in swept], arm=log)


# ---------------------------------------------------------------------- params ---
# out / up / fwd are multiples of U from the shoulder joint S; 'out' is +X on the left
# and -X on the right, so the same number reads the same way on both arms.
#
# palm_roll_deg tips the cup about the FINGER axis. It is the cheapest twist handle on
# this rig: 1 deg of roll is 1 deg off the Arm->Hand budget, it moves no joint centre,
# and because the finger axis runs roughly forward it is almost invisible from the SIDE
# camera -- the view that matters most in a 2.5D fighter -- showing only as a slight
# outward tip of the palm from the front. Palm-up on this rig costs ~150 deg of measured
# roll before any tipping (the rest pose has the palms facing medially), which is why
# every candidate needs some.
#
# pole_phi_deg swings the elbow hint about the shoulder->wrist line. On the RIGHT arm the
# arm is 94% extended, so the elbow rides a 29-unit circle and phi is nearly free in
# silhouette terms: -40 deg buys 38 deg of twist for 0.2 U of elbow travel.
PARAMS = {
    "A": dict(
        name="Offering",
        note="calm and regal: the cupped hand carried forward of the hip at navel "
             "height, palm up, the elbow hanging clear of the ribs.",
        L=dict(wrist=(0.30, -0.70, 0.55), pole=(0.42, -1.00, -0.15), pole_phi_deg=0.0,
               finger_dir=(-0.12, 0.00, 1.00), palm_normal=(0.0, 1.0, 0.0),
               palm_roll_deg=-39.0, shoulder_twist_target=80.0),
        R=dict(pole=(0.05, -1.30, -0.35), pole_phi_deg=0.0, wrist_xz=(0.57, -0.35),
               shaft_tilt=(0.12, 0.0)),
        clav=dict(protract_side="L", protract_deg=10.0, elev_ratio=1 / 3.0),
        head_turn_deg=0.0),
    "B": dict(
        name="Presenting",
        note="the cupped hand higher and further from the body, palm up and tilted 15 "
             "deg toward the opponent; the elbow carried out and slightly forward.",
        L=dict(wrist=(0.45, -0.35, 0.60), pole=(0.50, -0.75, 0.25), pole_phi_deg=0.0,
               finger_dir=(-0.15, 0.45, 1.00),
               palm_normal=(0.0, math.cos(math.radians(15)), math.sin(math.radians(15))),
               palm_roll_deg=-45.0, shoulder_twist_target=80.0),
        R=dict(pole=(0.05, -1.30, -0.35), pole_phi_deg=0.0, wrist_xz=(0.57, -0.35),
               shaft_tilt=(0.12, 0.0)),
        clav=dict(protract_side="L", protract_deg=11.0, elev_ratio=1 / 3.0),
        head_turn_deg=4.0),
    "C": dict(
        name="Low tide",
        note="most relaxed: the left arm hangs further down and out, the cupped hand at "
             "hip height with the fingers forward; the right elbow eased further out.",
        L=dict(wrist=(0.40, -0.95, 0.35), pole=(0.49, -1.05, -0.10), pole_phi_deg=0.0,
               finger_dir=(-0.10, 0.05, 1.00), palm_normal=(0.0, 1.0, 0.0),
               palm_roll_deg=-40.0, shoulder_twist_target=80.0),
        R=dict(pole=(0.18, -1.25, -0.35), pole_phi_deg=0.0, wrist_xz=(0.60, -0.33),
               shaft_tilt=(0.12, 0.0)),
        clav=dict(protract_side="L", protract_deg=8.0, elev_ratio=1 / 3.0),
        head_turn_deg=0.0),

    # ---- A2: the client shipped A, then circled two faults on the LEFT arm from her own
    # side camera -- the back of the upper arm pinched, and a hard kinked crease at the
    # elbow. Both are twist and flexion, not placement: A asked 79 deg of glenohumeral
    # roll and 98 deg of elbow roll on a rig with no twist bones, and bent the elbow 117
    # deg. A2 keeps the hand where it was (0.15 U further out along the shoulder->wrist
    # line, which is the whole leash allowed) and pays the budget down.
    #
    # MEASURED, and the reason this candidate looks the way it does: the clavicle-to-hand
    # roll a palm-up cupped left hand needs on this rig is ~187 deg and it is INVARIANT --
    # sweeping the wrist target, the elbow pole and the elbow flexion moves it by less
    # than 1 deg (pose_v2/sweep_A2.json, 594 solves). Only the hand's own orientation
    # changes it, 1 deg per 1 deg of palm roll, and palm roll is capped by readability at
    # -53 (palm normal . up = 0.60). 187 deg will not fit in 40 + 60 + 35 = 135. The
    # shortfall is paid by the CLAVICLE's axial roll, which is a real degree of freedom
    # and books the roll on the clavicle/scapula band instead of the three arm joints.
    "A2": dict(
        name="Offering, unwound",
        note="candidate A with the left arm's axial load taken off the shoulder and "
             "elbow: same hand placement, straighter elbow, palm tipped to 50 deg.",
        skip_right=True,                         # Shoulder/Arm/ForeArm/Hand.R untouched
        budget=dict(shoulder=40.0, elbow=60.0, wrist=35.0),
        L=dict(wrist=(0.348, -0.812, 0.638),     # A's target + 0.15 U straight out from S
               pole=(0.42, -1.00, -0.15), pole_phi_deg=0.0,
               finger_dir=(0.0, 0.0, 1.00), palm_normal=(0.0, 1.0, 0.0),
               palm_roll_deg=-50.0, shoulder_twist_target=38.0),
        R=dict(),
        clav=dict(protract_side="L", protract_deg=10.0, elev_ratio=1 / 3.0),
        head_turn_deg=0.0),
    # ---- A3: the elbow "bends like flat paper". Cause was not the mesh and not the
    # placement -- it was the SOLVER. The old twist-free 2-bone IK set the upper arm's
    # roll from the bend-plane normal, which put the posed bend direction 60.7 deg off
    # this rig's anatomical flexion plane (yemoja_measure.off_hinge). At -60.7 the
    # forearm folds toward the SIDE of the upper arm, so the crease lands where there is
    # no joint to make it. A, A2 and elbowTuck all read exactly -60.7; the v113 corkscrew
    # read -98.4. A3 is the same design as A2 rebuilt on the hinge-correct solver
    # (apply_pole._limb_ik, off_hinge=0), with the wrist pushed 0.095 U further out along
    # the shoulder->wrist line to bring flexion down to 100 deg.
    #
    # The hinge is not free: it removes the upper arm's roll as a degree of freedom, and
    # that roll is what the old solver was using to hide part of the Arm->Hand demand.
    # With the elbow held at 0.35-0.5 U out and the palm still reading up, elbow + wrist
    # is 152 deg against a 60 + 35 budget. Measured three ways to split it (see the
    # report): elbow 60 is best on every elbow number AND honours the elbow cap, and the
    # 92 deg it leaves at the wrist lands as uniform mild compression -- 0 crushed faces
    # -- where the same load at the elbow crushes 17.
    "A3": dict(
        name="Offering, hinged",
        note="A2 rebuilt on the hinge-correct solver: the elbow now folds in its own "
             "anatomical plane, flexion 100 deg, and the shoulder band comes back to 1.00.",
        skip_right=True,
        budget=dict(shoulder=40.0, elbow=60.0, wrist=35.0),
        L=dict(wrist=(0.3784, -0.8828, 0.6936),   # A2 target + 0.095 U out along S->W
               pole=(0.42, -1.00, -0.15), pole_phi_deg=0.0,
               finger_dir=(0.0, 0.0, 1.00), palm_normal=(0.0, 1.0, 0.0),
               palm_roll_deg=-53.0, shoulder_twist_target=35.0,
               elbow_twist_target=60.0, pole_sweep=(0.0,)),
        R=dict(),
        clav=dict(protract_side="L", protract_deg=10.0, elev_ratio=1 / 3.0),
        head_turn_deg=0.0),
}


# ----------------------------------------------------------------------- author ---
def author(candidate="A", params=None):
    """Build the candidate's arm pose on the OPEN file, from the current keyed master.

    Keys nothing. Returns every number worth checking: reach fractions, twist budget,
    clavicle angles, the trident plant and the feet.
    """
    ym, rt, ap, al = mods()
    P = params or PARAMS[candidate]
    a = A()
    a.data.pose_position = 'POSE'
    budget_was = dict(TWIST_BUDGET)
    TWIST_BUDGET.clear(); TWIST_BUDGET.update(P.get("budget", DEFAULT_BUDGET))
    skip_right = bool(P.get("skip_right"))       # A2: the right arm is the client's

    # Bind the MASTER explicitly first. detach_action() stashes whatever action happens
    # to be assigned, and load_keyed_pose() re-applies that stash -- so on a file left
    # showing Yemoja_Idle_Loop this silently authored on top of a LOOP frame, inheriting
    # the loop's re-solved right arm (off_hinge 61.6) instead of the master's (15.7).
    master = bpy.data.actions.get(MASTER)
    if master is not None:
        if a.animation_data is None:
            a.animation_data_create()
        a.animation_data.action = master
        rt._detached = None
    rt.detach_action()
    rt.load_keyed_pose(1)                        # the authored master is the starting point
    _upd()

    U, F = arm_lengths()
    base_low = al.lowest_world_z()
    hand_R_world = a.pose.bones[full("Hand.R")].matrix.to_3x3().copy()
    hand_R_head = a.pose.bones[full("Hand.R")].head.copy()
    butt_w, tip_w = al.trident_ends()
    base_butt_z = butt_w.z
    base_shaft = (tip_w - butt_w).normalized()
    # Butt position relative to the wrist, in ARMATURE units, with Hand.R's orientation
    # held fixed: a rigid offset, so planting the butt fixes the wrist's height. The
    # orientation is re-derived below, so this is only the starting estimate; the plant
    # loop then closes on the measured butt.
    MWi = a.matrix_world.inverted()
    butt_off = (MWi @ butt_w) - hand_R_head
    floor_arm_y = (MWi @ Vector((0, 0, 0))).y    # world z = 0 in armature Y
    # Shaft aim, in armature space: near vertical (+Y is up), tilted by the candidate.
    # shaft_tilt[0] > 0 tips the BUTT outboard (and the tines inboard): the butt then
    # plants clear of her leg, and the hand axis -- which is perpendicular to the shaft
    # in hand-local space -- tips down toward the hanging forearm, buying wrist angle.
    to, tf = P["R"].get("shaft_tilt", (0.0, 0.0))
    shaft_dir = Vector((to, 1.0, tf)).normalized()

    zero_arms(("L",) if skip_right else ("L", "R"))

    d_elev = {"L": 0.0, "R": 0.0}
    rest_arm_elev = {s: _elev_prot(s, "Arm." + s, False)[0] for s in ("L", "R")}
    clav_log, arm_log = {}, {}
    wrist_R_y = floor_arm_y - butt_off.y         # butt on the ground

    SIDES = ("L",) if skip_right else ("L", "R")

    def build():
        """One full pass: clavicles from the current elevation estimate, then the arms.

        With skip_right the RIGHT clavicle is not reset either -- resetting it would move
        Arm.R and with it the bone-parented trident, and the client's right arm is not
        ours to touch."""
        for s in SIDES:
            r = P["clav"]
            prot = r["protract_deg"] if s == r["protract_side"] else 0.0
            clav_log[s] = set_clavicle(s, d_elev[s] * r["elev_ratio"], prot)
        S = {s: a.pose.bones[full("Arm." + s)].head.copy() for s in ("L", "R")}
        wl = S["L"] + Vector([c * U * (_sgn("L") if i == 0 else 1.0)
                              for i, c in enumerate(P["L"]["wrist"])])
        pl = _pole_point(S["L"], U, *P["L"]["pole"], side="L", wrist=wl,
                         phi=P["L"].get("pole_phi_deg", 0.0))
        arm_log["L"] = _solve_one_arm("L", wl, pl, palm_normal=P["L"]["palm_normal"],
                                      finger_dir=P["L"]["finger_dir"],
                                      roll_deg=P["L"].get("palm_roll_deg", 0.0))
        wr = pr = None
        if not skip_right:
            ox, oz = P["R"]["wrist_xz"]
            wr = Vector((S["R"].x - ox * U, wrist_R_y, S["R"].z + oz * U))
            pr = _pole_point(S["R"], U, *P["R"]["pole"], side="R", wrist=wr,
                             phi=P["R"].get("pole_phi_deg", 0.0))
            arm_log["R"] = _solve_one_arm("R", wr, pr, shaft_dir=shaft_dir)
        for s in SIDES:                                    # book roll on the clavicle
            tgt = P[s].get("shoulder_twist_target")
            if tgt is not None:
                def again(_s=s, _w=(wl if s == "L" else wr), _p=(pl if s == "L" else pr)):
                    if _s == "L":
                        arm_log["L"] = _solve_one_arm("L", _w, _p,
                                                      palm_normal=P["L"]["palm_normal"],
                                                      finger_dir=P["L"]["finger_dir"],
                                                      roll_deg=P["L"].get("palm_roll_deg", 0.0))
                    else:
                        arm_log["R"] = _solve_one_arm("R", _w, _p, shaft_dir=shaft_dir)
                clav_log[s]["axial_roll_deg"] = round(
                    _aim_shoulder_twist(s, float(tgt), again), 2)
                clav_log[s]["shoulder_twist_deg"] = round(shoulder_twist(s), 2)
        return wl, pl, wr, pr

    wl, pl, wr, pr = build()
    for _ in range(3):                           # clavicle <-> arm elevation fixed point
        for s in SIDES:
            d_elev[s] = _elev_prot(s, "Arm." + s, True)[0] - rest_arm_elev[s]
        wl, pl, wr, pr = build()

    # --- trident: the butt has to reach the floor, so close the loop on the measurement
    plant = []
    for _ in range(0 if skip_right else 6):
        b, t = al.trident_ends()
        plant.append(round(b.z, 4))
        if abs(b.z) < 1e-4:
            break
        wrist_R_y -= b.z * 100.0                 # world z = 0.01 * armature Y
        wl, pl, wr, pr = build()

    # --- twist budget, with the elbow pole as the escape hatch
    def _resolver(side, wrist, pole):
        def go(phi):
            S = a.pose.bones[full("Arm." + side)].head.copy()
            u = (Vector(wrist) - S).normalized()
            p = S + Matrix.Rotation(math.radians(phi), 3, u) @ (Vector(pole) - S)
            if side == "L":
                return _solve_one_arm(side, wrist, p, palm_normal=P["L"]["palm_normal"],
                                      finger_dir=P["L"]["finger_dir"],
                                      roll_deg=P["L"].get("palm_roll_deg", 0.0))
            return _solve_one_arm(side, wrist, p, shaft_dir=shaft_dir)
        return go

    # pole_sweep=(0.0,) pins the authored elbow: the load-balancing sweep is free to
    # improve the twist split, but on A3 the elbow's 0.35-0.5 U window is a hard visual
    # constraint and the sweep will happily trade it away (it picked +40 deg, putting the
    # elbow 0.76 U out) -- so there it is not allowed to.
    twist = {"L": _apply_twist_budget("L", _resolver("L", wl, pl),
                                      force_elbow=P["L"].get("elbow_twist_target"),
                                      sweep=P["L"].get("pole_sweep"))}
    if not skip_right:
        twist["R"] = _apply_twist_budget("R", _resolver("R", wr, pr))
    else:
        arm_log.pop("R", None)
    for s in twist:
        arm_log[s] = dict(twist[s].pop("arm"))
        S = Vector(arm_log[s]["shoulder"]); E = Vector(arm_log[s]["elbow"])
        arm_log[s].update(pole_phi_deg=twist[s]["pole_phi_deg"],
                          elbow_from_shoulder_U=[round(_sgn(s) * (E.x - S.x) / U, 3),
                                                 round((E.y - S.y) / U, 3),
                                                 round((E.z - S.z) / U, 3)])

    # --- head: at most a small turn toward the opponent, on top of the authored rotation
    ht = P.get("head_turn_deg", 0.0)
    if ht:
        pb = a.pose.bones[full("Head")]
        Bl = a.data.bones[full("Head")].matrix_local.to_3x3()
        R = Matrix.Rotation(math.radians(ht), 3, Vector((0, 1, 0)))   # +Y = turn to her left
        pb.matrix_basis = (Bl.inverted() @ R @ Bl).to_4x4() @ pb.matrix_basis
        _upd()

    b, t = al.trident_ends()
    shaft = (t - b).normalized()
    low = al.lowest_world_z()
    TWIST_BUDGET.clear(); TWIST_BUDGET.update(budget_was)
    return dict(
        skip_right=skip_right, budget=dict(P.get("budget", DEFAULT_BUDGET)),
        candidate=candidate, name=P["name"], note=P["note"],
        U=round(U, 3), F=round(F, 3),
        arms=arm_log, clavicles=clav_log, twist=twist,
        trident=dict(butt_world=[round(v, 4) for v in b], tip_world=[round(v, 4) for v in t],
                     butt_z=round(b.z, 4),
                     shaft_off_vertical_deg=round(math.degrees(shaft.angle(Vector((0, 0, 1)))), 3),
                     butt_z_unchanged=round(b.z - base_butt_z, 6),
                     plant_iterations=plant),
        feet=dict(lowest_world_z=round(low, 6), base_lowest_world_z=round(base_low, 6),
                  delta=round(low - base_low, 6)),
        head_turn_deg=ht)


# -------------------------------------------------------------------- keying ---
def key_as(action_name, frame=1, fake_user=True):
    """Key the live pose into a NEW action: rotation on every humanoid bone plus Hips
    location, at `frame`. The existing master action is left untouched.

    Keying the whole humanoid set (not just the arms) means the action alone reproduces
    the pose, so anything measured after this is measuring what was saved.
    """
    _, rt, _, al = mods()
    a = A()
    names = al.humanoid_bones(a)
    keep = {n: (a.pose.bones[n].rotation_quaternion.copy(),
                a.pose.bones[n].location.copy()) for n in names}
    act = bpy.data.actions.get(action_name) or bpy.data.actions.new(action_name)
    act.use_fake_user = bool(fake_user)
    if a.animation_data is None:
        a.animation_data_create()
    a.animation_data.action = act
    if hasattr(a.animation_data, "action_slot") and len(act.slots):
        a.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(frame)
    for n in names:                              # undo any re-evaluation of the new action
        a.pose.bones[n].rotation_quaternion = keep[n][0]
        a.pose.bones[n].location = keep[n][1]
    _upd()
    for n in names:
        a.pose.bones[n].keyframe_insert("rotation_quaternion", frame=frame, group=n)
    a.pose.bones[full("Hips")].keyframe_insert("location", frame=frame, group=full("Hips"))
    _upd()
    bpy.context.scene.frame_set(frame)
    _upd()
    return dict(action=act.name, n_bones=len(names), frame=frame,
                fake_user=bool(act.use_fake_user))


def pose_json():
    """{bone: {'q': [w,x,y,z], 'loc': [x,y,z]}} for every bone on the rig."""
    out = {}
    for pb in A().pose.bones:
        q = pb.matrix_basis.to_quaternion()
        out[pb.name] = dict(q=[round(v, 8) for v in (q.w, q.x, q.y, q.z)],
                            loc=[round(v, 8) for v in pb.location])
    return out
