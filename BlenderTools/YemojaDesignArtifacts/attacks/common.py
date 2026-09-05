import bpy, sys, json, math, importlib.util, os
from mathutils import Vector, Matrix, Quaternion
BLEND = "/home/claude/work/attacks/Yemoja_WORKING_v114_idleClean.blend"
LIB = "/home/claude/work/attacks/yemoja_anim_lib.py"
REVIEW_DIR = "/home/claude/work/attacks/review"
def load(blend=BLEND, lib=LIB, review_dir=REVIEW_DIR):
    # SPEC_rebuild_v4.md: parameterised so attacks_build.py can point this at
    # the new source/library/review dir without editing this file -- lib
    # defaults to the old yemoja_anim_lib.py so every earlier caller of
    # load(blend) alone is unaffected.
    bpy.ops.wm.open_mainfile(filepath=blend)
    spec = importlib.util.spec_from_file_location("yemoja_anim_lib", lib)
    L = importlib.util.module_from_spec(spec); spec.loader.exec_module(L); sys.modules["yemoja_anim_lib"] = L
    L.REVIEW_DIR = review_dir
    return L
def apply_json_pose(L, path="/mnt/user-data/uploads/Elementals-Fight/BlenderTools/YemojaDesignArtifacts/pose_idle_master_2026-09-03_v114clean.json"):
    A = L.armature(); d = json.load(open(path))
    for n, v in d.items():
        pb = A.pose.bones.get(n)
        if not pb or n.startswith("hair_"): continue
        M = Quaternion(v["q"]).to_matrix().to_4x4(); M.translation = Vector(v["loc"]); pb.matrix_basis = M
    bpy.context.view_layer.update()
def twist_deg(L, n):
    q = L.armature().pose.bones[L.full(n)].matrix_basis.to_quaternion(); return math.degrees(2*math.atan2(q.y, q.w))
