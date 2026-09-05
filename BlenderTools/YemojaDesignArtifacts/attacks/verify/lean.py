import bpy,sys,math,importlib.util
sys.path.insert(0,"/tmp/vf"); from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath="/tmp/vf/Yemoja_WORKING_v115_attacks.blend")
s=importlib.util.spec_from_file_location("yemoja_anim_lib","/tmp/vf/yemoja_anim_lib.py")
L=importlib.util.module_from_spec(s); s.loader.exec_module(L)
A=L.armature(); PFX="mixamorig:"
def bw(n,w="head"):
    pb=A.pose.bones[L.full(n)]; return A.matrix_world@(pb.head if w=="head" else pb.tail)
A.animation_data.action=bpy.data.actions["Yemoja_Idle_MASTER"]; bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
idle_axis=(bw("Neck")-bw("Spine")).normalized()
idle_chest=(A.matrix_world.to_3x3()@(A.pose.bones[PFX+"Spine2"].matrix.to_3x3()@Vector((0,0,1)))).normalized()
print("idle torso axis",[round(c,3) for c in idle_axis],"chest fwd",[round(c,3) for c in idle_chest])
for name,kfs in (("Yemoja_Atk_HardKick",[6,12,16,22]),("Yemoja_Atk_Kick",[7]),("Yemoja_Atk_HardPunch",[7,12])):
    A.animation_data.action=bpy.data.actions[name]
    for f in kfs:
        bpy.context.scene.frame_set(f); bpy.context.view_layer.update()
        ax=(bw("Neck")-bw("Spine")).normalized()
        ch=(A.matrix_world.to_3x3()@(A.pose.bones[PFX+"Spine2"].matrix.to_3x3()@Vector((0,0,1)))).normalized()
        hips=(A.matrix_world.to_3x3()@(A.pose.bones[PFX+"Hips"].matrix.to_3x3()@Vector((0,0,1)))).normalized()
        # decompose torso tilt into lean-back(+y) and lean-left(+x) components
        print("%s f%-3d torso axis %s tilt from vert %.1f deg (back %+.1f, her-left %+.1f) | chest fwd %s (yaw from -Y %.1f) | hips fwd yaw %.1f"%(
          name,f,[round(c,3) for c in ax],math.degrees(math.acos(max(-1,min(1,ax.z)))),
          math.degrees(math.atan2(ax.y,ax.z)),math.degrees(math.atan2(ax.x,ax.z)),
          [round(c,3) for c in ch],math.degrees(math.atan2(ch.x,-ch.y)),
          math.degrees(math.atan2(hips.x,-hips.y))))
