import bpy, sys, math, importlib.util
sys.path.insert(0,"/tmp/vf")
from mathutils import Vector
from mathutils.bvhtree import BVHTree
bpy.ops.wm.open_mainfile(filepath="/tmp/vf/Yemoja_WORKING_v115_attacks.blend")
spec=importlib.util.spec_from_file_location("yemoja_anim_lib","/tmp/vf/yemoja_anim_lib.py")
L=importlib.util.module_from_spec(spec); spec.loader.exec_module(L)
PFX="mixamorig:"; A=L.armature(); BODY=bpy.data.objects["Yemoja_Body"]; CLOTH=bpy.data.objects["Yemoja_Clothes"]
def bw(n,w="head"):
    pb=A.pose.bones[L.full(n)]; return A.matrix_world@(pb.head if w=="head" else pb.tail)
def build(ob):
    dg=bpy.context.evaluated_depsgraph_get(); ev=ob.evaluated_get(dg); me=ev.to_mesh(); mw=ev.matrix_world
    vs=[mw@v.co for v in me.vertices]; ps=[list(p.vertices) for p in me.polygons]; ev.to_mesh_clear()
    return BVHTree.FromPolygons(vs,ps,all_triangles=False), vs
def inside1(bvh,p,d):
    n=0; o=p.copy()
    for _ in range(80):
        h=bvh.ray_cast(o+d*1e-4,d)
        if h[0] is None: break
        n+=1; o=h[0]
    return n%2==1
DIRS=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)),Vector((0.577,0.577,0.577)),Vector((-0.577,0.577,-0.577))]
def rins(bvh,p): return sum(1 for d in DIRS if inside1(bvh,p,d))>=3

print("--- HardKick clothes-in-body, robust 5-ray ---")
A.animation_data.action=bpy.data.actions["Yemoja_Atk_HardKick"]
for f in (1,6,12,16,22):
    bpy.context.scene.frame_set(f); bpy.context.view_layer.update()
    bvh,_=build(BODY)
    dg=bpy.context.evaluated_depsgraph_get(); ev=CLOTH.evaluated_get(dg); me=ev.to_mesh(); mw=ev.matrix_world
    n=sum(1 for v in me.vertices if rins(bvh, mw@v.co)); tot=len(me.vertices); ev.to_mesh_clear()
    print("  f%-3d clothes verts inside body %d / %d"%(f,n,tot))

print("--- HardKick right-ankle / right-knee world arc per frame ---")
for f in range(1,29):
    bpy.context.scene.frame_set(f); bpy.context.view_layer.update()
    a=bw("Foot.R"); k=bw("Leg.R"); h=bw("UpLeg.R")
    print("  f%-3d ankle (%7.3f,%7.3f,%7.3f)  knee (%7.3f,%7.3f,%7.3f)  hip->ankle %.3f"%(f,a.x,a.y,a.z,k.x,k.y,k.z,(a-h).length))
print("--- HardPunch right hand / trident tip per frame ---")
A.animation_data.action=bpy.data.actions["Yemoja_Atk_HardPunch"]
for f in range(1,27):
    bpy.context.scene.frame_set(f); bpy.context.view_layer.update()
    hnd=bw("Hand.R"); b,t=L.trident_ends()
    print("  f%-3d hand (%7.3f,%7.3f,%7.3f)  tip (%7.3f,%7.3f,%7.3f)  butt (%7.3f,%7.3f,%7.3f)"%(f,hnd.x,hnd.y,hnd.z,t.x,t.y,t.z,b.x,b.y,b.z))
