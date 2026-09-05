"""v115 mesh fix: elbow loop cuts on Yemoja_Body + pin the CL_GroinFlap leaf to Hips.

    from elbow_and_flap import apply
    apply()

Idempotent via Armature["YEMOJA_MESH_V2"] == "elbow_cuts2+flap_hips".
Elbow: loop-cuts the two coarse 12-vert rings either side of each elbow (t in [-0.25,-0.05] and
[0.05,0.25] along the arm axis), interpolating weights and UVs. +48 verts, no new n-gons.
Flap: every vertex in the CL_GroinFlap tag group gets Hips = 1.0 and loses every other deform
bone, so the leaf hangs from the pelvis and no longer stretches between the thighs.
"""
import bpy, os, sys, importlib.util
HERE = os.path.dirname(os.path.abspath(__file__))
STAMP = "YEMOJA_MESH_V2"; TAG = "elbow_cuts2+flap_hips"

def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m

def pin_flap(objname="Yemoja_Clothes", tag="CL_GroinFlap", bone="mixamorig:Hips"):
    ob = bpy.data.objects[objname]; m = ob.data
    gi = ob.vertex_groups[tag].index
    ids = [v.index for v in m.vertices if any(g.group == gi for g in v.groups)]
    hips = ob.vertex_groups[bone]
    removed = {}
    for vg in ob.vertex_groups:
        if vg.name.startswith("CL_") or vg.index == hips.index: continue
        n = 0
        for i in ids:
            if any(g.group == vg.index for g in m.vertices[i].groups):
                vg.remove([i]); n += 1
        if n: removed[vg.name] = n
    hips.add(ids, 1.0, 'REPLACE')
    return dict(n_verts=len(ids), removed=removed)

def apply():
    A = bpy.data.objects["Armature"]
    log = {"file": bpy.data.filepath}
    if A.get(STAMP) == TAG:
        log["already_applied"] = True; return log
    EL = _load("elbow_lib")
    log["elbow"] = EL.cut_elbows(strips=((-0.25, -0.05), (0.05, 0.25)))
    log["flap"] = pin_flap()
    A[STAMP] = TAG
    return log

def verify():
    EL = _load("elbow_lib"); ym = _load("yemoja_measure")
    out = {"elbow_metrics": {s: EL.elbow_metrics(s) for s in ("L", "R")},
           "ramp": {s: {k: v for k, v in ym.ramp_profile("Arm." + s, "ForeArm." + s).items()
                        if k in ("n_verts", "monotonic", "w50_crossing_childaxis", "mean_std_of_bins", "max_bin_std", "verts_sum_off_1pct", "sum_min", "sum_max")} for s in ("L", "R")}}
    ob = bpy.data.objects["Yemoja_Clothes"]; m = ob.data
    gi = ob.vertex_groups["CL_GroinFlap"].index; names = {g.index: g.name for g in ob.vertex_groups}
    bad = 0; groups = set()
    for v in m.vertices:
        if not any(g.group == gi for g in v.groups): continue
        d = {names[g.group]: g.weight for g in v.groups if not names[g.group].startswith("CL_")}
        groups |= set(d)
        if abs(d.get("mixamorig:Hips", 0) - 1.0) > 1e-6 or len(d) != 1: bad += 1
    out["flap"] = dict(deform_groups=sorted(groups), verts_not_pure_hips=bad)
    return out
