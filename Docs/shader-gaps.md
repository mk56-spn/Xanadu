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
| Nearest point and distance to the cell edge over a fixed set of points (`map`, `normalmap`: 70 lines of loops) | missing | Ashanoha | maybe `pattern.voronoi(position, points)` over an array: nearest point and edge distance; wait for a second shader that wants it |

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
| Folding space into slices round a point (`pmod`) | Ashanoha | `pattern.slice(count)`: `.local`, `.index`, `.turn` |
| Oblong tiles by hand | Ashanoha | `pattern.tile` with a `float2` size |
| iq's cosine palette by hand | Ashanoha | `colours.palette(t, a, b, c, d)`, `colours.rainbow(t, middle, swing)` |
| Gamma by hand (`pow(rgb, 1.0 / 2.2)`) | Ashanoha | `c.gammaCorrect(g)`, `c.gammaCorrected(g)` |
| `if (a != b)` on vectors: Slang's "expected bool, got vector<bool,2>" | Ashanoha | the editor says to wrap it in `any(…)` / `all(…)`; §17 row |
| A port's own `#define PI` against the library's | Ashanoha | §17 row |
| Is a clip playing now? (`c.started && !c.finished`) | Ashanoha (clips) | `clip.playing` |
| A fade over part of a clip | Ashanoha (clips) | `clip.part(from, to)`, on the timeline too |
| No smoothstep easing | Ashanoha (clips) | `eases.smooth`, `ease.smooth(t)` |
| XS003 flagged a local filled through an `out` parameter | Ashanoha | the lint reads an `out` argument of the code's own function as an assignment |
| The timeline showed 6 rows; later clips scrolled out of sight | Ashanoha (clips) | it grows a row per clip up to 12, then scrolls; the key guide gives way so the values keep room |
| A switch uniform was a 0..1 slider | every port | `uniform bool` gets an ON/OFF switch |
