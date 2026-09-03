# Eye standard - Elementals-Fight

Applies to **every** rigged humanoid character in the project. Not Yemoja-specific.
Character manifests should reference this file, never restate it.

Companion to `BlenderTools/README_rig_conventions.md`.

## The rule

**Eyes are a shared, reusable component - not part of the body mesh.**

    <Character>_Eyes          one object, two connected components
    mixamorig:Eye.L / .R      one bone each, parented to mixamorig:Head
    <Character>_Eye_mat       one material, opaque URP Lit
    Eye_BaseColor.png         one small texture, shared across the roster

Every eyeball in the project is geometrically the same object at a different
scale. Only the iris colour changes. Anything that repeats unchanged across
every character gets factored out once.

## Why not part of the body

Three concrete costs of eyes living inside the body mesh, all of them paid on
Yemoja before this document existed:

1. **The body atlas is mirrored, and mirroring destroys directional paint.**
   A radially symmetric iris survives it; a catchlight does not. Yemoja's
   catchlights landed 83.6 deg apart in world space - each eye lit from a
   different side, as if by two light sources.
2. **No eye bones means no gaze.** Unity's Humanoid auto-mapper does not leave
   `LeftEye`/`RightEye` empty when the bones are absent - it grabs the nearest
   plausible transforms. On Yemoja v2 it mapped both eyes and the jaw to *hair
   bones*. That has to be found and removed by hand.
3. **No upgrade path.** The eye is the component most likely to be replaced
   wholesale for the AAA target. If it is welded into the body it cannot be
   swapped without touching everything else.

## Geometry

- **One object, two components**, each skinned 100% to its own eye bone. One
  object keeps it to a single draw call; separate bones give independent
  rotation. Do not use two objects.
- **The bone head must sit at the eyeball's centre of rotation.** Derive it by
  least-squares sphere fit to the eyeball verts - never by bounding-box centre,
  which is pulled off by the corneal bulge. A wrong pivot makes the eye wobble
  in the socket instead of rotating in it.
- **Budget**: ~190 verts per eye is sufficient for mobile, with the iris region
  subdivided (see Tessellation).
- **Proportions**, expressed as ratios so they scale to any character height:

  | Measure | Ratio | Yemoja (1.8 m) | Human |
  |---|---|---|---|
  | Eyeball diameter / character height | 1.59% | 28.70 mm | ~1.4% |
  | Iris diameter / eyeball diameter | 45% | 12.90 mm | 48.75% |
  | Iris diameter / visible aperture width | 54% | 23.93 mm aperture | ~39% |
  | Pupil diameter / iris diameter | 46% | - | 17-67% (dilation) |

  Stylised characters run a larger iris-to-aperture ratio than life. 54% reads
  as alert; 39% (true human) reads beady on a game character.

## UVs - the rule that matters

**Both eyes may SHARE one UV island. They must never MIRROR it.**

Sharing is free and desirable - one iris texture serves both eyes and the whole
roster. Mirroring is what flips directional content. The two are easy to confuse
because they look identical on any radially symmetric feature.

*Acceptance test:* take a face on the left eye and its mirror partner on the
right. Compute the signed UV area (shoelace) of each. **Same sign = shared,
correct. Opposite sign = mirrored, reject.**

- Iris centred on a defined UV point; limbus at a defined radius fraction.
- Keep the iris centre **on the vertical axis of the island** unless the eyes are
  un-mirrored, because anything off-axis becomes directional.

## Texture

- Dedicated map, 512 or 1024. Do **not** park the eye in the body atlas: it
  wastes body texels, drags the eye into the body's mirroring, and blocks reuse.
- sRGB base colour. Contents: iris with radial striation, pupil, limbus ring,
  sclera with a soft upper lid-shadow gradient.
- **Texel density target: >= 10 texels/mm** on the eyeball surface. Yemoja
  measures 11.83. Below ~8 the pupil edge starts to read soft.
- **Iris >= 120 texels across.** Under that, the pupil circle facets.

### Catchlights

- If the island is shared (normal case): catchlights **on the vertical axis
  only** - 12 o'clock for the key, 6 o'clock for the bounce. On-axis paint is
  mirror-proof by construction.
- Off-axis directional catchlights require un-mirrored, per-eye islands. Only
  worth it for hero/close-up assets.
- Painted catchlights fight the real specular in engine and are wrong whenever
  the stage light moves. Prefer a real specular where lighting is controlled;
  paint one only where it is not.
- Never move a painted highlight as an additive difference: white minus a
  coloured iris is a colour, and adding it over a dark pupil tints the pupil.
  Erase with a donor at the **same radius**, then repaint as an alpha composite.

## Tessellation

Hard-edged features - the pupil circle and the limbus - are the constraint. UV
interpolates affinely *within* a face, so a hard circle crossing coarse faces
renders as a polygon.

**Face size where a hard edge falls must be <= 1/6 of the iris radius, in texels.**
Yemoja: iris radius 79 texels, fine faces 10-13. Beyond radius 90 faces jump to
~35 texels and the limbus would visibly facet.

Enlarging an iris later can push the limbus out of the fine region. Check before
committing to a size.

## Material

- URP Lit, **opaque**. No transparency on mobile - the second layer is an AAA
  feature (see below).
- Roughness 0.05 -> URP `_Smoothness` 0.95. IOR 1.376. Metallic 0.
- Costs **+1 draw call** per character. Accepted, budgeted, not a defect.

## Rig and gaze

- `mixamorig:Eye.L` / `mixamorig:Eye.R`, parented to `mixamorig:Head`, following
  the project rig-naming convention.
- **Bone head at the sphere-fit centre of the eyeball.** Not the bounding-box
  centre - the corneal bulge drags that forward, and a displaced pivot makes the
  eye wobble in the socket instead of rotating in it.
- **Accept L/R asymmetry if the mesh carries a world offset.** Where the body
  object sits off the rig midline, the eyeballs' true centres are asymmetric in
  world space. Place each bone on its own eyeball, not on the rig's midline:
  pivot accuracy beats rig symmetry. Then **exclude the eye bones from
  Symmetrize**, because they will not mirror cleanly.
- **Weight each eyeball 1.0 to its own eye bone**, removing head/neck influence.
  An eyeball should not flex with the neck; any such weight is a smoothing
  artefact.
- Eye bones remain children of Head, so they still inherit head motion.
- Map them explicitly in Unity's Humanoid config. Do not trust the auto-mapper.
- Default aim: converge on a point ~0.9 m ahead at eye level. Yemoja uses 3.78 deg
  total convergence (1.89 deg nasal per eye).

**Usable gaze range is capped by what is baked into the texture.** Painted
catchlights and a painted lid shadow both rotate with the eyeball, and without
modelled eyelids nothing tracks the gaze. With paint in place, keep to roughly
+/-8-10 deg horizontal and +/-5 deg vertical. Wider expressive gaze requires
taking the shadow and catchlights out of the atlas and letting real lighting do
it - which is the AAA path anyway.

**Measure gaze by DIRECTION, never by averaging positions.** Averaging the 3D
positions of iris verts and taking centre->centroid reports a false divergence,
because the UV mapping is distorted and the sampled cap is lopsided. Use either
a point sample at the iris-centre UV, or the area-weighted mean of unit
direction vectors - and confirm the two agree. On a mirrored setup the bias
comes out equal-and-opposite on the two eyes, so a measurement artefact is
indistinguishable from a real fault by symmetry alone.

## Verification checklist

Run before any character is signed off:

1. Sphere-fit residual < 1 mm; bone head within 0.5 mm of the fitted centre.
2. Signed UV area test: corresponding faces on both eyes have the **same sign**.
3. Iris diameter within 45-55% of visible aperture width.
4. Limbus radius inside the finely tessellated region.
5. Two independent gaze estimators agree within 0.3 deg.
6. Catchlight world-space offset directions on the two eyes agree within 5 deg.
7. Unity: `LeftEye`/`RightEye` map to the real eye bones - open Rig > Configure
   and read what they point at, not just that they are non-empty.

## AAA upgrade path (Unreal)

The mobile eye is deliberately a single opaque layer. The AAA copy adds:

- A second transparent cornea layer, high smoothness, near-zero albedo.
- A **concave** iris on the inner layer. A real iris sits behind fluid and the
  cornea magnifies it; dishing the geometry fakes the refraction cheaply.
- Subsurface on the sclera, and blend shapes for pupil dilation.
- Parallax or true refraction on the iris.

None of this belongs in the mobile asset. Because the eye is a separate object
with its own material and UVs, the upgrade touches nothing else on the body.

## Incidents behind this document

- **2026-08-12, Yemoja.** Catchlights mirrored to opposite sides; spotted by eye
  before any measurement. Iris measured undersized against the aperture after a
  socket edit. Both fixed in texture only.
- **Yemoja v2 import.** Unity's Humanoid auto-mapper bound `LeftEye`, `RightEye`
  and `Jaw` to hair bones because no eye or jaw bones existed.
- **2026-07-29.** See `README_rig_conventions.md` - the same mirroring failure
  class, in skin weights rather than paint.
