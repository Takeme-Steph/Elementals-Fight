"""
elbow_anatomy_v4.py -- pass 2 on the elbows: denser rings, then sharper anatomy.

V3 (elbow_anatomy.py) put the right shapes on the arm -- biceps, cubital fossa,
brachioradialis flare, olecranon -- and the rest-space radius profile proves they are
there. What it could not do was make them CRISP: the joint carried three 16-vertex rings
and the flare two 12-vertex rings, so every field landed on 19-43 vertices and a hollow
that needs a rim and a floor had about four vertices to build them out of.

So V4 does the thing V3's report said was the next lever and stopped short of doing:

  1. loop-cut the three 16-vert joint strips and the two 12-vert flare strips on both
     arms (cuts=1, grid fill). Weights and UVs interpolate through subdivision, and the
     flexor ramp is re-applied afterwards -- it is an absolute smoothstep target, not a
     relative edit, so re-running it on the new vertices is correct and idempotent.
  2. a refinement pass designed RELATIVE TO THE V3 SURFACE. It does not re-run V3's
     fields (they are fractions of the local radius and would compound); it adds four
     small, narrow terms that only the denser rings can carry:
        olecranon_tip   takes the point from +0.026 to +0.035 and narrows it, so the
                        back of the elbow reads as a corner rather than a dome;
        fossa_deepen    a little more hollow at the joint itself;
        fossa_lip       a narrow ridge just below the hollow -- the lower lip where the
                        brachioradialis mass starts. This is the edge that makes a
                        hollow read AS a hollow in the photo;
        flare_fold      moves the effective flare peak up to the fold, where V3 put it
                        0.10 below.

    import elbow_anatomy_v4 as v4
    v4.apply()          # idempotent; stamps Armature["YEMOJA_MESH_V4"]
    v4.verify()

Runs AFTER elbow_anatomy.apply() and asserts that: V4 on a mesh without V3 would be
sharpening shapes that are not there.
"""

import bpy, bmesh, math, os, importlib.util

BODY = "Yemoja_Body"
ARM_NAME = "Armature"
PFX = "mixamorig:"
STAMP = "YEMOJA_MESH_V4"
V3_STAMP = "YEMOJA_MESH_V3"
HERE = os.path.dirname(os.path.abspath(__file__))

# t-windows (arm axis, 0 at the joint) holding the rings to cut. Measured with
# elbow_lib.find_rings on the V3 mesh: three 16-vert rings across the joint and two
# 12-vert rings through the flare.
CUT_STRIPS = ((-0.075, 0.100), (0.100, 0.210))

DEFAULTS = dict(
    olecranon_tip=dict(amp=0.009, t0=0.005, t_half=0.045, ang_cos=0.50),
    fossa_deepen=dict(frac=-0.030, t_lo=-0.070, t_hi=0.030, lobe=(-0.15, -0.85)),
    fossa_lip=dict(frac=0.055, t_lo=0.030, t_hi=0.095, lobe=(0.05, -0.80)),
    flare_fold=dict(frac=0.050, t_lo=-0.010, t_hi=0.110, lobe=(0.30, -0.60)),
    region=dict(r_max=0.45, t_max=0.60),
)


def _v3():
    """The V3 module -- its frame(), extensor_dir(), _arm_verts(), _lobe() are the
    single definition of the arm frame, so V4 cannot drift from it."""
    for p in (os.path.join(HERE, "elbow_anatomy.py"),
              os.path.join(os.path.dirname(HERE), "pose_v2", "elbow_anatomy.py")):
        if os.path.exists(p):
            spec = importlib.util.spec_from_file_location("elbow_anatomy", p)
            m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
            return m
    raise ImportError("elbow_anatomy.py")


def _elbow_lib():
    for p in (os.path.join(HERE, "elbow_lib.py"),
              os.path.join(os.path.dirname(HERE), "pose_v2", "elbow_lib.py")):
        if os.path.exists(p):
            spec = importlib.util.spec_from_file_location("elbow_lib", p)
            m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
            return m
    raise ImportError("elbow_lib.py")


def armature():
    return bpy.data.objects[ARM_NAME]


# ------------------------------------------------------------------ 1. density ---
def cut_rings(sides=("L", "R"), strips=CUT_STRIPS, cuts=1):
    """Loop-cut the joint and flare rings. Returns the ring log and the vertex count."""
    el = _elbow_lib()
    before = len(bpy.data.objects[BODY].data.vertices)
    log = el.cut_elbows(sides=sides, strips=strips, cuts=cuts)
    log["verts_before"] = before
    log["verts_after"] = len(bpy.data.objects[BODY].data.vertices)
    return log


# ---------------------------------------------------------------- 2. refinement ---
def _profile(t, p):
    lo, hi = p["t_lo"], p["t_hi"]
    if t < lo or t > hi:
        return 0.0
    return 0.5 - 0.5 * math.cos(2.0 * math.pi * (t - lo) / (hi - lo))


def refine(side, params=None, objects=None):
    """The four pass-2 terms. Radial displacements, same frame as V3."""
    v3 = _v3()
    P = dict(DEFAULTS if params is None else params)
    reg = P["region"]
    objs = objects if objects is not None else \
        [BODY] + [n for n in v3.FOLLOWERS if bpy.data.objects.get(n)]
    log = {}
    for name in objs:
        ob = bpy.data.objects.get(name)
        if ob is None or ob.type != 'MESH':
            continue
        MW = ob.matrix_world; MWi = MW.inverted()
        counts = {}
        for kind in ("fossa_deepen", "fossa_lip", "flare_fold"):
            p = P[kind]; on, off = p["lobe"]; n = 0; peak = 0.0
            verts, C, axis, ext = v3._arm_verts(ob, side, reg["r_max"], reg["t_max"])
            for v, t, rad, rl, c in verts:
                f = _profile(t, p)
                if f <= 0.0:
                    continue
                a = p["frac"] * rl * f * v3._lobe(c, on, off)
                if abs(a) < 1e-7:
                    continue
                v.co = MWi @ (MW @ v.co + rad.normalized() * a)
                n += 1; peak = max(peak, abs(a))
            counts[kind] = dict(n=n, peak=round(peak, 5))
        p = P["olecranon_tip"]; n = 0; peak = 0.0
        verts, C, axis, ext = v3._arm_verts(ob, side, reg["r_max"], reg["t_max"])
        for v, t, rad, rl, c in verts:
            if abs(t - p["t0"]) > p["t_half"] or c < p["ang_cos"]:
                continue
            fa = (c - p["ang_cos"]) / (1.0 - p["ang_cos"])
            ft = 1.0 - ((t - p["t0"]) / p["t_half"]) ** 2
            a = p["amp"] * v3.smoothstep(fa) * ft
            if a < 1e-7:
                continue
            v.co = MWi @ (MW @ v.co + rad.normalized() * a)
            n += 1; peak = max(peak, a)
        counts["olecranon_tip"] = dict(n=n, peak=round(peak, 5))
        ob.data.update()
        log[name] = counts
    return log


# ---------------------------------------------------------------------- apply ---
def apply(sides=("L", "R"), params=None, force=False, cut=True):
    A = armature()
    assert A.get(V3_STAMP), ("elbow_anatomy.apply() (V3) must run first -- V4 sharpens "
                             "shapes it does not create")
    if A.get(STAMP) and not force:
        return dict(already_applied=A[STAMP], skipped=True)
    v3 = _v3()
    log = dict(sides=list(sides))
    if cut:
        log["cut"] = cut_rings(sides)
        # subdivision interpolates weights; the flexor handover is an absolute target, so
        # re-stating it on the new vertices is both correct and idempotent.
        log["reramp"] = {s: v3.flexor_ramp(s) for s in sides}
    log["refine"] = {s: refine(s, params) for s in sides}
    bpy.context.view_layer.update()
    A[STAMP] = "v4"
    log["stamp"] = A[STAMP]
    log["verify"] = verify(sides)
    return log


def verify(sides=("L", "R")):
    """Weight sanity, mesh sanity, and the radius profile V4 was meant to sharpen."""
    v3 = _v3()
    ob = bpy.data.objects[BODY]
    bones = set(b.name for b in armature().data.bones)
    over4 = off1 = 0
    lo_sum, hi_sum = 1.0, 1.0
    for v in ob.data.vertices:
        ws = [g.weight for g in v.groups
              if ob.vertex_groups[g.group].name in bones and g.weight > 1e-6]
        if len(ws) > 4:
            over4 += 1
        t = sum(ws)
        if abs(t - 1.0) > 0.01:
            off1 += 1
        lo_sum = min(lo_sum, t); hi_sum = max(hi_sum, t)
    out = dict(verts=len(ob.data.vertices),
               ngons=sum(1 for p in ob.data.polygons if len(p.vertices) > 4),
               tris=sum(1 for p in ob.data.polygons if len(p.vertices) == 3),
               verts_gt4_influences=over4, verts_sum_off_1pct=off1,
               sum_min=round(lo_sum, 4), sum_max=round(hi_sum, 4), profile={})
    for s in sides:
        verts, C, axis, ext = v3._arm_verts(ob, s, 0.45, 0.60)
        bins = {}
        for v, t, rad, rl, c in verts:
            bins.setdefault(round(t / 0.04) * 0.04, []).append((rl, c))
        prof = {}
        for k in sorted(bins):
            fl = [r for r, c in bins[k] if c < -0.3]
            ex = [r for r, c in bins[k] if c > 0.3]
            prof["%.2f" % k] = dict(flex=round(sum(fl) / len(fl), 4) if fl else None,
                                    ext=round(sum(ex) / len(ex), 4) if ex else None,
                                    n=len(bins[k]))
        out["profile"][s] = prof
    return out
