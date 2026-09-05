import bpy, sys, json, math, importlib.util
sys.path.insert(0,"/tmp/vf")
from mathutils import Vector, Matrix, Quaternion
bpy.ops.wm.open_mainfile(filepath="/tmp/vf/Yemoja_WORKING_v115_attacks.blend")
spec=importlib.util.spec_from_file_location("yemoja_anim_lib","/tmp/vf/yemoja_anim_lib.py")
L=importlib.util.module_from_spec(spec); spec.loader.exec_module(L); sys.modules["yemoja_anim_lib"]=L
PFX="mixamorig:"; A=L.armature(); BODY=bpy.data.objects["Yemoja_Body"]; CLOTH=bpy.data.objects["Yemoja_Clothes"]

def dom(ob):
    gn={g.index:g.name for g in ob.vertex_groups}
    vw=[[(gn[ge.group],ge.weight) for ge in v.groups if gn[ge.group].startswith(PFX)] for v in ob.data.vertices]
    vd=[max({b:w for b,w in x}.items(),key=lambda t:t[1])[0] if x else None for x in vw]
    fd=[]
    for p in ob.data.polygons:
        s={}
        for vi in p.vertices:
            for bn,w in vw[vi]: s[bn]=s.get(bn,0.0)+w
        fd.append(max(s,key=s.get) if s else None)
    return vd,fd
saved={pb.name:pb.matrix_basis.copy() for pb in A.pose.bones}
for pb in A.pose.bones: pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
BVD,BFD=dom(BODY); CVD,CFD=dom(CLOTH)
def areas(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh()
    a=[p.area for p in me.polygons]; ev.to_mesh_clear(); return a
BREST=areas(BODY); CREST=areas(CLOTH)
for n,m in saved.items(): A.pose.bones[n].matrix_basis=m
bpy.context.view_layer.update()

def audit(ob,fd,rest):
    a=areas(ob); per={}
    for bn,ra,pa in zip(fd,rest,a):
        if bn is None: continue
        e=per.setdefault(bn,[0.,0.,0,0]); e[0]+=ra;e[1]+=pa;e[2]+=1
        if ra>1e-9 and pa/ra<0.5: e[3]+=1
    return {bn[len(PFX):]:(pa/ra if ra>1e-9 else 1.0,cr,n) for bn,(ra,pa,n,cr) in per.items()}

def evv(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh(); mw=ev.matrix_world
    o=[mw@v.co for v in me.vertices]; ev.to_mesh_clear(); return o

def bw(n,w="head"):
    pb=A.pose.bones[L.full(n)]; return A.matrix_world@(pb.head if w=="head" else pb.tail)

def seg_pt(p,a,b):
    ab=b-a; t=max(0.,min(1.,(p-a).dot(ab)/max(ab.length_squared,1e-12))); return (p-(a+ab*t)).length
def seg_seg(p1,p2,p3,p4):
    import itertools
    best=1e9
    for i in range(101):
        t=i/100.0; P=p1+(p2-p1)*t; best=min(best,seg_pt(P,p3,p4))
    return best

GRIP=set(L.full(x) for x in ["Hand.R","ForeArm.R"]+["Hand%s%d.R"%(f,j) for f in ("Thumb","Index","Middle","Ring","Pinky") for j in (1,2,3)])
GRIPV=set()
for p,bn in zip(BODY.data.polygons,BFD):
    if bn in GRIP: GRIPV.update(p.vertices)

def trident_clear():
    b,t=L.trident_ends()
    vs=evv(BODY); lo=1e9; who=None
    for i,v in enumerate(vs):
        if i in GRIPV: continue
        d=seg_pt(v,b,t)
        if d<lo: lo=d; who=BVD[i]
    return lo,(who or "")[len(PFX):]

# ---- idle reference ----
A.animation_data.action=bpy.data.actions["Yemoja_Idle_MASTER"]; bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
IDLE_AUD_B=audit(BODY,BFD,BREST); IDLE_AUD_C=audit(CLOTH,CFD,CREST)
IDLE_SHOULDER_Z={s:bw("Arm."+s).z for s in ("L","R")}
IDLE_CLAV={s:bw("Shoulder."+s,"tail").z for s in ("L","R")}
IDLE_CLAVH={s:bw("Shoulder."+s,"head") for s in ("L","R")}
print("IDLE Arm head z L=%.4f R=%.4f ; Shoulder tail z L=%.4f R=%.4f"%(IDLE_SHOULDER_Z["L"],IDLE_SHOULDER_Z["R"],IDLE_CLAV["L"],IDLE_CLAV["R"]))
print("IDLE trident clearance %.4f (%s)"%trident_clear())
REG=["Arm.L","Arm.R","ForeArm.L","ForeArm.R","Shoulder.L","Shoulder.R","UpLeg.L","UpLeg.R","Leg.L","Leg.R","Spine2","Spine1","Spine","Hips","Neck","Head","Foot.L","Foot.R"]
print("IDLE body ratios:", {k:round(IDLE_AUD_B[k][0],3) for k in REG if k in IDLE_AUD_B})
print("IDLE clothes Arm.L/R:", {k:(round(v[0],3),v[1],v[2]) for k,v in IDLE_AUD_C.items() if k in ("Arm.L","Arm.R")})

CLIPS={"Yemoja_Atk_Punch":[1,4,7,9,14],"Yemoja_Atk_HardPunch":[1,7,12,15,26],
       "Yemoja_Atk_Kick":[1,4,7,9,16],"Yemoja_Atk_HardKick":[1,6,12,16,22,28]}
STRIKE={"Yemoja_Atk_Punch":("L",7),"Yemoja_Atk_HardPunch":("R",12),"Yemoja_Atk_Kick":("L",7),"Yemoja_Atk_HardKick":("R",12)}
out={}
for name,kfs in CLIPS.items():
    act=bpy.data.actions[name]; A.animation_data.action=act
    print("\n============",name)
    for f in kfs:
        bpy.context.scene.frame_set(f); L.attach_trident(); bpy.context.view_layer.update()
        ab=audit(BODY,BFD,BREST); ac=audit(CLOTH,CFD,CREST)
        below=[(k,round(v[0],3),v[1],v[2]) for k,v in sorted(ab.items(),key=lambda t:t[1][0]) if v[0]<0.95 and not (k.startswith("Hand") and k.endswith(".R") and any(x in k for x in ("Thumb","Index","Middle","Ring","Pinky")))]
        key_reg={k:round(ab[k][0],3) for k in REG if k in ab}
        tc,tcw=trident_clear()
        # head facing
        hq=A.matrix_world.to_3x3()@ (A.pose.bones[PFX+"Head"].matrix.to_3x3()@Vector((0,1,0)))
        # face direction: head local +? use armature-space Z forward
        headM=A.pose.bones[PFX+"Head"].matrix.to_3x3()
        # find which local axis of Head at idle points along armature +Z
        print(" f%-3d worst5=%s"%(f,[(k,round(v[0],3)) for k,v in sorted(ab.items(),key=lambda t:t[1][0])[:5]]))
        print("      key regions:",key_reg)
        print("      clothes Arm.L/R:", {k:(round(v[0],3),v[1],v[2]) for k,v in ac.items() if k in ("Arm.L","Arm.R")})
        print("      below0.95 (nonexempt) n=%d: %s"%(len(below),below[:12]))
        print("      trident clearance %.4f at %s ; hand.R %s"%(tc,tcw,[round(c,3) for c in bw("Hand.R")]))
        # clavicle
        for s in ("L","R"):
            armz=bw("Arm."+s).z; clz=bw("Shoulder."+s,"tail").z
            print("      Shoulder.%s: Arm head z %.4f (idle %.4f, elev %+.4f); clavicle tail z %.4f (idle %.4f, elev %+.4f) ratio %.2f"%(
                s,armz,IDLE_SHOULDER_Z[s],armz-IDLE_SHOULDER_Z[s],clz,IDLE_CLAV[s],clz-IDLE_CLAV[s],
                (clz-IDLE_CLAV[s])/(armz-IDLE_SHOULDER_Z[s]) if abs(armz-IDLE_SHOULDER_Z[s])>1e-4 else float('nan')))
        # shaft dir in armature space + dist to legs
        b,t=L.trident_ends()
        sd=(A.matrix_world.inverted()@t)-(A.matrix_world.inverted()@b); sd.normalize()
        print("      shaft dir(arm space) (%.3f,%.3f,%.3f)  butt=%s tip=%s"%(sd.x,sd.y,sd.z,[round(c,2) for c in b],[round(c,2) for c in t]))
        print("      shaft vs UpLeg.R seg %.3f  Leg.R seg %.3f"%(seg_seg(b,t,bw("UpLeg.R"),bw("Leg.R")), seg_seg(b,t,bw("Leg.R"),bw("Foot.R"))))
json.dump({},open("/tmp/vf/deep.json","w"))
