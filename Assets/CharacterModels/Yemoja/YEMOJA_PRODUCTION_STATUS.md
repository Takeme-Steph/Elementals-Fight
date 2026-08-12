# Yemoja — Production Readiness Status (Mobile / Unity)

_Last reviewed: 2026-07-30. Target: MOBILE (Elementals Fight, Unity). AAA base backed up separately._

## Current stats
- Meshes: Yemoja_Body, Yemoja_Clothes, Hair (separate objects, shared skeleton)
- Skeleton: 92 bones (finger leaf bones trimmed; hair 2-bone/loc)
- Triangles: ~62,200 total (Body 13.5k, Clothes 39.3k, Hair 9.4k)
- Topology: cleaned — 0 ngons, 0 duplicate verts, consistent normals, 100% weighted
- Clothing vertex groups: 9, all classified & verified

## READY (production-solid for mobile)
- [x] Body/Clothes/Hair separated (industry-standard structure)
- [x] Body mesh: clean quad topology, weights intact — this is the reused foundation
- [x] Skeleton optimized: finger leaf bones removed, hair rig 2-bone/loc (34 bones)
- [x] Hair loc rig: 34 bones, sway verified, isolation clean
- [x] Anklet clipping into foot: fixed
- [x] 9 clothing vertex groups classified (SW_Top, SW_Shorts, SW_Waistband,
      Yemoja_TopBand, Yemoja_ShortBeads, Yemoja_Bracelets, Yemoja_Necklace,
      Yemoja_Buckle, Yemoja_Anklets)
- [x] Topology cleanup: ngons + doubles fixed on all meshes

## BLOCKERS (must fix before mobile ship)
- [ ] **POLY BUDGET: 62k tris is too high for mobile.** Clothes alone = 39.3k (63%).
      Target ~15-30k total for a mobile fighting-game character (renders 2+ at once).
      -> This requires the CLOTHES REBUILD, not decimation (loose-island beadwork
         won't reduce cleanly). Rebuild with a hard tri budget.

## PLACEHOLDER (flagged, rebuild later)
- [ ] Clothes: rough topology, over-budget, hundreds of loose islands -> full rebuild
- [ ] Hair texture: workaround curl normal (loc UVs not consistently oriented)
- [ ] Baby hairs: deferred -> bake wisps into texture on hair rebuild (mobile-correct;
      avoid alpha cards - transparency/overdraw cost on mobile)

## VERIFY IN UNITY (can't check from Blender)
- [ ] Material texture references — confirm which BaseColor is used (Graded vs plain)
      before deleting duplicates. Same for standalone Metallic/Roughness vs combined
      MetallicSmoothness. Candidates flagged but NOT deleted pending Unity check:
        Clothes_BaseColor.png, Yemoja_Color.png (ungraded dupes)
        Clothes_Metallic.png, Clothes_Roughness.png, Trident_Metallic.png,
        Trident_Roughness.png, Yemoja_Roughness.png, Yemoja_Hair_Roughness.png (redundant)
- [ ] Skin reads as skin, clothes read as leather (URP shader + lighting judgment)
- [ ] Texture resolutions — consider 512 vs 1024 for mobile at gameplay distance
- [ ] Reimport after multi-mesh split: avatar rebuild, verify isHuman (not just isValid),
      trident re-parent, 11 hitbox colliders re-parent, 34 new hair bones + physics setup

## FUTURE / OPTIONAL
- [ ] Finger bones: could drop 3->2 joints/finger (~10 more bones saved) but costs
      fingertip articulation. Only if profiling demands it.
- [ ] AAA copy (future Unreal RPG): optimize UP from the AAA base backup, not this file.


## UPDATE (2026-07-30): POLY BUDGET RESOLVED
- Beadwork decimated: shorts-beads + bracelets aggressive (~0.3), verified clean.
- Character now ~34,700 tris (was 62,200). Body 13.5k / Clothes 11.8k / Hair 9.4k.
- Device target confirmed: 2 chars sustained, up to 4 briefly (tag-swap only).
  2x35k = 70k sustained is comfortable mobile. Poly budget is no longer a blocker.
- Anklets: kept at aggressive decimation with mild distortion (acceptable per review;
  they're small/distant on screen). A gentler re-decimate was attempted but made things
  worse overall, so reverted. Leave as-is; revisit only if anklets ever become a focal point.
- Clothing fabric (top/shorts/bands/necklace/buckle) was already lean (~4.5k tris), untouched.


## HANDOFF NOTE (2026-07-31) — pending items at chat handoff
Chat handed off to a new session. State at handoff:

**AWAITING CONFIRMATION (was the last action):**
- Armpit/top weight re-fix. Top re-transferred from body so it inherits the body's
  natural arm/shoulder/spine blend (first attempt over-corrected — stripped all arm
  influence, caused skin poke-through at the sides). Check render:
  BlenderTools/_previews/Yemoja_armpit_fixed2.png — top should move WITH the arm,
  no skin poking through near the armpit. Snapshot: Backups/Yemoja_PRE_armpitfix2.blend

**STILL PENDING:**
1. Confirm the armpit re-fix above. If good, skeletal-weight fixing is complete.
2. BODY + HAIR weight verification pass — check for the same classification-weight
   contamination that hit the clothes (36% of clothing verts were weighted to
   non-bone classification groups: SW_Top, Yemoja_ShortBeads, etc.). Clothes were
   worst-affected and are now fixed (classification groups removed, weights transferred
   from body; bracelets moved Hand->ForeArm; anklets verified correct). Body/hair are
   likely clean but VERIFY, don't assume — cheaper to catch here than in Unity.
3. PEARL BEADS: user wants shorts-beads + necklace recolored to PALE PEARL WHITE.
   They went blue in the hue-based garment recolor. Needs a robust method — the
   earlier UV-mask method FAILED (produced stippled dots); the whole-texture hue
   method is what worked for the garment.
4. Save final + update this doc when the above are done.

**KEY LESSON THIS SESSION:** clothing should inherit body weights via data-transfer
(POLYINTERP_NEAREST from Yemoja_Body), never be hand-assigned to single bones — the
body already has the correct arm/shoulder/spine blend. Read weights via bmesh deform
layer, not stale mesh.vertices.

**LATER — Unity reimport & verify:** multi-mesh split (Body/Clothes/Hair separate),
~92-bone skeleton (20 finger leaf bones removed, hair rig now 34 bones at 2/loc),
avatar rebuild + verify isHuman (not just isValid), trident re-parent to Hand.R,
11 hitbox colliders re-parent, hair bones -> physics component (Dynamic Bone/Magica
Cloth). Verify material texture references before deleting flagged-redundant textures.
Subsurface skin luminosity is Blender-preview only — mobile URP needs a cheaper approximation.
