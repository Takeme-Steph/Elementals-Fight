"""fix_shoulder_weights.py -- re-weight the shoulder girdle of the Yemoja rig.

Reproducible, idempotent, offline (bpy module).  Runs on the currently open blend:

    import bpy, importlib.util
    bpy.ops.wm.open_mainfile(filepath=".../v115_base.blend")
    spec = importlib.util.spec_from_file_location(
        "fsw", "/home/claude/yemoja/fix_shoulder/fix_shoulder_weights.py")
    fsw = importlib.util.module_from_spec(spec); spec.loader.exec_module(fsw)
    summary = fsw.apply()                       # both sides, params = DEFAULT_PARAMS

or from the shell:

    python3 fix_shoulder_weights.py IN.blend OUT.blend

WHAT IT DOES  (everything measured in REST space: rest bone heads/tails and the
undeformed mesh coordinates, both in ARMATURE space -- +X = her left, +Y = up,
+Z = forward)

  0. Restore.  The pre-fix weights of the eight girdle groups are stashed in a text
     datablock the first time apply() runs and restored at the start of every later
     run, so apply() is a projection: running it twice changes nothing the second time.

  1. Radius profile R(s).  s is the axial coordinate along the Arm bone in arm
     lengths (0 = acromion = Arm head, 1 = elbow).  R(s) is the `prof_pct` percentile
     of the perpendicular distance of the vertices that are almost pure Arm
     (weight >= prof_min_w), binned in s.  This is the upper arm's own thickness,
     so "how far off the arm is this vertex, in units of the arm's own radius" is
     scale-free and works at every station.

  2. Torso-ness T.  T = max of two smoothsteps:
       radial  rho = (distance to the Arm SEGMENT) / R(s), ramped over
               [rho_arm0, rho_arm1].  The segment (not the infinite axis) is what
               makes the neck base and the upper trapezius -- near the axis but far
               proximal -- count as torso, which is what they are.
       medial  the scapula and the posterior axillary fold sit only ~1.4 arm-radii
               off the axis, so the radial term alone cannot see them; but they are
               MEDIAL of the acromion, which no upper-arm skin near the shoulder is.
               Gated on rho too, so the genuinely medial deltoid (rho ~ 1) is spared,
               and measured against the acromion, so the medial skin of the mid shaft
               (which is lateral of it) is spared as well.
     Arm-ness A = 1 - T.  Nothing is ever hard-cut; both terms are smoothsteps.

  3. Girdle field C in [0,1].  An anisotropic distance from the Shoulder bone
     SEGMENT: an ellipse that is generous above (a_up_above) and in front
     (a_f_front) -- the clavicle ridge and the top of the trapezius junction -- and
     generous behind (a_f_back) as well, because the scapula is part of the shoulder
     girdle and the clavicle is the only bone this rig has to carry it; times a
     lateral gate in the segment parameter u, so the field decays to 0 at the sternal
     end; times a front guard that zeroes C below armature y ~ front_y0 anywhere in
     front of the clavicle plane -- this is what keeps the breasts on Spine2 -- and an
     inferior guard (low_y0/low_y1) that stops the field dropping into the axillary
     fold, which belongs to the spine, not to the girdle.

  4. One target assignment, not three sequential edits.  For each vertex let P be its
     GIRDLE POOL (the weight it spends on Shoulder + Arm + Spine2 + Spine1 + Spine)
     and f_s = smoothstep((s + hand_half) / 2*hand_half), the monotonic handover
     centred on the acromion (the house ramp of anim_lib.smooth_joint_weights):

         fArm = A * f_s
         fSh  = A * (1 - f_s)  +  (1 - A) * cl_target * C
         fTor = 1 - fArm - fSh        -> Spine2/Spine1/Spine in their existing ratio

     On the arm (A = 1) that is exactly the smoothstep about the joint, identical on
     every vertex of a ring, so the 0.5 crossing sits on the acromion and the
     per-ring spread is near zero.  On the torso (A = 0) it is the girdle field with
     everything else handed back to the spine -- which is both the Arm de-bleed and
     the clavicle's new territory.  In between it blends.  Because the target is a
     function of P, of rest geometry and of the pool's existing torso RATIO, it is a
     projection: a second run recomputes the same numbers.  A vertex with no torso
     weight and no girdle field cannot have spine weight invented for it -- its share
     goes back to Arm/Shoulder -- so nothing downstream (forearm, wrist, hand) can be
     contaminated.

  5. Laplacian smoothing.  Shoulder/Arm/Spine2/Spine1 are averaged over mesh edges
     for smooth_iters passes inside the touched region only (untouched neighbours are
     read but never written), then rescaled so each vertex's total over those four
     groups is exactly what it was.  Kills the circumferential unevenness.

  5b. Arm clamp.  Smoothing also diffuses Arm weight back onto the torso vertices
     step 4 just cleared, which is the very bleed being fixed, so afterwards Arm is
     capped at its target plus arm_clamp_slack and the excess returned to the other
     three groups in their smoothed ratio.

  6. Normalise: at most max_influences bones per vertex (smallest dropped), summing
     to 1.  Only touched vertices are written, so ForeArm/Hand ramps, the Clothes
     mesh and everything below the girdle are bit-for-bit unchanged.

  Both sides are computed in one canonical (left) frame: for the right side the
  vertex is mirrored through the rig's mirror plane and the LEFT bone frames and the
  LEFT radius profile are used.  The rest skeleton is an exact mirror, so the two
  sides get numerically identical treatment and the result stays L/R symmetric.
"""

import bpy
import json
import math
import os
import sys

from mathutils import Vector

ARMATURE_NAME = "Armature"
PFX = "mixamorig:"
BODY = "Yemoja_Body"
BACKUP_TEXT = "fix_shoulder_backup.json"

TORSO_BONES = ("Spine2", "Spine1", "Spine")
SIDED_BONES = ("Shoulder", "Arm")
SMOOTH_BONES = ("Shoulder", "Arm", "Spine2", "Spine1")

DEFAULT_PARAMS = dict(
    # --- 1. radius profile of the upper arm
    prof_bin=0.10,        # s-bin width, in arm lengths
    prof_min_w=0.90,      # a vertex counts as "pure arm" above this Arm weight
    prof_pct=0.80,        # percentile of r inside a bin -> R(s) (upper envelope)
    prof_scale=1.00,      # global fudge on R(s)
    # --- 2. Arm confinement
    rho_arm0=1.15,        # rho = d(Arm segment)/R(s); below this: pure arm, T = 0
    rho_arm1=1.50,        # above this: pure torso, T = 1
    med0=6.0,             # "medial of the acromion" ramp (armature units)
    med1=18.0,
    rho_med0=1.05,        # ... only counts once the vertex is off the arm as well
    rho_med1=1.40,
    # --- 3. girdle (clavicle + scapula) territory
    a_up_above=32.0,      # ellipse semi-axes around the Shoulder segment (arm units)
    a_up_below=34.0,
    a_f_front=34.0,
    a_f_back=46.0,
    cl_rho0=0.90,         # C = 1 inside this ellipse scale
    cl_rho1=1.40,         # C = 0 outside this one
    cl_t0=0.00,           # lateral gate along the clavicle (segment parameter u)
    cl_t1=0.45,
    cl_target=1.00,       # peak Shoulder weight the field asks for
    front_z=-35.0,        # "in front of the clavicle plane" (armature z)
    front_y0=575.0,       # C = 0 below this height at the front (keeps the breasts)
    front_y1=598.0,
    low_y0=546.0,         # inferior guard everywhere: the girdle field must not
    low_y1=566.0,         #   reach down into the axillary fold / lat
    # --- 4. handover
    hand_half=0.25,       # smoothstep half-width in arm lengths
    # --- 5/6. cleanup
    smooth_iters=10,
    smooth_lambda=0.55,
    arm_clamp_slack=0.08, # after smoothing Arm may exceed its target by this much
    arm_clamp_floor=1.0,  # 1 = uniform clamp slack, 0 = slack scales with arm-ness
    max_influences=4,
    min_weight=1e-4,
)


# ------------------------------------------------------------------ helpers ---
def _arm():
    return bpy.data.objects[ARMATURE_NAME]


def full(name):
    return name if name.startswith(PFX) else PFX + name


def _clamp01(x):
    return 0.0 if x < 0.0 else (1.0 if x > 1.0 else x)


def ss(x):
    """smoothstep on [0,1]."""
    x = _clamp01(x)
    return x * x * (3.0 - 2.0 * x)


def _bone_frame(name):
    """(head, unit axis, length) in armature space, REST."""
    b = _arm().data.bones[full(name)]
    ax = b.tail_local - b.head_local
    return b.head_local.copy(), ax.normalized(), ax.length


def _mirror_x():
    """Armature-space X of the rig's mirror plane, taken from the rig itself."""
    d = _arm().data.bones
    return 0.5 * (d[full("Shoulder.L")].head_local.x + d[full("Shoulder.R")].head_local.x)


def _seg_dist(p, a, b):
    """(distance to segment, unclamped parameter u in segment lengths)."""
    ab = b - a
    L2 = ab.length_squared
    u = (p - a).dot(ab) / L2
    t = _clamp01(u)
    return (p - (a + ab * t)).length, u


def _pct(vals, q):
    v = sorted(vals)
    if not v:
        return None
    return v[int(q * (len(v) - 1))]


# ----------------------------------------------------- weight table plumbing ---
def _girdle_groups():
    out = [full(b) for b in TORSO_BONES]
    for b in SIDED_BONES:
        out += [full(b + ".L"), full(b + ".R")]
    return out


def read_table(objname=BODY):
    """[{group_name: weight}] for every vertex -- DEFORM (armature bone) groups only.

    Shape groups such as Lips_sculpt / _LASH_CANDIDATES share the vertex-group list
    with the bones and must never be renormalised, so they are filtered out here and
    are therefore never written back either.
    """
    ob = bpy.data.objects[objname]
    bones = set(b.name for b in _arm().data.bones)
    gn = {g.index: g.name for g in ob.vertex_groups}
    tab = []
    for v in ob.data.vertices:
        tab.append({gn[ge.group]: ge.weight for ge in v.groups
                    if ge.weight > 0.0 and gn[ge.group] in bones})
    return tab


def _write_verts(objname, tab, orig, verts, min_weight):
    """Push tab[i] back into the mesh for i in verts (only groups that changed)."""
    ob = bpy.data.objects[objname]
    vg = ob.vertex_groups
    n = 0
    for i in sorted(verts):
        new, old = tab[i], orig[i]
        for name in set(new) | set(old):
            a = old.get(name, 0.0)
            b = new.get(name, 0.0)
            if abs(a - b) < 1e-7:
                continue
            if b < min_weight:
                if a > 0.0:
                    vg[name].remove([i])
            else:
                vg[name].add([i], b, "REPLACE")
            n += 1
    return n


def _backup(objname, params):
    """Stash / restore the pre-fix weights so apply() is idempotent.

    The whole bone-weight dict of every vertex that spends anything on the girdle is
    stored, not just the girdle groups: normalisation can drop a small influence of
    some other bone (Neck, Spine) from such a vertex, and a partial restore would
    leave run 2 starting from a slightly different state than run 1 did.
    """
    ob = bpy.data.objects[objname]
    gset = set(_girdle_groups())
    txt = bpy.data.texts.get(BACKUP_TEXT)
    if txt is None:
        tab = read_table(objname)
        data = {str(i): d for i, d in enumerate(tab) if gset & set(d)}
        txt = bpy.data.texts.new(BACKUP_TEXT)
        txt.write(json.dumps(dict(object=objname, groups=sorted(gset), weights=data)))
        return dict(created=True, n_verts=len(data), n_restored=0)
    data = json.loads(txt.as_string())["weights"]
    tab = read_table(objname)
    orig = [dict(d) for d in tab]
    touched = set()
    for k, d in data.items():
        i = int(k)
        tab[i] = {n: w for n, w in d.items() if w > 0.0}
        if tab[i] != orig[i]:
            touched.add(i)
    n = _write_verts(objname, tab, orig, touched, params["min_weight"])
    return dict(created=False, n_verts=len(data), n_restored=len(touched), n_edits=n)


# ------------------------------------------------------------ the geometry ---
class Frame(object):
    """Rest-space geometry of one shoulder girdle, in the canonical (left) frame."""

    def __init__(self, objname, params):
        self.p = params
        ob = bpy.data.objects[objname]
        self.to_arm = _arm().matrix_world.inverted() @ ob.matrix_world
        self.mx = _mirror_x()
        self.co_L = [self.to_arm @ v.co for v in ob.data.vertices]
        self.co_R = [Vector((2.0 * self.mx - c.x, c.y, c.z)) for c in self.co_L]
        self.AH, self.AX, self.AL = _bone_frame("Arm.L")
        self.AT = self.AH + self.AX * self.AL
        self.SH, self.SX, self.SL = _bone_frame("Shoulder.L")
        # clavicle-local basis: up-ish and forward-ish, both perpendicular to the bone
        y = Vector((0.0, 1.0, 0.0))
        self.e_up = (y - self.SX * y.dot(self.SX)).normalized()
        self.e_f = self.SX.cross(self.e_up).normalized()
        if self.e_f.z < 0.0:
            self.e_f = -self.e_f
        self.profile = self._radius_profile(objname)

    # -- 1. R(s) -------------------------------------------------------------
    def _radius_profile(self, objname):
        p = self.p
        tab = read_table(objname)
        arm = full("Arm.L")
        bins = {}
        for i, c in enumerate(self.co_L):
            if tab[i].get(arm, 0.0) < p["prof_min_w"]:
                continue
            s, r = self.axial(c)
            if s < 0.0 or s > 1.0:
                continue
            bins.setdefault(int(s / p["prof_bin"]), []).append(r)
        pts = [((k + 0.5) * p["prof_bin"], _pct(v, p["prof_pct"]) * p["prof_scale"])
               for k, v in sorted(bins.items()) if len(v) >= 2]
        if not pts:
            raise RuntimeError("no pure-Arm vertices: cannot estimate R(s)")
        return pts

    def R(self, s):
        pts = self.profile
        if s <= pts[0][0]:
            return pts[0][1]
        if s >= pts[-1][0]:
            return pts[-1][1]
        for j in range(len(pts) - 1):
            s0, r0 = pts[j]
            s1, r1 = pts[j + 1]
            if s0 <= s <= s1:
                t = (s - s0) / (s1 - s0)
                return r0 * (1.0 - t) + r1 * t
        return pts[-1][1]

    # -- per-vertex fields ---------------------------------------------------
    def axial(self, c):
        """(s in arm lengths from the acromion, perpendicular distance to the axis)."""
        d = c - self.AH
        s = d.dot(self.AX) / self.AL
        return s, (d - self.AX * (s * self.AL)).length

    def torso_ness(self, c):
        """(T, rho, s) -- how much this vertex is torso rather than upper arm.

        Two terms, combined with max():
          radial   how far off the Arm SEGMENT the vertex is, in units of the upper
                   arm's own radius at that station.  Catches the chest, the far
                   scapula, and (because it is the segment, not the axis) the neck
                   base and everything else parked off the proximal end.
          medial   the scapula / posterior armpit sit only ~1.4 arm-radii off the
                   axis, so the radial term alone cannot see them -- but they are
                   MEDIAL of the acromion, which no upper-arm skin near the shoulder
                   is.  Gated on rho as well, so the genuinely medial deltoid (which
                   sits at rho ~ 1) is never touched, and measured against the
                   acromion so the medial skin of the mid shaft (lateral of it) is
                   not either.
        """
        p = self.p
        s, _ = self.axial(c)
        dA, _u = _seg_dist(c, self.AH, self.AT)
        rho = dA / self.R(min(max(s, 0.0), 1.0))
        T_rad = ss((rho - p["rho_arm0"]) / (p["rho_arm1"] - p["rho_arm0"]))
        med = self.AH.x - c.x
        T_med = (ss((med - p["med0"]) / (p["med1"] - p["med0"]))
                 * ss((rho - p["rho_med0"]) / (p["rho_med1"] - p["rho_med0"])))
        return max(T_rad, T_med), rho, s

    def clavicle_field(self, c):
        """C in [0,1]: how much this vertex belongs to the clavicle."""
        p = self.p
        ab = self.SX * self.SL
        u = (c - self.SH).dot(ab) / ab.length_squared
        closest = self.SH + ab * _clamp01(u)
        q = c - closest
        qu = q.dot(self.e_up)
        qf = q.dot(self.e_f)
        au = p["a_up_above"] if qu >= 0.0 else p["a_up_below"]
        af = p["a_f_front"] if qf >= 0.0 else p["a_f_back"]
        rhoS = math.hypot(qu / au, qf / af)
        C = 1.0 - ss((rhoS - p["cl_rho0"]) / (p["cl_rho1"] - p["cl_rho0"]))
        C *= ss((u - p["cl_t0"]) / (p["cl_t1"] - p["cl_t0"]))
        C *= ss((c.y - p["low_y0"]) / (p["low_y1"] - p["low_y0"]))
        if c.z > p["front_z"]:
            C *= ss((c.y - p["front_y0"]) / (p["front_y1"] - p["front_y0"]))
        return C


# ------------------------------------------------------------------- stages ---
def _side_pass(fr, tab, side, params, stats, targets):
    """Stages 2-4 for one side, in place on `tab`.  Returns the touched vertex set.

    One target assignment rather than three sequential edits, which makes the whole
    thing a projection: the target is a function of the vertex's GIRDLE POOL (the
    weight it already spends on Shoulder + Arm + Spine2/1/Spine) and of rest geometry
    only, and the leftover torso share is redistributed in the pool's existing torso
    RATIO -- so a second run recomputes exactly the same numbers.

        A    = 1 - T                        arm-ness
        f_s  = smoothstep((s + half)/2half) the Shoulder->Arm handover, monotonic in s
        fArm = A * f_s
        fSh  = A * (1 - f_s)  +  (1 - A) * cl_target * C
        fTor = 1 - fArm - fSh

    On the arm (A=1) that is exactly the house smoothstep about the acromion; on the
    torso (A=0) it is the clavicle field with everything else back on the spine; in
    between it blends.  Vertices with no torso weight and no clavicle field cannot
    have torso weight invented for them -- their share is returned to Arm/Shoulder --
    so nothing downstream of the girdle (forearm, wrist, hand) can be contaminated.
    """
    p = params
    co = fr.co_L if side == "L" else fr.co_R
    g_sh = full("Shoulder." + side)
    g_arm = full("Arm." + side)
    g_tor = [full(b) for b in TORSO_BONES]
    half = p["hand_half"]
    touched = set()
    for i, c in enumerate(co):
        d = tab[i]
        w_arm = d.get(g_arm, 0.0)
        w_shl = d.get(g_sh, 0.0)
        C = fr.clavicle_field(c)
        if w_arm <= 0.0 and w_shl <= 0.0 and C <= 0.01:
            continue
        tor = {g: d.get(g, 0.0) for g in g_tor}
        pool_t = sum(tor.values())
        P = w_arm + w_shl + pool_t
        if P <= 1e-9:
            continue
        T, _rho, s = fr.torso_ness(c)
        A = 1.0 - T
        f_s = ss((s + half) / (2.0 * half))
        fArm = A * f_s
        fSh = A * (1.0 - f_s) + (1.0 - A) * p["cl_target"] * C
        fTor = 1.0 - fArm - fSh
        if pool_t <= 1e-9 and C <= 0.05:
            # nothing to hand the torso share to, and no clavicle claim: keep the
            # vertex on the arm chain rather than inventing spine weight on it.
            tot = fArm + fSh
            if tot <= 1e-6:
                continue
            fArm, fSh, fTor = fArm / tot, fSh / tot, 0.0

        before = dict(d)
        # cap for the post-smoothing clamp: the slack scales with arm-ness, so the
        # smoothing may blur Arm across the arm's own rings but not back out onto
        # torso vertices, which is the bleed being fixed.
        fl = p["arm_clamp_floor"]
        targets[i] = P * fArm + p["arm_clamp_slack"] * (fl + (1.0 - fl) * A)
        d[g_arm] = P * fArm
        d[g_sh] = P * fSh
        share = P * max(fTor, 0.0)
        if pool_t > 1e-9:
            for g in g_tor:
                if tor[g] > 0.0:
                    d[g] = share * tor[g] / pool_t
                else:
                    d.pop(g, None)
        elif share > 0.0:
            d[g_tor[0]] = share
        for g in list(d):
            if d[g] <= 0.0:
                del d[g]

        stats["arm_removed"] += max(before.get(g_arm, 0.0) - d.get(g_arm, 0.0), 0.0)
        stats["clav_gained"] += max(d.get(g_sh, 0.0) - before.get(g_sh, 0.0), 0.0)
        stats["clav_lost"] += max(before.get(g_sh, 0.0) - d.get(g_sh, 0.0), 0.0)
        if any(abs(d.get(g, 0.0) - before.get(g, 0.0)) > 1e-6
               for g in set(d) | set(before)):
            touched.add(i)
    return touched


def _laplacian(objname, tab, touched, side_groups, params):
    """Average the four girdle weights over mesh edges, per-vertex totals preserved."""
    ob = bpy.data.objects[objname]
    nb = [[] for _ in ob.data.vertices]
    for e in ob.data.edges:
        a, b = e.vertices
        nb[a].append(b)
        nb[b].append(a)
    idx = sorted(touched)
    lam = params["smooth_lambda"]
    for _ in range(int(params["smooth_iters"])):
        new = {}
        for i in idx:
            d = tab[i]
            tot = sum(d.get(g, 0.0) for g in side_groups)
            if tot <= 1e-9:
                continue
            acc = {g: 0.0 for g in side_groups}
            n = 0
            for j in nb[i]:
                dj = tab[j]
                for g in side_groups:
                    acc[g] += dj.get(g, 0.0)
                n += 1
            if not n:
                continue
            mixed = {g: (1.0 - lam) * d.get(g, 0.0) + lam * acc[g] / n
                     for g in side_groups}
            s = sum(mixed.values())
            if s <= 1e-9:
                continue
            new[i] = {g: v * tot / s for g, v in mixed.items()}
        for i, d in new.items():
            for g, v in d.items():
                if v > 0.0:
                    tab[i][g] = v
                else:
                    tab[i].pop(g, None)
    return len(idx)


def _clamp_arm(tab, verts, targets, groups, params):
    """Undo the part of the smoothing that hands Arm weight back to torso vertices.

    Laplacian smoothing is what kills the circumferential unevenness, but it also
    diffuses Arm weight back out onto the vertices step 2 just cleared -- which is
    exactly the bleed being fixed.  So after smoothing, Arm is capped at its target
    plus a small slack; the excess goes to the other three girdle groups in their
    (smoothed) ratio, so the vertex total and the smooth boundary both survive.
    """
    g_arm = groups[1]
    others = [groups[0], groups[2], groups[3]]
    n = 0
    for i in verts:
        cap = targets.get(i)
        if cap is None:
            continue
        d = tab[i]
        w = d.get(g_arm, 0.0)
        if w <= cap + 1e-9:
            continue
        excess = w - cap
        d[g_arm] = cap
        pool = sum(d.get(g, 0.0) for g in others)
        if pool > 1e-9:
            for g in others:
                if d.get(g, 0.0) > 0.0:
                    d[g] += excess * d[g] / pool
        else:
            d[others[0]] = d.get(others[0], 0.0) + excess
        n += 1
    return n


def _normalise(tab, touched, params):
    capped = 0
    for i in touched:
        d = tab[i]
        items = [(k, w) for k, w in d.items() if w > params["min_weight"]]
        if len(items) > params["max_influences"]:
            items.sort(key=lambda kv: -kv[1])
            items = items[:int(params["max_influences"])]
            capped += 1
        tot = sum(w for _, w in items)
        tab[i] = {k: w / tot for k, w in items} if tot > 0.0 else {}
    return capped


# -------------------------------------------------------------------- apply ---
def apply(objname=BODY, params=None):
    """Re-weight the shoulder girdle of `objname`.  Returns a summary dict."""
    p = dict(DEFAULT_PARAMS)
    if params:
        p.update(params)
    bpy.context.view_layer.update()

    restore = _backup(objname, p)
    fr = Frame(objname, p)
    tab = read_table(objname)
    orig = [dict(d) for d in tab]
    stats = dict(arm_removed=0.0, clav_gained=0.0, clav_lost=0.0)

    touched = set()
    per_side = {}
    targets = {}
    for side in ("L", "R"):
        targets[side] = {}
        t = _side_pass(fr, tab, side, p, stats, targets[side])
        per_side[side] = len(t)
        touched |= t

    smoothed = {}
    clamped = {}
    for side in ("L", "R"):
        groups = [full("Shoulder." + side), full("Arm." + side),
                  full("Spine2"), full("Spine1")]
        sel = set()
        co = fr.co_L if side == "L" else fr.co_R
        for i in touched:
            if any(g in tab[i] for g in groups[:2]) or fr.clavicle_field(co[i]) > 1e-3:
                sel.add(i)
        smoothed[side] = _laplacian(objname, tab, sel, groups, p)
        clamped[side] = _clamp_arm(tab, sel, targets[side], groups, p)

    capped = _normalise(tab, touched, p)
    edits = _write_verts(objname, tab, orig, touched, p["min_weight"])
    bpy.context.view_layer.update()

    maxes = {}
    for b in ("Shoulder.L", "Shoulder.R", "Arm.L", "Arm.R"):
        g = full(b)
        maxes[b] = round(max(d.get(g, 0.0) for d in tab), 3)
    sums = [sum(d.values()) for d in tab]
    return dict(
        object=objname,
        params=p,
        restore=restore,
        radius_profile=[(round(s, 3), round(r, 2)) for s, r in fr.profile],
        mirror_plane_x=round(fr.mx, 4),
        n_touched=len(touched),
        n_touched_per_side=per_side,
        n_smoothed=smoothed,
        n_arm_clamped=clamped,
        n_capped_influences=capped,
        n_group_edits=edits,
        arm_weight_removed=round(stats["arm_removed"], 2),
        clavicle_weight_gained=round(stats["clav_gained"], 2),
        clavicle_weight_lost=round(stats["clav_lost"], 2),
        max_weight=maxes,
        weight_sum_min=round(min(sums), 5),
        weight_sum_max=round(max(sums), 5),
        n_sum_off_1pct=sum(1 for s in sums if abs(s - 1.0) > 0.01),
    )


def diagnose(objname=BODY, params=None):
    """Field values along the girdle -- for tuning, not part of the fix."""
    p = dict(DEFAULT_PARAMS)
    if params:
        p.update(params)
    fr = Frame(objname, p)
    tab = read_table(objname)
    g_sh, g_arm = full("Shoulder.L"), full("Arm.L")
    rows = []
    for i, c in enumerate(fr.co_L):
        T, rho, s = fr.torso_ness(c)
        C = fr.clavicle_field(c)
        if C > 0.05 or (tab[i].get(g_arm, 0.0) > 0.1) or tab[i].get(g_sh, 0.0) > 0.1:
            rows.append(dict(vi=i, s=round(s, 3), rho=round(rho, 2), T=round(T, 3),
                             C=round(C, 3), y=round(c.y), z=round(c.z),
                             w_sh=round(tab[i].get(g_sh, 0.0), 3),
                             w_arm=round(tab[i].get(g_arm, 0.0), 3)))
    return dict(profile=fr.profile, n=len(rows), rows=rows)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    src = argv[0] if argv else "/home/claude/yemoja/v115_base.blend"
    dst = argv[1] if len(argv) > 1 else "/home/claude/yemoja/fix_shoulder/v115_shoulder.blend"
    bpy.ops.wm.open_mainfile(filepath=src)
    out = apply()
    os.makedirs(os.path.dirname(dst) or ".", exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=dst)
    print(json.dumps({k: v for k, v in out.items() if k != "params"}, indent=1))
