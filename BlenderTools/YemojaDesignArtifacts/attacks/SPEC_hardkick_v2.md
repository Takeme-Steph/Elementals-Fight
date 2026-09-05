# Yemoja_Atk_HardKick — re-block as a true roundhouse (v2)

Reviewer verdict on the v1 block: reads as a straight-up front high kick, not
a roundhouse, and the trident lies flat across her chest at f6/f22. Rebuild
only this clip. Same length (28 frames), same impact frame (12), same key
frames (1, 6, 12, 16, 22, 28). Everything else in SPEC_attacks.md stands.

## The mechanics we want

A rear-leg (right) roundhouse. The kick is a horizontal arc: the pelvis
opens so her right hip comes forward, the support (left) foot pivots on the
spot, the thigh swings around roughly level with the hip-to-target line,
and the knee is displaced to HER RIGHT (−X armature / −X world) of the
hip→foot line at impact, never above it. The torso leans back and away from
the kick, head stays on the opponent. Chest faces her left (+X) at impact.

Sign conventions, all measured in v114 (yemoja_anim_lib docstring):
Hips yaw about armature +Y by a POSITIVE angle brings her right hip forward.
Spine chain: −X = lean back, −Z = tilt toward her left, −Y = turn chest back
toward the opponent.

## Support foot pivot (this is what v1 got wrong)

Do not impose a rotation on `Foot.L` alone — that is what produced the −34°
foot twist. Rotate the WHOLE support leg about the world-vertical axis
through the idle ankle position:

    R = rotation(world Z, pivot_deg) about the idle Foot.L head
    ankle  = idle ankle (unchanged)
    knee_hint = R @ idle knee position          (rotate the hint too)
    leg_ik("L", ankle, knee_hint)
    Foot.L.matrix (armature space) = R_arm @ idle Foot.L matrix   (rotation part only, keep the head where leg_ik put it)

Then `Foot.L` twist relative to `Leg.L` stays near idle (measure it; budget
20°) and the sole stays flat because the idle sole was flat. Add this as
`pivot_support_foot(side, snap, pivot_deg)` in harness.py (replace the
reverted attempt). Verify: ankle error < 0.005, lowest body z within
[−0.005, 0.02], Foot.L twist < 20°.

## Keys

World coordinates. Hip socket at idle = `Arm`-style landmark `UpLeg.R` head
(−0.435, 0.499, 4.378). Max leg reach 3.96; keep the impact target at
60–70 % of reach so the knee has room to sit to the side.

| f | pelvis / weight | kicking leg (R) | support leg (L) | torso & head | arms |
|---|---|---|---|---|---|
| 1 | idle | idle | idle | idle | idle |
| 6 | Hips yaw +25°, Hips move world (+0.30, −0.10, −0.15) (over the left foot, knees soften) | knee lifts forward-right: ankle (−1.15, 0.80, 2.2), knee hint (−1.55, 0.25, 3.4); foot relaxed, toes down ~20° | pivot 15° | Spine chain X −6 total, Y −8; Neck/Head Y −15 total (eyes on +Z) | left fist guard at (0.55, −0.50, 5.70), elbow pole (0.95, −0.1, 5.2); right hand (−1.35, 1.30, 5.05), trident shaft direction in armature space normalised(−0.25, 0.80, −0.55) i.e. tip up and back over her right shoulder, use `orient_hand_for_shaft`, hint chosen for min Hand.R twist |
| 12 impact | Hips yaw +75°, Hips move world (+0.35, −0.05, −0.22) | ankle (0.10, −1.60, 5.85) — head height, in front of her; knee hint (−1.35, −0.55, 5.30) — to her right and slightly below the foot; after leg_ik, plantarflex `Foot.R` so the toe tip lies within 15° of the shin line (measure ToeBase.R tail vs Leg.R direction) | pivot 55° | Spine chain X −20 total (lean back), Z −10 total (tilt to her left, away from the kick), Y −15 (chest turns partly back toward the opponent); Neck −25, Head −30 about Y so the face is within 15° of +Z | left arm swings down-forward for balance: fist (0.45, −0.55, 4.60), pole (0.9, 0.2, 4.4); right arm swings back and up: hand (−1.05, 1.55, 5.60), shaft direction normalised(−0.35, 0.55, −0.75) (tip up-back, well clear of the leg); Shoulder.L elevate 8°, Shoulder.R elevate 15° |
| 16 follow-through | Hips yaw +95°, Hips move (+0.35, 0.00, −0.20) | ankle (0.95, −1.10, 5.10), knee hint (−0.20, −1.35, 4.50) (leg continuing the arc across her front) | pivot 70° | Spine X −18, Z −12, Y −25; Neck −25, Head −30 | left fist (0.30, 0.10, 4.40); right hand (−0.85, 1.55, 5.65), same shaft direction as f12 |
| 22 recover | Hips yaw +30°, Hips move (+0.25, −0.05, −0.12) | ankle (−0.75, 0.75, 1.00), knee hint (−1.2, 0.2, 2.6) (foot coming down, not yet planted) | pivot 15° | Spine X −6, Y −8; Neck/Head Y −15 total | left fist (0.55, −0.5, 5.7); right hand (−1.35, 1.30, 5.05), shaft as f6 |
| 28 | idle | idle | idle | idle | idle |

Right hand keeps `apply_captured_grip("R")` at every key; attach_trident
after every right-arm change; `trident_clearance` ≥ 0.15 at every key,
and the shaft must not cross the kicking leg's line (report the minimum
distance between the shaft segment and the UpLeg.R/Leg.R segments).

## What to report back

The usual report table plus: Foot.L twist at every key, Hand.R twist, the
angle between the thigh (UpLeg.R) and the horizontal plane at f12 (should be
+20° to +40°, i.e. thigh rising toward the target, not vertical), the
knee's lateral offset from the hip→foot line at f12 (should be ≥ 0.5 world
units toward −X), and fresh renders `Yemoja_Atk_HardKick_f*` (overwrite).
If a number cannot be met, move the target minimally along its own ray or
adjust the pole, and record it; do not fall back to the v1 vertical kick.
