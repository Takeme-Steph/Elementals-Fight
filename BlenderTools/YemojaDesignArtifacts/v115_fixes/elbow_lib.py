"""Elbow loop cuts + optional Arm/ForeArm ramp widening on Yemoja_Body. Pure bpy/bmesh, no ops needing a view."""
import bpy, bmesh, math
from mathutils import Vector

BODY = "Yemoja_Body"

def _frame(side):
    A = bpy.data.objects["Armature"]; AW = A.matrix_world
    b1 = A.data.bones["mixamorig:Arm." + side]; b2 = A.data.bones["mixamorig:ForeArm." + side]
    C = AW @ b2.head_local; axis = ((AW @ b2.tail_local) - (AW @ b1.head_local)).normalized()
    return C, axis

def _ring_walk(e0):
    ring = [e0]; seen = {e0}
    faces = list(e0.link_faces)
    if len(faces) != 2: return None
    for f0 in faces:
        e = e0; f = f0
        while True:
            if len(f.verts) != 4: return None
            opp = [x for x in f.edges if not (set(x.verts) & set(e.verts))][0]
            if opp in seen:
                if opp is e0: return ring
                return None
            ring.append(opp); seen.add(opp)
            nf = [x for x in opp.link_faces if x is not f]
            if len(nf) != 1: return None
            e = opp; f = nf[0]

def find_rings(bm, side, MW):
    """closed quad edge-rings running along the arm across the elbow; each as dict(lo, hi, n, edges)."""
    C, axis = _frame(side)
    def tv(v): return (MW @ v.co - C).dot(axis)
    def rv(v): d = MW @ v.co - C; return (d - axis * d.dot(axis)).length
    cand = [e for e in bm.edges if all(rv(v) < 0.4 and abs(tv(v)) < 0.6 for v in e.verts)
            and abs((MW @ e.verts[0].co - MW @ e.verts[1].co).normalized().dot(axis)) > 0.5]
    rings = {}
    for e in cand:
        r = _ring_walk(e)
        if r is None: continue
        key = frozenset(x.index for x in r)
        if key in rings: continue
        los = [min(tv(v) for v in x.verts) for x in r]; his = [max(tv(v) for v in x.verts) for x in r]
        rings[key] = dict(n=len(r), lo=sum(los) / len(los), hi=sum(his) / len(his), edges=list(r))
    return sorted(rings.values(), key=lambda d: d["lo"])

def cut_elbows(sides=("L", "R"), strips=((-0.25, -0.05), (0.05, 0.25)), cuts=1):
    """Loop-cut every closed ring whose [lo,hi] lies inside one of the given t-windows (t along the
    arm axis, 0 at the elbow). Returns log with per-side ring info and new vertex count."""
    ob = bpy.data.objects[BODY]; m = ob.data; MW = ob.matrix_world
    bm = bmesh.new(); bm.from_mesh(m)
    bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    n0 = len(bm.verts); f0 = len(bm.faces)
    todo = []; log = {}
    for s in sides:
        rings = find_rings(bm, s, MW)
        picked = [r for r in rings if any(lo <= r["lo"] and r["hi"] <= hi for lo, hi in strips)]
        log[s] = [dict(n=r["n"], lo=round(r["lo"], 3), hi=round(r["hi"], 3)) for r in picked]
        for r in picked: todo.extend(r["edges"])
    if todo:
        bmesh.ops.subdivide_edges(bm, edges=todo, cuts=cuts, use_grid_fill=True)
    bm.to_mesh(m); bm.free(); m.update()
    log["verts"] = (n0, len(m.vertices)); log["faces"] = (f0, len(m.polygons))
    log["ngons_after"] = sum(1 for p in m.polygons if len(p.vertices) > 4)
    return log

def smoothstep(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

def widen_ramp(side, half, profile="smoothstep"):
    """Rewrite the Arm<->ForeArm handover on the arm tube as a smoothstep of half-width `half`
    (t along the arm axis, 0 at the elbow). Only vertices whose Arm+ForeArm weight is >0.98 are
    touched, so nothing else in the mesh moves."""
    ob = bpy.data.objects[BODY]; m = ob.data; MW = ob.matrix_world
    C, axis = _frame(side)
    ga = ob.vertex_groups["mixamorig:Arm." + side]; gf = ob.vertex_groups["mixamorig:ForeArm." + side]
    n = 0
    for v in m.vertices:
        d = MW @ v.co - C; t = d.dot(axis); r = (d - axis * t).length
        if r > 0.45 or abs(t) > half + 0.3: continue
        wa = wf = 0.0
        for g in v.groups:
            if g.group == ga.index: wa = g.weight
            elif g.group == gf.index: wf = g.weight
        tot = wa + wf
        if tot < 0.98: continue
        s = smoothstep((t + half) / (2 * half))
        ga.add([v.index], tot * (1 - s), 'REPLACE'); gf.add([v.index], tot * s, 'REPLACE')
        n += 1
    return n

def elbow_metrics(side, twin=0.35):
    """On the CURRENT pose: face-area ratios and dihedral angles for faces near the elbow."""
    ob = bpy.data.objects[BODY]; m = ob.data; MW = ob.matrix_world
    C, axis = _frame(side)
    dg = bpy.context.evaluated_depsgraph_get()
    me_p = ob.evaluated_get(dg).to_mesh()
    faces = []
    for p in m.polygons:
        c = MW @ p.center; d = c - C; t = d.dot(axis); r = (d - axis * t).length
        if abs(t) < twin and r < 0.45: faces.append(p.index)
    fs = set(faces)
    ratios = []
    for i in faces:
        ar = m.polygons[i].area; ap = me_p.polygons[i].area
        if ar > 1e-12: ratios.append(ap / ar)
    ratios.sort()
    # dihedrals on posed mesh for edges with both faces in region
    ek = {}
    for p in me_p.polygons:
        if p.index not in fs: continue
        for k in p.edge_keys: ek.setdefault(k, []).append(p.index)
    dih = []
    for k, fl in ek.items():
        if len(fl) == 2:
            n1 = me_p.polygons[fl[0]].normal; n2 = me_p.polygons[fl[1]].normal
            dih.append(math.degrees(n1.angle(n2)) if n1.length and n2.length else 0.0)
    dih.sort()
    ob.evaluated_get(dg).to_mesh_clear()
    return dict(n_faces=len(faces), area_ratio=round(sum(ratios) / len(ratios), 4), min_ratio=round(ratios[0], 3),
                crushed_lt0p5=sum(1 for r in ratios if r < 0.5), p05=round(ratios[int(0.05 * (len(ratios) - 1))], 3),
                dihedral_max=round(dih[-1], 1), dihedral_p95=round(dih[int(0.95 * (len(dih) - 1))], 1),
                dihedral_p90=round(dih[int(0.90 * (len(dih) - 1))], 1), n_edges=len(dih))
