"""Yemoja v115 fixes -- reproducible chain. Run inside Blender (live or bpy module).

    from apply_all import run_in_place, run
    run_in_place(save_path=None)          # apply to the currently open file, optionally save
    run("IN.blend", "OUT.blend")          # open, apply, save

Steps, each idempotent:
  1. fix_shoulder_weights.apply()   shoulder-girdle re-weight on Yemoja_Body (stamps text block fix_shoulder_backup.json)
  2. retwist.set_split() + key_arms() elbow/wrist twist re-split, keyed into Yemoja_Idle_MASTER frame 1
  3. fix_bracelet_fit.apply()        rigid bracelet re-fit on Yemoja_Clothes (stamps YEMOJA_BRACELET_FIX)
  4. apply_pole.apply("L", -40)     left elbow tuck (approved 2026-09-03), keyed into frame 1 (stamps YEMOJA_POLE_FIX)
Measured on the idle master pose; see README_animation_guidelines.md section 17.
"""
import bpy, sys, os, json, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
TWIST = {"L": 168.0, "R": 42.3}   # target relative twist at the elbow (deg); from the sweep in section 17


def _load(name):
    path = os.path.join(HERE, name + ".py")
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def run_in_place(save_path=None, bracelet=True, twist=True, pole=True):
    log = {"file": bpy.data.filepath}
    sh = _load("fix_shoulder_weights")
    log["shoulder"] = {k: v for k, v in sh.apply().items() if k != "params"}

    if twist:
        tw = _load("retwist")
        A = tw.armature()
        A.data.pose_position = 'POSE'
        tw.detach_action(); tw.load_keyed_pose(1)
        # sides already carrying the pole tuck own their split; leave them alone
        poled = set(dict(A.get("YEMOJA_POLE_FIX") or {}).keys())
        cur = {s: round(tw.rel_twist(s, "elbow"), 2) for s in ("L", "R")}
        todo = [s for s in ("L", "R") if s not in poled and abs(cur[s] - TWIST[s]) >= 0.05]
        if not todo:
            tw.reattach_action(); bpy.context.scene.frame_set(1)
            log["twist"] = {"already_applied": cur, "poled": sorted(poled)}
        else:
            log["twist"] = {s: tw.set_split(s, TWIST[s]) for s in todo}
            tw.key_arms(1)
            bpy.context.scene.frame_set(1)
        log["twist_after"] = {s: {"elbow": round(tw.rel_twist(s, "elbow"), 2),
                                  "wrist": round(tw.rel_twist(s, "wrist"), 2)} for s in ("L", "R")}

    if bracelet:
        br = _load("fix_bracelet_fit")
        log["bracelet"] = br.apply()

    if pole:
        ap = _load("apply_pole")
        log["pole"] = ap.apply("L", -40.0)

    if save_path:
        os.makedirs(os.path.dirname(save_path) or ".", exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=save_path)
        log["saved"] = save_path
    return log


def run(src, dst, **kw):
    bpy.ops.wm.open_mainfile(filepath=src)
    return run_in_place(save_path=dst, **kw)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    print(json.dumps(run(argv[0], argv[1]), indent=1, default=str))
