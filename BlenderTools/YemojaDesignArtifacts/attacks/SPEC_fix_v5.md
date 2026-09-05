# Attack clips — fix round v5 (decisions on VERIFY_attacks_v4.md)

Read /home/claude/work/VERIFY_attacks_v4.md. Its numbers stand. The recurring
failure is the same one as v3: keys are verified, in-betweens are not, and
BUILD_NOTES then claims per-frame results that were never measured. That ends
here: see "Gate" below.

## Gate (do this first, before any pose change)

`attacks_build.py` must end with a `final_gate()` that, on the SAVED file,
re-opens it and runs, on EVERY frame of every clip: the signed BVH trident
penetration test (import and call the function from verify/pen.py, do not
re-implement it), the shaft-to-UpLeg.R/Leg.R segment distance, floor and
support-foot checks, hemisphere-flip count, quaternion norms (every key
|q| within 1e-4 of 1), full-keying check (every keyed frame carries all 57
humanoid bones + Hips location; no frame with a partial set), off_hinge per
arm, head-angle at keys, and the export-range/handoff state. It prints a
table and exits non-zero on any failure. BUILD_NOTES' acceptance section is
the verbatim stdout of that gate, nothing else. If the gate cannot pass, the
notes say which rows fail and why; do not paraphrase a pass.

## Fixes

1. **Trident through the body between keys (HardKick f5, 8–11, 17–21, 23;
   HardPunch f8, f10).** Add `enforce_trident_clear(action)`: step every
   frame; where the signed test reports non-grip penetration or the shaft is
   within 0.25 of the UpLeg.R/Leg.R segments (or 0.20 of any other body
   region), capture the interpolated pose, then re-orient Hand.R with
   `orient_hand_for_shaft` to the SLERP of the bracketing keys' shaft
   directions at that frame's parameter (shaft direction should turn along
   the short arc, not swing through the body), keep the hand position, re-run
   the right-arm solve if the elbow moved, re-run pin_foot for the support
   leg, normalise, and key ALL humanoid bones at that frame. Iterate to a
   fixed point (cap 8 passes). If a frame cannot be cleared this way, move the
   bracketing keys' shaft directions further behind the body plane and record
   it.

2. **Full keying, unit quaternions.** Every routine that inserts a key at a
   frame (pins, breakdowns, trident clear, hinge fix) keys the full humanoid
   set + Hips location at that frame, from a normalised snapshot. Add the
   check to the gate.

3. **HardKick impact must be the apex.** Between f6 and f12 the kicking
   ankle's forward coordinate (world −y) must increase monotonically and peak
   AT f12, and between f12 and f16 it must not go beyond f12's value. Do it by
   placing breakdowns at f9 and f11 whose ankle targets lie on the straight
   arc between the chamber and impact ankle positions (at 55 % and 90 % of the
   way), solved with leg IK and the same knee-side pole, then re-check every
   frame. Same rule for the front kick (f4→f7 monotonic, apex at f7).

4. **HardKick clavicles.** FABLE: elevation ≤ 30° and protraction ≤ 20° on
   every key of every clip (Unity clamps at 30 / 15 by default; the avatar
   limits will be raised to 30/20 on the Unity side, noted separately). At
   HardKick f12/f16 set Shoulder.L/R elevation to min(30°, 1/3 of the arm's
   rise above shoulder height, 0 if the arm is not above shoulder height) and
   accept the ratios. Report them.

5. **HardKick f6/f22 Arm.R 0.87 / Shoulder.R 0.87 (44 crushed).** Sweep the
   trident-hold pole and hand hint at those two keys for Arm.R ≥ 0.90 with
   Hand.R twist ≤ 30° and off_hinge < 5°; if no point satisfies all three,
   raise the hand target 0.15 and retry once, then report the best found.

6. **HardPunch head** at f12/f15 to within ±10° of +Z. **Toe-vs-shin** at
   HardKick f12/f16 ≤ 15°.

7. **Steps > 0.9 per frame** (HardPunch wrist f10 1.218, HardKick ankle f20
   1.710): these are the same in-between problems as item 1 and 3; after
   those fixes, any remaining step > 0.9 gets a breakdown at that frame with
   the limb target at the midpoint of its neighbours, keyed fully.

8. **Kick support-foot settle.** Spread the flat-foot settle over f2–f4 and
   the rise over f13–f16 (breakdowns at f2, f3, f13, f15) so no single frame
   moves the support ankle more than 0.06.

9. **Saved state.** Before saving: `preview_mode(False)` (Yemoja_Tattoos
   visible, Scalp Shrinkwrap on), Armature on `Yemoja_Idle_MASTER`, frame 1,
   scene frame range 1–121 as in the source. cmp.py must still show every
   pre-existing datablock identical to the source.

10. **Clothes-inside** worst frame per clip is reported from the gate (it was
    507 at HardKick f9, not 345); no threshold change, but the number in the
    notes must be the per-frame worst.

Deliver the same set as v4 (Yemoja_ATTACKS_v4.blend overwritten, review/v4,
BUILD_NOTES `## v5` section = gate stdout + deviations). Final message: the
gate table, compact, plus anything that still fails it.
