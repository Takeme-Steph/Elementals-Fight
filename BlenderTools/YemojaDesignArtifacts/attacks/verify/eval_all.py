import bpy, sys, json, math, importlib.util, os
sys.path.insert(0,"/tmp/vf")
from mathutils import Vector, Matrix, Quaternion

BLEND="/tmp/vf/Yemoja_WORKING_v115_attacks.blend"
bpy.ops.wm.open_mainfile(filepath=BLEND)
spec=importlib.util.spec_from_file_location("yemoja_anim_lib","/tmp/vf/yemoja_anim_lib.py")
L=importlib.util.module_from_spec(spec); spec.loader.exec_module(L); sys.modules["yemoja_anim_lib"]=L
PFX="mixamorig:"
A=L.armature()
BODY=bpy.data.objects["Yemoja_Body"]
CLOTH=bpy.data.objects["Yemoja_Clothes"]
print("body verts",len(BODY.data.vertices),"polys",len(BODY.data.polygons))

HUMAN=[pb.name for pb in A.pose.bones if pb.name.startswith(PFX) and not pb.name.endswith("_end") and not pb.name.startswith(PFX+"Eye")]

# ---------- dominant bone per face/vertex at rest ----------
def dominant(ob):
    gn={g.index:g.name for g in ob.vertex_groups}
    vdom=[]
    for v in ob.data.vertices:
        s={}
        for ge in v.groups:
            n=gn[ge.group]
            if n.startswith(PFX): s[n]=s.get(n,0.0)+ge.weight
        vdom.append(max(s,key=s.get) if s else None)
    fdom=[]
    for p in ob.data.polygons:
        s={}
        for vi in p.vertices:
            d=vdom[vi]
            if d: s[d]=s.get(d,0.0)+1.0
        # weight-sum version (matches harness): recompute properly
        fdom.append(None)
    # proper: sum of weights over face verts
    gn2=gn
    vw=[]
    for v in ob.data.vertices:
        vw.append([(gn2[ge.group],ge.weight) for ge in v.groups if gn2[ge.group].startswith(PFX)])
    fdom=[]
    for p in ob.data.polygons:
        s={}
        for vi in p.vertices:
            for bn,w in vw[vi]: s[bn]=s.get(bn,0.0)+w
        fdom.append(max(s,key=s.get) if s else None)
    return vdom,fdom

# rest pose caches
saved={pb.name:pb.matrix_basis.copy() for pb in A.pose.bones}
for pb in A.pose.bones: pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
BODY_VDOM,BODY_FDOM=dominant(BODY)
CLOTH_VDOM,CLOTH_FDOM=dominant(CLOTH)
def face_areas(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh()
    a=[p.area for p in me.polygons]; ev.to_mesh_clear(); return a
BODY_REST=face_areas(BODY); CLOTH_REST=face_areas(CLOTH)
for n,m in saved.items(): A.pose.bones[n].matrix_basis=m
bpy.context.view_layer.update()

def ev_verts(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh(); mw=ev.matrix_world
    out=[(mw@v.co) for v in me.vertices]; ev.to_mesh_clear(); return out

def area_audit(ob,fdom,rest):
    a=face_areas(ob)
    per={}
    for bn,ra,pa in zip(fdom,rest,a):
        if bn is None: continue
        e=per.setdefault(bn,[0.0,0.0,0,0]); e[0]+=ra; e[1]+=pa; e[2]+=1
        if ra>1e-9 and pa/ra<0.5: e[3]+=1
    return {bn[len(PFX):]:(pa/ra if ra>1e-9 else 1.0, cr, n) for bn,(ra,pa,n,cr) in per.items()}

# ---------- swing/twist ----------
def swing_twist(q, axis=Vector((0,1,0))):
    v=Vector((q.x,q.y,q.z))
    p=v.dot(axis)*axis
    tw=Quaternion((q.w,p.x,p.y,p.z))
    if tw.magnitude<1e-9: tw=Quaternion((1,0,0,0))
    else: tw.normalize()
    ang=2*math.atan2(p.dot(axis), q.w)
    # normalise to (-180,180]
    ang=math.degrees(ang)
    while ang>180: ang-=360
    while ang<=-180: ang+=360
    sw=q @ tw.conjugated()
    return math.degrees(sw.angle) if sw.angle else 0.0, ang

def twist_common(name):
    q=A.pose.bones[L.full(name)].matrix_basis.to_quaternion()
    return math.degrees(2*math.atan2(q.y,q.w))

def twist_st(name):
    q=A.pose.bones[L.full(name)].matrix_basis.to_quaternion()
    return swing_twist(q)[1]

def bone_w(name,which="head"):
    pb=A.pose.bones[L.full(name)]
    return A.matrix_world@(pb.head if which=="head" else pb.tail)

# ---------- idle reference ----------
import json as _j
IDLEJSON="/mnt/user-data/uploads/Elementals-Fight/BlenderTools/YemojaDesignArtifacts/pose_idle_master_2026-09-03_v114clean.json"
def apply_idle():
    d=_j.load(open(IDLEJSON))
    for n,v in d.items():
        pb=A.pose.bones.get(n)
        if not pb or n.startswith("hair_"): continue
        M=Quaternion(v["q"]).to_matrix().to_4x4(); M.translation=Vector(v["loc"]); pb.matrix_basis=M
    bpy.context.view_layer.update()

# Idle from the ACTION Yemoja_Idle_MASTER at frame 1 (spec item 3 says compare to that action)
A.animation_data.action=bpy.data.actions["Yemoja_Idle_MASTER"]
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
IDLE_BASIS={pb.name:pb.matrix_basis.copy() for pb in A.pose.bones}
IDLE_ANKLE={s:bone_w("Foot."+s) for s in ("L","R")}
IDLE_FOOTM={s:(A.matrix_world@A.pose.bones[L.full("Foot."+s)].matrix).copy() for s in ("L","R")}
IDLE_LOWZ=min(v.z for v in ev_verts(BODY))
IDLE_AUDIT_BODY=area_audit(BODY,BODY_FDOM,BODY_REST)
IDLE_AUDIT_CLOTH=area_audit(CLOTH,CLOTH_FDOM,CLOTH_REST)
IDLE_TWIST={n:(twist_common(n),twist_st(n)) for n in ("Hand.L","Hand.R","Foot.L","Foot.R")}
print("IDLE lowest z %.6f"%IDLE_LOWZ)
print("IDLE ankles", {k:[round(c,5) for c in v] for k,v in IDLE_ANKLE.items()})
print("IDLE twist", IDLE_TWIST)
# also compare json idle vs action idle
apply_idle()
mx=0; worstb=None
for pb in A.pose.bones:
    d=IDLE_BASIS[pb.name].to_quaternion().rotation_difference(pb.matrix_basis.to_quaternion())
    if math.degrees(abs(d.angle))>mx: mx=math.degrees(abs(d.angle)); worstb=pb.name
print("JSON idle vs Yemoja_Idle_MASTER f1: max %.5f deg (%s)"%(mx,worstb))

# ---------- per-frame ----------
CLIPS={"Yemoja_Atk_Punch":(1,14,[1,4,7,9,14],["R","L"],None),
       "Yemoja_Atk_HardPunch":(1,26,[1,7,12,15,26],["R","L"],None),
       "Yemoja_Atk_Kick":(1,16,[1,4,7,9,16],["R"],["Foot.L","ToeBase.L","ToeBase.L_end"]),
       "Yemoja_Atk_HardKick":(1,28,[1,6,12,16,22,28],["L"],["Foot.R","ToeBase.R","ToeBase.R_end"])}

def excl_vidx(fdom,names):
    ef=set(L.full(n) for n in names)
    s=set()
    for p,bn in zip(BODY.data.polygons,fdom):
        if bn in ef: s.update(p.vertices)
    return s

results={}
for name,(f0,f1,keyf,support,kick_ex) in CLIPS.items():
    act=bpy.data.actions[name]
    A.animation_data.action=act
    ex=excl_vidx(BODY_FDOM,kick_ex) if kick_ex else set()
    rows=[]
    for f in range(f0,f1+1):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        vs=ev_verts(BODY)
        lowz=min(v.z for v in vs)
        lowz_ex=min(v.z for i,v in enumerate(vs) if i not in ex) if ex else lowz
        # which vertex / dominant bone is lowest (excluded set applied)
        li=min((i for i in range(len(vs)) if i not in ex), key=lambda i: vs[i].z)
        lowbone=BODY_VDOM[li]
        r={"f":f,"lowz":lowz,"lowz_ex":lowz_ex,"lowbone":(lowbone or "")[len(PFX):]}
        for s in support:
            r["ankle_"+s]=(bone_w("Foot."+s)-IDLE_ANKLE[s]).length
        for s in ("L","R"):
            r["ank_all_"+s]=(bone_w("Foot."+s)-IDLE_ANKLE[s]).length
        for n in ("Hand.L","Hand.R","Foot.L","Foot.R"):
            r["tw_"+n]=twist_common(n); r["st_"+n]=twist_st(n)
        # idle delta
        d=0.0; wb=None
        for bn in HUMAN:
            q1=IDLE_BASIS[bn].to_quaternion(); q2=A.pose.bones[bn].matrix_basis.to_quaternion()
            ang=math.degrees(abs(q1.rotation_difference(q2).angle))
            if ang>d: d=ang; wb=bn
        r["idle_delta"]=d; r["idle_worst"]=(wb or "")[len(PFX):]
        r["hips_loc"]=[round(c,5) for c in A.pose.bones[PFX+"Hips"].matrix_basis.translation]
        rows.append(r)
        A.animation_data.action=act
    results[name]=rows
    print("\n===",name)
    print(" f  lowz     lowz_ex  lowbone      ankL      ankR     twHL    twHR    twFL    twFR   idleDelta")
    for r in rows:
        mark="*" if r["f"] in keyf else " "
        print("%s%2d %8.4f %8.4f %-12s %8.5f %8.5f %7.1f %7.1f %7.1f %7.1f %9.3f %s"%(
            mark,r["f"],r["lowz"],r["lowz_ex"],r["lowbone"],r["ank_all_L"],r["ank_all_R"],
            r["tw_Hand.L"],r["tw_Hand.R"],r["tw_Foot.L"],r["tw_Foot.R"],r["idle_delta"],r["idle_worst"] if r["f"] in (rows[0]["f"],rows[-1]["f"]) else ""))

json.dump({k:[{kk:(vv if not isinstance(vv,float) else round(vv,6)) for kk,vv in r.items()} for r in v] for k,v in results.items()},
          open("/tmp/vf/perframe.json","w"))
print("\nDONE")
