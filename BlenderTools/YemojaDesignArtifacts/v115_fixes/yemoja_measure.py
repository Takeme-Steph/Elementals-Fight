"""
yemoja_measure.py -- skin-weight / deformation measurement for the Yemoja rig.

Reusable companion to yemoja_anim_lib.py. Import it AFTER a blend is loaded:

    import bpy, importlib.util
    bpy.ops.wm.open_mainfile(filepath="/home/claude/yemoja/v114.blend")
    spec = importlib.util.spec_from_file_location("ym", "/home/claude/yemoja/yemoja_measure.py")
    ym = importlib.util.module_from_spec(spec); spec.loader.exec_module(ym)
    data = ym.full_audit("/home/claude/yemoja/audit/metrics_check.json")

Every measurement function is pure: it prints nothing, returns a dict, and saves/restores
the armature's pose_position so callers are never left in a surprise state. The render
functions are the only ones that touch scene settings, and they set everything they need.

FRAME NOTES (measured, see yemoja_anim_lib.py)
    armature space : +X = her left, +Y = up, +Z = forward.  Object scale 0.01.
    world space    : +X = her left, -Y = the direction she faces, +Z = up.
    mirror plane   : world x = -0.0044 (not 0).
    Vertex groups live in the mesh deform layer (vertex.groups), not mesh.attributes.

WHAT THE NUMBERS MEAN
    area ratio        posed face area / rest face area, aggregated per region.
                      < 0.5 = crushed, > 2.0 = stretched.
    ramp              child-bone weight as a function of distance along the bone from
                      the joint, in child-bone lengths. Good = monotonic, w50 near 0,
                      low per-station std (high std = one side of the joint hands over
                      earlier than the other, i.e. circumferential unevenness).
    relative twist    axial roll of a bone minus that of its parent: what the skin at
                      that joint has to absorb. This rig has NO twist bones.
"""

import bpy, math, json, os
from mathutils import Vector, Matrix, kdtree
from mathutils.bvhtree import BVHTree

ARM_NAME = "Armature"
PFX      = "mixamorig:"
BODY     = "Yemoja_Body"
CLOTHES  = "Yemoja_Clothes"
MIRROR_PLANE_X = -0.0044

# Half-widths of the smoothstep handovers that smooth_joint_weights() applied.
RAMP_HALF = {("Arm", "ForeArm"): 0.18, ("ForeArm", "Hand"): 0.22, ("Shoulder", "Arm"): 0.20}

# Objects that must not be in a deformation render: hair locs, tattoos, prop, lash/brow
# cards, loose jewellery variants. The scalp Shrinkwrap lie is handled by preview_mode().
RENDER_HIDE = ("Trident", "Yemoja_Scalp", "Yemoja_Fuzz", "Yemoja_LashCards",
               "Yemoja_BrowCards_A_R", "Yemoja_Eyeliner", "JW_Charm_Card", "JW_Cuff_Cage",
               "JW_Cuff_Coil", "JW_Var0_Cage", "JW_Var1_CoilCharm")

CHAIN = ("Shoulder", "Arm", "ForeArm", "Hand")
JOINTS = (("clavicle", ("Spine2", "Spine1"), "Shoulder"),
          ("shoulder", ("Shoulder",),        "Arm"),
          ("armpit",   ("Spine2", "Spine1"), "Arm"),
          ("elbow",    ("Arm",),             "ForeArm"),
          ("wrist",    ("ForeArm",),         "Hand"))
DOM_BONES = ("Shoulder.L", "Shoulder.R", "Arm.L", "Arm.R", "ForeArm.L", "ForeArm.R",
             "Hand.L", "Hand.R", "Spine2")
BAND_T = 0.15                 # a vertex/face is "in the band" when BOTH bones reach this
BRACELET_MARGIN = 0.15        # under-bracelet window, as a fraction of bracelet length


# --------------------------------------------------------------------- basics ---
def armature():
    return bpy.data.objects[ARM_NAME]

def full(name):
    """'Arm.L' -> 'mixamorig:Arm.L'; already-prefixed names pass through."""
    return name if name.startswith(PFX) or name.startswith("hair_") else PFX + name

def short(name):
    return name[len(PFX):] if name.startswith(PFX) else name

def bone_names():
    return set(b.name for b in armature().data.bones)

def set_pose_state(rest):
    """Switch the armature between rest and posed evaluation.

    rest=True  -> pose_position 'REST' (the bind pose; deform is identity)
    rest=False -> pose_position 'POSE' and the action re-applied at frame 1

    Returns {'previous': 'REST'|'POSE', 'current': ...} so a caller can put it back.
    """
    arm = armature()
    prev = arm.data.pose_position
    arm.data.pose_position = 'REST' if rest else 'POSE'
    if not rest:
        bpy.context.scene.frame_set(bpy.context.scene.frame_current)
    bpy.context.view_layer.update()
    bpy.context.evaluated_depsgraph_get().update()
    return dict(previous=prev, current=arm.data.pose_position)

def _is_posed():
    return armature().data.pose_position == 'POSE'

def _obj(objname):
    return bpy.data.objects[objname]

def _to_arm(ob):
    """Object-space -> armature-space matrix (base-mesh coords are rest coords)."""
    return armature().matrix_world.inverted() @ ob.matrix_world

def read_weights(objname):
    """[{bone_name: weight}] per vertex, deform groups only, weights > 1e-6."""
    ob = _obj(objname); bn = bone_names()
    gn = {g.index: g.name for g in ob.vertex_groups}
    out = []
    for v in ob.data.vertices:
        d = {}
        for ge in v.groups:
            n = gn[ge.group]
            if n in bn and ge.weight > 1e-6:
                d[n] = ge.weight
        out.append(d)
    return out

def read_group(objname, group):
    """Vertex indices with non-zero weight in a named (possibly non-deform) group."""
    ob = _obj(objname); gi = ob.vertex_groups[group].index
    ids = []
    for v in ob.data.vertices:
        for ge in v.groups:
            if ge.group == gi and ge.weight > 1e-6:
                ids.append(v.index); break
    return ids

def face_weights(objname, wts=None):
    """Per-face average of its vertices' weights -- what decides region membership."""
    ob = _obj(objname); wts = wts if wts is not None else read_weights(objname)
    out = []
    for p in ob.data.polygons:
        d = {}; n = len(p.vertices)
        for vi in p.vertices:
            for k, w in wts[vi].items():
                d[k] = d.get(k, 0.0) + w / n
        out.append(d)
    return out

def _eval_face_areas(objname):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = _obj(objname).evaluated_get(dg)
    me = ev.to_mesh()
    ar = [p.area for p in me.polygons]
    ev.to_mesh_clear()
    return ar

def _eval_mesh_arm(objname):
    """Evaluated verts/faces of an object in ARMATURE space, for BVH work."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = _obj(objname).evaluated_get(dg)
    me = ev.to_mesh()
    M = armature().matrix_world.inverted() @ ev.matrix_world
    vs = [M @ v.co for v in me.vertices]
    fs = [list(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    return vs, fs

def bone_frame_rest(name):
    """(head, unit axis, length) of a bone in armature space, rest."""
    b = armature().data.bones[full(name)]
    ax = b.tail_local - b.head_local
    return b.head_local.copy(), ax.normalized(), ax.length

def _ring_frame(name, posed):
    """Origin + orthonormal (axis, e1, e2) for cylindrical coords about a bone."""
    nm = full(name)
    if posed:
        pb = armature().pose.bones[nm]
        o = pb.head.copy(); ax = (pb.tail - pb.head).normalized()
        e1 = pb.matrix.to_3x3().col[0].normalized()
    else:
        b = armature().data.bones[nm]
        o = b.head_local.copy(); ax = (b.tail_local - b.head_local).normalized()
        e1 = b.matrix_local.to_3x3().col[0].normalized()
    e1 = (e1 - ax * e1.dot(ax)).normalized()
    return o, ax, e1, ax.cross(e1)


# ------------------------------------------------------------ 1. region areas ---
def _bracelet_geometry(side):
    """Rest-space extent of one CL_Bracelet_* ring in ForeArm-local cylindrical coords."""
    o, ax, L = bone_frame_rest("ForeArm." + side)
    up = Vector((0, 1, 0)) if abs(ax.dot(Vector((0, 1, 0)))) < 0.9 else Vector((0, 0, 1))
    e1 = (up - ax * up.dot(ax)).normalized(); e2 = ax.cross(e1)
    cl = _obj(CLOTHES); Mc = _to_arm(cl)
    ids = read_group(CLOTHES, "CL_Bracelet_" + side)
    ys, rs = [], []
    for vi in ids:
        p = (Mc @ cl.data.vertices[vi].co) - o
        ys.append(p.dot(ax)); rs.append(math.hypot(p.dot(e1), p.dot(e2)))
    y0, y1 = min(ys), max(ys)
    blen = y1 - y0; mg = BRACELET_MARGIN * blen
    return dict(bone=full("ForeArm." + side), bone_len=round(L, 3), n_bracelet_verts=len(ids),
                y_span=[round(y0, 3), round(y1, 3)],
                y_span_frac_of_bone=[round(y0 / L, 4), round(y1 / L, 4)],
                bracelet_len=round(blen, 3), r_min=round(min(rs), 3), r_max=round(max(rs), 3),
                window=[round(y0 - mg, 3), round(y1 + mg, 3)]), \
           (o, ax, e1, e2, L, y0, y1, y0 - mg, y1 + mg, max(rs), ids)

def under_bracelet_verts(side):
    """Body verts inside the bracelet's bone-space cylinder + 15% length each side."""
    info, g = _bracelet_geometry(side)
    o, ax, e1, e2, L, y0, y1, lo, hi, rmax, _ = g
    body = _obj(BODY); Mb = _to_arm(body)
    ids = []
    for v in body.data.vertices:
        p = (Mb @ v.co) - o; y = p.dot(ax)
        if lo <= y <= hi and math.hypot(p.dot(e1), p.dot(e2)) <= rmax:
            ids.append(v.index)
    info = dict(info); info["n_body_verts"] = len(ids)
    return set(ids), info

def face_regions(objname):
    """{region_name: [face indices]} for the dominant-bone regions, the joint bands and
    (Body only) the under-bracelet patches. A face joins a region on its centre weights."""
    fw = face_weights(objname); ob = _obj(objname)
    reg = {"dom_" + b: [] for b in DOM_BONES}
    want = {full(b): "dom_" + b for b in DOM_BONES}
    for i, d in enumerate(fw):
        if not d:
            continue
        best = max(d.items(), key=lambda kv: kv[1])[0]
        if best in want:
            reg[want[best]].append(i)
    for s in ("L", "R"):
        for jname, parents, child in JOINTS:
            c = full(child + "." + s) if child != "Spine2" else full(child)
            ps = [full(p + "." + s) if p in CHAIN else full(p) for p in parents]
            reg["band_%s.%s" % (jname, s)] = [
                i for i, d in enumerate(fw)
                if d.get(c, 0) >= BAND_T and max(d.get(p, 0) for p in ps) >= BAND_T]
    if objname == BODY:
        for s in ("L", "R"):
            ids, _ = under_bracelet_verts(s)
            reg["under_bracelet." + s] = [p.index for p in ob.data.polygons
                                          if all(vi in ids for vi in p.vertices)]
    return reg

def region_area_audit(objname, regions=None):
    """Per-region posed/rest face-area audit for the CURRENT pose state.

    regions: None for every region, else an iterable of region names to keep.
    Returns {'regions': {name: {...}}, 'armpit_band_all_bones': {...},
             'face_area_rest': {...}}.
    """
    arm = armature(); prev = arm.data.pose_position
    reg = face_regions(objname)
    if regions is not None:
        keep = set(regions); reg = {k: v for k, v in reg.items() if k in keep}
    posed_now = prev == 'POSE'
    set_pose_state(rest=True);  ar_r = _eval_face_areas(objname)
    set_pose_state(rest=False); ar_p = _eval_face_areas(objname)
    if not posed_now:
        set_pose_state(rest=True)
    out = {}
    for name, faces in reg.items():
        if not faces:
            out[name] = dict(n_faces=0); continue
        tr = sum(ar_r[i] for i in faces); tp = sum(ar_p[i] for i in faces)
        rs = sorted(ar_p[i] / ar_r[i] for i in faces if ar_r[i] > 1e-12)
        out[name] = dict(n_faces=len(faces), rest_area=round(tr, 6), pose_area=round(tp, 6),
                         area_ratio=round(tp / tr, 4) if tr > 0 else None,
                         crushed_lt0p5=sum(1 for r in rs if r < 0.5),
                         stretched_gt2=sum(1 for r in rs if r > 2.0),
                         min_ratio=round(rs[0], 4), max_ratio=round(rs[-1], 4),
                         p05=round(rs[int(0.05 * (len(rs) - 1))], 4),
                         p50=round(rs[len(rs) // 2], 4),
                         p95=round(rs[int(0.95 * (len(rs) - 1))], 4))
    fw = face_weights(objname)
    armpit = {}
    for s in ("L", "R"):
        acc = {}
        for i in reg.get("band_armpit." + s, []):
            for k, w in fw[i].items():
                acc[k] = max(acc.get(k, 0), w)
        armpit[s] = {short(k): round(v, 3)
                     for k, v in sorted(acc.items(), key=lambda kv: -kv[1])}
    srt = sorted(ar_r); med = srt[len(srt) // 2] if srt else 0.0
    return dict(regions=out, armpit_band_all_bones=armpit,
                face_area_rest=dict(min=round(srt[0], 8), median=round(med, 6),
                                    max=round(srt[-1], 6),
                                    n_lt_1pct_median=sum(1 for a in ar_r if a < 0.01 * med)))


# ----------------------------------------------------------- 2. weight ramps ---
def _binstats(bins):
    out = {}
    for k in sorted(bins):
        v = bins[k]; m = sum(v) / len(v)
        out["%.2f" % k] = dict(n=len(v), mean=round(m, 3),
                               std=round((sum((x - m) ** 2 for x in v) / len(v)) ** 0.5, 3))
    return out

def ramp_profile(parent, child, objname=BODY, half=None, step=0.05):
    """Quality of one joint's weight handover, for the band where both bones reach 0.15.

    parent may be a single bone or a tuple (the dominant one is picked and reported).
    Vertices are projected onto the bone axis, 0 at the joint, negative toward the
    parent, in child-bone lengths; both the child-axis and parent-axis projections are
    binned. half=<smoothstep half-width> additionally scores the weights against the
    analytic smoothstep smooth_joint_weights() would have written.
    """
    ob = _obj(objname); wts = read_weights(objname); M = _to_arm(ob)
    parents = (parent,) if isinstance(parent, str) else tuple(parent)
    pfulls = [full(p) for p in parents]; cfull = full(child)
    ids = [v.index for v in ob.data.vertices
           if wts[v.index].get(cfull, 0) >= BAND_T
           and max(wts[v.index].get(p, 0) for p in pfulls) >= BAND_T]
    res = dict(parent_candidates=[short(p) for p in pfulls], child=short(cfull),
               n_verts=len(ids))
    if not ids:
        return res
    co, cax, cL = bone_frame_rest(cfull)
    pmax = {p: max(wts[i].get(p, 0) for i in ids) for p in pfulls}
    pbone = max(pmax.items(), key=lambda kv: kv[1])[0]
    po, pax, _ = bone_frame_rest(pbone)
    bc, bp = {}, {}
    extra = {}; over4 = 0; badsum = 0; sums = []
    for i in ids:
        p = (M @ ob.data.vertices[i].co) - co
        w = wts[i].get(cfull, 0)
        bc.setdefault(math.floor((p.dot(cax) / cL) / step) * step, []).append(w)
        bp.setdefault(math.floor((p.dot(pax) / cL) / step) * step, []).append(w)
        d = wts[i]
        for k, val in d.items():
            if k != cfull and k not in pfulls:
                extra[k] = max(extra.get(k, 0), val)
        if sum(1 for x in d.values() if x > 1e-5) > 4:
            over4 += 1
        t = sum(d.values()); sums.append(t)
        if abs(t - 1.0) > 0.01:
            badsum += 1
    ks = sorted(bc); means = [sum(bc[k]) / len(bc[k]) for k in ks]
    cross = None
    for i in range(len(ks) - 1):
        if means[i] < 0.5 <= means[i + 1]:
            cross = ks[i] + step * (0.5 - means[i]) / (means[i + 1] - means[i])
    stds = [(sum((x - sum(v) / len(v)) ** 2 for x in v) / len(v)) ** 0.5 for v in bc.values()]
    res.update(parent_used=short(pbone), child_bone_len=round(cL, 3),
               parents_maxw={short(p): round(v, 3) for p, v in pmax.items()},
               monotonic=all(means[i + 1] >= means[i] - 1e-9 for i in range(len(means) - 1)),
               w50_crossing_childaxis=(round(cross, 3) if cross is not None else None),
               mean_std_of_bins=round(sum(stds) / len(stds), 3), max_bin_std=round(max(stds), 3),
               extra_bones={short(k): round(v, 3)
                            for k, v in sorted(extra.items(), key=lambda kv: -kv[1])[:8]},
               n_extra_bones=len(extra), verts_gt4_influences=over4,
               verts_sum_off_1pct=badsum, sum_min=round(min(sums), 4), sum_max=round(max(sums), 4),
               bins_child_axis=_binstats(bc), bins_parent_axis=_binstats(bp))
    if half:
        res["fidelity"] = ramp_fidelity(pbone, cfull, half, objname)
    return res

def ramp_fidelity(parent, child, half, objname=BODY):
    """Do the weights match the smoothstep of half-width `half`, and does the mesh have
    the vertex rings to carry it? A perfect ramp sampled by 4 rings still creases."""
    ob = _obj(objname); wts = read_weights(objname); M = _to_arm(ob)
    p, c = full(parent), full(child)
    co, cax, cL = bone_frame_rest(c)
    rows = []
    for v in ob.data.vertices:
        d = wts[v.index]; tot = d.get(p, 0) + d.get(c, 0)
        if tot < 0.5:
            continue
        s = ((M @ v.co) - co).dot(cax) / cL
        if abs(s) > half * 1.6:
            continue
        t = min(1.0, max(0.0, (s + half) / (2 * half)))
        # s is rounded before the ring clustering below so that the grouping is
        # reproducible: at full precision, vertices sitting 1e-5 apart flip between
        # adjacent rings depending on float ordering.
        rows.append((round(s, 4), d.get(c, 0), tot * t * t * (3 - 2 * t)))
    rows.sort()
    if not rows:
        return dict(n_verts=0)
    rings = []
    for s, w, _ in rows:
        if not rings or abs(s - rings[-1][0]) > 0.004:
            rings.append([s, []])
        rings[-1][1].append(w)
    err = [abs(w - th) for _, w, th in rows]
    means = [sum(r[1]) / len(r[1]) for r in rings]
    gaps = [(round(rings[i + 1][0] - rings[i][0], 4), round(means[i + 1] - means[i], 3))
            for i in range(len(rings) - 1)]
    worst = max(gaps, key=lambda g: abs(g[1])) if gaps else None
    return dict(n_verts=len(rows), half=half, n_rings_in_ramp=len(rings),
                ring_s=[round(r[0], 3) for r in rings],
                ring_mean_w=[round(m, 3) for m in means],
                ring_std_w=[round((sum((x - m) ** 2 for x in r[1]) / len(r[1])) ** 0.5, 3)
                            for r, m in zip(rings, means)],
                ring_n=[len(r[1]) for r in rings],
                mean_abs_err_vs_smoothstep=round(sum(err) / len(err), 4),
                max_abs_err_vs_smoothstep=round(max(err), 4),
                biggest_ring_gap=worst)

def weight_sanity(objname):
    """Export-rule hygiene: influence counts and whether weights sum to 1."""
    wts = read_weights(objname)
    inf = [len(d) for d in wts]; sums = [sum(d.values()) for d in wts]
    nz = [s for s in sums if s > 0]
    return dict(n_verts=len(wts), n_no_bone_weight=sum(1 for d in wts if not d),
                max_influences=max(inf), n_gt4=sum(1 for i in inf if i > 4),
                hist_influences={str(i): inf.count(i) for i in range(0, max(inf) + 1)},
                n_sum_off_1pct=sum(1 for s in sums if abs(s - 1) > 0.01 and s > 0),
                n_sum_off_10pct=sum(1 for s in sums if abs(s - 1) > 0.10 and s > 0),
                sum_mean=round(sum(sums) / len(sums), 4),
                sum_min=round(min(nz), 4) if nz else None, sum_max=round(max(sums), 4))

def bleed_radius(bone, objname=BODY, thr=BAND_T):
    """How far from its own bone a bone still holds >= thr, in bone lengths. A bone that
    owns skin a whole length away from itself drags that skin when it moves."""
    ob = _obj(objname); wts = read_weights(objname); M = _to_arm(ob)
    nm = full(bone); b = armature().data.bones[nm]
    h, t = b.head_local, b.tail_local; L = (t - h).length; ax = (t - h) / L
    far = []
    for v in ob.data.vertices:
        w = wts[v.index].get(nm, 0)
        if w < thr:
            continue
        p = (M @ v.co) - h; s = p.dot(ax); perp = (p - ax * s).length
        d = p.length if s < 0 else ((p - ax * L).length if s > L else perp)
        far.append((d / L, s / L, perp / L, v.index, round(w, 3)))
    far.sort(reverse=True)
    if not far:
        return dict(n_verts=0, bone_len=round(L, 2))
    return dict(n_verts=len(far), bone_len=round(L, 2),
                max_dist_over_bonelen=round(far[0][0], 3),
                p99=round(far[int(0.01 * len(far))][0], 3),
                worst=[dict(vi=f[3], w=f[4], dist_L=round(f[0], 3), s_L=round(f[1], 3),
                            perp_L=round(f[2], 3)) for f in far[:6]])


# -------------------------------------------------------------- 3. symmetry ---
def symmetry_check(bones=CHAIN, objname=BODY, tol_frac=0.005):
    """Pair vertices across the mirror plane and compare each bone's L vs R weight.

    Asymmetric weights are the usual reason one side deforms worse than the other; if
    this comes back clean, any L/R difference in the deformation is the POSE.
    """
    ob = _obj(objname); wts = read_weights(objname)
    MW = ob.matrix_world
    verts = [MW @ v.co for v in ob.data.vertices]
    zs = [p.z for p in verts]; height = max(zs) - min(zs); tol = tol_frac * height
    kd = kdtree.KDTree(len(verts))
    for i, p in enumerate(verts):
        kd.insert(p, i)
    kd.balance()
    pairs = []
    for i, p in enumerate(verts):
        if p.x <= MIRROR_PLANE_X:
            continue
        co, j, d = kd.find(Vector((2 * MIRROR_PLANE_X - p.x, p.y, p.z)))
        if d <= tol and j != i:
            pairs.append((i, j, d))
    per = {}
    for b in bones:
        L, R = full(b + ".L"), full(b + ".R")
        diffs = []; rel = []
        for i, j, _ in pairs:
            wl = wts[i].get(L, 0.0); wr = wts[j].get(R, 0.0)
            diffs.append(abs(wl - wr))
            if max(wl, wr) > 0.01:
                rel.append((abs(wl - wr), i, j, wl, wr))
        rel.sort(reverse=True)
        per[b] = dict(n_pairs=len(pairs), n_pairs_relevant=len(rel),
                      mean_abs_diff_all=round(sum(diffs) / len(diffs), 5) if diffs else None,
                      mean_abs_diff_relevant=round(sum(r[0] for r in rel) / len(rel), 5) if rel else None,
                      count_gt_0p1=sum(1 for r in rel if r[0] > 0.1),
                      max_diff=round(rel[0][0], 4) if rel else None,
                      worst=[dict(vL=r[1], vR=r[2], wL=round(r[3], 3), wR=round(r[4], 3))
                             for r in rel[:5]])
    mass = {}
    for b in list(bones) + ["Spine2"]:
        for nm in ([full(b)] if b == "Spine2" else [full(b + ".L"), full(b + ".R")]):
            if nm in bone_names():
                mass[short(nm)] = round(sum(d.get(nm, 0) for d in wts), 3)
    return dict(height=round(height, 4), tol=round(tol, 5), plane_x=MIRROR_PLANE_X,
                n_left_verts=sum(1 for p in verts if p.x > MIRROR_PLANE_X),
                n_paired=len(pairs), max_pair_dist=round(max(p[2] for p in pairs), 6),
                per_bone=per, total_weight_mass=mass)


# ------------------------------------------------------------ 4. pose stress ---
def _swing_twist(q):
    """Split a bone-local rotation into swing and twist about the bone's own Y."""
    w, x, y, z = q.w, q.x, q.y, q.z
    n = math.hypot(w, y)
    if n < 1e-9:
        tw = 0.0; qt = (1.0, 0.0, 0.0, 0.0)
    else:
        tw = 2 * math.atan2(y, w) if w >= 0 else 2 * math.atan2(-y, -w)
        qt = (w / n, 0.0, y / n, 0.0)
    a = (w, x, y, z); b = (qt[0], -qt[1], -qt[2], -qt[3])
    sw0 = a[0] * b[0] - a[1] * b[1] - a[2] * b[2] - a[3] * b[3]
    return math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(sw0))))), math.degrees(tw)

def _roll_deg(name):
    """Axial roll of a bone in armature space beyond the shortest-arc swing: the twist
    the skin actually has to carry."""
    nm = full(name); arm = armature()
    mr = arm.data.bones[nm].matrix_local.to_3x3(); mp = arm.pose.bones[nm].matrix.to_3x3()
    xr, yr = mr.col[0].normalized(), mr.col[1].normalized()
    xp, yp = mp.col[0].normalized(), mp.col[1].normalized()
    xt = (yr.rotation_difference(yp).to_matrix() @ xr).normalized()
    xt = (xt - yp * xt.dot(yp)).normalized()
    xa = (xp - yp * xp.dot(yp)).normalized()
    return math.degrees(math.atan2(xt.cross(xa).dot(yp), xt.dot(xa)))

def _wrap180(a):
    while a > 180.0:
        a -= 360.0
    while a <= -180.0:
        a += 360.0
    return a

# ---------------------------------------------------- 4b. the elbow is a hinge ---
# A human elbow flexes in ONE plane. On this rig that plane is fixed by the bind pose:
# Mixamo's rest arm already carries a 12.3 deg elbow bend pointing forward, and that
# direction IS the anatomical flexion plane. off_hinge() measures how far a pose has
# swung the forearm out of it, with the upper arm's own pose rotation divided out, so a
# real hinge reads 0 at every flexion angle.
#
# This is the metric that caught the "bends like flat paper" fault: every pose built by
# the twist-free 2-bone solver read -60.7 deg on the left, because that solver chose the
# upper arm's roll from the bend-plane normal instead of from the rest bend direction.
# At -60 deg off-hinge the forearm folds toward the SIDE of the upper arm, so the crease
# lands where there is no joint to make it -- a sheet of paper folding, not an elbow.
HINGE_TOL = 5.0            # deg; the working limit on |off_hinge| for authored arms


def _hinge_rest(side):
    """(rest arm axis, rest bend direction, rest hinge axis) in ARMATURE space."""
    arm = armature()
    ba = arm.data.bones[full("Arm." + side)]; bf = arm.data.bones[full("ForeArm." + side)]
    ua = (ba.tail_local - ba.head_local).normalized()
    fo = (bf.tail_local - bf.head_local).normalized()
    bend = (fo - ua * fo.dot(ua)).normalized()
    return ua, bend, ua.cross(bend).normalized()


def off_hinge(side):
    """Signed degrees the forearm has swung out of the elbow's anatomical flexion plane.

    The posed bend direction is mapped back through the upper arm's own pose rotation and
    compared, about the rest upper-arm axis, with the rest bend direction. 0 = a true
    hinge at any flexion; the sign says which way it has slipped.
    """
    arm = armature()
    pa = arm.pose.bones[full("Arm." + side)]; pf = arm.pose.bones[full("ForeArm." + side)]
    ua_r, bend_r, _ = _hinge_rest(side)
    ua_p = (pa.tail - pa.head).normalized()
    fo_p = (pf.tail - pf.head).normalized()
    bend_p = (fo_p - ua_p * fo_p.dot(ua_p))
    if bend_p.length < 1e-7:
        return 0.0
    bend_p.normalize()
    R = pa.matrix.to_3x3() @ arm.data.bones[full("Arm." + side)].matrix_local.to_3x3().inverted()
    v = R.inverted() @ bend_p
    v = (v - ua_r * v.dot(ua_r)).normalized()
    return math.degrees(math.atan2(ua_r.dot(bend_r.cross(v)), bend_r.dot(v)))


def elbow_decomp(side):
    """Split the elbow's relative rotation into what an elbow may do and what it may not.

    hinge flexion  -- rotation about the anatomical hinge axis; unlimited, free.
    pronation      -- twist about the FOREARM's own axis; real, and what the forearm
                      skin is built to carry.
    off_hinge      -- swing out of the flexion plane; not a joint motion at all. Any
                      non-zero value here is the upper arm's roll having been mis-set.
    """
    arm = armature()
    pa = arm.pose.bones[full("Arm." + side)]; pf = arm.pose.bones[full("ForeArm." + side)]
    Ba = arm.data.bones[full("Arm." + side)].matrix_local.to_3x3()
    Bf = arm.data.bones[full("ForeArm." + side)].matrix_local.to_3x3()
    ua_r, bend_r, h_r = _hinge_rest(side)
    ua_p = (pa.tail - pa.head).normalized()
    fo_p = (pf.tail - pf.head).normalized()
    R = pa.matrix.to_3x3() @ Ba.inverted()          # rest arm frame -> posed arm frame
    fo_0 = (R @ (Bf.col[1].normalized())).normalized()   # forearm if the elbow never moved
    h_p = (R @ h_r).normalized()
    a = (fo_0 - h_p * fo_0.dot(h_p)).normalized()
    b = (fo_p - h_p * fo_p.dot(h_p)).normalized()
    flex = math.degrees(math.atan2(h_p.dot(a.cross(b)), a.dot(b)))
    ideal = Matrix.Rotation(math.radians(flex), 3, h_p) @ R @ Bf
    q = (ideal.inverted() @ pf.matrix.to_3x3()).to_quaternion()
    swing, twist = _swing_twist(q)
    rest_flex = math.degrees(ua_r.angle((Bf.col[1]).normalized()))
    return dict(off_hinge_deg=round(off_hinge(side), 2),
                hinge_flexion_deg=round(flex, 2),
                total_flexion_deg=round(math.degrees(ua_p.angle(fo_p)), 2),
                rest_flexion_deg=round(rest_flex, 2),
                pronation_deg=round(twist, 2),
                residual_swing_deg=round(swing, 2),
                within_tol=bool(abs(off_hinge(side)) <= HINGE_TOL))


def hinge_table(sides=("L", "R")):
    return {s: elbow_decomp(s) for s in sides}


def pose_twist_table():
    """Everything about how much each arm joint is being asked to do at frame 1."""
    arm = armature(); prev = arm.data.pose_position
    set_pose_state(rest=False)

    def bvec(nm, posed):
        if posed:
            pb = arm.pose.bones[full(nm)]; return (pb.tail - pb.head).normalized()
        b = arm.data.bones[full(nm)]; return (b.tail_local - b.head_local).normalized()

    basis, roll, geo = {}, {}, {}
    for s in ("L", "R"):
        prev_roll = 0.0
        for b in ("Spine2",) + CHAIN:
            nm = b if b == "Spine2" else b + "." + s
            r = _roll_deg(nm)
            roll[b + "." + s] = dict(roll_deg=round(r, 2),
                                     rel_twist_at_joint_deg=round(r - prev_roll, 2),
                                     rel_twist_wrapped_deg=round(_wrap180(r - prev_roll), 2))
            prev_roll = r
        for b in CHAIN:
            q = arm.pose.bones[full(b + "." + s)].matrix_basis.to_quaternion()
            sw, tw = _swing_twist(q)
            basis[b + "." + s] = dict(swing_deg=round(sw, 2), twist_deg=round(tw, 2),
                                      total_deg=round(math.degrees(q.angle), 2))
        sg = 1.0 if s == "L" else -1.0

        def ang(v):
            return (math.degrees(math.asin(max(-1.0, min(1.0, v.y)))),
                    math.degrees(math.atan2(v.z, v.x * sg)))
        for nm, lbl in (("Shoulder." + s, "Shoulder"), ("Arm." + s, "Arm")):
            er, pr = ang(bvec(nm, False)); ep, pp = ang(bvec(nm, True))
            geo[lbl + "." + s] = dict(rest_elev_deg=round(er, 2), pose_elev_deg=round(ep, 2),
                                      d_elev=round(ep - er, 2),
                                      rest_protraction_deg=round(pr, 2),
                                      pose_protraction_deg=round(pp, 2),
                                      d_protraction=round(pp - pr, 2))
        sp2 = arm.pose.bones[full("Spine2")]
        tdown = -(sp2.tail - sp2.head).normalized()
        sp2r = bvec("Spine2", False)
        ap, ar_ = bvec("Arm." + s, True), bvec("Arm." + s, False)
        g = geo["Arm." + s]
        g["angle_to_torso_down_rest"] = round(math.degrees(ar_.angle(-sp2r)), 2)
        g["angle_to_torso_down_pose"] = round(math.degrees(ap.angle(tdown)), 2)
        g["arm_elev_rel_torso_deg"] = round(g["angle_to_torso_down_pose"]
                                            - g["angle_to_torso_down_rest"], 2)
        fp, fr = bvec("ForeArm." + s, True), bvec("ForeArm." + s, False)
        hp, hr = bvec("Hand." + s, True), bvec("Hand." + s, False)
        geo["Elbow." + s] = dict(rest_included_deg=round(180 - math.degrees(ar_.angle(fr)), 2),
                                 pose_included_deg=round(180 - math.degrees(ap.angle(fp)), 2))
        geo["Wrist." + s] = dict(rest_included_deg=round(180 - math.degrees(fr.angle(hr)), 2),
                                 pose_included_deg=round(180 - math.degrees(fp.angle(hp)), 2))
    hinge = {s: elbow_decomp(s) for s in ("L", "R")}
    if prev == 'REST':
        set_pose_state(rest=True)
    return dict(matrix_basis_swing_twist=basis, geometric_roll=roll, geometry=geo,
                elbow_hinge=hinge)


# ---------------------------------------------------------- 5. bracelet fit ---
def bracelet_fit(side, n_stations=6, n_dirs=24):
    """Does the bracelet ring fit the arm, in the CURRENT pose state?

    Rays are cast outward from the forearm axis, so 'skin outside the ring' means the
    skin has punched through the bracelet's inner surface, and 'bracelet vert inside the
    skin' means a bead is buried. Negative station clearance is a rest-state fit fault,
    not a weighting fault.
    """
    posed = _is_posed()
    info, g = _bracelet_geometry(side)
    _, _, _, _, L, y0, y1, lo, hi, _, bids = g
    sk, info = under_bracelet_verts(side)
    o, ax, e1, e2 = _ring_frame("ForeArm." + side, posed)
    bv, bf = _eval_mesh_arm(BODY)
    cv, cf = _eval_mesh_arm(CLOTHES)
    bset = set(bids)
    bfaces = [cf[p.index] for p in _obj(CLOTHES).data.polygons
              if all(vi in bset for vi in p.vertices)]
    br = BVHTree.FromPolygons([tuple(v) for v in cv], bfaces, all_triangles=False, epsilon=0.0)
    bd = BVHTree.FromPolygons([tuple(v) for v in bv], bf, all_triangles=False, epsilon=0.0)

    def cyl(p):
        q = p - o
        return q.dot(ax), Vector((q.dot(e1), q.dot(e2))).length, math.atan2(q.dot(e2), q.dot(e1))

    pokes = []; nohit = 0; in_span = 0
    for vi in sk:
        y, r, th = cyl(bv[vi])
        if not (y0 <= y <= y1):
            continue
        in_span += 1
        base = o + ax * y; d = (bv[vi] - base).normalized()
        hit = br.ray_cast(base + d * 0.01, d, 200.0)
        if hit[0] is None:
            nohit += 1; continue
        r_in = (hit[0] - base).length
        if r > r_in + 1e-6:
            pokes.append((vi, round(r - r_in, 3), round(y, 2), round(math.degrees(th), 1)))
    pokes.sort(key=lambda t: -t[1])
    ins = []
    for vi in bids:
        y, r, th = cyl(cv[vi])
        base = o + ax * y; d = (cv[vi] - base).normalized()
        hit = bd.ray_cast(base + d * 0.01, d, 200.0)
        if hit[0] is None:
            continue
        r_sk = (hit[0] - base).length
        if r < r_sk - 1e-6:
            ins.append((vi, round(r_sk - r, 3), round(y, 2), round(math.degrees(th), 1)))
    ins.sort(key=lambda t: -t[1])
    stations = {}
    for k in range(n_stations):
        ymid = y0 + (y1 - y0) * (k + 0.5) / n_stations
        base = o + ax * ymid
        rin, rsk = [], []
        for j in range(n_dirs):
            th = 2 * math.pi * j / n_dirs
            d = (e1 * math.cos(th) + e2 * math.sin(th)).normalized()
            h1 = br.ray_cast(base + d * 0.01, d, 200.0)
            h2 = bd.ray_cast(base + d * 0.01, d, 200.0)
            if h1[0] is not None:
                rin.append((h1[0] - base).length)
            if h2[0] is not None:
                rsk.append((h2[0] - base).length)
        stations["y%.1f" % ymid] = dict(
            bracelet_inner_r_mean=round(sum(rin) / len(rin), 2) if rin else None,
            bracelet_inner_r_min=round(min(rin), 2) if rin else None,
            skin_r_mean=round(sum(rsk) / len(rsk), 2) if rsk else None,
            skin_r_max=round(max(rsk), 2) if rsk else None,
            clearance_mean=round(sum(rin) / len(rin) - sum(rsk) / len(rsk), 2) if rin and rsk else None,
            n_dirs_skin_outside=sum(1 for j in range(len(rin)) if j < len(rsk) and rsk[j] > rin[j]))
    return dict(side=side, pose_state='POSE' if posed else 'REST', region=info,
                n_skin_verts_in_span=in_span, n_skin_poking_outside=len(pokes),
                max_poke_depth=pokes[0][1] if pokes else 0.0, worst_pokes=pokes[:8],
                skin_rays_no_hit=nohit, n_bracelet_verts=len(bids),
                n_bracelet_verts_inside_skin=len(ins),
                max_inside_depth=ins[0][1] if ins else 0.0, worst_inside=ins[:8],
                stations=stations)


# ----------------------------------------------------------------- 6. renders ---
_CAM_NAME = "_MEASURE_cam"

# --- scene state guard -------------------------------------------------------
# prepare_render() deliberately vandalises the live scene: Workbench engine, square
# resolution, flat/single shading, half the character hidden, the source collection
# excluded. That is fine inside a measurement; it is not fine afterwards, and once it
# was left that way in a saved file. Every entry point that calls prepare_render() now
# snapshots the scene first and restores it in a `finally` -- but only when it is the
# OUTERMOST such call, so full_audit()'s hundred renders pay for one snapshot and
# nested helpers do not fight each other. _RENDER_DEPTH is that counter.
_RENDER_DEPTH = 0


def _layer_collections(lc=None, path="", out=None):
    """[(path, layer_collection)] for the whole view-layer tree, root first."""
    out = [] if out is None else out
    if lc is None:
        lc = bpy.context.view_layer.layer_collection
        path = lc.name
    out.append((path, lc))
    for ch in lc.children:
        _layer_collections(ch, path + "/" + ch.name, out)
    return out


def _view3d_spaces():
    """[(key, space)] for every 3D view in every screen -- these carry their own shading."""
    out = []
    for scr in bpy.data.screens:
        for ai, area in enumerate(scr.areas):
            if area.type != 'VIEW_3D':
                continue
            for si, sp in enumerate(area.spaces):
                if sp.type == 'VIEW_3D':
                    out.append(("%s/%d/%d" % (scr.name, ai, si), sp))
    return out


def scene_snapshot():
    """Capture every scene setting the render/measure helpers are known to change.

    Covers: render engine, resolution (+percentage), film_transparent, output path and
    format, the scene camera, frame range and current frame, the workbench display
    shading, the world, each object's hide_viewport / hide_render / hide_get, each
    modifier's show_viewport / show_render, every layer collection's exclude flag, the
    armature's pose_position, and every 3D view's shading type / color_type / cavity.

    Pass the result to scene_restore(); compare two with scene_diff().
    """
    sc = bpy.context.scene
    r, sh = sc.render, sc.display.shading
    objs, mods = {}, {}
    for ob in bpy.data.objects:
        try:
            hg = ob.hide_get()
        except RuntimeError:                       # not in this view layer
            hg = None
        objs[ob.name] = (bool(ob.hide_viewport), bool(ob.hide_render), hg)
        for m in ob.modifiers:
            mods[(ob.name, m.name)] = (bool(m.show_viewport), bool(m.show_render))
    arm = bpy.data.objects.get(ARM_NAME)
    return dict(
        engine=r.engine,
        resolution=(r.resolution_x, r.resolution_y, r.resolution_percentage),
        film_transparent=bool(r.film_transparent),
        filepath=r.filepath, file_format=r.image_settings.file_format,
        camera=sc.camera.name if sc.camera else None,
        frames=(sc.frame_start, sc.frame_end, sc.frame_current),
        shading=dict(light=sh.light, color_type=sh.color_type,
                     show_cavity=bool(sh.show_cavity), single_color=tuple(sh.single_color),
                     background_type=sh.background_type,
                     background_color=tuple(sh.background_color)),
        world=sc.world.name if sc.world else None,
        objects=objs, modifiers=mods,
        collections={p: bool(lc.exclude) for p, lc in _layer_collections()},
        pose_position=arm.data.pose_position if arm else None,
        views={k: (sp.shading.type, sp.shading.color_type, bool(sp.shading.show_cavity))
               for k, sp in _view3d_spaces()},
    )


def scene_restore(snap):
    """Put everything scene_snapshot() captured back. Returns the list of keys touched."""
    sc = bpy.context.scene
    r, sh = sc.render, sc.display.shading
    touched = []
    if r.engine != snap["engine"]:
        r.engine = snap["engine"]; touched.append("engine")
    if (r.resolution_x, r.resolution_y, r.resolution_percentage) != tuple(snap["resolution"]):
        r.resolution_x, r.resolution_y, r.resolution_percentage = snap["resolution"]
        touched.append("resolution")
    if bool(r.film_transparent) != snap["film_transparent"]:
        r.film_transparent = snap["film_transparent"]; touched.append("film_transparent")
    if r.filepath != snap["filepath"]:
        r.filepath = snap["filepath"]; touched.append("filepath")
    if r.image_settings.file_format != snap["file_format"]:
        r.image_settings.file_format = snap["file_format"]; touched.append("file_format")
    cam = bpy.data.objects.get(snap["camera"]) if snap["camera"] else None
    if sc.camera is not cam:
        sc.camera = cam; touched.append("camera")
    if (sc.frame_start, sc.frame_end) != tuple(snap["frames"][:2]):
        sc.frame_start, sc.frame_end = snap["frames"][:2]; touched.append("frame_range")
    s = snap["shading"]
    for k in ("light", "color_type", "show_cavity", "background_type"):
        if getattr(sh, k) != s[k]:
            setattr(sh, k, s[k]); touched.append("shading." + k)
    for k in ("single_color", "background_color"):
        if tuple(getattr(sh, k)) != tuple(s[k]):
            setattr(sh, k, s[k]); touched.append("shading." + k)
    w = bpy.data.worlds.get(snap["world"]) if snap["world"] else None
    if sc.world is not w:
        sc.world = w; touched.append("world")
    for name, (hv, hr, hg) in snap["objects"].items():
        ob = bpy.data.objects.get(name)
        if ob is None:
            continue
        if bool(ob.hide_viewport) != hv:
            ob.hide_viewport = hv; touched.append("hide_viewport:" + name)
        if bool(ob.hide_render) != hr:
            ob.hide_render = hr; touched.append("hide_render:" + name)
        if hg is not None:
            try:
                if ob.hide_get() != hg:
                    ob.hide_set(hg); touched.append("hide_get:" + name)
            except RuntimeError:
                pass
    for (obn, mn), (sv, sr) in snap["modifiers"].items():
        ob = bpy.data.objects.get(obn)
        m = ob.modifiers.get(mn) if ob else None
        if m is None:
            continue
        if bool(m.show_viewport) != sv:
            m.show_viewport = sv; touched.append("mod_viewport:%s/%s" % (obn, mn))
        if bool(m.show_render) != sr:
            m.show_render = sr; touched.append("mod_render:%s/%s" % (obn, mn))
    have = dict(_layer_collections())
    for p, ex in snap["collections"].items():
        lc = have.get(p)
        if lc is not None and bool(lc.exclude) != ex:
            lc.exclude = ex; touched.append("exclude:" + p)
    arm = bpy.data.objects.get(ARM_NAME)
    if arm and snap["pose_position"] and arm.data.pose_position != snap["pose_position"]:
        arm.data.pose_position = snap["pose_position"]; touched.append("pose_position")
    # The measurement camera is created on demand by _camera(); if it did not exist when
    # the snapshot was taken, remove it again -- "leave the scene as found" includes not
    # leaving a stray camera behind.
    mc = bpy.data.objects.get(_CAM_NAME)
    if mc is not None and _CAM_NAME not in snap["objects"]:
        cd = mc.data
        bpy.data.objects.remove(mc, do_unlink=True)
        if cd.users == 0:
            bpy.data.cameras.remove(cd)
        touched.append("removed:" + _CAM_NAME)
    have_v = dict(_view3d_spaces())
    for k, (ty, ct, cav) in snap["views"].items():
        sp = have_v.get(k)
        if sp is None:
            continue
        if sp.shading.type != ty:
            sp.shading.type = ty; touched.append("view.type:" + k)
        if sp.shading.color_type != ct:
            sp.shading.color_type = ct; touched.append("view.color_type:" + k)
        if bool(sp.shading.show_cavity) != cav:
            sp.shading.show_cavity = cav; touched.append("view.cavity:" + k)
    # frame_current last: it re-evaluates the action, which must happen after
    # pose_position is back.
    if sc.frame_current != snap["frames"][2]:
        sc.frame_set(snap["frames"][2]); touched.append("frame_current")
    bpy.context.view_layer.update()
    return touched


def _norm(v):
    """Compare-friendly form: tuples/lists collapse to tuples, everything else as-is."""
    if isinstance(v, (tuple, list)):
        return tuple(_norm(x) for x in v)
    return v


def scene_diff(a, b):
    """{key: (a_value, b_value)} for every captured setting that differs. {} == identical."""
    out = {}
    for k in a:
        av, bv = a[k], b.get(k)
        if isinstance(av, dict):
            bv = bv or {}
            for sk in set(av) | set(bv):
                if _norm(av.get(sk)) != _norm(bv.get(sk)):
                    out["%s[%s]" % (k, sk)] = (av.get(sk), bv.get(sk))
        elif _norm(av) != _norm(bv):
            out[k] = (av, bv)
    return out


class render_state(object):
    """Context manager: snapshot the scene on the OUTERMOST entry, restore on its exit.

        with render_state():
            prepare_render()
            ...renders...
        # scene is exactly as it was

    Nested uses (render_region inside full_audit) only bump the counter, so one snapshot
    covers the whole batch and the inner calls do not restore mid-run.
    """
    def __init__(self):
        self.snap = None

    def __enter__(self):
        global _RENDER_DEPTH
        if _RENDER_DEPTH == 0:
            self.snap = scene_snapshot()
        _RENDER_DEPTH += 1
        return self

    def __exit__(self, *exc):
        global _RENDER_DEPTH
        _RENDER_DEPTH -= 1
        if _RENDER_DEPTH == 0 and self.snap is not None:
            scene_restore(self.snap)
        return False


def prepare_render():
    """preview_mode(True) equivalent plus hiding everything that occludes skin.

    This deliberately leaves the scene in measurement state, so it must only be called
    inside a `with render_state():` block -- which is what render_region(), render_heat()
    and full_audit() do. Calling it bare leaves the live scene vandalised."""
    changed = []
    sc = bpy.context.scene
    for obj_name, mod_name in (("Yemoja_Scalp", "Shrinkwrap"),):
        ob = bpy.data.objects.get(obj_name)
        if ob and ob.modifiers.get(mod_name):
            m = ob.modifiers[mod_name]; m.show_viewport = False; m.show_render = False
            changed.append(obj_name + "/" + mod_name)
    for nm in ("Yemoja_Tattoos",) + RENDER_HIDE:
        ob = bpy.data.objects.get(nm)
        if ob:
            ob.hide_viewport = True; ob.hide_render = True; changed.append(nm)
    lc = bpy.context.view_layer.layer_collection.children.get("Yemoja")
    if lc and lc.children.get("Yemoja_Source"):
        lc.children["Yemoja_Source"].exclude = True; changed.append("Yemoja_Source excluded")
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.image_settings.file_format = 'PNG'
    bpy.context.view_layer.update()
    return changed

def _camera():
    cam = bpy.data.objects.get(_CAM_NAME)
    if cam is None:
        cam = bpy.data.objects.new(_CAM_NAME, bpy.data.cameras.new(_CAM_NAME))
        bpy.context.scene.collection.objects.link(cam)
    cam.data.lens = 50.0
    return cam

def _aim(focus, dirv, width):
    """Place the camera at `width` of framed height along `dirv` from `focus`."""
    cam = _camera(); cd = cam.data
    d = Vector(dirv).normalized()
    fov = 2 * math.atan(0.5 * cd.sensor_width / cd.lens)
    loc = Vector(focus) + d * (width / (2 * math.tan(fov / 2)))
    z = (loc - Vector(focus)).normalized()
    x = Vector((0, 0, 1)).cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize(); y = z.cross(x)
    cam.matrix_world = Matrix(((x.x, y.x, z.x, loc.x), (x.y, y.y, z.y, loc.y),
                               (x.z, y.z, z.z, loc.z), (0.0, 0.0, 0.0, 1.0)))
    bpy.context.scene.camera = cam
    bpy.context.view_layer.update()
    return cam

def bone_world(name, which="head"):
    pb = armature().pose.bones[full(name)]
    return armature().matrix_world @ (pb.head if which == "head" else pb.tail)

F = Vector((0, -1, 0)); B = Vector((0, 1, 0)); UPV = Vector((0, 0, 1)); DN = Vector((0, 0, -1))

def shot_spec(name):
    """(focus, direction, framed width, hide_clothes) for a named close-up, computed off
    the CURRENT pose's bone landmarks so rest and pose frame the same anatomy."""
    bw = bone_world
    if name in ("clavicleFront", "clavicleBack"):
        mid = (bw("Shoulder.L") + bw("Shoulder.R")) / 2
        if name == "clavicleFront":
            return mid + Vector((0, 0, -0.05)), F * 1.0 + UPV * 0.40, 1.05, False
        return mid, B * 1.0 + UPV * 0.40, 1.30, False
    if name == "ctx_front":
        return Vector((0.08, 0.46, 4.5)), F, 6.0, False
    if name in ("ctx_upperbodyL", "ctx_upperbodyR"):
        o = Vector((1, 0, 0)) if name.endswith("L") else Vector((-1, 0, 0))
        return Vector((0.08, 0.46, 5.6)), F * 0.7 + o * 0.7 + UPV * 0.15, 3.2, False
    if name == "ctx_armL_top":
        return (bw("Arm.L") + bw("Hand.L", "tail")) / 2, F * 0.3 + Vector((1, 0, 0)) * 0.6 + UPV * 0.7, 2.2, False
    base, s = name[:-1], name[-1]
    if s not in ("L", "R"):
        raise ValueError("unknown shot %r" % name)
    out = Vector((1, 0, 0)) if s == "L" else Vector((-1, 0, 0))
    f = F if s == "L" else B
    S = bw("Arm." + s); T = bw("Arm." + s, "tail")
    E = bw("ForeArm." + s); W = bw("ForeArm." + s, "tail"); H = bw("Hand." + s, "tail")
    ua = (E - S).normalized(); fa = (W - E).normalized(); ha = (H - W).normalized()
    n = ua.cross(fa)
    n = (out.cross(ua) if n.length < 1e-4 else n).normalized()
    if n.dot(out) < 0:
        n = -n
    if base == "elbowHinge":                       # along the hinge: reads the bend profile
        return E, n, 0.60, False
    if base == "elbowCrease":                      # out of the crease along the bisector
        return E, -(ua + fa).normalized(), 0.60, False
    if base == "elbow":
        return E, out * 1.0 + f * 0.35 + UPV * 0.10, 0.58, False
    if base in ("wristOut", "wristOutNoClothes"):
        d1 = (out - fa * out.dot(fa)).normalized()
        return W + (H - W) * 0.10, d1, 0.50, base.endswith("NoClothes")
    if base == "wristHinge":
        nh = fa.cross(ha)
        nh = (n if nh.length < 1e-4 else nh).normalized()
        if nh.dot(out) < 0:
            nh = -nh
        return W + (H - W) * 0.10, nh, 0.50, False
    if base == "wrist":
        return W + (H - W) * 0.12, out * 0.6 + f * 0.85 + UPV * 0.45, 0.46, False
    if base == "shoulder":
        return S + (T - S) * 0.12, out * 1.0 + f * 0.55 + UPV * 0.55, 0.85, False
    if base == "scapula":
        return S + (T - S) * 0.10, out * 0.45 - f * 1.0 + UPV * 0.20, 1.10, False
    if base == "armpit":
        return S + (T - S) * 0.30, out * 1.0 + DN * 0.55 + f * 0.30, 1.15, False
    if base == "armpitSkin":                       # clothes off: the fold itself
        return S + (T - S) * 0.30, out * 0.85 + f * 0.55 + DN * 0.42, 1.25, True
    if base == "arm":
        return (S + W) * 0.5, out * 1.0 + f * 0.45 + UPV * 0.10, 1.9, False
    raise ValueError("unknown shot %r" % name)

SHOT_NAMES = tuple(
    ["clavicleFront", "clavicleBack", "ctx_front", "ctx_upperbodyL", "ctx_upperbodyR",
     "ctx_armL_top"] +
    [b + s for s in ("L", "R") for b in ("elbowHinge", "elbowCrease", "elbow", "wristOut",
                                         "wristOutNoClothes", "wristHinge", "wrist",
                                         "shoulder", "scapula", "armpit", "armpitSkin", "arm")])

def render_region(name, rest, out_path, res=700, noclothes=None):
    """Clay close-up of one named region. rest=True renders the bind pose with the camera
    re-placed on the same bone landmark, so the two frames are comparable.

    The scene is snapshotted on entry and restored in a `finally` (unless this call is
    nested inside another render_state(), in which case the outer one restores)."""
    with render_state():                       # restores in its __exit__ (a finally)
            prepare_render()
            prev = armature().data.pose_position
            set_pose_state(rest=rest)
            focus, dirv, width, hide_cl = shot_spec(name)
            if noclothes is not None:
                hide_cl = noclothes
            cl = bpy.data.objects.get(CLOTHES); was = cl.hide_render if cl else None
            if cl:
                cl.hide_render = bool(hide_cl)
            sc = bpy.context.scene; sh = sc.display.shading
            sh.light = "STUDIO"; sh.color_type = "SINGLE"; sh.single_color = (0.62, 0.63, 0.66)
            sh.show_cavity = True
            sc.render.resolution_x = sc.render.resolution_y = res
            sc.render.resolution_percentage = 100
            _aim(focus, dirv, width)
            os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
            sc.render.filepath = out_path
            bpy.ops.render.render(write_still=True)
            if cl:
                cl.hide_render = was
            set_pose_state(rest=(prev == 'REST'))
            return dict(name=name, path=out_path, rest=bool(rest), width=round(width, 3),
                        focus=[round(v, 4) for v in focus],
                        direction=[round(v, 4) for v in Vector(dirv).normalized()],
                        clothes_hidden=bool(hide_cl))

_HEAT_ATTR = "AUDIT_W"

def _heat_ramp(w):
    w = max(0.0, min(1.0, w)); x = w * 4
    if x < 1:   c = (0.0, x, 1.0)
    elif x < 2: c = (0.0, 1.0, 2 - x)
    elif x < 3: c = (x - 2, 1.0, 0.0)
    else:       c = (1.0, 4 - x, 0.0)
    return (c[0], c[1], c[2], 1.0)

def render_heat(bone, view, out_path, objname=BODY, res=700):
    """Bake one bone's weight into a colour attribute (blue 0 -> red 1) and render it flat
    in the bind pose. view is 'front' or 'top'.

    Scene state is snapshotted and restored (see render_state); the baked colour
    attribute is mesh data, not scene state, and is left in place on purpose."""
    with render_state():                       # restores in its __exit__ (a finally)
        prepare_render()
        for nm in (CLOTHES, "Yemoja_Nails"):
            ob = bpy.data.objects.get(nm)
            if ob:
                ob.hide_render = True
        prev = armature().data.pose_position
        set_pose_state(rest=True)
        ob = _obj(objname); me = ob.data
        wts = read_weights(objname); nm = full(bone)
        ca = me.color_attributes.get(_HEAT_ATTR) or me.color_attributes.new(
            name=_HEAT_ATTR, type='FLOAT_COLOR', domain='POINT')
        me.attributes.default_color_name = ca.name
        me.attributes.active_color_name = ca.name
        for v in me.vertices:
            ca.data[v.index].color = _heat_ramp(wts[v.index].get(nm, 0.0))
        sc = bpy.context.scene; sh = sc.display.shading
        sh.light = "FLAT"; sh.color_type = "VERTEX"; sh.show_cavity = False
        sc.render.resolution_x = sc.render.resolution_y = res
        sc.render.resolution_percentage = 100
        side = bone[-1] if bone[-1] in ("L", "R") else "L"
        focus = (bone_world("Shoulder." + side) + bone_world("Hand." + side, "tail")) / 2
        dirv = F * 1.0 + UPV * 0.18 if view == "front" else UPV * 1.0 + F * 0.35
        _aim(focus, dirv, 2.6)
        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        sc.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        set_pose_state(rest=(prev == 'REST'))
        return dict(bone=bone, view=view, path=out_path, attribute=ca.name,
                    max_weight=round(max(d.get(nm, 0.0) for d in wts), 3))


# ------------------------------------------------------------- 7. full audit ---
def stress_poses_schema(text_name="stress_poses.json"):
    """Describe (do not run) the stress-pose spec stored as a text datablock."""
    t = bpy.data.texts.get(text_name)
    if t is None:
        return dict(present=False)
    d = json.loads(t.as_string())
    poses = d.get("poses", {})
    return dict(present=True, keys=sorted(d.keys()), axes=d.get("axes"),
                n_poses=len(poses),
                poses={k: dict(n_entries=len(v), bones=[e[0] for e in v]) for k, v in poses.items()},
                hips_offset=d.get("hips_offset"), invert_deg=d.get("invert_deg"),
                entry_shape="[bone_name, [lat, fwd, up]] -- unit target direction for the "
                            "bone's axis in the axes basis")

def full_audit(out_json, renders_dir=None, res=700):
    """Run every measurement, write it to out_json, and return it.

    renders_dir=None (default) skips rendering, which is what you want when you only need
    the numbers; pass a directory to also produce the full close-up and heat-map set.
    """
    arm = armature(); prev = arm.data.pose_position
    res_d = {}
    set_pose_state(rest=False)
    res_d["area_audit"] = {k: region_area_audit(k) for k in (BODY, CLOTHES)}
    ramps = {}
    for objname in (BODY, CLOTHES):
        for s in ("L", "R"):
            for jname, parents, child in JOINTS:
                ps = tuple(p if p.startswith("Spine") else p + "." + s for p in parents)
                c = child if child.startswith("Spine") else child + "." + s
                half = None
                for (pp, cc), h in RAMP_HALF.items():
                    if cc == child and pp in parents:
                        half = h
                ramps["%s.%s.%s" % (objname, jname, s)] = ramp_profile(
                    ps, c, objname=objname, half=(half if objname == BODY else None))
    res_d["weight_quality"] = ramps
    res_d["weight_sanity"] = {k: weight_sanity(k) for k in (BODY, CLOTHES)}
    res_d["bleed_radius"] = {b: bleed_radius(b) for b in
                             ("Shoulder.L", "Shoulder.R", "Arm.L", "Arm.R",
                              "ForeArm.L", "ForeArm.R", "Spine2")}
    wb = read_weights(BODY)
    res_d["max_weight"] = {short(full(b)): round(max(d.get(full(b), 0) for d in wb), 3)
                           for b in DOM_BONES + ("Spine1",)}
    res_d["symmetry"] = symmetry_check(CHAIN)
    res_d["pose_stress"] = pose_twist_table()
    bf = {}
    for rest in (True, False):
        set_pose_state(rest=rest)
        for s in ("L", "R"):
            bf["%s.%s" % ('REST' if rest else 'POSE', s)] = bracelet_fit(s)
    set_pose_state(rest=False)
    res_d["bracelet_fit"] = bf
    res_d["stress_poses_schema"] = stress_poses_schema()
    if renders_dir:
        made = []
        with render_state():                   # one snapshot for the whole batch
            for name in SHOT_NAMES:
                for rest in (False, True):
                    tag = "rest" if rest else "pose"
                    made.append(render_region(name, rest,
                                              os.path.join(renders_dir, "%s_%s.png" % (name, tag)),
                                              res=res))
            for s in ("L", "R"):
                for b in CHAIN:
                    for view in ("front", "top"):
                        made.append(render_heat(b + "." + s, view,
                                                os.path.join(renders_dir,
                                                             "heat_%s.%s_%s_rest.png" % (b, s, view)),
                                                res=res))
        res_d["renders"] = made
    if out_json:
        os.makedirs(os.path.dirname(out_json) or ".", exist_ok=True)
        with open(out_json, "w") as fh:
            json.dump(res_d, fh, indent=1)
    set_pose_state(rest=(prev == 'REST'))
    return res_d
