"""
elbow_anatomy.py -- give the elbows anatomy, in REST space, on Yemoja_Body.

The client's note was "her elbow still looks off, the cubital fossa especially", against a
photo of a real arm bent ~100 deg. The pose was already right by then (A3: off_hinge 0.00,
flexion 99.9). The fault is that the arm HAS no elbow to bend: measured on the rest mesh,
the mean radius runs 0.152 - 0.166 world units unbroken from 0.55 above the joint to 0.50
below it. It is a cylinder, so folding it can only ever produce a folded cylinder.

The reference photo says what a bent elbow reads as, and it is three masses and a hollow:

    biceps      a fullness on the FLEXOR side of the distal upper arm that STOPS about
                0.1 U short of the joint;
    fossa       the hollow left between that and the forearm -- the cubital fossa. It
                reads because the two masses around it read, so it is cut only lightly;
    brachiorad. a flare on the flexor/lateral side of the PROXIMAL forearm, widest just
                below the joint and gone by mid-forearm;
    olecranon   a point on the EXTENSOR side, at the joint, with a flatter plane behind.

All four are radial displacements in the arm-axis frame (elbow_lib2.frame): t along the
axis with 0 at the joint, and a lobe factor from the radial direction's dot with the
extensor direction. Because the frame is derived per side, applying the same numbers to
"L" and "R" mirrors them automatically.

Second half of the fix is skinning. Widening the Arm<->ForeArm blend (section 21) made the
inner pinch WORSE: a wide blend on the flexor side drags the whole crease into one long
line. This does the opposite on that side only -- a SHARP handover (half ~0.05) whose w50
line is shifted PROXIMALLY, so the forearm mass slides up under the biceps and the fold
closes as a hollow instead of a crease. The extensor side's ramp is left exactly as it is;
that side needs its width to carry the olecranon.

    import elbow_anatomy
    elbow_anatomy.apply()            # idempotent; stamps Armature["YEMOJA_MESH_V3"]
    elbow_anatomy.verify()

Everything is measured off the rig, so there are no absolute paths and no baked numbers
that assume a particular blend file.
"""

import bpy, math, os, importlib.util
from mathutils import Vector

BODY = "Yemoja_Body"
ARM_NAME = "Armature"
PFX = "mixamorig:"
STAMP = "YEMOJA_MESH_V3"
HERE = os.path.dirname(os.path.abspath(__file__))

# Meshes that sit ON the body surface and must move with it. The scalp and fuzz carry a
# Shrinkwrap so they re-project themselves; the tattoos' Conform is switched off in the
# working file, and the arm tattoo sits at t ~ 0.25 -- right inside the forearm flare --
# so it is displaced by the same field rather than left floating.
FOLLOWERS = ("Yemoja_Tattoos", "Yemoja_Fuzz", "Yemoja_Scalp")

# --- sculpt, in world units (1 world unit = 100 armature units; U = 0.9476, F = 0.7865)
DEFAULTS = dict(
    # Tuned against the render, not guessed. The first pass (0.16 / 0.095 / -0.05) moved
    # every number the right way but was invisible at the client's framing: the elbow
    # rings carry ~16 verts, so a gentle field lands on 14-40 vertices and reads as
    # nothing. These are at the top of the brief's allowed range, and the two masses are
    # pulled APART in t so the hollow between them has room to exist.
    olecranon=dict(amp=0.026, t0=0.005, t_half=0.075, ang_cos=0.35),
    forearm=dict(frac=0.19, t_peak=0.100, t_end=0.360, lobe=(0.35, -0.55)),
    biceps=dict(frac=0.120, t_lo=-0.360, t_hi=-0.100, lobe=(0.20, -0.65)),
    fossa=dict(frac=-0.085, t_lo=-0.110, t_hi=0.060, lobe=(-0.10, -0.85)),
    skin=dict(half_flex=0.06, w50_shift=0.035, blend=(0.35, -0.30)),
    region=dict(r_max=0.45, t_max=0.60),
)


# --------------------------------------------------------------------- helpers ---
def _lib(name):
    """Sibling module by path -- next to this file, else the project's pose_v2 folder."""
    for p in (os.path.join(HERE, name + ".py"),
              os.path.join(os.path.dirname(HERE), "pose_v2", name + ".py"),
              os.path.join(os.path.dirname(HERE), name + ".py")):
        if os.path.exists(p):
            spec = importlib.util.spec_from_file_location(name, p)
            m = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(m)
            return m
    raise ImportError(name)


def armature():
    return bpy.data.objects[ARM_NAME]


def smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def frame(side):
    """(joint centre, arm axis) in WORLD space, from the REST skeleton."""
    A = armature(); AW = A.matrix_world
    b1 = A.data.bones[PFX + "Arm." + side]
    b2 = A.data.bones[PFX + "ForeArm." + side]
    return AW @ b2.head_local, ((AW @ b2.tail_local) - (AW @ b1.head_local)).normalized()


def extensor_dir(side):
    """Unit direction to the BACK of the elbow, in rest space.

    Taken from the bind pose, not from the current pose: the rest forearm already carries
    Mixamo's 12.3 deg bend, and that bend points to the FLEXOR side by definition. Reading
    it from the rest skeleton means the sculpt does not change if someone re-poses the arm
    before running this."""
    A = armature(); AW = A.matrix_world
    b1 = A.data.bones[PFX + "Arm." + side]
    b2 = A.data.bones[PFX + "ForeArm." + side]
    ua = ((AW @ b1.tail_local) - (AW @ b1.head_local)).normalized()
    fo = ((AW @ b2.tail_local) - (AW @ b2.head_local))
    bend = fo - ua * fo.dot(ua)
    _, axis = frame(side)
    bend = (bend - axis * bend.dot(axis)).normalized()
    return -bend


def _lobe(c, on, off):
    """1 where the radial direction is on the wanted side, 0 on the other, smooth between.
    `on`/`off` are dot-with-extensor values: lobe((0.35, -0.55)) is a broad flexor lobe."""
    return smoothstep((on - c) / (on - off))


def _arm_verts(ob, side, r_max, t_max):
    """[(vertex, t, radial vector, |radial|, dot-with-extensor)] inside the arm tube."""
    MW = ob.matrix_world
    C, axis = frame(side)
    ext = extensor_dir(side)
    out = []
    for v in ob.data.vertices:
        d = MW @ v.co - C
        t = d.dot(axis)
        rad = d - axis * t
        rl = rad.length
        if rl > r_max or rl < 1e-6 or abs(t) > t_max:
            continue
        out.append((v, t, rad, rl, rad.normalized().dot(ext)))
    return out, C, axis, ext


# ----------------------------------------------------------------------- sculpt ---
def _profile(t, p, kind):
    """Longitudinal profile of each mass, 0 outside its span, 1 at its peak."""
    if kind == "forearm":                      # widest just below the joint, gone by mid
        if t < 0.0 or t > p["t_end"]:
            return 0.0
        if t <= p["t_peak"]:
            return smoothstep(t / max(1e-6, p["t_peak"]))
        return smoothstep((p["t_end"] - t) / max(1e-6, p["t_end"] - p["t_peak"]))
    if kind in ("biceps", "fossa"):            # a raised cosine over [t_lo, t_hi]
        lo, hi = p["t_lo"], p["t_hi"]
        if t < lo or t > hi:
            return 0.0
        return 0.5 - 0.5 * math.cos(2.0 * math.pi * (t - lo) / (hi - lo))
    return 0.0


def sculpt(side, params=None, objects=None):
    """Displace the rest mesh radially: forearm flare, biceps, cubital fossa, olecranon.

    Returns per-mass vertex counts and peak displacements, in world units.
    """
    P = dict(DEFAULTS if params is None else params)
    reg = P["region"]
    log = {}
    objs = objects if objects is not None else [BODY] + [n for n in FOLLOWERS
                                                         if bpy.data.objects.get(n)]
    for name in objs:
        ob = bpy.data.objects.get(name)
        if ob is None or ob.type != 'MESH':
            continue
        MW = ob.matrix_world; MWi = MW.inverted()
        verts, C, axis, ext = _arm_verts(ob, side, reg["r_max"], reg["t_max"])
        counts = {}
        for kind in ("forearm", "biceps", "fossa"):
            p = P[kind]; n = 0; peak = 0.0
            on, off = p["lobe"]
            for v, t, rad, rl, c in verts:
                f = _profile(t, p, kind)
                if f <= 0.0:
                    continue
                a = p["frac"] * rl * f * _lobe(c, on, off)
                if abs(a) < 1e-7:
                    continue
                v.co = MWi @ (MW @ v.co + rad.normalized() * a)
                n += 1; peak = max(peak, abs(a))
            counts[kind] = dict(n=n, peak=round(peak, 5))
        # the olecranon is a point, not a mass: narrow in t and tight to the extensor side
        p = P["olecranon"]; n = 0; peak = 0.0
        verts, C, axis, ext = _arm_verts(ob, side, reg["r_max"], reg["t_max"])
        for v, t, rad, rl, c in verts:
            if abs(t - p["t0"]) > p["t_half"] or c < p["ang_cos"]:
                continue
            fa = (c - p["ang_cos"]) / (1.0 - p["ang_cos"])
            ft = 1.0 - ((t - p["t0"]) / p["t_half"]) ** 2
            a = p["amp"] * smoothstep(fa) * ft
            if a < 1e-7:
                continue
            v.co = MWi @ (MW @ v.co + rad.normalized() * a)
            n += 1; peak = max(peak, a)
        counts["olecranon"] = dict(n=n, peak=round(peak, 5))
        ob.data.update()
        log[name] = counts
    return log


# ------------------------------------------------------------------- skinning ---
def flexor_ramp(side, params=None):
    """Sharpen and shift the Arm<->ForeArm handover on the FLEXOR side only.

    half_flex is the half-width of the smoothstep; w50_shift moves its midpoint toward the
    upper arm, so the forearm's influence starts before the joint and its mass slides in
    under the biceps. The extensor side keeps whatever ramp it has -- the blend factor
    fades this rewrite out before it gets there -- and each vertex's Arm+ForeArm total is
    redistributed, never changed, so weights still sum to 1 and no vertex gains an
    influence.
    """
    P = dict(DEFAULTS if params is None else params)
    sk = P["skin"]; reg = P["region"]
    ob = bpy.data.objects[BODY]
    ga = ob.vertex_groups[PFX + "Arm." + side]
    gf = ob.vertex_groups[PFX + "ForeArm." + side]
    verts, C, axis, ext = _arm_verts(ob, side, reg["r_max"], 0.45)
    on, off = sk["blend"]
    half = sk["half_flex"]; shift = sk["w50_shift"]
    n = 0; worst = 0.0
    for v, t, rad, rl, c in verts:
        wa = wf = 0.0
        for g in v.groups:
            if g.group == ga.index:
                wa = g.weight
            elif g.group == gf.index:
                wf = g.weight
        tot = wa + wf
        if tot < 0.98:                          # not a pure arm-tube vertex; leave alone
            continue
        kf = _lobe(c, on, off)                  # 1 on the flexor side, 0 on the extensor
        if kf < 1e-4:
            continue
        s_new = smoothstep((t + shift + half) / (2.0 * half))
        s_old = wf / tot
        s = s_old + (s_new - s_old) * kf
        ga.add([v.index], tot * (1.0 - s), 'REPLACE')
        gf.add([v.index], tot * s, 'REPLACE')
        n += 1; worst = max(worst, abs(s - s_old))
    return dict(n=n, max_weight_change=round(worst, 4))


# ---------------------------------------------------------------------- apply ---
def apply(sides=("L", "R"), params=None, force=False):
    """Sculpt + reskin both elbows. Idempotent: the stamp makes a second call a no-op,
    which matters because every term here is RELATIVE to the current radius and would
    compound."""
    A = armature()
    if A.get(STAMP) and not force:
        return dict(already_applied=A[STAMP], skipped=True)
    log = dict(sides=list(sides), sculpt={}, skin={})
    for s in sides:
        log["sculpt"][s] = sculpt(s, params)
        log["skin"][s] = flexor_ramp(s, params)
    for nm in FOLLOWERS:                        # let the shrinkwrapped meshes re-settle
        ob = bpy.data.objects.get(nm)
        if ob:
            for m in ob.modifiers:
                if m.type == 'SHRINKWRAP':
                    m.show_viewport = m.show_viewport
    bpy.context.view_layer.update()
    A[STAMP] = "v3"
    log["stamp"] = A[STAMP]
    log["verify"] = verify(sides)
    return log


def verify(sides=("L", "R")):
    """Weight sanity + the radius profile the sculpt was supposed to produce."""
    ob = bpy.data.objects[BODY]
    bones = set(b.name for b in armature().data.bones)
    over4 = 0; off1 = 0; worst_sum = (1.0, 1.0)
    for v in ob.data.vertices:
        ws = [g.weight for g in v.groups
              if ob.vertex_groups[g.group].name in bones and g.weight > 1e-6]
        if len(ws) > 4:
            over4 += 1
        t = sum(ws)
        if abs(t - 1.0) > 0.01:
            off1 += 1
        worst_sum = (min(worst_sum[0], t), max(worst_sum[1], t))
    out = dict(verts_gt4_influences=over4, verts_sum_off_1pct=off1,
               sum_min=round(worst_sum[0], 4), sum_max=round(worst_sum[1], 4), profile={})
    for s in sides:
        verts, C, axis, ext = _arm_verts(ob, s, 0.45, 0.60)
        bins = {}
        for v, t, rad, rl, c in verts:
            bins.setdefault(round(t / 0.05) * 0.05, []).append((rl, c))
        prof = {}
        for k in sorted(bins):
            fl = [r for r, c in bins[k] if c < -0.3]
            ex = [r for r, c in bins[k] if c > 0.3]
            prof["%.2f" % k] = dict(flex=round(sum(fl) / len(fl), 4) if fl else None,
                                    ext=round(sum(ex) / len(ex), 4) if ex else None,
                                    n=len(bins[k]))
        out["profile"][s] = prof
    return out
