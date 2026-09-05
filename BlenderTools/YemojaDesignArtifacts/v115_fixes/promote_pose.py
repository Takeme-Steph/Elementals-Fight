"""Promote an authored idle pose candidate to Yemoja_Idle_MASTER and rebuild the loop.

    from promote_pose import promote
    promote("A", save_path=None)

Steps (idempotent via Armature["YEMOJA_POSE_V2"]):
  1. author_idle_pose.author(candidate)      build the arm pose from the current master
  2. rename the old Yemoja_Idle_MASTER  ->   Yemoja_Idle_MASTER_before_<candidate> (fake user)
  3. author_idle_pose.key_as("Yemoja_Idle_MASTER")
  4. build_idle_loop.build(...) with the twist splits measured on the new master
  5. build_idle_loop.verify()
"""
import bpy, os, json, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
STAMP = "YEMOJA_POSE_V2"
MASTER = "Yemoja_Idle_MASTER"
OLD_MASTER_FMT = "Yemoja_Idle_MASTER_before_{}"


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def promote(candidate="A", save_path=None, rebuild_loop=True):
    A = bpy.data.objects["Armature"]
    log = {"file": bpy.data.filepath, "candidate": candidate}
    if A.get(STAMP) == candidate:
        log["already_applied"] = True
    else:
        ap = _load("author_idle_pose")
        log["author"] = ap.author(candidate)
        old = bpy.data.actions.get(MASTER)
        if old is not None:
            backup = OLD_MASTER_FMT.format(candidate)
            if bpy.data.actions.get(backup) is not None:
                bpy.data.actions.remove(bpy.data.actions[backup])
            old.name = backup
            old.use_fake_user = True
        ap.key_as(MASTER)
        A[STAMP] = candidate
        log["master_keyed"] = True

    if rebuild_loop:
        rt = _load("retwist")
        A.data.pose_position = 'POSE'
        rt.detach_action(); rt.load_keyed_pose(1)
        tw = {s: round(rt.rel_twist(s, "elbow"), 3) for s in ("L", "R")}
        rt.reattach_action()
        bil = _load("build_idle_loop")
        p = dict(bil.DEFAULT_PARAMS); p["twist_L"] = tw["L"]; p["twist_R"] = tw["R"]
        log["loop_twist_targets"] = tw
        bil.build(p)
        v = bil.verify(p, do_deform=False)
        log["verify"] = {k: v[k] for k in ("seam", "feet", "floor", "hand", "twist_dev")}
        log["n_keyed_bones"] = v["channels"]["n_bones"]

    bpy.context.scene.frame_set(1)
    if save_path:
        bpy.ops.wm.save_as_mainfile(filepath=save_path)
        log["saved"] = save_path
    return log


if __name__ == "__main__":
    import sys
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    bpy.ops.wm.open_mainfile(filepath=argv[0])
    print(json.dumps(promote(argv[2] if len(argv) > 2 else "A", save_path=argv[1]), indent=1, default=str))
