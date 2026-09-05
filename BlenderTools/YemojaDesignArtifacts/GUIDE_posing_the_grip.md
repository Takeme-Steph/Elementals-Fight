# Posing Yemoja's trident grip — a walkthrough

Everything is already set up for you. This file explains what to do **and why**,
because the "why" is what makes it transferable to the next character.

---

## What I already did

- **Slimmed the trident shaft** from 58 mm to **34 mm** diameter (41% thinner).
  Length and tine spread are untouched. The old FBX is backed up beside this file
  as `Yemoja_Trident.fbx.bak_2026-09-03`.
- Re-exported over the same FBX path so Unity's GUID survived — both prefabs still
  resolve the mesh, the material and the `A_Trident` hitbox.
- Solved a starting grip and matched the trident's hand offset to it in both
  `Yemoja.prefab` and `YemojaDisplay.prefab`.
- Opened `Yemoja_WORKING_v112_anim.blend` in **Pose Mode** with the **15 right-hand
  finger bones already selected**, and made a bone collection `GRIP_R_fingers` so
  you can re-select them in one click next time.

Your job is the last 10%: make the fingers actually close around the shaft.

---

## The one concept that matters

**A finger curls by rotating about its own local X axis.**

Every bone has three local axes. On this rig, Y runs *along* the bone (useless for
curling — it just twists), Z spreads the finger sideways, and **X is the curl**.
I verified this by measurement, not assumption.

Blender's shortcut for "rotate about the bone's own axis, not the world's" is to
**press the axis key twice**:

    R  X  X        rotate about LOCAL X   <- this is the curl
    R  X           rotate about WORLD X   <- almost never what you want

That double-tap is the single most useful habit in rigging. It works for any
object, any bone.

You can also type an exact number: `R X X 45 Enter` curls that joint 45 degrees.

---

## Getting a good view

1. Hover the mouse over the 3D viewport and press **numpad `.`** (View Selected) —
   this frames the selected bones, so you'll zoom straight to her hand.
2. **Numpad 1 / 3 / 7** = front / side / top. Orbit with **middle mouse**.
3. Press **`Z` → Wireframe** (or toggle **Alt+Z** for X-ray) when you want to see
   the shaft *through* her fingers. This is how you check for gaps and for fingers
   sinking into the shaft — you cannot judge either from a solid view.

---

## The order to work in

Work **proximal to distal** — the joint nearest the palm first, then the middle,
then the tip. Each one changes where the next sits, so going the other way means
redoing your work.

1. **Middle finger first.** Set it so the three segments hug the shaft: the first
   joint brings the finger onto the shaft, the second and third carry it around
   the far side. It's your reference — everything else matches it.
2. **Ring finger** next, almost identical to the middle.
3. **Index and pinky** slightly less curl — they sit at the ends of the knuckle
   line and reach the shaft at a different angle. A hand where all four fingers
   curl identically looks like a mannequin.
4. **Thumb last**, laid across the front of the index and middle fingers.

### What "good" looks like

- Fingers **wrap past the far side** of the shaft — the fingertips should be
  hidden behind it from the front, not stopping short.
- **No visible gap** between the palm and the shaft. The shaft should look like
  it's resting *in* her hand, taking her weight.
- **Nothing sinking in.** In X-ray, no finger geometry inside the shaft cylinder.
- The four fingertips should form a **slight diagonal**, not a straight line.

Don't aim for anatomical perfection. Aim for a shape that reads as "gripping"
at a glance — that's the whole job.

---

## Undo and safety

- **Ctrl+Z** undoes pose changes normally.
- To reset one bone: select it and **Alt+R** (clear rotation).
- To reset the whole hand: select all 15 and **Alt+R**.
- The file is saved at the current state, so worst case you close without saving.

---

## When you're happy

Tell me, and I'll run `capture_grip()` — it reads the rotation off each of the 15
bones and writes them permanently into `yemoja_anim_lib.py` as `CAPTURED_GRIP_R`.
From then on every clip calls `apply_captured_grip("R")` and gets your exact grip.

You only ever have to do this once.

---

## If you want to go further

The same three ideas cover most hand posing:

- **Local axes** (`R` `X` `X`) — the double-tap.
- **Proximal to distal** — parents move children, so work outward.
- **Break the symmetry** — identical fingers read as fake; small variation reads
  as alive.
