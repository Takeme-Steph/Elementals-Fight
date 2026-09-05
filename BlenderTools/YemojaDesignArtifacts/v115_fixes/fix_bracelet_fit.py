"""
fix_bracelet_fit.py -- rigid reposition/rescale of the CL_Bracelet_L/R rings (Yemoja_Clothes)
so they clear the wrist weight ramp and fit the forearm skin, measured in REST geometry.

REV 2 (v115_sh_tw.blend base; wrist twist now L -10 deg / R +28.5 deg). Superseded rev 1
(cylindrical scale about the bone axis): the ring's own axis is tilted ~18 deg off the bone
axis, so scaling radially about the BONE axis stretched/distorted the beads -- rejected on
review. This revision works entirely in the RING's OWN frame and only ever applies a rigid
translate + a single UNIFORM 3D scale about the ring's own centre, so bead proportions never
change.

Root problem (see /home/claude/yemoja/audit/REPORT.md and yemoja_measure.bracelet_fit):
  The bracelet ring spans ForeArm-local axial fraction ~0.87 -> ~1.10 of the bone -- part of
  it sits past the wrist joint, over Hand-weighted skin and inside the ForeArm->Hand
  smoothstep ramp. Because the bracelet is rigidly bound 100% to ForeArm, skin in the ramp
  band moves differently from the ring across the pose, producing buried/floating eccentricity.

Method:
  1. Per side, take the CL_Bracelet_<side> vertex group (32 bead/rod islands, connected
     components of the group). RING FRAME: centre C = mean of the island centroids; axis A =
     normal of the plane PCA-fit to those centroids (sign resolved to point distally, i.e.
     same broad direction as the ForeArm bone axis) -- this is the ring's own axis, not the
     bone's; the angle between them is reported per side.
  2. Allowed operations, all rigid / shape-preserving:
       (a) translate along the ring's OWN axis A, proximally, by shift_frac * ForeArm_length
           (grid-searched over {0, 0.03, 0.06, 0.09, 0.12}).
       (b) a single UNIFORM 3D scale about the (translated) ring centre -- scales every
           vertex's offset from the centre by the same factor in all three axes, so bead
           shape/proportions are preserved exactly (grid-searched over {1.00, 1.02, ..., 1.08}).
       (c) a translation perpendicular to A, solved analytically per (shift, scale) candidate:
           ray-cast the REST skin cross-section around the ring's new station (12+ directions
           in the e1/e2 plane through the shifted centre) against a Body BVH, take the hit
           centroid's offset from the centre, and cap its magnitude at 2 armature units.
     Vertex GROUP membership and deform WEIGHTS are never touched -- only vertex.co on the
     mesh's base (REST) data -- so every bracelet vertex stays exactly 100% ForeArm.<side>.
  3. Fit is measured against the REAL skin surface, not an idealised cylinder: BVHTree.find_nearest
     against the evaluated Yemoja_Body (built once per pose state, REST and POSE separately) gives
     a nearest point + face normal for each bracelet vertex; the signed distance (point - nearest)
     . normal is negative when the bracelet vertex is buried in flesh. Symmetrically, Body
     vertices near the ring are tested with find_nearest against a BVH of the (candidate-posed)
     bracelet faces; a positive signed distance there means skin has poked outside the ring.
  4. Candidates {shift_frac} x {scale} (25 combinations, same values tried for both sides
     together) are scored lexicographically: (i) zero skin-outside-ring vertices in REST: (ii)
     in POSE, <=3 skin-outside verts with depth < 0.6 AND <=3 bracelet-inside-skin verts with
     depth < 0.6, both sides; (iii) minimise scale; (iv) minimise shift_frac. The first
     candidate meeting (i)+(ii) with the smallest scale (ties broken by smallest shift) wins.
     If none meets (i)+(ii), the search reports the least-bad candidate instead of raising.

Usage:
    python3 fix_bracelet_fit.py <input.blend> <output.blend>

Or, from another script that already has a blend open:
    import importlib.util
    spec = importlib.util.spec_from_file_location("fbf", ".../fix_bracelet_fit.py")
    fbf = importlib.util.module_from_spec(spec); spec.loader.exec_module(fbf)
    report = fbf.apply()
"""

import bpy, sys, os, math, json, importlib.util
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

YM_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "yemoja_measure.py")
YM_PATH = os.path.normpath(YM_PATH)
CLOTHES = "Yemoja_Clothes"
BODY = "Yemoja_Body"
MARKER = "YEMOJA_BRACELET_FIX"       # custom property on Yemoja_Clothes recording an applied fix
MARKER_VERSION = 2                    # bump this if the fix logic changes again; apply() only
                                       # treats a stamp with this version as "already applied" --
                                       # a rev-1 stamp (no "version" key, or a lower one) is
                                       # ignored so the new logic can re-run over it.

DEFAULT_PARAMS = dict(
    shift_grid=[0.0, 0.03, 0.06, 0.09, 0.12],   # ForeArm-length fractions, along the ring's own axis
    scale_grid=[1.00, 1.02, 1.04, 1.06, 1.08],  # uniform 3D scale about the ring centre
    recenter_cap=2.0,        # armature units, cap on the analytic perpendicular re-centre offset
    n_recenter_dirs=24,      # ray directions used to find the REST skin cross-section centroid
    pose_skin_outside_max=3, pose_skin_outside_depth=0.6,
    pose_inside_max=3, pose_inside_depth=0.6,
    body_search_margin=8.0,  # armature units, extra radius around the ring for candidate Body verts
    near_distance_cutoff=2.0,  # armature units; a Body vertex only counts as "poking outside" the
                                # ring when its nearest bracelet surface is within this distance --
                                # the ring is an openwork mesh (32 separate bead/rod islands with
                                # real gaps between them), so nearest-face signed distance from a
                                # Body vertex that is simply nowhere near any bracelet face (it sits
                                # in a gap, or well past the ring's edge) is not a meaningful "poking
                                # through" reading; without this cutoff nearly every Body vertex in
                                # the search sphere reads as a false positive (median nearest-face
                                # distance ~19 units). Calibrated so the REST baseline (no fix
                                # applied) reproduces the audit's known pokes (~11-14/side).
    sides=("L", "R"),
)


def _load_ym():
    spec = importlib.util.spec_from_file_location("yemoja_measure", YM_PATH)
    ym = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ym)
    return ym


def _islands(cl, ids):
    """Connected components of the bracelet vertex-group ids, via mesh edges (bead/rod islands)."""
    idset = set(ids)
    adj = {vi: [] for vi in ids}
    for e in cl.data.edges:
        a, b = e.vertices[0], e.vertices[1]
        if a in idset and b in idset:
            adj[a].append(b)
            adj[b].append(a)
    seen = set()
    islands = []
    for vi in ids:
        if vi in seen:
            continue
        stack = [vi]
        comp = []
        seen.add(vi)
        while stack:
            u = stack.pop()
            comp.append(u)
            for w in adj[u]:
                if w not in seen:
                    seen.add(w)
                    stack.append(w)
        islands.append(comp)
    return islands


def _ring_frame(ym, cl, ids, Mc, bone_axis):
    """Ring's own centre/axis (island-centroid PCA) + a perpendicular (e1, e2) basis."""
    islands = _islands(cl, ids)
    orig_arm = {vi: Mc @ cl.data.vertices[vi].co for vi in ids}
    cents = [sum((orig_arm[vi] for vi in comp), Vector((0, 0, 0))) / len(comp) for comp in islands]
    C = sum(cents, Vector((0, 0, 0))) / len(cents)
    cov = np.zeros((3, 3))
    for c in cents:
        d = np.array([c.x - C.x, c.y - C.y, c.z - C.z])
        cov += np.outer(d, d)
    w, v = np.linalg.eigh(cov)
    A = Vector(v[:, 0])
    if A.dot(bone_axis) < 0:
        A = -A
    A.normalize()
    up = Vector((0, 1, 0)) if abs(A.dot(Vector((0, 1, 0)))) < 0.9 else Vector((0, 0, 1))
    e1 = (up - A * up.dot(A)).normalized()
    e2 = A.cross(e1)
    angle = math.degrees(A.angle(bone_axis))
    if angle > 90:
        angle = 180.0 - angle
    radial_extent = max((orig_arm[vi] - C - A * (orig_arm[vi] - C).dot(A)).length for vi in ids)
    axial_extent = max(abs((orig_arm[vi] - C).dot(A)) for vi in ids)
    return dict(islands=islands, n_islands=len(islands), orig_arm=orig_arm, C=C, A=A, e1=e1, e2=e2,
                angle_vs_bone_deg=round(angle, 2),
                pca_eigenvalues=[round(float(x), 1) for x in w],
                radial_extent=radial_extent, axial_extent=axial_extent)


def _recenter_offset(bd_rest, C1, A, e1, e2, n_dirs, cap):
    pts = []
    for j in range(n_dirs):
        th = 2 * math.pi * j / n_dirs
        d = (e1 * math.cos(th) + e2 * math.sin(th)).normalized()
        co, normal, idx, dist = bd_rest.ray_cast(C1 + d * 0.01, d, 200.0)
        if co is not None:
            pts.append(co)
    if not pts:
        return Vector((0.0, 0.0, 0.0))
    cen = sum(pts, Vector((0, 0, 0))) / len(pts)
    off = cen - C1
    off = off - A * off.dot(A)   # project out any axial component (should be ~0 already)
    if off.length > cap:
        off = off * (cap / off.length)
    return off


def _signed(bvh, point):
    co, normal, idx, dist = bvh.find_nearest(point)
    if co is None:
        return None
    return (point - co).dot(normal)


def _candidate_positions(rd, shift_amt, scale, rc):
    C, A = rd['C'], rd['A']
    C1 = C - A * shift_amt
    out = {}
    for vi, p0 in rd['orig_arm'].items():
        d0 = p0 - C
        out[vi] = C1 + d0 * scale + rc
    return out, C1


def _write_positions(cl, Minv, positions):
    for vi, p in positions.items():
        cl.data.vertices[vi].co = Minv @ p


def _body_candidate_ids(ym, C, radial_extent, axial_extent, shift_max_units, margin):
    body = bpy.data.objects[BODY]
    Mb = ym._to_arm(body)
    R = radial_extent + axial_extent + shift_max_units + margin
    R2 = R * R
    ids = []
    for v in body.data.vertices:
        p = Mb @ v.co
        if (p - C).length_squared <= R2:
            ids.append(v.index)
    return ids


def _measure(ym, cl, ids_by_side, body_bvh, body_ids_arm, bracelet_faces_all_arm, near_cutoff):
    """One pose state's worth of skin-outside / bracelet-inside metrics, both sides.

    body_bvh: BVHTree of the evaluated Body in the current pose state.
    body_ids_arm: {index: Vector} evaluated Body vertex positions (armature space, current pose),
                  restricted to each side's candidate set.
    bracelet_faces_all_arm: (verts, faces_by_side) evaluated Clothes mesh + per-side bracelet
                             face-index lists, current pose state.
    """
    cv, cf = bracelet_faces_all_arm
    out = {}
    for side, ids in ids_by_side.items():
        idset = set(ids)
        bfaces = [cf[p.index] for p in cl.data.polygons if all(vi in idset for vi in p.vertices)]
        bvh_bracelet = BVHTree.FromPolygons([tuple(v) for v in cv], bfaces,
                                             all_triangles=False, epsilon=0.0)
        # bracelet vertex -> skin (buried if signed < 0)
        inside = []
        for vi in ids:
            sd = _signed(body_bvh, cv[vi])
            if sd is not None and sd < -1e-6:
                inside.append((vi, -sd))
        inside.sort(key=lambda t: -t[1])
        # skin vertex -> bracelet (poking outside if signed > 0), only counted when the
        # Body vertex is actually near a bracelet face (see near_distance_cutoff docstring)
        outside = []
        for vi, p in body_ids_arm[side].items():
            co, normal, idx, dist = bvh_bracelet.find_nearest(p)
            if co is None or dist > near_cutoff:
                continue
            sd = (p - co).dot(normal)
            if sd > 1e-6:
                outside.append((vi, sd))
        outside.sort(key=lambda t: -t[1])
        out[side] = dict(
            n_outside=len(outside), max_outside_depth=outside[0][1] if outside else 0.0,
            worst_outside=[(vi, round(d, 3)) for vi, d in outside[:6]],
            n_inside=len(inside), max_inside_depth=inside[0][1] if inside else 0.0,
            worst_inside=[(vi, round(d, 3)) for vi, d in inside[:6]],
        )
    return out


def _score(rest_m, pose_m, scale, shift, p):
    """Lower is better; (0, ..., scale, shift) = fully meets the objective."""
    rest_fail = sum(rest_m[s]['n_outside'] for s in rest_m)
    pose_fail = 0
    for s in pose_m:
        m = pose_m[s]
        bad_out = m['n_outside'] > p['pose_skin_outside_max'] or \
            (m['n_outside'] > 0 and m['max_outside_depth'] >= p['pose_skin_outside_depth'])
        bad_in = m['n_inside'] > p['pose_inside_max'] or \
            (m['n_inside'] > 0 and m['max_inside_depth'] >= p['pose_inside_depth'])
        over_out = max(0, m['n_outside'] - p['pose_skin_outside_max']) + max(0.0, m['max_outside_depth'] - p['pose_skin_outside_depth'])
        over_in = max(0, m['n_inside'] - p['pose_inside_max']) + max(0.0, m['max_inside_depth'] - p['pose_inside_depth'])
        pose_fail += (over_out if bad_out else 0) + (over_in if bad_in else 0)
    passed = (rest_fail == 0 and pose_fail == 0)
    return (0 if passed else 1, rest_fail + pose_fail, scale, shift), passed


def apply(params=None, verbose=False):
    """Apply the rigid bracelet fix to the currently-open blend. Returns a report dict.

    Idempotent: if Yemoja_Clothes already carries a MARKER stamp at MARKER_VERSION, this call
    is a no-op (returns the stored report). A stamp from an older revision does not block --
    the new logic re-runs and overwrites it.
    """
    p = dict(DEFAULT_PARAMS)
    if params:
        p.update(params)

    cl = bpy.data.objects[CLOTHES]
    if MARKER in cl:
        try:
            stored = json.loads(cl[MARKER])
        except Exception:
            stored = {}
        if stored.get("marker_version") == MARKER_VERSION:
            return dict(stored, skipped=True,
                        note="fix already applied at MARKER_VERSION %d -- no-op" % MARKER_VERSION)

    ym = _load_ym()
    prev_pose = ym.armature().data.pose_position
    ym.set_pose_state(rest=True)

    Mc = ym._to_arm(cl)
    Minv = Mc.inverted()

    # -- per-side ring frame + candidate Body id set (fixed for the whole search) --
    sides_ids = {}
    rd_by_side = {}
    body_ids_by_side = {}
    shift_max_units = {}
    for side in p["sides"]:
        ids = ym.read_group(CLOTHES, "CL_Bracelet_" + side)
        sides_ids[side] = ids
        o, ax, L = ym.bone_frame_rest("ForeArm." + side)
        rd = _ring_frame(ym, cl, ids, Mc, ax)
        rd["L"] = L
        rd_by_side[side] = rd
        shift_max_units[side] = max(p["shift_grid"]) * L
        body_ids_by_side[side] = _body_candidate_ids(
            ym, rd["C"], rd["radial_extent"], rd["axial_extent"],
            shift_max_units[side], p["body_search_margin"])

    # -- weight sanity before any edit --
    bone_names = ym.bone_names()
    for side in p["sides"]:
        bone = "mixamorig:ForeArm." + side
        for vi in sides_ids[side]:
            v = cl.data.vertices[vi]
            w = {cl.vertex_groups[ge.group].name: ge.weight for ge in v.groups
                 if cl.vertex_groups[ge.group].name in bone_names}
            if not (len(w) == 1 and bone in w and w[bone] > 0.999):
                raise RuntimeError("bracelet vert %d not 100%% %s before edit: %r" % (vi, bone, w))

    orig_local = {side: {vi: cl.data.vertices[vi].co.copy() for vi in sides_ids[side]}
                  for side in p["sides"]}

    # -- Body BVHs + candidate positions, once per pose state (independent of the candidate) --
    ym.set_pose_state(rest=True)
    bv_r, bf_r = ym._eval_mesh_arm(BODY)
    body_bvh_rest = BVHTree.FromPolygons([tuple(v) for v in bv_r], bf_r, all_triangles=False, epsilon=0.0)
    body_arm_rest = {side: {vi: bv_r[vi] for vi in body_ids_by_side[side]} for side in p["sides"]}

    ym.set_pose_state(rest=False)
    bv_p, bf_p = ym._eval_mesh_arm(BODY)
    body_bvh_pose = BVHTree.FromPolygons([tuple(v) for v in bv_p], bf_p, all_triangles=False, epsilon=0.0)
    body_arm_pose = {side: {vi: bv_p[vi] for vi in body_ids_by_side[side]} for side in p["sides"]}
    ym.set_pose_state(rest=True)

    # -- grid search --
    trials = []
    for shift in p["shift_grid"]:
        for scale in p["scale_grid"]:
            rc_by_side = {}
            newpos_by_side = {}
            for side in p["sides"]:
                rd = rd_by_side[side]
                shift_amt = shift * rd["L"]
                C1 = rd["C"] - rd["A"] * shift_amt
                rc = _recenter_offset(body_bvh_rest, C1, rd["A"], rd["e1"], rd["e2"],
                                       p["n_recenter_dirs"], p["recenter_cap"])
                rc_by_side[side] = rc
                newpos, _ = _candidate_positions(rd, shift_amt, scale, rc)
                newpos_by_side[side] = newpos
                _write_positions(cl, Minv, newpos)
            cl.data.update()
            bpy.context.view_layer.update()

            ym.set_pose_state(rest=True)
            cv_r, cf_r = ym._eval_mesh_arm(CLOTHES)
            rest_m = _measure(ym, cl, sides_ids, body_bvh_rest, body_arm_rest, (cv_r, cf_r),
                               p["near_distance_cutoff"])

            ym.set_pose_state(rest=False)
            cv_p, cf_p = ym._eval_mesh_arm(CLOTHES)
            pose_m = _measure(ym, cl, sides_ids, body_bvh_pose, body_arm_pose, (cv_p, cf_p),
                               p["near_distance_cutoff"])
            ym.set_pose_state(rest=True)

            key, passed = _score(rest_m, pose_m, scale, shift, p)
            trials.append(dict(shift=shift, scale=scale, key=key, passed=passed,
                                rc=rc_by_side, rest=rest_m, pose=pose_m))
            if verbose:
                print("shift=%.2f scale=%.2f -> key=%s" % (shift, scale, key))

            # restore for the next candidate
            for side in p["sides"]:
                for vi, co in orig_local[side].items():
                    cl.data.vertices[vi].co = co
            cl.data.update()
            bpy.context.view_layer.update()

    trials.sort(key=lambda t: t["key"])
    best = trials[0]

    # -- commit the winning candidate --
    for side in p["sides"]:
        rd = rd_by_side[side]
        shift_amt = best["shift"] * rd["L"]
        newpos, C1 = _candidate_positions(rd, shift_amt, best["scale"], best["rc"][side])
        _write_positions(cl, Minv, newpos)
    cl.data.update()
    bpy.context.view_layer.update()

    # -- weight sanity after edit (we only ever touched vertex.co) --
    for side in p["sides"]:
        bone = "mixamorig:ForeArm." + side
        for vi in sides_ids[side]:
            v = cl.data.vertices[vi]
            w = {cl.vertex_groups[ge.group].name: ge.weight for ge in v.groups
                 if cl.vertex_groups[ge.group].name in bone_names}
            assert len(w) == 1 and bone in w and w[bone] > 0.999, (vi, w)

    report = dict(marker_version=MARKER_VERSION,
                  params={k: v for k, v in p.items()},
                  chosen=dict(shift_frac=best["shift"], scale=best["scale"], passed=best["passed"]),
                  sides={})
    for side in p["sides"]:
        rd = rd_by_side[side]
        shift_amt = best["shift"] * rd["L"]
        report["sides"][side] = dict(
            n_bracelet_verts=len(sides_ids[side]), n_islands=rd["n_islands"],
            ring_axis_vs_bone_axis_deg=rd["angle_vs_bone_deg"],
            ring_plane_pca_eigenvalues=rd["pca_eigenvalues"],
            bone_len=round(rd["L"], 3),
            translate_bone_frac=best["shift"], translate_armature_units=round(shift_amt, 3),
            scale=best["scale"],
            recenter_offset_armature_units=[round(x, 3) for x in best["rc"][side]],
            recenter_offset_magnitude=round(best["rc"][side].length, 3),
            rest_fit=best["rest"][side], pose_fit=best["pose"][side],
        )
    report["n_trials"] = len(trials)
    report["all_trials_summary"] = [
        dict(shift=t["shift"], scale=t["scale"], passed=t["passed"],
             rest_outside=sum(t["rest"][s]["n_outside"] for s in t["rest"]),
             pose_outside=sum(t["pose"][s]["n_outside"] for s in t["pose"]),
             pose_inside=sum(t["pose"][s]["n_inside"] for s in t["pose"]))
        for t in trials]

    cl[MARKER] = json.dumps(report)
    ym.set_pose_state(rest=(prev_pose == 'REST'))
    return report


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--" in args:
        args = args[args.index("--") + 1:]
    verbose = "-v" in args
    args = [a for a in args if a != "-v"]
    in_path = args[0] if len(args) > 0 else "/home/claude/yemoja/v115_sh_tw.blend"
    out_path = args[1] if len(args) > 1 else "/home/claude/yemoja/fix_bracelet/v115_bracelet.blend"

    bpy.ops.wm.open_mainfile(filepath=in_path)
    rep = apply(verbose=verbose)
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out_path)
    print(json.dumps(rep, indent=1))
