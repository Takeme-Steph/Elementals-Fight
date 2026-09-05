"""
SPEC_fix_v5.md's Gate item 1: "import and call the function from verify/pen.py,
do not re-implement it." This file is BOTH the original standalone verify
script (its own per-key report, run directly: `python3 pen.py`) AND, as of
v5, an importable module -- everything through nearest_bone() plus the new
trident_shaft_runs()/clothes_inside_count() wrapper functions below are
plain module-level defs with no side effects; the ORIGINAL script body
(which used to run unconditionally on import -- opening a hardcoded blend
file and clobbering whatever scene the importer already had loaded) is now
gated behind `if __name__ == "__main__":` so `import pen` from
attacks_build.py's final_gate() is safe and reuses this exact geometry code
against the CALLER's own already-open scene instead.
"""
import bpy, sys, math, importlib.util
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def build(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh(); mw=ev.matrix_world
    vs=[mw@v.co for v in me.vertices]; ps=[list(p.vertices) for p in me.polygons]; ev.to_mesh_clear()
    return BVHTree.FromPolygons(vs,ps,all_triangles=False), vs
def inside(bvh,p,d=Vector((1,0,0))):
    n=0; o=p.copy()
    for _ in range(80):
        h=bvh.ray_cast(o+d*1e-4,d)
        if h[0] is None: break
        n+=1; o=h[0]
    return n%2==1
def robust_inside(bvh,p):
    dirs=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)),Vector((0.577,0.577,0.577)),Vector((-0.577,0.577,-0.577))]
    v=sum(1 for d in dirs if inside(bvh,p,d))
    return v>=3
def _vdom(BODY, PFX):
    gn={g.index:g.name for g in BODY.vertex_groups}
    out=[]
    for v in BODY.data.vertices:
        s={}
        for ge in v.groups:
            n=gn[ge.group]
            if n.startswith(PFX): s[n]=s.get(n,0.)+ge.weight
        out.append(max(s,key=s.get) if s else None)
    return out


def nearest_bone(vs,p,vdom,PFX="mixamorig:"):
    bi=min(range(len(vs)),key=lambda i:(vs[i]-p).length_squared)
    return (vdom[bi] or "?")[len(PFX):], (vs[bi]-p).length


def trident_shaft_runs(L, BODY, n_samples=401):
    """SPEC_fix_v5.md Gate item 1's own function, made importable: the
    signed BVH inside/outside test along the trident shaft, at whatever
    frame/pose is CURRENT on the caller's already-open scene (caller is
    responsible for frame_set + L.attach_trident() + view_layer.update()
    first, same contract the original script's own per-key loop followed).
    Returns [(s0,s1,nearest_bone_name,nearest_dist), ...], one tuple per
    contiguous run of the shaft parameter s in [0,1] testing INSIDE the
    evaluated Yemoja_Body mesh. Identical algorithm to the original script
    (robust_inside, 5-direction ray parity) -- not re-implemented, only
    wrapped so it can run per-frame from outside this file."""
    PFX = getattr(L, "PFX", "mixamorig:")
    vdom = _vdom(BODY, PFX)
    bvh, vs = build(BODY)
    b, t = L.trident_ends()
    runs = []; cur = None
    for i in range(n_samples):
        s = i/float(n_samples-1)
        p = b+(t-b)*s
        ins = robust_inside(bvh, p)
        if ins and cur is None: cur=[s,s]
        elif ins: cur[1]=s
        elif cur is not None: runs.append(cur); cur=None
    if cur: runs.append(cur)
    out=[]
    for r in runs:
        mid = b+(t-b)*((r[0]+r[1])/2)
        nb, d = nearest_bone(vs, mid, vdom, PFX)
        out.append((r[0], r[1], nb, d))
    return out


def clothes_inside_count(BODY, CLOTH):
    """Same single-ray clothes-vs-body test the original script ran inline
    at every reported key, made importable. Caller must have already
    evaluated the pose for this frame (frame_set + view_layer.update())."""
    bvh, _ = build(BODY)
    dg = bpy.context.evaluated_depsgraph_get()
    evc = CLOTH.evaluated_get(dg); mec = evc.to_mesh(); mwc = evc.matrix_world
    n = 0
    for v in mec.vertices:
        if inside(bvh, mwc @ v.co): n += 1
    evc.to_mesh_clear()
    return n


if __name__ == "__main__":
    sys.path.insert(0, "/tmp/vf")
    bpy.ops.wm.open_mainfile(filepath="/tmp/vf/Yemoja_WORKING_v115_attacks.blend")
    spec = importlib.util.spec_from_file_location("yemoja_anim_lib", "/tmp/vf/yemoja_anim_lib.py")
    L = importlib.util.module_from_spec(spec); spec.loader.exec_module(L); sys.modules["yemoja_anim_lib"] = L
    PFX = "mixamorig:"; A = L.armature()
    BODY = bpy.data.objects["Yemoja_Body"]; CLOTH = bpy.data.objects["Yemoja_Clothes"]

    def bw(n, w="head"):
        pb = A.pose.bones[L.full(n)]; return A.matrix_world @ (pb.head if w == "head" else pb.tail)

    for name, kfs in (("Yemoja_Atk_HardKick", [1, 6, 12, 16, 22]), ("Yemoja_Atk_HardPunch", [7, 12, 15]),
                       ("Yemoja_Atk_Punch", [7]), ("Yemoja_Atk_Kick", [7])):
        A.animation_data.action = bpy.data.actions[name]
        for f in kfs:
            bpy.context.scene.frame_set(f); L.attach_trident(); bpy.context.view_layer.update()
            runs = trident_shaft_runs(L, BODY, n_samples=401)
            desc = ["%.2f-%.2f(%s,%.1fcm-deep-ish nearest surf %.3f)" % (r[0], r[1], r[2], 0, r[3]) for r in runs]
            print("%s f%-3d shaft-inside-body runs: %s" % (name, f, desc if desc else "none"))
            if name == "Yemoja_Atk_HardKick":
                legdir = (bw("Foot.R") - bw("Leg.R")).normalized()
                toetail = bw("ToeBase.R", "tail")
                v1 = (toetail - bw("Leg.R")).normalized()
                v2 = (bw("ToeBase.R", "tail") - bw("Foot.R")).normalized()
                print("    toe: (Leg.R head->ToeBase tail) vs shin %.1f deg ; (ankle->toe tail) vs shin %.1f deg" % (
                    math.degrees(math.acos(max(-1, min(1, v1.dot(legdir))))),
                    math.degrees(math.acos(max(-1, min(1, v2.dot(legdir)))))))
            n = clothes_inside_count(BODY, CLOTH)
            print("    clothes verts inside body: %d" % n)
