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
| A tile's middle (`floor(pos) + 0.5`) came out as `tile.id - 0.5` | diamond ripple | `tile.centre` |
| A repeating ramp by hand (`2.0 * fract(x * 0.5)`) | diamond ripple | `saw(x, period)`, `triangle(x, period)` |
| Porting conventions worked out each time (y up, `mat2` order, uv scales, alpha) | diamond ripple | shaders.md §17 "From shadertoy" |
