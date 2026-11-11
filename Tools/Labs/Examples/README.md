# Shader examples

Common 2D effects, the kind found all over Shadertoy, written fresh against
the xanadu shader library (no code copied); the library itself is described in
`Docs/shaders.md`. Each is a level shader slot, so
anything here pastes into a level. Open one in the playground:

    ./run.sh labs --shader=Tools/Labs/Examples/plasma.slang

| File | What it shows |
|---|---|
| `plasma.slang` | sines summed into a field, a cosine palette |
| `starfield.slang` | layered grids of random stars, depth by zoom and fade |
| `voronoi.slang` | nearest of the wandering points in the 3×3 cells round a pixel, borders |
| `tunnel.slang` | polar coordinates as a texture lookup, the atan2 seam handled |
| `metaballs.slang` | a loop of circles joined with `sdf.melted`, filled, outlined and haloed from one distance |
| `badge.slang` | layering library shapes: placement, booleans, repeated, stroked |
| `ripple.slang` | reading what is already drawn (`sampleScreen`) through a warp |
