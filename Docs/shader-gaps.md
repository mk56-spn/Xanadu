# Shader DSL gaps

Found by porting shaders into the DSL (the ports live in
`~/Desktop/shader-ports/`, outside the repo: most are shadertoy's CC BY-NC-SA).
Each gap is one of:

- **missing:** written by hand in the port, should be in the library
- **unreadable:** correct, but reads worse than the original
- **editor:** completion, hover or problems got in the way
- **engine:** the host can't do it yet

A gap seen in more than one shader gets its count bumped; fixed ones say how.

## Open

| Gap | Kind | Seen in | Proposal |
|---|---|---|---|
| Mouse button state (`iMouse.z`) | engine | iq disk | `mouse.z` / a `mouseDown` the lab fills, then the game's pointer |
| A tile's middle: `length(floor(pos) + 0.5 - centre)` became `length(tile.id - 0.5)` with corner tiles | unreadable | diamond ripple | `tile.centre`: where the tile's middle is, in pixels from the origin |
| A repeating ramp: `2.0 * fract(x * 0.5)` | unreadable | diamond ripple | `saw(x, period)` (0..1, then back to 0) and `triangle(x, period)` (0..1..0) |
| Porting conventions worked out each time: GLSL's y up, `mat2` column-major (which way `m * uv` turns), uv over width or height, shadertoy ignoring the alpha | unreadable | diamond ripple | a "From shadertoy" section in shaders.md: each GLSL idiom beside ours |

## Fixed

| Gap | Seen in | Fix |
|---|---|---|
| Colour changes by hand (`col *= …`) | iq disk | rgb ops: `brighten`, `tint`, `contrast`, … (`colour.slang`) |
| Distance-field colouring by hand (`1 - exp(-6·abs(d))`, `0.8 + 0.2·cos(150·d)`) | iq disk | `sdf.shadowed`, `sdf.contoured`, `sdf.shown` |
| No `distance()` at the shaded pixel | iq disk | `s.distance()` |
| Soft edge fading outwards only (`smoothstep(w, 0, d)`) | pulse dots | `s.feathered(width)`, `sdf.feathered(d, width)` |
| Corner-on-centre tiles (`fract(uv * n)`) needed a half-tile shift | pulse dots | `pattern.tile(size, anchor.topLeft)` |
| Remapping by hand (`sin(x) * 0.5 + 0.5`, `x * 0.5 + 0.25`) | pulse dots | `sin01`, `cos01`, `remapped(x, from, to)` |
| Contrast on a plain number (`lerp(v, 0.5, 0.3)`) | pulse dots | `v.contrast(f)`, `v.contrasted(f)` |
