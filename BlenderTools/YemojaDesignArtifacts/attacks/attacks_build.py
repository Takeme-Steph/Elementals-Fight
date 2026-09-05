"""
Yemoja attack clips -- builds all four Yemoja_Atk_* actions from
SPEC_attacks.md and saves Yemoja_WORKING_v115_attacks.blend.

Idempotent: always starts from the v114 source file and rebuilds every
action from scratch (build_clip() clears each action's fcurves before
keying), so re-running this script is safe.

Deviations from the literal spec numbers are called out in comments right
where they happen, each with the measured reason (a limb_ik reach failure,
a deformation-budget number, a twist-budget number, a pin_foot tolerance
failure) and are summarized with full numbers in BUILD_NOTES.md.
"""
import bpy, sys, os, math, re, importlib.util
sys.path.insert(0, "/home/claude/work/attacks")
from mathutils import Vector
import common, harness as H

# SPEC_fix_v5.md: sweep/report data gathered by the v5 fix passes below,
# for the BUILD_NOTES ## v5 section (kept module-level so final_gate() and
# main() can both see it without threading extra return values around).
V5_REPORT = {}

# SPEC_fix_v3.md item 11f / SPEC_rebuild_v4.md: source blend, library and
# output path all parameterised via env vars so this one script rebuilds
# against either model of record without being edited. v4 (the new model
# of record, README 17-23) is now the DEFAULT; point YEMOJA_BLEND_IN/
# YEMOJA_LIB/YEMOJA_BLEND_OUT at the v114/v115_attacks/yemoja_anim_lib.py
# trio to reproduce the v3 build instead.
BLEND_IN = os.environ.get("YEMOJA_BLEND_IN",
                           "/home/claude/work/attacks/Yemoja_WORKING_v115_idleWeights.blend")
LIB = os.environ.get("YEMOJA_LIB", "/home/claude/work/attacks/yemoja_anim_lib_v115.py")
BLEND_OUT = os.environ.get("YEMOJA_BLEND_OUT",
                            "/home/claude/work/attacks/Yemoja_ATTACKS_v4.blend")
REVIEW_DIR = os.environ.get("YEMOJA_REVIEW_DIR", "/home/claude/work/attacks/review/v4")
# True exactly when building against the new (v115_idleWeights) source --
# selects RESET_POSE (action-based, not the old v114 JSON) and the
# retargeting/hinge-safe arm solver below.
V4 = "v115_idleWeights" in BLEND_IN or "yemoja_anim_lib_v115" in LIB

L = common.load(BLEND_IN, lib=LIB, review_dir=REVIEW_DIR)
H.REVIEW_DIR = L.REVIEW_DIR   # item 11e: one REVIEW_DIR -- common.load() is the source of truth
L.preview_mode(True)
A = L.armature()

# SPEC_rebuild_v4.md: 'the authority is the action itself' -- H.apply_idle_action
# reads Yemoja_Idle_MASTER frame 1 directly rather than a JSON snapshot (the
# shipped pose_idle_master_2026-09-04_v115_A3.json does NOT match the action
# on this file: Hips loc differs 7.36 armature units, HandPinky2.L quaternion
# 7.46deg -- see H.apply_idle_action's docstring). v3's own RESET_POSE
# (common.apply_json_pose, the v114 JSON) is kept for exact v3 reproduction.
RESET_POSE = H.apply_idle_action if V4 else common.apply_json_pose
IDLE_SNAP = H.snapshot_feet(L)
# SPEC_rebuild_v4.md: "the right arm holds and the [trident] butt stays
# planted... in Yemoja_Atk_Kick at every frame" -- Kick never explicitly
# poses the right arm (it just holds the trident the way idle does), so as
# Hips moves through the kick the trident (rigidly parented to Hand.R)
# sweeps with it unless something actively holds Hand.R's world transform
# fixed. Snapshotted once here, at the file's own loaded (idle) pose, same
# pattern as IDLE_SNAP for the feet -- see pin_trident_hand() below.
IDLE_HAND_R = H.snapshot_bone(L, "Hand.R")
IDLE_ELBOW_R = H.snapshot_bone(L, "ForeArm.R")["head"]

# SPEC_fix_v5.md item 1: the verifier's own signed BVH trident-penetration
# test, imported (not re-implemented) from verify/pen.py -- see that file's
# module docstring for the v5 refactor that made this import safe (it used
# to run unconditionally on import, clobbering whatever scene was open).
_VERIFY_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "verify")
if _VERIFY_DIR not in sys.path:
    sys.path.insert(0, _VERIFY_DIR)
_pen_spec = importlib.util.spec_from_file_location("pen", os.path.join(_VERIFY_DIR, "pen.py"))
PEN = importlib.util.module_from_spec(_pen_spec)
_pen_spec.loader.exec_module(PEN)
BODY_OBJ = bpy.data.objects["Yemoja_Body"]


# ================================================================= v4 ===
# SPEC_rebuild_v4.md: every world-space target below this point that was
# written against the v114 idle (a fist, guard hand, elbow pole -> same-side
# shoulder joint; an ankle, knee hint -> Hips) is re-derived as
# new_idle_landmark + (old_target - old_idle_landmark), NOT pasted, via
# H.retarget_shoulder/H.retarget_hips (v3-frozen v114 landmarks vs THIS
# file's own Yemoja_Idle_MASTER). Every SOLVED arm (not the idle bookends,
# which are Stephanie's hand-authored pose and are read-only here) goes
# through H.arm_ik_hinge (README 22: v115_fixes/apply_pole._limb_ik,
# off_hinge < 5deg) instead of yemoja_anim_lib's own arm_ik. Three thin
# wrappers below are how every pose-building function gets both without
# individually importing/retargeting: ARM() for an old v114-relative
# world target, ARM_CURRENT() for a target already expressed in the
# CURRENT frame (e.g. H.chest_local_target's output -- must NOT be
# retargeted again), LEG() for an old v114-relative leg target (retargeted
# by Hips, solved with the old non-hinge leg_ik -- "Leg IK may stay on the
# old solver", SPEC_rebuild_v4.md). Building against v114/v3 (V4 == False)
# makes all three plain pass-throughs to the original solver, so the same
# pose-building functions reproduce the v3 build byte-for-byte-in-intent
# when pointed at the old source.
def ARM(side, wrist_world, elbow_pole_world, off_hinge=0.0, pronation=0.0):
    if not V4:
        return L.arm_ik(side, wrist_world, elbow_pole_world)
    w = H.retarget_shoulder(L, side, wrist_world)
    p = H.retarget_shoulder(L, side, elbow_pole_world)
    ok, hinge = H.arm_ik_hinge(L, side, w, p, off_hinge=off_hinge, pronation=pronation)
    return ok


def ARM_CURRENT(side, wrist_world, elbow_pole_world, off_hinge=0.0, pronation=0.0):
    if not V4:
        return L.arm_ik(side, wrist_world, elbow_pole_world)
    ok, hinge = H.arm_ik_hinge(L, side, wrist_world, elbow_pole_world,
                                off_hinge=off_hinge, pronation=pronation)
    return ok


def LEG(side, ankle_world, knee_hint_world):
    if not V4:
        return L.leg_ik(side, ankle_world, knee_hint_world)
    a = H.retarget_hips(L, ankle_world)
    k = H.retarget_hips(L, knee_hint_world)
    return L.leg_ik(side, a, k)

# SPEC_fix_v3.md item 3/4: bone-name lists used both to exclude the kicking
# foot from floor checks and (for Kick) to derive the support-foot's flat
# floor target below. Module level so both the pose functions and
# enforce_pins' per-clip closures in main() can use them.
KICKING_L = ["Foot.L", "ToeBase.L", "Toe_End.L"]
KICKING_R = ["Foot.R", "ToeBase.R", "Toe_End.R"]

# SPEC_fix_v3.md item 3 (Kick: plant her), FABLE method. At idle, the right
# (support, for this clip) foot sits H.lowest_world_z_excluding(...,
# KICKING_L) above the floor -- the raised-idle-position bug VERIFY found
# (measured ~0.0715). Lower the ankle TARGET by exactly that much so the
# sole lands on z=0 instead of pinning the foot back to its raised idle
# spot; flatten it to match the LEFT foot's own (already-flat-at-idle)
# ankle-to-toe height drop, since the geometry is mirrored.
_idle_raised = H.lowest_world_z_excluding(L, "Yemoja_Body", KICKING_L)
_other_toe_tail = (A.matrix_world @ A.pose.bones[L.full("ToeBase.L")].tail).copy()
KICK_ANKLE_TARGET = Vector((IDLE_SNAP["R"]["head"].x, IDLE_SNAP["R"]["head"].y,
                             IDLE_SNAP["R"]["head"].z - _idle_raised))
KICK_TARGET_DROP = _other_toe_tail.z - IDLE_SNAP["L"]["head"].z
KICK_SUPPORT_KNEE_HINT = IDLE_SNAP["R"]["knee"] + Vector((0.0, -1.0, 0.0)) * 0.4


# BUG FOUND during the fix round's own verification (not in any spec item,
# caught by re-deriving eval_all.py's per-frame numbers independently of
# the harness's own report()): settle_R() used to iterate Hips z against
# lowest_world_z_excluding(..., KICKING_L) -- foot/toe bones ONLY. At the
# moment settle_R() runs inside kick_f4()/kick_f7(), the kicking (left) LEG
# (UpLeg.L/Leg.L, not excluded by KICKING_L) is still sitting at its
# UNPOSED, idle-relative orientation, hanging from a Hips that settle_floor
# is actively dragging DOWN to plant the support foot -- so the idle-shaped
# kicking leg's shin gets dragged down WITH it and dips well below the
# floor (measured: Leg.L/Foot.L-dominant mesh as low as z=-0.376 mid-solve).
# settle_floor's Hips-z iteration then converges "lo" to zero against THAT
# contaminating low point, not the actual support foot, and stops early --
# leaving the real support foot floating ~0.047 above the true floor (out
# of the +0.02 band) at every one of f4/f7/f9. This is invisible to
# report()'s own numbers because report() re-measures with the (correct,
# narrow) KICKING_L exclusion AFTER the whole pose (including the kicking
# leg's own final leg_ik) is built -- by then the kicking leg is back above
# the floor at its proper chamber/impact height and the residual reads as
# a small, easy-to-miss 0.047, not the -0.39 it transiently reached mid-
# build. It also explains why SPEC_fix_v3 item 4's between-key pinning
# pass for Yemoja_Atk_Kick never converged (6/7/4/6/4/4 fixes over 6
# passes, hitting the pass cap without reaching 0): _kick_check_fix's
# "support" branch re-runs settle_R() every time it fires, hitting this
# same contamination on every pass and re-corrupting frame 7 worse each
# time (an interpolated key was rebuilt on top of a leg that had briefly
# gone -0.39 underground mid-fix).
#
# Fix: widen the exclusion used ONLY for this Hips-z convergence loop to
# the WHOLE kicking-leg chain (UpLeg.L/Leg.L in addition to the foot/toe),
# so the iteration is driven purely by the support foot (and everything
# else that IS already correctly posed at this point), never by a kicking
# leg that hasn't been given its own pose yet. KICKING_L itself (foot/toe
# only) is untouched and still used everywhere the spec's "except the
# kicking foot" language actually means the foot -- report()'s display,
# and _kick_check_fix's own per-frame checks, where by construction the
# kicking leg IS already correctly posed (either a real key, built after
# settle_R() returns, or an interpolated frame, where the leg's transit
# through the air is exactly what _kick_check_fix's separate "kicking"
# branch already exists to police).
KICK_LEG_L_FULL = ["UpLeg.L", "Leg.L", "Foot.L", "ToeBase.L", "Toe_End.L"]


def settle_R():
    """Kick f4/f7/f9's support (right) foot: FABLE floor-plant, see above.
    exclude=KICK_LEG_L_FULL (not just KICKING_L): see the bug note above --
    the entire kicking leg, not just the foot, must be excluded from the
    Hips-z convergence target, because it is still unposed/idle-shaped
    every time this runs (leg_ik("L",...) for the kicking leg always comes
    AFTER settle_R() in every pose function below, so by the time settle_R()
    finishes and Hips is final, the kicking leg's own subsequent leg_ik call
    solves correctly against the now-settled Hips -- no reordering needed,
    only a wider exclusion during the iteration itself)."""
    return H.settle_floor(L, KICK_ANKLE_TARGET, "R", KICK_TARGET_DROP,
                           KICK_SUPPORT_KNEE_HINT, exclude=KICK_LEG_L_FULL)


def settle_R_frac(frac):
    """SPEC_fix_v5.md item 8: a FRACTIONAL settle_R() -- lerps the support
    ankle world target `frac` of the way from its unsettled (idle-raised)
    position to the fully-flat KICK_ANKLE_TARGET (and scales the floor drop
    the same way), so the single-frame settle step VERIFY_attacks_v4.md
    measured (0.143 world units in one frame, both ends of the clip) can be
    spread across several breakdown frames instead."""
    tgt = IDLE_SNAP["R"]["head"].lerp(KICK_ANKLE_TARGET, frac)
    return H.settle_floor(L, tgt, "R", KICK_TARGET_DROP * frac,
                           KICK_SUPPORT_KNEE_HINT, exclude=KICK_LEG_L_FULL)

# A moderately-closed left fist. Spec's literal curl (70,95,60)/thumb(30,40,20)
# crushes the left hand badly on its own, independent of arm position -- e.g.
# HandPinky3.L 0.646 (7 crushed/63), HandRing3.L 0.659 (5/63) with ZERO arm
# motion, just the fingers curled that far. This rig has no per-finger-joint
# weight smoothing (README 12 only smoothed the wrist/elbow), so any tight
# curl folds the finger mesh -- README 14 documents the identical problem on
# the RIGHT hand's grip fingers, which is why those are the one exemption the
# spec grants. Eased to (50,68,42)/thumb(24,32,16): still reads as a closed
# fist (finger-level detail is invisible at silhouette scale anyway -- see
# BUILD_NOTES) while keeping the worst non-exempt ratio well clear of total
# collapse. Some regions (esp. HandPinky2.L) are already below the 0.90 floor
# in the UNTOUCHED idle master pose (0.859, zero attack posing at all) and
# cannot be fixed by any pose choice; documented, not silently accepted.
FISTCURL = (50, 68, 42)
FISTTHUMB = (24, 32, 16)
FIST_SPREAD = None  # set below once L is loaded


def fist_spread():
    return L.COILED_HAND["spread"]


def pin_both():
    H.pin_foot(L, "L", IDLE_SNAP)
    H.pin_foot(L, "R", IDLE_SNAP)


# Pinning Hand.R's WORLD matrix (position+orientation) exactly to idle while
# Arm.R/ForeArm.R re-solve to compensate for Hips motion forces Hand.R's own
# LOCAL rotation to absorb whatever the elbow solve didn't -- measured
# directly, with pronation=0 this pushed Hand.R's raw twist_deg (relative to
# REST, not idle) to 33-37deg at several Kick frames, over the 30deg budget
# (idle itself was a constant -12.2, never touched before this v4 fix
# existed). apply_pole._limb_ik's own `pronation` param (an explicit twist
# about the forearm's axis) cancels this almost exactly 1:1 -- swept kick_f4
# (needs +30 for ~zero) and kick_f7 (needs +22) together for the shared
# constant with the lowest worst-case |twist| across both: +27 gives
# f4=4.96, f7=-7.09 (worst 7.09, comfortably under budget; the un-tuned
# extremes at 0 were 33/21).
KICK_TRIDENT_PRONATION = 27.0


def pin_trident_hand():
    """SPEC_rebuild_v4.md, Yemoja_Atk_Kick only: re-solve Arm.R/ForeArm.R
    (hinge-safe under V4) so Hand.R returns to EXACTLY its idle world
    position, then force Hand.R's own matrix back to the idle snapshot too
    (same technique pin_foot uses for Foot -- IK only guarantees the HEAD
    lands on target, the snapshot's full matrix restores the grip's exact
    orientation as well). Since the trident is rigidly parented to Hand.R
    with a fixed local transform, this reproduces the trident's idle world
    transform exactly regardless of where Hips/torso have moved -- the butt
    stays at z=0.000 (not merely inside the 0.02 budget) at every frame.
    KICK_TRIDENT_PRONATION keeps Hand.R's own resulting twist near zero
    (see that constant's own comment). Call AFTER apply_captured_grip("R")
    (which this overrides for Hand.R itself -- the captured grip is idle-
    derived anyway, so this is a no-op on the grip's shape) and BEFORE
    attach_trident() (which reads Hand.R's final matrix to place the prop)."""
    target = IDLE_HAND_R["head"]
    if V4:
        ok, hinge = H.arm_ik_hinge(L, "R", target, IDLE_ELBOW_R, off_hinge=0.0,
                                    pronation=KICK_TRIDENT_PRONATION)
    else:
        ok = L.arm_ik("R", target, IDLE_ELBOW_R)
    pb = A.pose.bones[L.full("Hand.R")]
    pb.matrix = A.matrix_world.inverted() @ IDLE_HAND_R["matrix"]
    bpy.context.view_layer.update()
    err = ((A.matrix_world @ pb.head) - IDLE_HAND_R["head"]).length
    return ok, err


# HardKick's support (left) foot uses H.pivot_support_foot(), not pin_both():
# it stays planted at the idle ankle POSITION throughout but pivots on the
# spot as the hips yaw +15..+70..+95..+15 -- see that function's docstring
# in harness.py for why (rotating Foot.L alone against an unrotated knee
# hint, tried first, pushed Foot.L twist to -34/-43 deg at f12/f16, over the
# 20 deg budget; pivoting the whole leg -- knee hint and foot orientation
# together -- keeps it at 3-4 deg).


# =====================================================================
# Yemoja_Atk_Punch -- left jab, 14 frames, impact f7
# =====================================================================
def punch_f1():
    RESET_POSE(L)


def punch_f4():
    RESET_POSE(L)
    L.rot("Hips", "Y", 8)
    L.loc("Hips", 0, -3, 0)          # drop 3 armature units (0.03 world) -- spec's bare "units"
    pin_both()
    L.rot("Shoulder.L", "Y", 5)      # retract 5 (spec, exact)
    ARM("L", (0.55, 0.05, 5.55), (0.45, 0.15, 5.35))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.apply_captured_grip("R")
    L.attach_trident()


def punch_f7():
    RESET_POSE(L)
    L.rot("Hips", "Y", -12)
    pin_both()
    L.rot("Spine1", "Y", -4)
    L.rot("Spine2", "Y", -4)
    # SPEC_fix_v3.md item 7 (FABLE): elevation only for arms above shoulder
    # height -- this is a straight jab, never above the shoulder, so
    # Shoulder.L elevation is capped at 8 deg (was 24, chasing a 0.95 ratio
    # that isn't the spec's call to make -- see BUILD_NOTES for the measured
    # ratio at 8). Protract stays at the spec's exact 18. Added Shoulder.R
    # retraction 6 (off-side clavicle follows the punch back), sign per the
    # rig's own convention -- Shoulder.R retract is -Y, matching the -8
    # retract already used for HardPunch's off-side shoulder at f7 below.
    L.rot("Shoulder.L", "Y", -18)
    L.rot("Shoulder.L", "Z", 8)
    L.rot("Shoulder.R", "Y", -6)
    # Fist reach: spec target (0.45,-1.95,5.75) is 2.054 world units from the
    # (moved) shoulder; max straight-arm reach is (Arm+ForeArm)*0.97 = 1.683.
    # limb_ik's own tolerance is 99.9% of raw length, so an unclamped call
    # reports ok=False. clamp_reach pulls the target back along the SAME ray
    # (direction/height preserved) to the 0.97-of-max budget -- frac=1.0 here
    # matches the spec's own 0.97 factor exactly (no extra safety margin).
    # v4: clamp_reach measures S from the CURRENT shoulder head, so the
    # literal spec target must be retargeted to the new idle FIRST -- passing
    # the raw v114-relative literal in directly would clamp against the wrong
    # ray. Once clamped, tgt/pole are both already current-frame, so this
    # goes through ARM_CURRENT (not ARM, which would retarget a second time).
    base = H.retarget_shoulder(L, "L", (0.45, -1.95, 5.75)) if V4 else Vector((0.45, -1.95, 5.75))
    tgt, clamped, dorig, dbudget = H.clamp_reach(L, "Arm.L", "ForeArm.L", base, frac=1.0)
    pole = H.retarget_shoulder(L, "L", (0.9, -0.9, 5.4)) if V4 else (0.9, -0.9, 5.4)
    ok = ARM_CURRENT("L", tgt, pole)
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.rot("Neck", "Y", 12)
    L.rot("Head", "Y", 8)
    H.aim_head(L, target_angle=0.0, tol=10.0)   # item 5: face within +-10 deg of +Z
    L.apply_captured_grip("R")
    L.attach_trident()
    assert ok


def punch_f9():
    RESET_POSE(L)
    L.rot("Hips", "Y", -14)
    # SPEC_fix_v3.md item 9: keep the fist where it is but drive the strike
    # from the torso -- Hips forward 0.05 world (-Y), on top of the yaw.
    dx, dy, dz = H.world_delta_to_armature((0.0, -0.05, 0.0))
    L.loc("Hips", dx, dy, dz)
    pin_both()
    L.rot("Spine1", "Y", -4)
    L.rot("Spine2", "Y", -4)
    L.rot("Shoulder.L", "Y", -18)
    L.rot("Shoulder.L", "Z", 8)      # item 7, same cap as f7
    L.rot("Shoulder.R", "Y", -6)
    bpy.context.view_layer.update()
    # v4: retarget the v114-relative base point FIRST, then build "0.1
    # further out" from the CURRENT shoulder -- computing dirv from a mix of
    # an old-relative literal and the new Arm.L head would point the wrong
    # way. target is then already current-frame, so it goes through
    # ARM_CURRENT (not ARM, which would retarget it a second time); the pole
    # is still an old-relative literal and is retargeted explicitly.
    base = H.retarget_shoulder(L, "L", (0.45, -1.95, 5.75)) if V4 else Vector((0.45, -1.95, 5.75))
    Sw = L.a2w(A.pose.bones[L.full("Arm.L")].head.copy())
    dirv = (base - Sw).normalized()
    target = base + dirv * 0.1   # "0.1 further out"
    target, clamped, dorig, dbudget = H.clamp_reach(L, "Arm.L", "ForeArm.L", target, frac=1.0)
    pole = H.retarget_shoulder(L, "L", (0.9, -0.9, 5.4)) if V4 else (0.9, -0.9, 5.4)
    ok = ARM_CURRENT("L", target, pole)
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.rot("Neck", "Y", 14)
    L.rot("Head", "Y", 9)
    H.aim_head(L, target_angle=0.0, tol=10.0)
    L.apply_captured_grip("R")
    L.attach_trident()
    assert ok


PUNCH_KEYS = [(1, punch_f1), (4, punch_f4), (7, punch_f7), (9, punch_f9), (14, punch_f1)]


# =====================================================================
# Yemoja_Atk_HardPunch -- trident thrust, 26 frames, impact f12
# =====================================================================
# v4: under the new idle (Hips moved -14.7 armature X, legs re-solved to
# compensate), a yaw+lunge sweep at the OLD LUNGE_SCALE=0.95/yaw=25 combo
# gave a rear-ankle pin error of 0.00837 (budget 0.005) -- REGARDLESS of
# lunge-depth scale (swept 0.5-0.95, error plateaus around 0.0075-0.0086,
# never clearing budget), so the v3 fix (scale the DEPTH down 5%) no longer
# works: it isn't depth that's infeasible here, it's the yaw. Bisected yaw
# alone (translation held at FULL spec depth, scale=1.0): yaw 20 errs 0,
# yaw 25 errs 0.00884, yaw 24 errs 0.00445 (under budget), yaw 23.5 errs
# 0.00227 (comfortable margin). Kept HP_YAW=23.5 (1.5deg off the spec's 25,
# visually negligible) and LUNGE_SCALE=1.0 -- this reproduces the spec's
# lunge DEPTH exactly (better than v3's own 5% depth cut) at the cost of a
# small yaw deviation instead. v3/non-V4 builds keep the original 25/0.95
# pair unchanged (that pair is a proven fit for the OLD idle geometry).
HP_YAW = 23.5 if V4 else 25.0
LUNGE_SCALE = 1.0 if V4 else 0.95   # see hardpunch_f12 -- pin_foot tolerance
# v4: the new trident offset (yemoja_anim_lib_v115's TRIDENT_U origin) and
# new idle/arm geometry moved every hand-Y free-spin optimum found under v3
# -- re-measured with the OLD hints in place, Hand.R twist blew the 30deg
# budget at EVERY orient_hand_for_shaft key (HardPunch f7 -32.2, f12/f15
# -66.7/-63.1; HardKick f6 -58.5, f12 -117.4, f16 -125.6, f22 -46.0). Re-swept
# each the same way as v3 (5deg coarse step over the full circle in the
# hand-local plane perpendicular to the shaft, min |twist| subject to zero
# trident_penetration_bad() runs at n_samples=201 AND 401, then 1deg refine
# around the coarse winner, then a +-3deg stability check). All 7 re-swept
# points landed clean with comfortable margin except HardKick f22, whose
# window closes at +115deg (a thin pocket, same shape v3 saw at its own f16)
# -- backed off to +108deg (7deg of margin) instead of the literal nearest-
# zero-twist point at +114deg (1deg margin), trading 2.6deg of extra twist
# (-10.78 vs -8.15, still comfortably under the 30deg budget) for real
# clearance from the pocket. v3/non-V4 builds keep the original hints.
HP_HINT7 = Vector((0.694658, 0.719340, 0.0)) if V4 else Vector((0.0, 1.0, 0.0))
HP_HINT12 = (-0.958820, -0.284015, 0.0) if V4 else (-0.87, 0.5, 0)   # see hardpunch_f12 -- twist/clearance tuning


def hardpunch_f1():
    RESET_POSE(L)


def hardpunch_f7():
    RESET_POSE(L)
    L.rot("Hips", "Y", -18)
    dx, dy, dz = H.world_delta_to_armature((-0.15, 0.20, -0.10))
    L.loc("Hips", dx, dy, dz)
    pin_both()
    L.rot("Shoulder.R", "Y", -8)   # retract 8 (spec, exact)
    # v4: retarget before clamp_reach (see punch_f7), then ARM_CURRENT since
    # tgt/pole are already current-frame.
    base = H.retarget_shoulder(L, "R", (-1.30, 1.85, 5.30)) if V4 else Vector((-1.30, 1.85, 5.30))
    tgt, clamped, dorig, dbudget = H.clamp_reach(L, "Arm.R", "ForeArm.R", base, frac=1.0)
    pole = H.retarget_shoulder(L, "R", (-1.6, 1.2, 5.0)) if V4 else (-1.6, 1.2, 5.0)
    ok = ARM_CURRENT("R", tgt, pole)
    L.orient_hand_for_shaft((0, 0, 1), HP_HINT7)
    L.apply_captured_grip("R")
    okL = ARM("L", (0.65, -0.45, 5.9), (0.5, -0.1, 5.6))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    H.aim_head(L, target_angle=0.0, tol=10.0)   # item 5
    L.attach_trident()
    assert ok and okL


def hardpunch_f12():
    RESET_POSE(L)
    L.rot("Hips", "Y", HP_YAW)
    # Hips lunge translation (0.10,-0.35,-0.15)*LUNGE_SCALE -- see HP_YAW's
    # own comment above for the v4 yaw/depth tradeoff (this is now full
    # spec depth under v4, LUNGE_SCALE=1.0).
    dx, dy, dz = H.world_delta_to_armature((0.10 * LUNGE_SCALE, -0.35 * LUNGE_SCALE, -0.15 * LUNGE_SCALE))
    L.loc("Hips", dx, dy, dz)
    pin_both()
    L.rot("Spine1", "Y", 6)
    L.rot("Spine2", "Y", 6)
    L.rot("Shoulder.R", "Y", 20)     # protract 20 (spec, exact)
    # SPEC_fix_v3.md item 7 (FABLE): elevation only for arms above shoulder
    # height. Capped at 8 (was 15). The resulting Shoulder.R/L ratios are
    # reported, not chased -- see BUILD_NOTES ("a joint-weight issue for the
    # model of record, not a pose issue").
    L.rot("Shoulder.R", "Z", -8)
    # v4: retarget before clamp_reach (see punch_f7), then ARM_CURRENT.
    base = H.retarget_shoulder(L, "R", (-0.55, -1.40, 5.45)) if V4 else Vector((-0.55, -1.40, 5.45))
    tgt, clamped, dorig, dbudget = H.clamp_reach(L, "Arm.R", "ForeArm.R", base, frac=1.0)
    pole = H.retarget_shoulder(L, "R", (-0.9, -0.4, 4.9)) if V4 else (-0.9, -0.4, 4.9)
    ok = ARM_CURRENT("R", tgt, pole)
    # Hand-Y hint DEVIATED from the windup's (0,1,0): at that hint here, the
    # forearm/hand geometry produced Hand.R twist -58 deg (budget is 30) and
    # trident_clearance 0.027 (budget 0.15). Swept the hint through a full
    # circle in the hand-local plane perpendicular to the shaft; (-0.87,0.5,0)
    # (-150 deg from the windup hint) is the point nearest zero twist that also
    # clears the body: twist 3.4 deg, clearance 0.289. Shaft direction itself
    # is unchanged (armature +Z, per spec).
    L.orient_hand_for_shaft((0, 0, 1), HP_HINT12)
    L.apply_captured_grip("R")
    # SPEC_fix_v3.md item 7: the off-side (left) shoulder RETRACTS 6 instead
    # of elevating (this arm never goes above shoulder height -- the old
    # +16 elevate was exactly the "elevation for an arm that isn't raised"
    # VERIFY flagged). Sign: Shoulder.L retract is +Y (mirror of the R
    # retract used at f7/hardkick).
    L.rot("Shoulder.L", "Y", 6)
    okL = ARM("L", (0.75, 0.70, 5.2), (0.6, 1.0, 4.7))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.rot("Neck", "Y", -14)
    L.rot("Head", "Y", -9)
    H.aim_head(L, target_angle=0.0, tol=10.0)   # item 5
    L.attach_trident()
    assert ok and okL


def hardpunch_f15():
    RESET_POSE(L)
    L.rot("Hips", "Y", HP_YAW)
    # SPEC_fix_v3.md item 9 DEVIATED: the literal "+0.05 world forward" is
    # infeasible here -- f12's own lunge is already at (under v4) full spec
    # depth (LUNGE_SCALE=1.0, see HP_YAW's comment); re-checked directly
    # under v4 (yaw=HP_YAW): y=-0.35 (matching f12 exactly) errs 0.00227,
    # y=-0.38 (+0.03 further) jumps to 0.01416, well over budget -- still
    # infeasible to carry further, same conclusion as v3, just at the new
    # geometry's own numbers. Kept the same y=-0.3330 depth found to work
    # (errs 0.0 here) and carried the rest of the "torso drives further"
    # intent as an extra 3 deg of forward lean on Spine1/Spine2 (on top of
    # the existing Y protraction) instead, which doesn't touch the pinned
    # rear ankle at all.
    dx, dy, dz = H.world_delta_to_armature((0.10 * LUNGE_SCALE, -0.3330, -0.15 * LUNGE_SCALE))
    L.loc("Hips", dx, dy, dz)
    pin_both()
    L.rot("Spine1", "Y", 6)
    L.rot("Spine2", "Y", 6)
    L.rot("Spine1", "X", 3)   # item 9: extra forward drive, carried here instead
    L.rot("Spine2", "X", 3)
    L.rot("Shoulder.R", "Y", 20)
    L.rot("Shoulder.R", "Z", -8)   # item 7, same cap as f12
    bpy.context.view_layer.update()
    # v4: retarget the v114-relative base point FIRST (see punch_f9), then
    # build "0.1 further out" from the CURRENT shoulder; clamp_reach and
    # ARM_CURRENT both then operate on already-current-frame points.
    base = H.retarget_shoulder(L, "R", (-0.55, -1.40, 5.45)) if V4 else Vector((-0.55, -1.40, 5.45))
    Sw = L.a2w(A.pose.bones[L.full("Arm.R")].head.copy())
    dirv = (base - Sw).normalized()
    target = base + dirv * 0.1
    target, clamped, dorig, dbudget = H.clamp_reach(L, "Arm.R", "ForeArm.R", target, frac=1.0)
    pole = H.retarget_shoulder(L, "R", (-0.9, -0.4, 4.9)) if V4 else (-0.9, -0.4, 4.9)
    ok = ARM_CURRENT("R", target, pole)
    L.orient_hand_for_shaft((0, 0, 1), HP_HINT12)
    L.apply_captured_grip("R")
    L.rot("Shoulder.L", "Y", 6)     # item 7, same retract as f12
    okL = ARM("L", (0.75, 0.70, 5.2), (0.6, 1.0, 4.7))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.rot("Neck", "Y", -14)
    L.rot("Head", "Y", -9)
    H.aim_head(L, target_angle=0.0, tol=10.0)
    L.attach_trident()
    assert ok and okL


HARDPUNCH_KEYS = [(1, hardpunch_f1), (7, hardpunch_f7), (12, hardpunch_f12),
                  (15, hardpunch_f15), (26, hardpunch_f1)]


# =====================================================================
# Yemoja_Atk_Kick -- left front snap kick, 16 frames, impact f7
# =====================================================================
def kick_f1():
    RESET_POSE(L)


def kick_f4():
    RESET_POSE(L)
    dx, dy, dz = H.world_delta_to_armature((-0.25, 0.05, -0.10))
    L.loc("Hips", dx, dy, dz)
    settle_R()   # item 3 (FABLE): support foot settles flat on the floor, not idle-raised
    ok = LEG("L", (0.45, -0.25, 2.6), (0.45, -0.9, 3.4))
    L.rot("Foot.L", "X", -20)     # toes pulled up (spec, exact)
    L.rot("ToeBase.L", "X", -15)  # spec, exact
    okL = ARM("L", (0.55, -0.5, 5.7), (0.4, -0.2, 5.4))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.apply_captured_grip("R")
    okR, errR = pin_trident_hand()   # v4: trident butt planted, see docstring
    L.attach_trident()
    assert ok and okL and okR
    assert errR < 0.005


def kick_f7():
    RESET_POSE(L)
    L.rot("Hips", "Y", -10)
    L.rot("Hips", "X", -6)
    settle_R()   # item 3 (FABLE)
    bpy.context.view_layer.update()
    # Spec's literal ankle target (0.40,-1.90,3.1) is only 2.29 world units
    # from the (moved) hip socket against a max straight-leg reach of 3.96 --
    # 58% extension, which leaves the knee bent roughly 38% of leg-length
    # sideways (measured via the 2-bone triangle: h=1.49 of a 3.96 chain) and
    # does not read as "leg straight, foot ball leading" as the spec asks --
    # confirmed by render, the raised leg looked chambered, not extended.
    # Extended the SAME ray (direction/height preserved, from the hip socket
    # through the spec target) out to 88% of max reach instead. This keeps
    # the leg's line of action identical to what the spec describes, just
    # carried further along it; the leg-region deformation at that extension
    # is fine (Leg.L 0.984, Foot.L 1.003, UpLeg.L 1.024 -- README 4 already
    # notes high hip flexion/full extension is safe on this rig).
    S = L.a2w(A.pose.bones[L.full("UpLeg.L")].head.copy())
    l1 = A.pose.bones[L.full("UpLeg.L")].length
    l2 = A.pose.bones[L.full("Leg.L")].length
    # v4: retarget the v114-relative spec point (hips landmark) BEFORE using
    # it to build a ray direction from the CURRENT (new-idle) hip socket --
    # using the un-retargeted old point here would point the ray at the
    # wrong place relative to the new UpLeg.L head.
    spec_target = H.retarget_hips(L, (0.40, -1.90, 3.1)) if V4 else Vector((0.40, -1.90, 3.1))
    dirv = (spec_target - S).normalized()
    target = S + dirv * (l1 + l2) / 100.0 * 0.88
    ok = L.leg_ik("L", target, H.retarget_hips(L, (0.45, -1.2, 3.6)) if V4 else (0.45, -1.2, 3.6))
    # SPEC_fix_v3.md item 6: plantarflex the striking foot so it's ball-of-
    # foot leading, not a pointed toe. VERIFY measured this foot never
    # articulated at all before (100.7 deg, same as idle). Target the middle
    # of the spec's 20-35 deg band; detwist afterward to keep Foot.L's own
    # twist near idle (same technique as HardKick's impact feet).
    H.plantarflex_and_detwist(L, "L", target_toe_angle=27.0, target_twist=0.0)
    L.rot("Spine", "X", 4)
    L.rot("Shoulder.L", "Z", 5)
    okL = ARM("L", (0.6, -0.6, 5.8), (0.4, -0.3, 5.5))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    L.apply_captured_grip("R")
    okR, errR = pin_trident_hand()   # v4: trident butt planted, see docstring
    L.rot("Neck", "Y", 10)
    L.rot("Head", "Y", 7)
    H.aim_head(L, target_angle=0.0, tol=10.0)   # item 5
    L.attach_trident()
    assert ok and okL and okR
    assert errR < 0.005


KICK_KEYS = [(1, kick_f1), (4, kick_f4), (7, kick_f7), (9, kick_f4), (16, kick_f1)]


# =====================================================================
# Yemoja_Atk_HardKick v2 -- rear-leg (right) ROUNDHOUSE, 28 frames, impact f12
# Rebuilt per SPEC_hardkick_v2.md. v1 read as a straight-up front high kick
# and the trident lay flat across her chest at f6/f22; the reviewer's fix
# (this v2) is a horizontal arc: pelvis opens (Hips yaw POSITIVE brings the
# right hip forward), the support foot pivots on the spot via the new
# H.pivot_support_foot() (rotates the whole support leg -- knee hint AND
# foot orientation together -- about world-vertical through the idle ankle,
# so Foot.L twist stays near idle instead of the -34..-43 deg v1 produced by
# rotating Foot.L's orientation alone against an unrotated knee hint), and
# the knee sits to her right (-X) of the hip->foot line at impact.
# Two shaft-orientation hints below (f6/f22 share one, f12/f16 share another)
# were found by sweeping the free hand-Y spin DOF in the plane perpendicular
# to the spec's fixed shaft direction, in 3 deg steps, for min |twist| with
# trident_clearance >= 0.15 -- see BUILD_NOTES.md v2 section for the numbers.
# v4: re-swept for the new trident offset/geometry (see HP_HINT7's own
# comment above for the method and why every old hint blew its twist
# budget) -- f6 landed clean with wide margin (+-3deg stable, pocket-free at
# 401 samples); f22 backed off from the nearest-zero-twist point (+114deg
# from its own reference axis, 1deg from a thin pocket) to +108deg (7deg
# margin, twist -10.78 vs -8.15, still well under the 30deg budget).
HK_HINT_UP = Vector((0.868377, -0.066488, -0.491427)) if not V4 else Vector((-0.684767, -0.545905, -0.482786))    # for shaft ~(-0.25,0.80,-0.55), f6
HK_HINT_UP22 = Vector((0.738885, -0.209417, -0.640463)) if not V4 else Vector((-0.299254, -0.602303, -0.740053))  # same shaft, f22 (different body pose)
# f12/f16 shaft direction changed under SPEC_fix_v3.md item 2 (FABLE) to
# armature normalised(-0.20,0.95,-0.22) -- nearly vertical, tip up, butt
# behind her right hip -- from v2's diagonal (-0.35,0.55,-0.75). The v2
# hand-Y hints (found by sweeping the old shaft direction) no longer apply;
# HK_HINT_BACK12/16 are re-swept for the new shaft/hand-position pair by
# scratch_hk_hint_sweep.py (see BUILD_NOTES for the measured twist/
# penetration numbers) and pasted in below.
# Re-swept 5deg then 1-2deg step over the free hand-Y spin DOF for min
# |twist| subject to zero non-grip trident_penetration_bad() runs (checked
# at BOTH n_samples=201, harness's own default, AND 401, pen.py's own --
# the penetration pocket here is thin enough that a coarser sample count
# can step over it entirely, see BUILD_NOTES) AND shaft-to-UpLeg.R/Leg.R
# segment distance > 0.25 at the built pose. f16: the near-vertical shaft
# at this hand position passes close behind her Hips for most of the spin
# range -- deg 319 is the last clean degree before the window closes (deg
# 320 already shows a thin 0.107-deep pocket at s=0.285, invisible at 151
# samples, real at 201/401); twist -26.36 (budget 30, 3.6 deg of margin).
# f12: HK_HINT_BACK12 re-swept a second time (deg 345, 5deg step) after
# raising Shoulder.L/R elevation below (item 8's ratio floor) shifted the
# arm/chest geometry enough to move the hand-Y free-spin optimum; twist
# -15.0 (budget 30), pen=0, shaft-to-UpLeg.R/Leg.R segment dist unchanged.
# v4: re-swept for f12/f16's own shaft under the new geometry -- both
# landed clean with wide (+-3deg stable) margin (see HP_HINT7's comment).
HK_HINT_BACK12 = Vector((0.946229, 0.247458, 0.208362)) if not V4 else Vector((0.926238, 0.111621, -0.360033))
HK_HINT_BACK16 = Vector((0.739320, 0.295736, 0.604935)) if not V4 else Vector((0.979459, 0.191769, -0.062324))
# item 7: Shoulder.R elevation at f16 capped to <= 1/3 of Arm.R's own rise
# (the upper-arm bone's elevation angle above horizontal, f12 -> f16, with
# Shoulder.R itself held at 0 for the measurement so it isn't measuring its
# own assist): measured -7.68 -> -2.54 deg, a rise of 5.14 deg, so the cap
# is 1.71 deg. Swept 0/8/11/15/20 anyway to confirm the ratio barely moves
# in this range (0.64 -> 0.72 Yemoja_Body/Shoulder.R over that whole span)
# -- the follow-through arm just isn't rising enough here for clavicle
# elevation to be the fix; reported as a measured rig limit in BUILD_NOTES
# rather than pushed past what the actual arm motion supports.
HK_F16_SHOULDER_R = 1.71


def hardkick_f1():
    RESET_POSE(L)


def hardkick_f6():
    RESET_POSE(L)
    L.rot("Hips", "Y", 25)
    dx, dy, dz = H.world_delta_to_armature((0.30, -0.10, -0.15))
    L.loc("Hips", dx, dy, dz)
    okS, errS = H.pivot_support_foot(L, "L", IDLE_SNAP, 15)
    ok = LEG("R", (-1.15, 0.80, 2.2), (-1.55, 0.25, 3.4))
    L.rot("ToeBase.R", "X", 20)      # toes relaxed/down ~20 (spec, exact)
    # Spine chain totals (X -6, Y -8) split evenly across the 3-bone chain --
    # no per-bone split given by the spec, and an even split reads smoothly.
    for b in ("Spine", "Spine1", "Spine2"):
        L.rot(b, "X", -2.0)
        L.rot(b, "Y", -2.667)
    # Neck/Head Y -15 "total" (spec's own wording) -- split 8/7.
    L.rot("Neck", "Y", -8)
    L.rot("Head", "Y", -7)
    H.aim_head(L, target_angle=0.0, tol=15.0)   # item 5
    okL = ARM("L", (0.55, -0.50, 5.70), (0.95, -0.1, 5.2))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    # SPEC_fix_v5.md item 5: sweep the pole/hand-hint for Arm.R/Shoulder.R
    # >= 0.90 (VERIFY_attacks_v4.md measured 0.87/0.87 here) instead of the
    # single fixed pole/hint pair -- see H.sweep_arm_pole_and_hint's own
    # docstring; result recorded in V5_REPORT for BUILD_NOTES.
    shaft = Vector((-0.25, 0.80, -0.55)).normalized()
    sw = H.sweep_arm_pole_and_hint(L, ARM, "R", (-1.35, 1.30, 5.05), (-1.6, 0.9, 4.8),
                                    shaft, HK_HINT_UP)
    V5_REPORT.setdefault("item5", {})["HardKick_f6"] = sw
    okR = sw["ok"]
    assert ok and okL and okR and okS
    assert errS < 0.005


def hardkick_f12():
    RESET_POSE(L)
    L.rot("Hips", "Y", 75)
    dx, dy, dz = H.world_delta_to_armature((0.35, -0.05, -0.22))
    L.loc("Hips", dx, dy, dz)
    okS, errS = H.pivot_support_foot(L, "L", IDLE_SNAP, 55)
    ok = LEG("R", (0.10, -1.60, 5.85), (-1.35, -0.55, 5.30))
    # SPEC_fix_v3.md item 6: plantarflex+detwist via bisection instead of the
    # v2 hardcoded X=-65/Y=+30 -- self-correcting if upstream geometry
    # (spine/arm changes below) shifts the leg slightly. Target 14.5 (within
    # the spec's 15 deg budget, matching v2's own measured result).
    H.plantarflex_and_detwist(L, "R", target_toe_angle=14.5, target_twist=0.0)
    # Spine chain totals (X -20 lean back, Z -10 tilt to her left, Y -15
    # chest turns back toward opponent) split evenly across the 3-bone chain.
    for b in ("Spine", "Spine1", "Spine2"):
        L.rot(b, "X", -6.667)
        L.rot(b, "Z", -3.333)
        L.rot(b, "Y", -5.0)
    L.rot("Neck", "Y", -25)
    L.rot("Head", "Y", -30)
    H.aim_head(L, target_angle=0.0, tol=15.0)   # item 5 (HardKick budget +-15)
    # SPEC_fix_v3.md item 8 (FABLE): left arm target in Spine2's POSED
    # (chest-local) frame, not world space -- the old world-space target
    # crushed Shoulder.L to 0.75 once the chest yawed 75-95 deg out from
    # under it. Elbow pole chest-local too (same pole for f12/f16, per spec).
    tgtL = H.chest_local_target(L, forward=0.45, down=0.95, left=0.35)
    poleL = H.chest_local_target(L, forward=0.1, down=0.6, left=0.9)
    rtgtR = (-1.30, 1.25, 6.00)
    # SPEC_fix_v5.md item 4: v4's own literal 40/-46 (chosen to sweep
    # Shoulder.L/R up to the 0.90 deformation floor) blew the 30deg clavicle
    # cap VERIFY_attacks_v4.md measured (40.1/46.7). Replaced with the
    # spec's formula -- min(30, 1/3 of the arm's rise above shoulder height,
    # 0 if not above shoulder height) -- measured BEFORE either shoulder is
    # rotated (elevL/elevR use the pre-elevation Arm.L/Arm.R socket, per
    # H.clavicle_elevation_deg's own docstring); the resulting Arm.L/
    # Shoulder.L/Arm.R/Shoulder.R ratios are accepted and reported per spec
    # rather than pushed back up past the cap.
    elevL = H.clavicle_elevation_deg(L, "L", tgtL)
    elevR = H.clavicle_elevation_deg(L, "R", rtgtR)
    V5_REPORT.setdefault("item4", {})["HardKick_f12"] = dict(elevL=elevL, elevR=elevR)
    L.rot("Shoulder.L", "Z", elevL)
    L.rot("Shoulder.R", "Z", -elevR)
    okL = ARM_CURRENT("L", tgtL, poleL)
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    # SPEC_fix_v3.md item 2 (FABLE): right hand world position and shaft
    # direction given exactly -- trident stays BEHIND her body plane, nearly
    # vertical, tip up, butt hanging down behind her right hip (replaces the
    # v2 target/shaft that swung the butt end through Spine1).
    okR = ARM("R", rtgtR, (0.9, 1.5, 4.9))
    shaft = Vector((-0.20, 0.95, -0.22)).normalized()
    L.orient_hand_for_shaft(shaft, HK_HINT_BACK12)
    L.apply_captured_grip("R")
    L.attach_trident()
    assert ok and okL and okR and okS
    assert errS < 0.005


def hardkick_f16():
    RESET_POSE(L)
    L.rot("Hips", "Y", 95)
    dx, dy, dz = H.world_delta_to_armature((0.35, 0.00, -0.20))
    L.loc("Hips", dx, dy, dz)
    okS, errS = H.pivot_support_foot(L, "L", IDLE_SNAP, 70)
    ok = LEG("R", (0.95, -1.10, 5.10), (-0.20, -1.35, 4.50))
    # item 6: "same treatment as f12" -- toe within 15 deg of the shin.
    H.plantarflex_and_detwist(L, "R", target_toe_angle=12.0, target_twist=0.0)
    for b in ("Spine", "Spine1", "Spine2"):
        L.rot(b, "X", -6.0)
        L.rot(b, "Z", -4.0)
        L.rot(b, "Y", -8.333)
    L.rot("Neck", "Y", -25)
    L.rot("Head", "Y", -30)
    H.aim_head(L, target_angle=0.0, tol=15.0)   # item 5
    # SPEC_fix_v5.md item 4: same formula as f12 (min(30, rise/3, 0 below
    # shoulder height)), replacing the old fixed 0/-HK_F16_SHOULDER_R pair
    # -- they were already close to what the formula gives (the arm barely
    # rises here), kept dynamic for consistency and so a future target
    # change can't silently reintroduce the 40/-46-style cap violation.
    tgtL = H.chest_local_target(L, forward=0.30, down=1.05, left=0.30)
    poleL = H.chest_local_target(L, forward=0.1, down=0.6, left=0.9)
    rtgtR = (-1.15, 1.30, 6.05)
    elevL = H.clavicle_elevation_deg(L, "L", tgtL)
    elevR = H.clavicle_elevation_deg(L, "R", rtgtR)
    V5_REPORT.setdefault("item4", {})["HardKick_f16"] = dict(elevL=elevL, elevR=elevR)
    L.rot("Shoulder.L", "Z", elevL)
    L.rot("Shoulder.R", "Z", -elevR)
    okL = ARM_CURRENT("L", tgtL, poleL)
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    # item 2 (FABLE): same method as f12.
    okR = ARM("R", rtgtR, (0.9, 1.5, 4.9))
    shaft = Vector((-0.20, 0.95, -0.22)).normalized()   # same direction as f12 (spec)
    L.orient_hand_for_shaft(shaft, HK_HINT_BACK16)
    L.apply_captured_grip("R")
    L.attach_trident()
    assert ok and okL and okR and okS
    assert errS < 0.005


def hardkick_f22():
    RESET_POSE(L)
    L.rot("Hips", "Y", 30)
    dx, dy, dz = H.world_delta_to_armature((0.25, -0.05, -0.12))
    L.loc("Hips", dx, dy, dz)
    okS, errS = H.pivot_support_foot(L, "L", IDLE_SNAP, 15)
    ok = LEG("R", (-0.75, 0.75, 1.00), (-1.2, 0.2, 2.6))
    for b in ("Spine", "Spine1", "Spine2"):
        L.rot(b, "X", -2.0)
        L.rot(b, "Y", -2.667)
    L.rot("Neck", "Y", -8)
    L.rot("Head", "Y", -7)
    H.aim_head(L, target_angle=0.0, tol=15.0)   # item 5
    okL = ARM("L", (0.55, -0.5, 5.70), (0.95, -0.1, 5.2))
    L.hand_shape("L", curl=FISTCURL, thumb=FISTTHUMB, spread=fist_spread())
    # SPEC_fix_v5.md item 5: same sweep as f6 -- VERIFY_attacks_v4.md
    # measured Arm.R/Shoulder.R 0.87/0.87 here too.
    shaft = Vector((-0.25, 0.80, -0.55)).normalized()   # same direction as f6 (spec)
    sw = H.sweep_arm_pole_and_hint(L, ARM, "R", (-1.35, 1.30, 5.05), (-1.6, 0.9, 4.8),
                                    shaft, HK_HINT_UP22)
    V5_REPORT.setdefault("item5", {})["HardKick_f22"] = sw
    okR = sw["ok"]
    assert ok and okL and okR and okS
    assert errS < 0.005


HARDKICK_KEYS = [(1, hardkick_f1), (6, hardkick_f6), (12, hardkick_f12),
                 (16, hardkick_f16), (22, hardkick_f22), (28, hardkick_f1)]


# =====================================================================
# SPEC_fix_v3.md item 4: between-key pinning. One check/fix closure pair per
# clip, all sharing the "skip the idle bookend keys themselves" guard --
# item 4 is about frames NOBODY explicitly posed (interpolation drift
# between keys), not about re-litigating the deliberate return-to-idle at
# each clip's first/last frame (whose own foot position/floor height IS the
# idle pose, by construction of IDLE_SNAP itself).
# =====================================================================
def _full_pin_check_fix(sides, f_first, f_last):
    def check_frame(f):
        if f in (f_first, f_last):
            return False, None
        for side in sides:
            pb = A.pose.bones[L.full("Foot." + side)]
            err = ((A.matrix_world @ pb.head) - IDLE_SNAP[side]["head"]).length
            if err > 0.005:
                return True, None
        lo = L.lowest_world_z()
        if lo < -0.005 or lo > 0.02:
            return True, None
        return False, None

    def fix_frame(f, detail):
        for side in sides:
            H.pin_foot(L, side, IDLE_SNAP)
        bpy.context.view_layer.update()
        # item 4's own wording: "re-run pin_foot (and the Hips z
        # correction)" -- iterate Hips z the same way settle_floor does,
        # re-pinning both feet each time (pin_foot re-solves the leg to the
        # fixed ankle regardless of where Hips just moved).
        lo = L.lowest_world_z()
        for _ in range(6):
            if abs(lo) < 0.005:
                break
            dx, dy, dz = H.world_delta_to_armature((0.0, 0.0, -lo))
            L.loc("Hips", dx, dy, dz)
            for side in sides:
                H.pin_foot(L, side, IDLE_SNAP)
            bpy.context.view_layer.update()
            lo = L.lowest_world_z()
        return True
    return check_frame, fix_frame


def trident_butt_z():
    L.attach_trident()
    bpy.context.view_layer.update()
    butt_local, _ = L.trident_local_ends()
    ob = bpy.data.objects["Trident"]
    return (ob.matrix_world @ butt_local).z


def _kick_check_fix(f_first, f_last):
    """Kick has THREE independent ways a frame can fail its floor/plant
    band (see the trident_bad branch below for the third, SPEC_rebuild_v4.md's
    own "butt stays planted" rule -- pin_trident_hand() pins Hand.R exactly
    at the four real keys, but the in-between BEZIER interpolation of
    Arm.R/ForeArm.R's local quaternions between two keys that reached the
    SAME world Hand.R position via DIFFERENT Hips positions is not itself
    guaranteed to hold that world position at every interpolated frame --
    measured directly, it does not). Below that, Kick has TWO independent
    eval_all.py's own per-frame scan (which checks lowz_ex AND plain lowz
    separately) is what caught the second one: settle_R() alone found
    every "support foot not settled yet" frame, but between-key quaternion
    interpolation of the KICKING leg itself (UpLeg.L/Leg.L swinging from
    hanging-down at f1 to raised at f4 in just 3 frames) can send the
    SHIN through the floor at some interpolated frames -- measured -0.40
    to -0.54 world z at f2/6/7/14/15, invisible to lowz_ex (Foot.L/
    ToeBase.L excluded) because the culprit there is Leg.L, not the foot.
    check_frame's two independent tests catch both; fix_frame runs
    whichever correction the specific violation needs (support settle via
    Hips z, kicking-leg-shin clip via lifting the LEFT ankle target)."""
    def check_frame(f):
        if f in (f_first, f_last):
            return False, None
        lo_ex = H.lowest_world_z_excluding(L, "Yemoja_Body", KICKING_L)
        lo_all = L.lowest_world_z()
        support_bad = lo_ex < -0.005 or lo_ex > 0.02
        kicking_bad = lo_all < -0.005
        trident_bad = abs(trident_butt_z()) > 0.02
        if support_bad or kicking_bad or trident_bad:
            return True, dict(support=support_bad, kicking=kicking_bad, trident=trident_bad)
        return False, None

    def fix_frame(f, detail):
        # trident re-pinned LAST (settle_R()'s own Hips-z iteration would
        # otherwise re-break an already-good plant) -- but always re-run it
        # whenever support/kicking fixed too, since either can move Hips.
        if detail["support"]:
            settle_R()
        if detail["kicking"]:
            # Lift the LEFT (kicking) leg's ankle TARGET straight up in
            # world z, keeping x/y (so the kick's forward reach/timing
            # doesn't change, just the height), re-solving with the
            # CURRENT interpolated knee as hint, until the whole body
            # clears the floor. This is the direct equivalent of
            # settle_floor's Hips-z iteration but applied to the leg
            # that's actually clipping, since shifting Hips here would
            # just as easily re-break the (already-corrected) support foot.
            pbA = A.pose.bones[L.full("Foot.L")]
            pbK = A.pose.bones[L.full("Leg.L")]
            ankle = A.matrix_world @ pbA.head
            knee = A.matrix_world @ pbK.head
            lo = L.lowest_world_z()
            for _ in range(8):
                if lo > -0.005:
                    break
                ankle = Vector((ankle.x, ankle.y, ankle.z + (-lo) + 0.005))
                L.leg_ik("L", ankle, knee)
                bpy.context.view_layer.update()
                lo = L.lowest_world_z()
        if detail["trident"] or detail["support"] or detail["kicking"]:
            pin_trident_hand()
        return True
    return check_frame, fix_frame


def _punch_left_hinge_check_fix():
    """README 22 / SPEC_rebuild_v4.md: off_hinge must stay under 5deg on any
    solved arm. Every REAL key of Punch's left (jab) arm measures exactly
    0.0 (it goes through H.arm_ik_hinge with off_hinge=0.0 explicitly, same
    as every other solved arm here) -- but off_hinge, like twist, is a
    nonlinear function of the pose, so plain Bezier interpolation between
    two off_hinge=0 keys does not itself stay near 0 in between. Measured
    directly with v115_fixes/yemoja_measure.off_hinge (not itself part of
    the harness's own key-only report): every key and most interpolated
    frames read clean, but the f9 (jab extended) -> f14 (return to idle)
    gap peaks at -16.22deg at f11, over the 5deg budget, with f10/f12/f13
    also over (-8.19/-13.17/-4.60). No other clip/side showed this (Kick's
    left arm peaks 3.17, HardPunch/HardKick both arms stay under 5 at every
    non-idle-adjacent frame -- see BUILD_NOTES for the full per-frame table).
    Fix: at each bad frame, re-solve Arm.L/ForeArm.L via H.arm_ik_hinge
    (off_hinge=0.0 explicit) to the CURRENT interpolated Hand.L head
    position, using the CURRENT interpolated ForeArm.L head as the pole (a
    "keep whatever bend direction interpolation already chose, just zero
    the hinge deviation" solve, same technique pin_foot/pin_trident_hand use
    for position). This never touches Hand.L's own rotation (arm_ik_hinge
    only sets Arm.L/ForeArm.L), so the already-correct interpolated fist
    shape and hand position are untouched -- only the elbow's bend-plane
    corrects. Re-measured: worst -16.22 -> a bounded few tenths of a degree
    (well under budget) at every previously-bad frame."""
    def check_frame(f):
        if not V4:
            return False, None
        _apply_pole, ym = H.v4_mods()
        return abs(ym.off_hinge("L")) > 5.0, None

    def fix_frame(f, detail):
        pbH = A.pose.bones[L.full("Hand.L")]
        pbE = A.pose.bones[L.full("ForeArm.L")]
        target = A.matrix_world @ pbH.head
        pole = A.matrix_world @ pbE.head
        H.arm_ik_hinge(L, "L", target, pole, off_hinge=0.0)
        bpy.context.view_layer.update()
        return True
    return check_frame, fix_frame


def _hardpunch_hand_twist_check_fix():
    """Found verifying item 1 (not itself a literal spec line, but the same
    class of finding item 1 exists to catch): fix_quaternion_hemispheres only
    fixes SIGN discontinuities between consecutive KEYS. It does not, and
    cannot, fix a large TWIST the hand genuinely sweeps through between two
    keys that are individually fine but differ substantially in their
    orient_hand_for_shaft "hand_y_hint" (the free spin DOF) -- f7's windup
    hint is (0,1,0), f12's thrust hint is HP_HINT12=(-0.87,0.5,0), ~150 deg
    apart in the plane perpendicular to the shaft (BUILD_NOTES documents
    both were independently swept for their OWN twist/clearance budgets, not
    for closeness to each other). Measured with eval_all.py's own per-frame
    scan (not the harness's key-only report): Hand.R twist is fine at every
    key AND at f8/f10/f11, but spikes to -87.3 deg at f9 -- BOTH raw
    component-Bezier interpolation of the built curve AND a from-scratch
    Quaternion.slerp between the two keys' actual quaternions confirm this
    isn't an interpolation-method artifact: q7.dot(q12) = 0.053, i.e. the two
    orientations are ~174 deg apart as unit quaternions, so ANY continuous
    path between them sweeps a large rotation somewhere, and the hint gap
    puts most of it on the twist axis specifically. Fix: at f9 (already a
    breakdown key from the floor-pin pass, so its Hand.R value is otherwise
    whatever that pass happened to leave behind), rebuild ONLY Hand.R's
    rotation via orient_hand_for_shaft with a hint LINEARLY interpolated
    between f7's and f12's own hints at f9's fractional position (t=0.4) --
    same shaft direction (0,0,1) as both keys, ForeArm.R left at whatever
    the (already-correct) position interpolation gives it, since
    orient_hand_for_shaft only ever sets Hand's OWN rotation (verified in
    yemoja_anim_lib.py: it writes pb.matrix with head=pb.head.copy(), and a
    connected child bone ignores the translation part -- same property
    pin_foot already relies on). Re-measured: -87.3 -> -16.0 deg, comfortably
    inside the 30 deg budget. Scoped to exactly the bracket/frame the
    per-frame scan found bad -- f1-f7, f12-f15 and f15-f26 all measured
    clean and are left untouched."""
    def check_frame(f):
        if f != 9:
            return False, None
        tw = common.twist_deg(L, "Hand.R")
        return abs(tw) > 30.0, None

    def fix_frame(f, detail):
        t = (f - 7) / (12 - 7)
        hint7 = Vector(HP_HINT7)
        hint12 = Vector(HP_HINT12)
        lerp_hint = hint7.lerp(hint12, t)
        L.orient_hand_for_shaft((0, 0, 1), lerp_hint)
        L.apply_captured_grip("R")
        L.attach_trident()
        return True
    return check_frame, fix_frame


def _hardkick_check_fix(f_first, f_last):
    def check_frame(f):
        if f in (f_first, f_last):
            return False, None
        pb = A.pose.bones[L.full("Foot.L")]
        err = ((A.matrix_world @ pb.head) - IDLE_SNAP["L"]["head"]).length
        if err > 0.005:
            return True, None
        lo = H.lowest_world_z_excluding(L, "Yemoja_Body", KICKING_R)
        if lo < -0.005 or lo > 0.02:
            return True, None
        return False, None

    def fix_frame(f, detail):
        # Position-only reposition: preserve the CURRENT interpolated
        # orientation (the support foot is continuously pivoting, not
        # static -- forcing it back to flat idle orientation mid-pivot
        # would be wrong), re-solve the leg to the fixed idle ankle point
        # using the current interpolated knee as the ik hint, then restore
        # Foot.L's own rotation to what it was, keeping only leg_ik's
        # corrected translation.
        def reposition():
            pbF = A.pose.bones[L.full("Foot.L")]
            cur_rot = (A.matrix_world @ pbF.matrix).to_3x3()
            knee_now = A.matrix_world @ A.pose.bones[L.full("Leg.L")].head
            L.leg_ik("L", IDLE_SNAP["L"]["head"], knee_now)
            bpy.context.view_layer.update()
            new_world = cur_rot.to_4x4()
            new_world.translation = (A.matrix_world @ pbF.matrix).translation
            pbF.matrix = A.matrix_world.inverted() @ new_world
            bpy.context.view_layer.update()

        reposition()
        lo = H.lowest_world_z_excluding(L, "Yemoja_Body", KICKING_R)
        for _ in range(6):
            if abs(lo) < 0.005:
                break
            dx, dy, dz = H.world_delta_to_armature((0.0, 0.0, -lo))
            L.loc("Hips", dx, dy, dz)
            reposition()
            lo = H.lowest_world_z_excluding(L, "Yemoja_Body", KICKING_R)
        return True
    return check_frame, fix_frame


# =====================================================================
# Build, report, save
# =====================================================================
def main():
    # support_sides: BOTH feet stay planted in Punch/HardPunch (pin_both() on
    # every key). Kick/HardKick only keep ONE foot on the ground -- the other
    # is the kicking leg, which is SUPPOSED to leave the idle position -- so
    # checking it against idle_snap would flag correct, intentional motion as
    # a "support foot" failure.
    clips = [
        # extra=[_punch_left_hinge_check_fix()]: README 22's off_hinge<5deg
        # budget, blown at interpolated frames between two off_hinge=0 keys
        # -- see that function's own docstring above.
        ("Yemoja_Atk_Punch", PUNCH_KEYS, [1, 4, 7, 9, 14], ("L", "R"), None,
         _full_pin_check_fix(("L", "R"), 1, 14), [_punch_left_hinge_check_fix()]),
        # extra=[_hardpunch_hand_twist_check_fix()]: a SECOND enforce_pins
        # pass, after the floor-pin one, for the Hand.R twist spike found by
        # eval_all.py's per-frame scan at f9 -- see that function's own
        # docstring above. Not a floor/foot issue, so it's a separate pass
        # rather than folded into _full_pin_check_fix's own check/fix.
        ("Yemoja_Atk_HardPunch", HARDPUNCH_KEYS, [1, 7, 12, 15, 26], ("L", "R"), None,
         _full_pin_check_fix(("L", "R"), 1, 26), [_hardpunch_hand_twist_check_fix()]),
        ("Yemoja_Atk_Kick", KICK_KEYS, [1, 4, 7, 9, 16], ("R",), KICKING_L,
         _kick_check_fix(1, 16), []),
        ("Yemoja_Atk_HardKick", HARDKICK_KEYS, [1, 6, 12, 16, 22, 28], ("L",), KICKING_R,
         _hardkick_check_fix(1, 28), []),
    ]
    # item 10: exact export frame ranges per clip.
    FRAME_RANGES = {"Yemoja_Atk_Punch": (1, 14), "Yemoja_Atk_HardPunch": (1, 26),
                     "Yemoja_Atk_Kick": (1, 16), "Yemoja_Atk_HardKick": (1, 28)}
    all_rows = {}
    all_flips = {}
    all_breakdowns = {}
    for name, keys, report_frames, support_sides, kicking_exclude, (check_fn, fix_fn), extra_passes in clips:
        print("=== building", name, "===")
        act = H.build_clip(L, RESET_POSE, name, keys)
        n_fcurves = sum(len(cb.fcurves) for cb in H._all_channelbags(act))
        print(name, "built,", n_fcurves, "fcurves")

        # item 1: quaternion hemisphere continuity, every action.
        n_flipped, flips = H.fix_quaternion_hemispheres(act)
        all_flips[name] = flips
        print(name, "hemisphere flips fixed:", n_flipped, flips)

        # item 4: between-key pinning/floor enforcement.
        added, pass_counts = H.enforce_pins(L, name, fix_fn, check_fn)
        all_breakdowns[name] = added
        print(name, "breakdown frames added:", added, "passes:", pass_counts)

        # Extra clip-specific enforce_pins passes (currently: HardPunch's
        # Hand.R twist-spike fix -- see _hardpunch_hand_twist_check_fix()).
        for extra_check, extra_fix in extra_passes:
            eadded, epasses = H.enforce_pins(L, name, extra_fix, extra_check)
            if eadded:
                all_breakdowns[name] = sorted(set(all_breakdowns[name]) | set(eadded))
            print(name, "extra pass breakdown frames:", eadded, "passes:", epasses)

        # General backstop for any remaining twist-budget overshoot on a
        # frame nobody explicitly built (plain Bezier/AUTO_CLAMPED overshoot
        # on a long inter-key gap -- see fix_twist_overshoot()'s own
        # docstring in harness.py for the specific case this catches, found
        # by eval_all.py's per-frame scan on HardKick's Hand.R between f22
        # and f28). Runs after any clip-specific twist fix above, which
        # takes precedence where one exists.
        twist_fixed = H.fix_twist_overshoot(L, act)
        if twist_fixed:
            print(name, "twist-overshoot frames fixed:", twist_fixed)
            all_breakdowns[name] = sorted(set(all_breakdowns[name]) | set(f for _, f, _ in twist_fixed))

        # item 1 (arc check) -- HardKick's right ankle f6->f12: TRIED a
        # surgical per-frame leg_ik re-solve here (fix_ankle_arc_monotonic,
        # still in harness.py) to clamp the ~0.2-0.4 world unit residual
        # "forward" dip that survives fix_quaternion_hemispheres (see that
        # function's docstring for why the dip itself is real, not a bug --
        # SPEC_hardkick_v2.md's own literal f6/f12 ankle targets put the
        # natural IK arc's most-forward point around f9-f10, slightly
        # forward of f12's own less-forward target). MEASURED RESULT: worse,
        # not better -- L.leg_ik's fresh quaternion at the re-solved frames
        # is not hemisphere/shape-matched to its neighbours, and Bezier
        # interpolation through it produced a NEW spike (Foot.R world
        # position swinging to y=+1.29 at f10, a 3.4-3.6 world-unit step,
        # versus the original ~0.2-0.4 unit dip it was meant to fix).
        # Reverted; NOT called. The residual dip is reported in BUILD_NOTES
        # as a measured, unresolved item instead of trading it for a worse
        # regression.

        # item 1 (arc check) -- per-frame step of any ankle/wrist < 0.9 world
        # units at every frame: TRIED a bisection-based breakdown-key
        # backstop here (add_arc_breakdowns, still in harness.py) before
        # realising it cannot work for this particular violation shape.
        # Measured directly: every surviving >=0.9 step (HardPunch Hand.R
        # f8-11, Kick Foot.L f2-9, HardKick Foot.R f6-10/f18-22) sits
        # between two frames that are ALREADY BOTH real or breakdown keys,
        # one integer frame apart -- there is no unkeyed interior frame left
        # to bisect into. And where a gap genuinely does have interior
        # frames, inserting a key there via key_from_snapshot captures
        # whatever value the already-smooth curve already produces at that
        # frame -- identical to the value eval_all.py's per-frame scan
        # already measures, so the reported step does not change; it only
        # freezes that in-between shape against future edits. Net effect on
        # every actually-violating pair here: none. These are genuine
        # fast-motion frame-to-frame displacements (a kick/thrust extending
        # or a support foot settling in as few as 1-3 frames at 30fps), not
        # an interpolation smoothing defect -- reported as measured,
        # unresolved numbers in BUILD_NOTES rather than "fixed" by a
        # mechanism that provably does nothing to them.

        # =============================================== SPEC_fix_v5.md ===
        # item 3: HardKick apex retiming -- breakdowns at f9 (55%) and f11
        # (90%) along the straight chamber(f6)->impact(f12) ankle/knee arc,
        # solved via the SAME LEG() wrapper (retargeting included) the real
        # keys themselves used, so the interpolation between the two real
        # keys' own IK solutions no longer overshoots past f12 before
        # coming back (VERIFY_attacks_v4.md: apex measured at f10, not f12).
        if name == "Yemoja_Atk_HardKick":
            # f7/f8/f10 ALSO get a straight-arc point, not just the spec's
            # named f9(55%)/f11(90%): item 4 pinning already froze breakdown
            # keys at f7/f8/f10 (measured -- enforce_pins's own support/
            # floor correction pass touches every interpolated frame in
            # range, HardKick included), reflecting the OLD 2-key Bezier
            # arc's overshoot. Leaving those frozen keys in place while only
            # adding f9/f11 produces a WORSE result, not a fixed one (f8's
            # stale high value sits above f9's new, more moderate one --
            # measured directly: f8=0.855 sitting above a freshly-lerped
            # f9=0.519 breaks monotonicity right where item 3 is supposed to
            # fix it). Overwriting all five interior frames with points on
            # ONE piecewise-linear ankle-target arc through the four control
            # fractions (f6=0%, f9=55%, f11=90%, f12=100%, f7/f8/f10 lerped
            # between their own bracketing control points) keeps the exact
            # f9/f11 targets the spec names while making the whole f6->f12
            # span internally consistent.
            apex_added = H.apex_breakdowns(
                L, LEG, name, "R",
                {7: 0.55 * 1 / 3, 8: 0.55 * 2 / 3, 9: 0.55,
                 10: 0.55 + (0.90 - 0.55) * 0.5, 11: 0.90},
                Vector((-1.15, 0.80, 2.2)), Vector((0.10, -1.60, 5.85)),
                Vector((-1.55, 0.25, 3.4)), Vector((-1.35, -0.55, 5.30)))
            all_breakdowns[name] = sorted(set(all_breakdowns[name]) | set(apex_added))
            print(name, "item3 apex breakdowns:", apex_added)
            # A fresh leg_ik solve at a brand-new frame is not guaranteed to
            # land in the same quaternion hemisphere as its new neighbours
            # (this is exactly what the v3-era fix_ankle_arc_monotonic
            # attempt hit, see the comment above) -- re-run the hemisphere
            # fix immediately so f9/f11 are continuous with f6/f12/f16.
            nf2, flips2 = H.fix_quaternion_hemispheres(act)
            if nf2:
                print(name, "item3 hemisphere re-flips:", nf2, flips2)

        # item 8: Kick support-foot settle spread over f2/f3 (down) and
        # f13/f15 (up) instead of one single >0.9-unit step at f1->f2 and
        # f15->f16 (VERIFY_attacks_v4.md: 0.143 in one frame, both ends).
        if name == "Yemoja_Atk_Kick":
            settle_added = []
            for f, frac in ((2, 0.40), (3, 0.75), (13, 0.70), (15, 0.30)):
                settle_added += H.settle_spread_breakdowns(
                    L, name, (lambda fr=frac: settle_R_frac(fr)), [f])
            all_breakdowns[name] = sorted(set(all_breakdowns[name]) | set(settle_added))
            print(name, "item8 settle-spread breakdowns:", settle_added)
            nf3, flips3 = H.fix_quaternion_hemispheres(act)
            if nf3:
                print(name, "item8 hemisphere re-flips:", nf3, flips3)

        # item 6: late per-key corrections -- these run AFTER every other
        # per-action pass above, because VERIFY_attacks_v4.md measured both
        # violations at frames whose OWN pose-building function already
        # asserted a clean value at build time (HardPunch head f12/f15
        # within budget when hardpunch_f12/f15() ran; HardKick toe f12/f16
        # within budget when hardkick_f12/f16() ran) -- something in a
        # LATER global pass (hemisphere fix / enforce_pins / twist-overshoot
        # backstop) nudges the key afterward. Re-asserting here, last, is
        # what actually makes the saved key match the number BUILD_NOTES
        # reports, instead of the pre-later-pass value.
        if name == "Yemoja_Atk_HardPunch":
            H.final_key_correction(L, name, {
                12: [lambda: H.aim_head(L, target_angle=0.0, tol=10.0)],
                15: [lambda: H.aim_head(L, target_angle=0.0, tol=10.0)],
            })
            print(name, "item6 head re-correction applied at f12,f15")
        if name == "Yemoja_Atk_HardKick":
            H.final_key_correction(L, name, {
                12: [lambda: H.plantarflex_and_detwist(L, "R", target_toe_angle=14.5, target_twist=0.0)],
                16: [lambda: H.plantarflex_and_detwist(L, "R", target_toe_angle=12.0, target_twist=0.0)],
            })
            print(name, "item6 toe re-correction applied at f12,f16")

        # item 1: trident vs. body between keys (HardKick f5,8-11,17-21,23;
        # HardPunch f8,f10 per VERIFY_attacks_v4.md). Runs LAST of the
        # pose-modifying passes -- it only re-orients Hand.R (position and
        # every other bone untouched), so nothing after it can undo it, and
        # it needs every other pass's final pose (esp. item 3's new f9/f11
        # keys) as its own bracketing keys where relevant.
        if name in ("Yemoja_Atk_HardKick", "Yemoja_Atk_HardPunch"):
            clear_added, clear_bad = H.enforce_trident_clear(
                L, name, [f for f, _ in keys], PEN, BODY_OBJ, leg_side="R")
            all_breakdowns[name] = sorted(set(all_breakdowns[name]) | set(clear_added))
            V5_REPORT.setdefault("item1", {})[name] = dict(added=clear_added, still_bad=clear_bad)
            print(name, "item1 trident-clear breakdowns:", clear_added,
                  "still violating after cap:", clear_bad)

        # item 2 (again): every v5 pass above (item1/3/6/8) is its own
        # freshly-solved key -- re-run the hemisphere fix one last time,
        # unconditionally, so nothing any of them added can leave a flip
        # for the gate to catch that a rebuild would just fix anyway.
        nf_final, flips_final = H.fix_quaternion_hemispheres(act)
        if nf_final:
            print(name, "final hemisphere re-flips:", nf_final, flips_final)

        # item 10: export frame range.
        f0, f1 = FRAME_RANGES[name]
        act.use_frame_range = True
        act.frame_start, act.frame_end = f0, f1

        rows = H.report(L, RESET_POSE, name, report_frames,
                         support_sides=support_sides, idle_snap=IDLE_SNAP,
                         kicking_exclude=kicking_exclude)
        all_rows[name] = rows
        for r in rows:
            print(" frame", r["frame"], "lowest_z", round(r["lowest_z"], 4),
                  "support_err", round(r["support_err"], 5),
                  "idle_delta", r["idle_delta"],
                  "head_angle", round(r["head_angle"], 2),
                  "pen", r["pen"],
                  "below(non-exempt<0.90)",
                  [(b["mesh"], b["bone"], round(b["ratio"], 3)) for b in r["below"] if b["ratio"] < 0.90])

    # item 9: leave the file pointed at idle, frame 1, preview mode OFF
    # (Tattoos visible, Scalp Shrinkwrap on -- SPEC_fix_v5.md item 9;
    # VERIFY_attacks_v4.md found the v4 deliverable saved with preview_mode
    # still ON, left that way since module load and never undone).
    A.animation_data.action = bpy.data.actions["Yemoja_Idle_MASTER"]
    bpy.context.scene.frame_set(1)
    changed = L.preview_mode(False)
    print("item9 preview_mode(False):", changed)

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    print("SAVED", BLEND_OUT)
    print("=== hemisphere flips remaining (should all be 0 after fix) ===")
    for name in all_flips:
        print(" ", name, "flips fixed this build:", len(all_flips[name]))
    print("=== breakdown frames added (item 4) ===")
    for name in all_breakdowns:
        print(" ", name, all_breakdowns[name])
    return all_rows


_ROT_DP = re.compile(r'pose\.bones\["([^"]+)"\]\.rotation_quaternion')
_LOC_DP = re.compile(r'pose\.bones\["([^"]+)"\]\.location')


def _action_keyed_data(action, bones, hips_name):
    """{bone: {frame: {idx: value}}} for every rotation_quaternion curve on
    a humanoid bone, and {frame: {idx: value}} for Hips.location -- read
    directly off the fcurve keyframe_points, not evaluated -- so the gate's
    quaternion-norm and full-keying checks see exactly what is SAVED, not
    what the depsgraph happens to interpolate."""
    per_bone = {b: {} for b in bones}
    hips_loc = {}
    for cb in H._all_channelbags(action):
        for fc in cb.fcurves:
            dp = fc.data_path
            idx = fc.array_index
            m = _ROT_DP.match(dp)
            if m and m.group(1) in per_bone:
                for kp in fc.keyframe_points:
                    f = int(round(kp.co.x))
                    per_bone[m.group(1)].setdefault(f, {})[idx] = kp.co.y
                continue
            m2 = _LOC_DP.match(dp)
            if m2 and m2.group(1) == hips_name:
                for kp in fc.keyframe_points:
                    f = int(round(kp.co.x))
                    hips_loc.setdefault(f, {})[idx] = kp.co.y
    return per_bone, hips_loc


# SPEC_fix_v5.md Gate: per-clip constants shared with main()'s own build so
# the gate checks the SAME budgets the build targeted, not new numbers
# invented after the fact.
GATE_CLIPS = [
    # name, frame_range, support_sides, kicking_exclude, head_tol, head_key_frames
    ("Yemoja_Atk_Punch",     (1, 14), ("L", "R"), None,      10.0, (1, 4, 7, 9, 14)),
    ("Yemoja_Atk_HardPunch", (1, 26), ("L", "R"), None,      10.0, (1, 7, 12, 15, 26)),
    ("Yemoja_Atk_Kick",      (1, 16), ("R",),     KICKING_L, 10.0, (1, 4, 7, 9, 16)),
    ("Yemoja_Atk_HardKick",  (1, 28), ("L",),     KICKING_R, 15.0, (1, 6, 12, 16, 22, 28)),
]
GATE_FLOOR_LO, GATE_FLOOR_HI = -0.005, 0.02
GATE_SUPPORT_TOL = 0.005
GATE_LEG_CLEAR = 0.25
GATE_HINGE_TOL = 5.0
GATE_QUAT_TOL = 1e-4


def final_gate(blend_path=None, lib_path=None, n_samples=401):
    """SPEC_fix_v5.md Gate. Re-opens the SAVED file fresh and runs, on
    EVERY frame of every clip: the signed BVH trident penetration test
    (verify/pen.py's own trident_shaft_runs -- imported, not
    re-implemented), the shaft-to-UpLeg.R/Leg.R segment distance, floor and
    support-foot checks, hemisphere-flip count, quaternion norms, the
    full-keying check, off_hinge per arm, head-angle at keys, and the
    export-range/handoff state. Prints a table and returns (passed: bool,
    rows). Called from __main__ AFTER main() so main()'s own save is what
    gets checked; BUILD_NOTES' acceptance section is this function's
    verbatim stdout."""
    blend_path = blend_path or BLEND_OUT
    lib_path = lib_path or LIB
    bpy.ops.wm.open_mainfile(filepath=blend_path)
    spec = importlib.util.spec_from_file_location("yemoja_anim_lib_gate", lib_path)
    GL = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(GL)
    pen_spec = importlib.util.spec_from_file_location("pen_gate", os.path.join(_VERIFY_DIR, "pen.py"))
    GPEN = importlib.util.module_from_spec(pen_spec)
    pen_spec.loader.exec_module(GPEN)

    GA = GL.armature()
    BODY = bpy.data.objects["Yemoja_Body"]
    CLOTH = bpy.data.objects["Yemoja_Clothes"]
    bones = GL.humanoid_bones(GA)
    hips_name = GL.full("Hips")
    _apply_pole, ym = H.v4_mods()

    print("=" * 78)
    print("SPEC_fix_v5.md final_gate() -- %s" % blend_path)
    print("=" * 78)

    rows = []
    passed = True

    def rec(clip, check, ok, detail=""):
        nonlocal passed
        rows.append((clip, check, "PASS" if ok else "FAIL", detail))
        if not ok:
            passed = False

    # -------------------------------------------------- idle support snap ---
    H.apply_idle_action(GL, "Yemoja_Idle_MASTER")
    idle_snap = H.snapshot_feet(GL)

    worst_clothes_overall = (-1, None, None)   # (count, clip, frame)

    for name, (f0, f1), support_sides, kicking_exclude, head_tol, head_keys in GATE_CLIPS:
        act = bpy.data.actions.get(name)
        if act is None:
            rec(name, "action exists", False, "missing")
            continue
        GA.animation_data_create()
        GA.animation_data.action = act

        # per-frame checks -------------------------------------------------
        pen_bad_frames = []
        leg_clear_bad = []
        floor_bad = []
        support_bad = []
        hinge_bad = []
        worst_clothes_clip = (-1, None)
        for f in range(f0, f1 + 1):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()

            b, t = GL.trident_ends()
            runs = GPEN.trident_shaft_runs(GL, BODY, n_samples=n_samples)
            bad = [r for r in runs if r[2] not in H._GRIP_BONE_NAMES]
            if bad:
                pen_bad_frames.append((f, bad))
            leg_d = H._leg_seg_clearance(GL, b, t, "R")
            if leg_d < GATE_LEG_CLEAR:
                leg_clear_bad.append((f, round(leg_d, 4)))

            if kicking_exclude:
                lo = H.lowest_world_z_excluding(GL, "Yemoja_Body", kicking_exclude)
            else:
                lo = GL.lowest_world_z(("Yemoja_Body",))
            if lo < GATE_FLOOR_LO or lo > GATE_FLOOR_HI:
                floor_bad.append((f, round(lo, 4)))

            serr = 0.0
            for side in support_sides:
                pb = GA.pose.bones[GL.full("Foot." + side)]
                e = ((GA.matrix_world @ pb.head) - idle_snap[side]["head"]).length
                serr = max(serr, e)
            if serr > GATE_SUPPORT_TOL:
                support_bad.append((f, round(serr, 5)))

            for side in ("L", "R"):
                hg = ym.off_hinge(side)
                if abs(hg) >= GATE_HINGE_TOL:
                    hinge_bad.append((f, side, round(hg, 2)))

            n_cloth = GPEN.clothes_inside_count(BODY, CLOTH)
            if n_cloth > worst_clothes_clip[0]:
                worst_clothes_clip = (n_cloth, f)
            if n_cloth > worst_clothes_overall[0]:
                worst_clothes_overall = (n_cloth, name, f)

        rec(name, "trident penetration (non-grip, every frame)", not pen_bad_frames,
            ("clean" if not pen_bad_frames else
             "%d frames: %s" % (len(pen_bad_frames), [f for f, _ in pen_bad_frames])))
        rec(name, "shaft-to-UpLeg.R/Leg.R clearance >= %.2f" % GATE_LEG_CLEAR, not leg_clear_bad,
            ("clean" if not leg_clear_bad else "%d frames: %s" % (len(leg_clear_bad), leg_clear_bad)))
        rec(name, "floor band [%.3f, %.3f]" % (GATE_FLOOR_LO, GATE_FLOOR_HI), not floor_bad,
            ("clean" if not floor_bad else "%d frames: %s" % (len(floor_bad), floor_bad)))
        rec(name, "support-foot pin <= %.3f" % GATE_SUPPORT_TOL, not support_bad,
            ("clean" if not support_bad else "%d frames: %s" % (len(support_bad), support_bad)))
        rec(name, "off_hinge < %.1f, both arms, every frame" % GATE_HINGE_TOL, not hinge_bad,
            ("clean" if not hinge_bad else "%d instances: %s" % (len(hinge_bad), hinge_bad[:10])))
        rec(name, "clothes-inside worst frame (item 10)", True,
            "%d verts @ f%s" % (worst_clothes_clip[0], worst_clothes_clip[1]))

        # head-angle at keys -------------------------------------------------
        head_bad = []
        for f in head_keys:
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            ang = H.head_angle_to_z(GL)
            if ang > head_tol:
                head_bad.append((f, round(ang, 2)))
        rec(name, "head-angle <= %.0f deg at keys" % head_tol, not head_bad,
            ("clean" if not head_bad else str(head_bad)))

        # hemisphere flips / quat norms / full-keying (from saved fcurves) ---
        per_bone, hips_loc = _action_keyed_data(act, bones, hips_name)
        flip_count = 0
        for bone, fr in per_bone.items():
            fs = sorted(fr.keys())
            prev = None
            for f in fs:
                comps = fr[f]
                if len(comps) != 4:
                    continue
                q = (comps.get(0, 0), comps.get(1, 0), comps.get(2, 0), comps.get(3, 0))
                if prev is not None:
                    dot = sum(a * b for a, b in zip(prev, q))
                    if dot < 0:
                        flip_count += 1
                prev = q
        rec(name, "hemisphere-flip count == 0", flip_count == 0,
            "clean" if flip_count == 0 else "%d flips" % flip_count)

        norm_bad = []
        keying_bad = []
        all_frames = set(hips_loc.keys())
        for fr in per_bone.values():
            all_frames |= set(fr.keys())
        for f in sorted(all_frames):
            for bone in bones:
                comps = per_bone[bone].get(f)
                if comps is None or len(comps) != 4:
                    keying_bad.append((f, bone[len(H.PFX):] if bone.startswith(H.PFX) else bone,
                                        "missing" if comps is None else "%d/4" % len(comps)))
                else:
                    norm = math.sqrt(sum(v * v for v in comps.values()))
                    if abs(norm - 1.0) > GATE_QUAT_TOL:
                        norm_bad.append((f, bone, round(norm, 6)))
            hc = hips_loc.get(f)
            if hc is None or len(hc) != 3:
                keying_bad.append((f, "Hips.location", "missing" if hc is None else "%d/3" % len(hc)))
        rec(name, "quaternion norms within %.0e of 1" % GATE_QUAT_TOL, not norm_bad,
            ("clean" if not norm_bad else "%d keys: %s" % (len(norm_bad), norm_bad[:10])))
        rec(name, "full-keying (57 bones + Hips.loc, every keyed frame)", not keying_bad,
            ("clean" if not keying_bad else "%d gaps: %s" % (len(keying_bad), keying_bad[:10])))

        # export-range/handoff ------------------------------------------------
        range_ok = (act.use_frame_range and int(round(act.frame_start)) == f0
                    and int(round(act.frame_end)) == f1 and act.use_fake_user)
        rec(name, "export frame range == (%d,%d), fake_user" % (f0, f1), range_ok,
            "use_frame_range=%s start=%s end=%s fake_user=%s" %
            (act.use_frame_range, act.frame_start, act.frame_end, act.use_fake_user))

    # -------------------------------------------------------- scene state ---
    idle_act_ok = (GA.animation_data and GA.animation_data.action and
                   GA.animation_data.action.name == "Yemoja_Idle_MASTER")
    scn = bpy.context.scene
    scalp_mod = bpy.data.objects["Yemoja_Scalp"].modifiers.get("Shrinkwrap")
    tattoo_ob = bpy.data.objects["Yemoja_Tattoos"]
    handoff_ok = (idle_act_ok and scn.frame_start == 1 and scn.frame_end == 121
                  and scalp_mod is not None and scalp_mod.show_viewport and scalp_mod.show_render
                  and not tattoo_ob.hide_viewport and not tattoo_ob.hide_render)
    rec("(handoff)", "Armature=Idle_MASTER, scene 1-121, preview_mode(False)", handoff_ok,
        "idle_act=%s scene=(%s,%s) scalp_shrinkwrap=%s tattoos_hidden=%s" %
        (idle_act_ok, scn.frame_start, scn.frame_end,
         scalp_mod.show_viewport if scalp_mod else None, tattoo_ob.hide_viewport))
    rec("(handoff)", "clothes-inside true per-clip-per-frame worst (item 10)", True,
        "%d verts @ %s f%s" % worst_clothes_overall)

    # -------------------------------------------------------------- table ---
    w0 = max(len(r[0]) for r in rows)
    w1 = max(len(r[1]) for r in rows)
    for clip, check, status, detail in rows:
        print("%-*s  %-*s  %-4s  %s" % (w0, clip, w1, check, status, detail))
    n_fail = sum(1 for r in rows if r[2] == "FAIL")
    print("-" * 78)
    print("TOTAL: %d checks, %d FAIL" % (len(rows), n_fail))
    print("GATE %s" % ("PASS" if passed else "FAIL"))
    return passed, rows


if __name__ == "__main__":
    main()
    gate_passed, gate_rows = final_gate()
    if not gate_passed:
        sys.exit(1)
