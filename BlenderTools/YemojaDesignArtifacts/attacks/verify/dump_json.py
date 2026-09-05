"""
Dumper for keys.py / cmp.py's v114.json / v115.json inputs (SPEC_fix_v3.md
item 11 / preamble: "run them against the rebuilt file"). Not part of the
verifier's own original scripts -- written here because keys.py/cmp.py
require a JSON dump neither eval_all.py/arc.py/pen.py/lean.py/deep.py
produce, in the schema those two scripts' own field access implies
(f["path"], f["idx"], f["kps"][i] as an 9-element list, f["extrap"];
a["bones"][name]; a["objects"][name]; a["actions"][name]["frame_range"/
"fake"/"slots"/"layers"]).

Usage: blender-python dump_json.py <in.blend> <out.json>
(here: python3 dump_json.py <in.blend> <out.json>, bpy-as-a-module).
"""
import bpy, sys, json, hashlib, random

BLEND = sys.argv[-2]
OUT = sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=BLEND)

PFX = "mixamorig:"


def dump_action(act):
    layers = []
    for lyr in act.layers:
        strips = []
        for st in lyr.strips:
            cbs_out = []
            cbs = getattr(st, "channelbags", None)
            if cbs is None:
                cbs = []
                get_cb = getattr(st, "channelbag", None)
                if get_cb is not None:
                    for slot in act.slots:
                        try:
                            cb = get_cb(slot)
                        except TypeError:
                            try:
                                cb = get_cb(slot, ensure=False)
                            except Exception:
                                cb = None
                        if cb is not None:
                            cbs.append(cb)
            for cb in cbs:
                fcs = []
                for fc in cb.fcurves:
                    kps = []
                    for kp in fc.keyframe_points:
                        kps.append([round(kp.co[0], 5), round(kp.co[1], 6), kp.interpolation,
                                    round(kp.handle_left[0], 5), round(kp.handle_left[1], 6),
                                    round(kp.handle_right[0], 5), round(kp.handle_right[1], 6),
                                    kp.handle_left_type, kp.handle_right_type])
                    fcs.append(dict(path=fc.data_path, idx=fc.array_index, kps=kps,
                                     extrap=fc.extrapolation))
                cbs_out.append(dict(fcurves=fcs))
            strips.append(dict(channelbags=cbs_out))
        layers.append(dict(strips=strips))
    return dict(fake=bool(act.use_fake_user), frame_range=[round(c, 3) for c in act.frame_range],
                slots=len(act.slots), layers=layers)


def dump_object(ob):
    d = dict(type=ob.type,
             location=[round(c, 5) for c in ob.location],
             rotation_quaternion=[round(c, 5) for c in ob.rotation_quaternion],
             scale=[round(c, 5) for c in ob.scale],
             parent=ob.parent.name if ob.parent else None,
             data=ob.data.name if ob.data else None)
    if ob.type in ("MESH", "ARMATURE"):
        d["vgroups"] = [g.name for g in ob.vertex_groups] if hasattr(ob, "vertex_groups") else []
    return d


def dump_bones(arm_ob):
    out = {}
    for b in arm_ob.data.bones:
        out[b.name] = dict(parent=b.parent.name if b.parent else None,
                            head_local=[round(c, 6) for c in b.head_local],
                            tail_local=[round(c, 6) for c in b.tail_local],
                            matrix_local=[[round(c, 6) for c in row] for row in b.matrix_local],
                            use_connect=b.use_connect)
    return out


scene = bpy.context.scene
out = dict(fps=scene.render.fps, fps_base=scene.render.fps_base,
           frame_start=scene.frame_start, frame_end=scene.frame_end)

out["actions"] = {a.name: dump_action(a) for a in bpy.data.actions}
out["objects"] = {ob.name: dump_object(ob) for ob in bpy.data.objects}
out["materials"] = sorted(m.name for m in bpy.data.materials)
out["cameras"] = sorted(ob.name for ob in bpy.data.objects if ob.type == "CAMERA")

arm_ob = bpy.data.objects.get("Yemoja_Rig") or next(
    (ob for ob in bpy.data.objects if ob.type == "ARMATURE"), None)
out["bones"] = dump_bones(arm_ob) if arm_ob else {}

body = bpy.data.objects.get("Yemoja_Body")
if body:
    random.seed(1234)
    idx = sorted(random.sample(range(len(body.data.vertices)), min(2000, len(body.data.vertices))))
    gn = {g.index: g.name for g in body.vertex_groups}
    bw = {}
    for i in idx:
        v = body.data.vertices[i]
        bw[str(i)] = sorted([[gn.get(ge.group, "?"), round(ge.weight, 5)] for ge in v.groups])
    out["body_weights"] = bw
    h = hashlib.sha1()
    for v in body.data.vertices:
        h.update(b"%.6f,%.6f,%.6f;" % (v.co.x, v.co.y, v.co.z))
    out["body_co_hash"] = h.hexdigest()
else:
    out["body_weights"] = {}
    out["body_co_hash"] = None

json.dump(out, open(OUT, "w"))
print("DUMPED", OUT, "actions=%d objects=%d bones=%d" %
      (len(out["actions"]), len(out["objects"]), len(out["bones"])))
