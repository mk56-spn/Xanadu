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
| Soft edge on one side only: `smoothstep(w, 0, d)` fades outwards from the edge, `filled` fades across it | missing | pulse dots | `sdf.feathered(d, width)` and `s.feathered(width)`: 1 inside, fading to 0 `width` outside |
| Tiles with a corner on the centre (`fract(uv * n)`): ours are centred, so a port shifts by half a tile | unreadable | pulse dots | `pattern.tile(size, anchor.topLeft)`: which point of the tile sits on the centre |
| `sin(x) * 0.5 + 0.5` and `x * 0.5 + 0.25`: remapping by hand | unreadable | pulse dots | `wave(x)` (a 0..1 sine) and `remapped(x, from, to)` in math |
| Contrast on a plain number: `lerp(v, 0.5, 0.3)` | unreadable | pulse dots | maybe `contrasted` on floats too; low value |

## Fixed

| Gap | Seen in | Fix |
|---|---|---|
| Colour changes by hand (`col *= …`) | iq disk | rgb ops: `brighten`, `tint`, `contrast`, … (`colour.slang`) |
| Distance-field colouring by hand (`1 - exp(-6·abs(d))`, `0.8 + 0.2·cos(150·d)`) | iq disk | `sdf.shadowed`, `sdf.contoured`, `sdf.shown` |
| No `distance()` at the shaded pixel | iq disk | `s.distance()` |
