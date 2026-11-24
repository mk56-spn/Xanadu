# Learn

Shaders for learning the xanadu library (`Docs/shaders.md`), built only from
library calls. Open any of them in the shader tool (`./run.sh shaders`, then
`Learn/…` in the list). Each is a level shader slot, so it also pastes into a
level.

## Walkthroughs

Each shows an effect built in numbered steps (`xanadu.steps`): every panel is the
picture as it was after that step, numbered with dots in its corner. Change a
step's numbers and only that panel and the ones after it move.

| File | Builds |
|---|---|
| `glow_badge` | a shape's distance → filled → outlined → glowing → a ring of copies → a beat pulse |
| `clouds` | one noise → fbm → warped → cut by cover → coloured → lit from above |
| `stained_glass` | Worley distance → Voronoi cells → lead walls → thickness → grain → light sweeping through |
| `fire` | billowed noise → rising → a swaying teardrop → temperature → inferno colours → HDR and tonemapping |
| `beat_hit` | the screen behind → a shockwave → colour fringes → a flash → vignette → zoom blur and grain |
| `planet` | in 3D (`xanadu.mesh`): the flat grid → bent → a cylinder → a sphere → far side hidden → lit → shiny → painted → mountains, clouds and city lights |
| `beat_tunnel` | a tunnel of real triangles you fly down: a cylinder seen from inside, rings rushing past and flashing on the beat |
| `ocean` | open water: the flat grid → Gerstner waves → lit → water (Fresnel sky, sun glint, foam, haze) |
| `write_on` | a path drawing itself on: points → straight lines → a spline through them → traced part way → a glowing pen tip → looping |
| `fold_on_beat` | a card that snaps into a ball on every beat and eases back flat (`morphed`, `beats.`): play the timeline |
| `synthwave_stage` | sky → stars → sun → the grid floor → mountains → a pulse on the beat (`xanadu.looks`) |

## Tours

One per library area, a panel for each thing in it:

| File | Shows |
|---|---|
| `tour_shapes` | every shape and join as its distance field (`sdf.shown`) |
| `tour_noise` | value, Perlin, simplex, Worley, fbm, ridged, billowed, warped, cells, curl |
| `tour_pattern` | hexagons, Truchet, dots, waves, zigzag, bricks, triangles, shaping curves, a Julia set, a log-polar warp |
| `tour_colour` | hsv against Oklch, lerp against Oklab mixing, colour maps, blackbody, the tonemaps |
| `tour_filter` | every filter in `xanadu.filter`, reading the shader tool's own code |
| `tour_looks` | every ready-made piece in `xanadu.looks` |
| `tour_extras` | grading, halftone and hatching, beats, springs, digits, arrow, squircle, egg, Oklab gradients |
| `tour_mesh` | every surface in `xanadu.mesh` from one grid: sphere, torus, cylinder, cone, waving sheet, lathe, ribbon, tube, terrain, lumpy, cel-shaded |
| `tour_mesh_more` | tubes and ribbons along a spline and a helix, superquadrics, a Bézier patch, morphs, a 2D spline |
| `tour_bends` | the 2D corner moves: wave, twist, bulge, squash, sway, shake, jitter, tilt, page curl |
| `tour_mesh_2d` | 2D made 3D: shapes raised as reliefs (star, heart, live digits, Truchet, hexagons, arrow), a spline lathed into a goblet, a spline extruded into a wall |
