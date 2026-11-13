# Writing shaders

How to write a Xanadu effect with the shipped library
(`Assets/Shaders/Library`): what a shader is, the coordinates it gets, and
every type and call the library offers. How the host compiles and runs effects
is in `Docs/host.md` §5.5; the house style is `Docs/shader-style.md`; worked
examples are in `Tools/Labs/Examples/`.

**Naming.** Library calls on a value follow one rule. A verb in the
imperative (`rotate`, `paint`, `erase`) changes the value it's called on and
returns nothing. Its past participle (`rotated`, `painted`, `filled`,
`stroked`) leaves the value alone and returns the result: a changed copy, a
new shape, or a coverage number.

**Centred, then placed.** Anything the library draws starts on the quad's
centre, at the pixel being shaded, so you never pass `canvas.centred` around.
You make it, then place it from there with `moved`, `rotated`, `scaled` or
`anchored`, and draw it with `filled`, `stroked` or `c.paint`:
`c.paint(Grid(1, 40).rotated(0.2), colours.white)`. That is why grids,
lines, checkers and gradients are values you place (§10) rather than
functions of a position. A
form that takes an explicit point (`filled(position)`, `pattern.tile(position,
size)`) is the way out, for a point the library can't know: a warped or
layered position, or a tile's `.local`. New library code follows the same
rule: anything that draws becomes a shape (`IShape`), or else takes the
pixel from `shading.canvas.centred` and keeps the explicit point as an extra
overload.

## Contents

1. [A shader](#1-a-shader)
2. [Where a pixel is](#2-where-a-pixel-is)
3. [Colours](#3-colours)
4. [Shapes](#4-shapes)
5. [Placing shapes](#5-placing-shapes)
6. [Combining shapes](#6-combining-shapes)
7. [Drawing: filled, stroked, haloed](#7-drawing-filled-stroked-haloed)
8. [Distances by hand: `sdf.`](#8-distances-by-hand-sdf)
9. [Painting a picture in layers](#9-painting-a-picture-in-layers)
10. [Patterns, gradients, easing, curves](#10-patterns-gradients-easing-curves)
11. [Animating: clocks, clips and keys](#11-animating-clocks-clips-and-keys)
12. [Maths and random numbers](#12-maths-and-random-numbers)
13. [Reading textures and the screen](#13-reading-textures-and-the-screen)
14. [Keeping state between frames](#14-keeping-state-between-frames)
15. [Trying things out](#15-trying-things-out)
16. [Pitfalls](#16-pitfalls)
17. [From shadertoy](#17-from-shadertoy)

---

## 1. A shader

A level's shader slot (and the lab's playground and editor) holds an
*effect body*: some uniforms and one function, `shade`, which returns the
pixel's colour.

```slang
import xanadu;

[Default(colours.gold)] uniform colour tint;
[Instance] [Default(0.5)] uniform float strength;

float4 shade(Canvas canvas)
{
    colour c = colours.midnightBlue;
    c.paint(Circle(120), tint.withAlpha(strength));
    return c;
}
```

- `import xanadu;` brings in the whole library. Single modules work too
  (`import xanadu.colour;`).
- **Uniforms** are the values a level (or the tuning sliders) sets. Their
  types can be `float`, `float2`, `float3`, `float4`, `colour`, `int` or
  `uint`.
  - `[Default(…)]` gives the starting value. It can name library constants:
    `[Default(colours.gold)]`, `[Default(PI, 2)]`.
  - `[Instance]` marks a value a group's item-shader track may animate per
    item. Motion that belongs to the effect itself is a clip (§11).
  - A four-channel value named like a colour (`tint`, `…colour`, `…color`)
    gets a colour picker in the tuners.
- **Which pixel:** `shade` runs once per pixel of the quad being drawn and
  returns a straight-alpha colour. It takes what it needs about that pixel
  (§2): `Canvas canvas`, `ItemInput i`, both in either order
  (`shade(ItemInput i, Canvas canvas)`), or nothing. Names are yours.
- **Numbers:** write whole numbers without `.0` (`Circle(120)`, `sin(time * 2)`);
  Slang makes them floats where floats are wanted. See `Docs/shader-style.md`.
- **Named arguments:** Slang has none, so the shader editor shows parameter
  names as hints (`Box(size: …, radius: …)`).

## 2. Where a pixel is

`Canvas canvas` is the quad being drawn, and the pixel's place on it:

| Field | What it is |
|---|---|
| `canvas.centred` | **The usual one.** Pixels from the quad's centre, along the quad's own sides (y down). Turning the quad turns it too. Shapes use it by default. |
| `canvas.size` | The quad's width and height in pixels. `Box(canvas.size)` fills the quad. |
| `canvas.aspect` | Width over height. |
| `canvas.position`, `.corner`, `.rotation`, `.shear`, `.xAxis`, `.yAxis` | Where the quad lands on the target: its centre, its uv-(0, 0) corner, its turn (radians, clockwise), its lean, and its two sides as vectors. |
| `canvas.uvAt(pixel)` | The quad's uv at any target pixel. |

`ItemInput i` has the rest (and the canvas again, as `i.canvas`):

| Field | What it is |
|---|---|
| `i.uv` | 0..1 across the quad. |
| `i.colour` | The item's own colour: vertex colour × modulates × its texture. |
| `i.vertexColour` | The vertex colour alone. |
| `i.local` | The pixel in the item's own space. |
| `i.pixel` | The pixel on the target, in pixels. |
| `i.screenUv` | The pixel on the target, 0..1 (for `sampleScreen`). |
| `i.time` | Seconds since the game started (the same as `time`). |

The host also keeps the pixel being shaded in `shading` (an `ItemInput`). That
is how shapes draw without being given a point.

**Values the lab provides.** In the playground and editor, using one of these
names declares it for you and the lab fills it every frame:

- `mouse`: the pointer, in pixels from the centre like `canvas.centred`,
  so `s.move(mouse)` puts a shape under it
- `mouseUv`: the pointer, 0..1 on the stage
- `resolution`: the stage's size in pixels

**`time`** (seconds since the game started) is global in every shader, lab,
level or shipped, and in any function it calls: no need to declare it or pass it down.

## 3. Colours

```slang
colour c = colours.gold;                 // a colour is a float4: r, g, b, a (straight alpha)
colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
colour half = colours.black.withAlpha(0.5);
colour hue = colours.hsv(0.6, 0.8, 1);   // hue, saturation, value, each 0..1
colour mixed = top.over(bottom);         // straight-alpha "over"
```

- **The type:** `colour` is another name for `float4`. Swizzles (`.rgb`,
  `.a`), arithmetic, `lerp` and anything else that takes a `float4` work on it
  unchanged.
- **Named colours:** the 141 X11/web colours live under `colours.`
  (`colours.gold`, `colours.hotPink`, `colours.transparent`, …). The editor
  shows each one's swatch in completion.
- **`colours.hsv(h, s, v)`** and **`colours.hue(h)`** (full saturation and
  value) give opaque colours.
- **`c.withAlpha(a)`:** the same colour with another alpha; **`c.withRgb(rgb)`**
  the same alpha with another rgb.
- **Changing the rgb, keeping the alpha:** each as a verb that changes the
  colour (`c.invert()`) or a past participle that gives a changed copy
  (`c.inverted()`):

  | Change | Copy | Does |
  |---|---|---|
  | `invert()` | `inverted()` | 1 − rgb |
  | `grey()` | `greyed()` | just how bright it looks (`c.luminance()`, Rec. 709) |
  | `brighten(f)` | `brightened(f)` | rgb × f |
  | `saturateBy(f)` | `saturatedBy(f)` | 0 grey, 1 as is, above 1 more vivid (`saturate` is Slang's own) |
  | `contrast(f)` | `contrasted(f)` | 0 flat mid grey, 1 as is, above 1 punchier |
  | `tint(to, t)` | `tinted(to, t)` | rgb towards `to`'s by `t` |
  | `shiftHue(turns)` | `hueShifted(turns)` | hue turned round; 1 is all the way |

  Anything else: `c.rgb` is a plain `float3`, so `c.rgb = pow(c.rgb, 2.0)`.
- **`top.over(bottom)`:** lays one colour on another by `top`'s alpha.
- **Building a picture:** for that, `paint` (§9) is usually what you want.

## 4. Shapes

A shape is a value: make one, place it, draw it. Sizes are **full** widths and
heights in pixels (by default); round shapes take a radius.

| Shape | Made with | Notes |
|---|---|---|
| `Circle` | `Circle(radius)` | |
| `Line` | `Line(a, b, width = 0)` | From `a` to `b`, round ends. With width 0 it's a bare line: draw it with `stroked` or `haloed`. |
| `Box` | `Box(size, radius = 0)` | `radius` rounds the corners. |
| `CornerBox` | `CornerBox(size, corners)` | One radius per corner: (bottom right, top right, bottom left, top left). |
| `ChamferBox` | `ChamferBox(size, chamfer)` | Corners cut at 45°. |
| `Cross` | `Cross(size, radius = 0)` | A plus: `size.x` end to end, arms `size.y` thick. |
| `Arc` | `Arc(radius, thickness, gap = 0)` | A ring with a gap facing down; `gap` is the gap's half angle in radians. |
| `Triangle` | `Triangle(radius)` | Equilateral, pointing up. |
| `Parallelogram` | `Parallelogram(width, height, skew)` | Top shifted right by `skew`. |
| `Star6` | `Star6(radius)` | Six points. |
| `Moon` | `Moon(radius, bite, offset)` | A circle with a bite of radius `bite` taken `offset` to its right. |

Most distances are Inigo Quilez's (iquilezles.org/articles/distfunctions2d).

**Writing your own shape.** Implement `IShape`: a `placement` field and
`local(p)`, the signed distance (negative inside) in the shape's own space.
Everything below then works on it.

```slang
struct Ellipse : IShape
{
    Placement placement = Placement();
    float2 size;
    __init(float2 size) { this.size = size; }
    float local(float2 p) { /* … */ }
}
```

## 5. Placing shapes

Every shape carries its placement: an offset, a turn and a scale, **each round
the shape's own centre**, so the order you apply them in doesn't matter.
Gradients and sprites are placed the same way (all three are `IPlaced`).

| Changes the shape | Returns a changed copy | Does |
|---|---|---|
| `s.move(offset)` | `s.moved(offset)` | moves it by `offset` pixels |
| `s.rotate(angle)` | `s.rotated(angle)` | turns it `angle` radians, clockwise (y down) |
| `s.scale(factor)` | `s.scaled(factor)` | makes it `factor` times bigger |
| `s.anchor(point)` | `s.anchored(point)` | moves it by a point of the quad |

```slang
Circle c = Circle(40);          // declared with its type (or var), so it can change
c.rotate(time);
c.move(float2(-60, 60)).anchor(anchor.topRight);   // in-place calls chain too

let petals = Box(float2(40), 6).rotate(0.3).repeated(8, 140).rotate(time);
let copy = c.moved(float2(10, 0));                  // a changed copy; c stays as it is
```

**Chaining.** Slang itself can't chain in-place calls: it has no references,
so a method can't hand back the value it changed. The host rewrites your
code before Slang sees it, so chains work anyway, and mean what they say:

- On a variable, as a statement of its own: `c.move(a).rotate(b);` becomes
  `c = c.moved(a).rotated(b);`, changing `c`.
- On a new shape or any other temporary: `Box(s).move(o).rotate(t)` becomes
  `Box(s).moved(o).rotated(t)`.

`paint` chains the same way (§9). The `-ed` forms are still there for a
changed copy of a variable that should stay as it is. The rewrite only works
in effect bodies (level slots, the lab), not in shipped `.slang` files, and
it can move error columns a little on the lines it touches.

**Anchor points:** `anchor.centre`, `.top`, `.bottom`, `.left`, `.right`,
`.topLeft`, `.topRight`, `.bottomLeft` and `.bottomRight`. Any `float2` works
too: a fraction of the quad's size from its centre, y down
(`float2(0.25, 0)`). Anchoring puts the shape's **centre** on that point, so a
circle anchored to a corner shows a quarter of itself.

**Orbiting:** placement turns a shape round its own centre. To swing it round
another point, move it by a turned offset: `c.move(rotate(float2(100, 0), time))`.

## 6. Combining shapes

These build a new shape out of others. The new shape can be placed and drawn
like any other.

| Call | Gives |
|---|---|
| `a.or(b)` | both together (union) |
| `a.and(b)` | only where they overlap (intersection) |
| `a.minus(b)` | `a` with `b` cut out |
| `a.melted(b, blend)` | both, melted together over `blend` pixels (smooth union) |
| `a.meltedAnd(b, blend)` | the overlap, its edge rounded over `blend` (smooth intersection) |
| `a.meltedMinus(b, blend)` | `a` with `b` cut out, the cut rounded (smooth cut) |
| `s.repeated(count, radius)` | `count` copies in a ring, `radius` out, each facing outwards |
| `s.hollowed(thickness)` | just a shell `thickness` wide round the edge |

```slang
let badge = Circle(70).meltedMinus(Star6(34).rotated(-time), 4);
let ring = Arc(110, 14, 0.5).or(Box(float2(14, 4), 2).repeated(24, 135));
```

**In a loop:** each combination has its own type, which grows with every
join, so a loop can't keep combining shapes into one variable. Combine their
distances instead (§8).

## 7. Drawing: filled, stroked, haloed

Each call turns a shape into a 0..1 coverage at the pixel. Leave the point
out and it's the pixel being shaded (`canvas.centred`). Pass one to draw
somewhere else, e.g. `s.filled(canvas.centred - mouse)` to follow the
pointer.

| Call | Gives |
|---|---|
| `s.filled(soft = 1)` / `s.filled(position, soft)` | 1 inside, 0 outside, the edge smoothed over `soft` pixels. A big `soft` makes a blurred shadow. |
| `s.stroked(width, soft = 1)` / `s.stroked(position, width, soft)` | 1 on a line `width` wide along the edge |
| `s.feathered(width)` / `s.feathered(position, width)` | 1 inside, fading to 0 `width` pixels **outside** the edge (`filled` fades across it, half in, half out) |
| `s.haloed(reach, falloff = 1)` / `s.haloed(position, reach, falloff)` | 1 at the edge, fading to 0 `reach` pixels away on both sides; higher `falloff` is tighter |
| `s.outlined(width, soft = 1)` / `s.outlined(position, width, soft)` | 1 on a line `width` wide just **inside** the edge, so the shape keeps its size (`stroked` is centred on the edge, half outside) |
| `box.outlined(sides, width, soft = 1)` / `box.outlined(position, sides, width, soft)` | `Box` only: `outlined` on just the chosen sides, joined with `\|`: `side.top \| side.left`, or `side.all`. A chosen side ends at the corner's diagonal where it meets one that isn't |
| `s.distance()` / `s.distance(position)` | the signed distance itself: negative inside, in pixels |

## 8. Distances by hand: `sdf.`

When you build a distance yourself (in a loop, from a formula, from
`s.distance(position)`), the same drawing and joining work on the plain number:

| Call | Gives |
|---|---|
| `sdf.filled(d, soft = 1)` | as `filled` |
| `sdf.stroked(d, width, soft = 1)` | as `stroked` |
| `sdf.haloed(d, reach, falloff = 1)` | as `haloed` |
| `sdf.feathered(d, width)` | as `feathered`: `smoothstep(width, 0, d)` |
| `sdf.shadowed(d, reach)` | a shadow hugging the edge on both sides: 0 at it, 63% back to 1 by `reach` out (`1 - exp(-\|d\| / reach)`); multiply a colour by it |
| `sdf.contoured(d, spacing, depth = 0.4)` | contour lines `spacing` apart: 1 on a line, `1 - depth` between; multiply a colour by it |
| `sdf.shown(d, reach = 80)` | the field drawn as Inigo Quilez draws one (orange out, blue in, the two above, a white edge), all scaled from `reach`: for seeing any distance |
| `sdf.melted(a, b, blend)` | smooth union (a smooth min) |
| `sdf.meltedAnd(a, b, blend)` | smooth intersection (a smooth max) |
| `sdf.meltedMinus(a, b, blend)` | `a` with `b` cut out, smoothly |
| `min(a, b)`, `max(a, b)`, `max(a, -b)` | the hard union, intersection and cut |

```slang
float d = 1e5;
for (int b = 0; b < 7; b++)
{
    Circle ball = Circle(50);
    ball.move(float2(sin(time + b), cos(time * 0.7 + b * 2)) * 250);
    d = sdf.melted(d, ball.distance(canvas.centred), 60);
}
float blob = sdf.filled(d);
```

## 9. Painting a picture in layers

Start from a colour and paint on top of it, layer by layer. A layer goes
where a shape is (or any 0..1 coverage), with the paint colour's own alpha,
and with a blend if you like:

```slang
colour c = colours.midnightBlue;
c.paint(plate.moved(float2(10, 14)).filled(30), colours.black.withAlpha(0.5));   // soft shadow
c.paint(plate, colours.darkSlateBlue);                                          // a shape: its fill
c.paint(plate.stroked(3), colours.slateBlue);                                    // any coverage
c.paint(disc.haloed(40), colours.hotPink, blend.screen);                          // with a blend
c.erase(hole);                                                                  // cut a hole
return c;
```

- **A look:** `c.paint(shape, Look(fill, outline, width))` paints the fill, then
  an outline just inside the edge. Every part of a `Look` starts see-through,
  so `Look(colours.gold)` is a plain fill. Its fields can be set too:

  ```slang
  Look look = Look(colours.darkSlateBlue, colours.white, 2);
  look.sides = side.top | side.bottom;    // a Box outlines only these
  look.soft = 1.5;                        // edge smoothing
  c.paint(box, look);
  ```
- **No colour given:** white. `c.paint(box.outlined(side.top, 2));` paints a white line.
- **In place or a copy:** `c.paint(…)` changes `c` and chains
  (`c.paint(a, x).paint(b, y);`); `c.painted(…)` returns a painted copy.
- **What it takes:** the first argument is a shape (painted where it's
  filled) or a coverage number (`s.stroked(3)`, `sdf.filled(d)`, a pattern, a
  gradient).
- **Blends** are presets under `blend.`: `normal` (the default), `multiply`,
  `screen`, `darken`, `lighten`, `overlay`, `hardLight`, `softLight`,
  `difference`, `exclusion`, `dodge`, `burn` and `add` (light added, for
  glows). Each mixes the colours the
  standard way (W3C compositing, Photoshop's formulas) and lays the paint on
  top by its alpha, so painting never makes anything more see-through. Where
  the picture is see-through there is nothing to blend with, so the paint
  shows as it is.
- **What happens to the alpha** is the other half of a blend; add it to any
  preset:
  - `.behind` lays the paint underneath, so it shows where the picture is
    see-through (partly where it's partly) and never on an opaque pixel:
    `c.paint(grid, colours.white, blend.behind)`, `blend.screen.behind`.
  - `.erasing` multiplies the alpha by the paint's, so a see-through paint
    colour makes the picture see-through there: `blend.erase` is
    `blend.normal.erasing`.
  - `.over` is the default, laying it on top.

  A blend is a `blend.Blend` struct (a `blend.Mode` and a `blend.Alpha`), so
  `blend.Blend(blend.Mode.Screen, blend.Alpha.Behind)` works too, e.g. for a
  mode picked at run time.
- **Cutting holes:** `c.erase(hole);` (or `c.erase(coverage)`, `c.erased(…)`
  for a copy) makes `c` see-through inside the shape, soft edge and all. It is
  `c.paint(hole, colours.transparent, blend.erase);`.
- **The colour formulas on their own** (two `float3`s in, one out) are under
  `blend.colours.`: `blend.colours.overlay(under, over)`,
  `blend.colours.by(blend.Mode.Screen, under, over)`.

## 10. Patterns, gradients, easing, curves

**Patterns are shapes.** `Grid`, `Columns`, `Rows`, `Stripes` and `Checker` sit on the
quad's centre like `Circle` does, in pixels, so `moved`, `rotated`, `scaled`,
`anchored`, `filled`, `stroked` and `c.paint` all work on them.

| Shape | Is |
|---|---|
| `Grid(width, spacing)` | graph paper: lines across and down, `width` thick and `spacing` apart, one of each through the centre. A `float2` spacing gives oblong cells. `c.paint(Grid(1, 40), colours.white)` draws 1px white lines every 40px. |
| `Columns(width, spacing)` / `Rows(width, spacing)` | upright or level lines only, one through the centre. |
| `Stripes(size, fill = 0.5)` | diagonal bands repeating every `size` px across them, each band `fill` of that, one through the centre. `rotated` turns them further. `c.paint(Stripes(24), colours.gold)` draws even gold stripes. |
| `Checker(size, levels = 0)` | squares `size` across, corners meeting at the centre; filled on every other one. `levels` (0 to 3) splits each square into a smaller checker again. |

`pattern.tile(size)` cuts the quad into tiles from its centre and gives a
`Tile`: `.local` is the pixel's place in its tile (centred there), `.id` is
which tile. Draw one shape at `.local` and it repeats; seed randomness with
`.id`. `pattern.tile(size, anchor.topLeft)` puts that point of a tile on the
centre instead (a corner, as GLSL's `fract(uv * n)` cuts them); `.local` is
still from the tile's own centre; `.centre` is where the tile's middle is, so
`length(tile.centre)` is how far out the tile sits. `pattern.tile(position, size)` does the same at any point, for tiles
that move or layer.

**Gradients** are centred, then placed, too: make one, place it with
`moved`, `rotated`, `scaled` or `anchored`, then use it.

| Gradient | Is |
|---|---|
| `Linear(length)` | left (0) to right (1) across `length` pixels, centred on the quad. `rotated(PI / 2)` runs it top to bottom. |
| `Reflected(length)` | a linear gradient mirrored about its middle: 0 on an upright line through the centre, 1 at `length / 2` either side. |
| `Radial(radius)` | out from the centre (0) to a circle `radius` pixels round it (1). A float2 `radius` makes an ellipse. |
| `Focal(radius, focus)` | a radial gradient whose 0 sits at `focus` (pixels from the centre, kept inside the circle) instead of the centre: SVG's fx, fy. |
| `Conical()` | round the centre (conic, angular, sweep): 0 pointing right, clockwise (y down) round to 1. Rotate it to start elsewhere. |
| `Diamond(radius)` | out from the centre in diamonds, to the one whose corners are `radius` away. |
| `Square(radius)` | out from the centre in squares, to the one whose sides are `radius` away; a float2 makes oblongs. |
| `Spiral(pitch, arms = 1)` | round and out at once: a whole turn every `pitch` pixels out, wrapping 0..1; `arms` spirals together. |

Using one:

- `c.paint(g, from, to)` lays it over the colour `c`, `from` fading to `to`,
  with an optional blend: `c.paint(Radial(200), colours.gold, colours.transparent, blend.screen)`.
- `g.coloured(from, to)` gives that colour at the pixel; `g.coloured(a, b, c)`
  and `g.coloured(a, b, c, d)` spread three or four stops evenly.
- `g.amount()` gives how far along it the pixel is, 0..1, for `lerp`,
  `ease.` or anything else.
- All of them take a point first (`g.amount(position)`) for a position the
  library can't know.
- `g.repeated(times)` starts it again past its end (CSS's `repeating-…`), and
  `g.mirrored(times)` runs it back and forth (SVG's `reflect`); either runs it
  `times` over its length: `Radial(40).repeated(1)` is rings 40 px apart.

Plain functions under `gradient.`:

- `gradient.stops(t, a, b, c)` and `(t, a, b, c, d)`: evenly spaced stops for
  any `t`; `gradient.stops(t, float3(at), from, via, to)` puts them at `at`.
- `gradient.corners(topLeft, topRight, bottomLeft, bottomRight)`: a colour per
  corner of the quad, blended across it (or `(uv, …)` for any uv).

**`ease.`** holds Robert Penner's easings: 0..1 in, eased 0..1 out (`back`
and `elastic` overshoot).

- `ease.linear` plus `in`, `out` and `inOut` versions of `Quad`, `Cubic`,
  `Quart`, `Quint`, `Sine`, `Expo`, `Circ`, `Elastic`, `Back` and `Bounce`,
  e.g. `ease.inOutSine(t)`.
- `ease.by(n, t)` picks one by number, for an easing chosen from a uniform.

**`Bezier(start, control1, control2, end)`** is a cubic curve. `.at(t)` gives
its point and `.tangent(t)` its direction, for `t` in 0..1.

## 11. Animating: clocks, clips and keys

`xanadu.anim` makes animation a value, the way shapes are. A **clock** says
where time comes from, a **clip** is a span of time on a clock, and **keys**
are values along a clip. A clip reads its clock by itself, as a shape reads the
pixel, so nothing is passed down. It is all sums on the clock: seeking back,
scrubbing and pausing show exactly the frame they land on, and no kept state
(§14) is needed.

```slang
Clock loop = clock.looped(2.4);
Clip slide = loop.clip(0, 0.5).eased(eases.outCubic);
Clip spin  = slide.then(0.4).eased(eases.inOutSine);

Box card = Box(float2(160, 90), 18);
card.move(float2(slide.mixed(-300, 0), 0));
card.rotate(spin.mixed(0, PI / 2));
```

**Clocks**

| Call | What it is |
|---|---|
| `clock` | Seconds since the game started (the same as `time`). |
| `clock.beats` | Song beats, from the song's timing (BPM changes included). The shader editor counts them at 120 BPM. |
| `clock.since(keys.action1)` | Seconds since a play key was last pressed: a clip on it plays each time the key goes down. |
| `clock.since(events.hit)` | Seconds since a play event last happened (below): a clip on it plays on each one. |
| `clock.since(notes[i])` | Song seconds since a note of the chart was judged (below): 0 as it is hit or missed, 1e6 while it waits. |
| `.looped(length)` | A copy that wraps every `length` seconds (or beats). |
| `.now`, `.count`, `.total` | Where it is inside its loop, how many loops are done, and its reading before any loop. |
| `.clip(start, length)` | A clip on this clock. |

**Clips.** Every builder returns a changed copy, so one clip can seed many:

| Call | What it does |
|---|---|
| `.eased(eases.outBack)` | An easing. `eases.` holds one value per `ease.` function. |
| `.delayed(by)` | Starts later. |
| `.staggered(index, step)` | Starts `index × step` later: per tile, per ring, per copy. |
| `.bounced()` | Plays forward then back, taking twice as long. |
| `.reversed()` | Plays 1 to 0. |
| `.then(length)` | A new clip starting where this one ends. |

Read one with `.progress` (eased 0..1, held at 0 before and 1 after),
`.linear` (without the easing), `.mixed(from, to)` (floats, vectors or
colours), `.started` and `.finished`. `.at(t)` is the way out: progress at a
reading of the clock you give it.

**Play keys.** `keys.` names the game's seven keys: `left`, `right`, `up`,
`down` (S D K L by default) and `action1`, `action2`, `action3` (F Space J),
plus `anyDirection` and `anyAction` for whichever of those went down last.
They follow the player's keybinds. `keys.left.since` is the seconds since that
key was last pressed (1e6 when it never has been), and
`keys.left.pressedWithin(0.1)` asks whether it was just now:

```slang
Clip flash = clock.since(keys.action1).clip(0, 0.35).eased(eases.outQuad);
c.paint(Circle(flash.mixed(40, 160)), colours.white.withAlpha(1 - flash.progress));
```

In a level the presses are the ones the play input reads, while a run is
playing. In the lab, click the preview so the code lets go of the keyboard,
then S D K L and F Space J press them. Presses run on real time: scrubbing
the timeline doesn't move them.

**The run** (`xanadu.play`, part of `import xanadu`). Plain values:

| Value | What it is |
|---|---|
| `player.position` | The player's centre in the quad's pixels from its middle, like `canvas.centred`. `player.screen` is the same on the target. |
| `player.velocity`, `player.speed` | Pixels a second, on the target. |
| `player.onPlayfield` | Where it is on the playfield, 0..1 from the top-left. |
| `player.grounded` / `inAir`, `dashing`, `holding` / `skidding`, `dead` | Its state; `holding` is the HOLD ground mode (F8). |
| `player.lastMove` | The way the last dash or momentum note sent it, unit length. |
| `score.accuracy` (0..1), `score.combo`, `score.maxCombo`, `score.points` (0..1,000,000) | How the run is going. |
| `score.judgement` | The last judgement: `judgements.flawlessPlus` … `judgements.miss`, `judgements.contact`. |
| `song.progress` (0..1), `song.seconds`, `song.beats`, `song.bpm` | The song. |
| `song.untilNote` | Seconds until the next note is due (1e6 when none is left). |
| `contact.bars`, `contact.fill`, `contact.cap` | Contact's three bars: counts, 0..1 full, and the most each holds (5). |

Events, for `clock.since(...)` or read straight off with `.since` and
`.within(seconds)`: `events.hit` (any hit), `miss`, one per judgement
(`flawlessPlus`, `flawless`, `clean`, `fair`, `deficient`, `terrible`),
`judged` (any judgement, misses and failed barriers too), `comboBroken`,
`dash`, `momentum`, `moved` (either), `songStart`, `countdownDone`, `death`,
`barrierPaid`, `barrierFailed` and `barrier` (either). Like keys, they run on
real time.

```slang
Clip pulse = clock.since(events.hit).clip(0, 0.3).eased(eases.outBack);
float near = 1 - saturate(length(player.position) / 400);
c.paint(Circle(pulse.mixed(30, 60)).moved(player.position), colours.gold.withAlpha(near));
```

**The chart.** `notes.` is the whole chart of the run, every note from the
start, with each one's result written in as it is judged. It lives on the GPU
for the whole run, so a shader can look at any note, past or coming:

| Call | What it is |
|---|---|
| `notes.count` | How many notes the chart has (0 with no chart, and in `update`). |
| `notes[i]` | Note `i`, by time. |
| `notes.dueAfter(seconds)` | The first note due at or after then: `notes.dueAfter(song.seconds)` is the next one. |
| `note.time`, `note.until` | Song seconds it is due, and seconds until then (negative once past). |
| `note.kind`, `note.key`, `note.colour` | `noteKinds.hit` … `noteKinds.wall`, the action key it asks for (0..2), its colour. |
| `note.place`, `note.screen`, `note.position` | Where it is placed: playfield units, target pixels, and the quad's pixels from its middle. Groups' moves aren't in these. |
| `note.waiting`, `hit`, `missed`, `judged` | How it went. |
| `note.judgement`, `note.error` | The judgement (`judgements.`), and how early (−) or late (+) it was hit, in milliseconds. |
| `note.sinceJudged` | Song seconds since it was judged; 1e6 while it waits. `clock.since(note)` is a clock on it. |
| `note.judgedScreen`, `note.judgedPosition` | Where it was when judged, groups' moves included. |

A note's clock runs on the song, not real time, so pausing holds it and a
seek shows the frame it lands on. The game's hit bar is one quad per hit,
told its note's index, and everything else comes from here
(`Assets/Shaders/hit_bar.slang`):

```slang
uniform float hit_note;

float4 shade(Canvas canvas)
{
    Note note = notes[int(hit_note + 0.5)];
    Clip life = clock.since(note).clip(0, 0.26);
    if (!note.hit || life.finished)
        return colours.transparent;
    ...
}
```

The timeline draws a clip on a note's clock from each judgement, as it does
`events.judged`, since it can't know which note the shader will pick.

In the shader editor there is no game, so the editor stands in: the pointer
on the preview is the player (its speed is the velocity), F fakes a FLAWLESS
hit, Space a FLAWLESS+, J a miss (breaking the combo), S D K L a dash that
way; accuracy, combo and score follow those. The timeline is the song, with a
note on every beat: a chart of 64 notes zig-zagging across the preview, and F,
Space or J judges the one on the nearest beat. It starts over each time the
timeline goes round. Standing, the ground mode, death and Contact's bars are
sliders under STAND-INS in the values column, shown once the code reads them.
Song start fakes on the timeline going round; countdown, death, momentum and
barriers aren't faked.

**Keys** hold values at points along a clip's progress, up to 8. Each key's
easing shapes the stretch arriving at it; `T` is `float`, `float2`, `float3`
or `colour`:

```slang
Clip trip = clock.looped(3).clip(0.2, 2.2).eased(eases.inOutSine);
Keys<float2> path = Keys<float2>(float2(-180, 60))
    .to(0.5, float2(0, -90), eases.outQuad)
    .to(1, float2(180, 60), eases.inQuad);
dot.move(path.at(trip));
```

**The timeline.** The shader editor reads every `Clip` local out of the code
as it is typed and shows it on a timeline under the preview: a row per clip,
named after it, its span shaped by its easing and repeated round its loop,
keys read at it as diamonds, and a clip staggered by a pixel's value followed
by fading copies. A clip on a play key's press or a play event starts from where on the
timeline it last happened, or says what it is waiting for. Numbers, number locals and plain sums are worked out; a
clip whose start or length comes from a uniform or the pixel is listed with a
`?` but not drawn. In the lab the timeline is the shader's clock: it plays
round the clips' loops, a press on the ruler scrubs (and pauses), the button
on the left plays and pauses, and a press on a clip's name puts the caret on
it. In the shader tool, whose effects keep the host's time, it follows that
time.

## 12. Maths and random numbers

| Name | What it is |
|---|---|
| `PI`, `TAU` | π and 2π |
| `mod(x, y)` | GLSL's mod: the remainder takes the divisor's sign (HLSL's `fmod` truncates) |
| `rotate(position, angle)` | a point turned round the origin, clockwise on screen like shapes |
| `sin01(x)`, `cos01(x)` | a sine or cosine from 0 to 1: `sin(x) * 0.5 + 0.5` |
| `saw(x, period = 1)` | a ramp 0 to 1 every `period`, dropping back to 0: `frac(x / period)` |
| `triangle(x, period = 1)` | up 0 to 1 and back down every `period`, 0 at 0 |
| `remapped(x, from, to)` | `x` moved from one range to another in proportion, ranges as `float2(low, high)`, not clamped: `remapped(v, float2(0, 1), float2(0.25, 0.75))` |
| `v.contrast(f)` / `v.contrasted(f)` | a number's distance from 0.5 scaled, as a colour's contrast: `lerp(v, 0.5, 0.3)` is `v.contrasted(0.7)` |
| `random(seed)` | a repeatable number in 0..1. `seed` is a `float` or `float2`. |
| `random2(seed)`, `random3(seed)` | two or three independent numbers in 0..1 for a `float2` seed |

The random numbers hash the seed's exact bits (PCG). Big, negative or
fractional seeds are as random as small ones, and `random(id)`, `random2(id)`
and `random3(id)` don't repeat each other. Seed with a tile or cell id.

Slang's own functions are all there too: `sin`, `cos`, `atan2`, `lerp`,
`smoothstep`, `step`, `saturate`, `clamp`, `frac`, `floor`, `length`, `dot`,
`normalize`, `pow`, `exp`, `fwidth`, …

## 13. Reading textures and the screen

- **`sampleItem(uv)`** reads the item's own texture (as straight colour).
- **`sampleScreen(screenUv, lod = 0)`** reads what has already been drawn on
  the target, for distortion, blur and glass. Start from `i.screenUv`. To
  move by pixels, divide by the screen size: `i.screenUv + offset / frame.screen`.
  A higher `lod` reads a blurrier copy.

```slang
float2 push = normalize(canvas.centred) * sin(length(canvas.centred) / 20 - time * 6) * 8;
return sampleScreen(i.screenUv + push / frame.screen);
```

- **`Sprite`** (`xanadu.sprite`) is a picture as a value, drawn like a shape:
  one cell of a strip (equal pictures side by side in a texture of your own;
  a lone picture is a strip of one), `size` across, centred, then placed.
  `filled()` is how much of its ink covers the pixel, `haloed(reach)` a soft
  glow round that ink, `sampled()` its own colours; outside its box it is
  see-through, so neighbouring cells never bleed in. `note_background.slang`
  draws its notes this way:

  ```slang
  let note = Sprite(notes, 4, glyph, float2(size)).rotated(tilt).moved(offset);
  c.paint(note.haloed(cell.local, 0.09 * size), colours.gold, blend.add);
  c.paint(note.filled(cell.local), colours.white);
  ```

## 14. Keeping state between frames

`shade` runs once per pixel and forgets everything when the frame ends. To keep
something from frame to frame, such as a light that trails the pointer,
declare a `State` struct and an `update`:

```slang
struct State { float2 light; };

void update(inout State s, float dt)
{
    s.light = lerp(s.light, mouse, 1 - exp(-dt * 8));
}

float4 shade(Canvas canvas)
{
    float d = distance(canvas.centred, state.light);
    return colour(0, 0, 0, smoothstep(100, 140, d));
}
```

- `update` runs **once a frame**, on one GPU thread, before the item draws.
  Every pixel of `shade` then reads the same result as `state`.
- `dt` is the frame's length in seconds. Ease with `1 - exp(-dt * rate)`,
  not a fixed fraction, so it feels the same at any frame rate.
- `update` sees the same uniforms as `shade` (`mouse`, `time`, sliders).
- State starts zeroed and may take up to 1 KB. Each material keeps its own.
  A recompile starts it again from zero.
- If you also bind textures, give them `[[vk::binding(n, 2)]]` numbers from
  2 up, with no gaps.
- **Lab only for now.** State runs forward only: it pauses when the song
  pauses and carries on from where it is after a seek. A seek backwards does
  not roll it back, and never will. Levels get kept state once their runtime
  passes it a paused `dt` of 0. Until then a level shader with an `update` is
  an error.

See `Tools/Labs/Examples/flashlight.slang`.

## 15. Trying things out

- **Shader playground** (`./run.sh labs`, first tab): edit
  `Tools/Labs/Playground/playground.slang` in any editor. Every save
  recompiles; errors show in the panel and the last good version stays.
  `--shader=<file>` watches another file, e.g.
  `./run.sh labs --shader=Tools/Labs/Examples/voronoi.slang`.
- **Shader editor** (second tab): a code editor with
  - live problems under their lines
  - parameter hints
  - completion follows a few rules, so a new wish is a new rule (or a tag in
    the library), never a special case (`Slang/SlangCompletions*.cs`; each rule
    is a row in `SlangCompletionsTests.Rules`):
    - **after a dot:** the scope's members, its interfaces' and a vector's
      components
    - **while typing a word:** the code's names, what the host provides, the
      library's top-level names, Slang's types, intrinsics and keywords
    - **families:** every interface is a family of the structs that implement
      it. Typing its word from three letters (`sha` → shapes, `gra` →
      gradients) or its name (`IGr`) lists them all first, written in as
      `Linear()` with the caret between the brackets
    - **what the value wants:** where a value starts (after `=`, `(`, `,`,
      `return`, an operator), what makes the wanted type comes up at once: the
      named colours (as `colours.name`) for a colour, the family for an
      interface, the struct for a struct. The wanted type comes from what is
      assigned to (`colour c = `, `t.colour = `), a `[Default(` uniform, or the
      parameter of the call being written (`c.paint(` wants a shape or a
      gradient; its next argument a colour). After those, every family whose
      interface's `@fits` names the type: gradients for a colour (their
      `coloured()`), shapes and gradients for a float. Anything unknown gets the
      shapes, then every other family. Too much beats nothing
    - **snippets:** `shade` and `update` whole at the top level (`update` with a
      `State` if there is none yet); `if`, `for`, `uniform` whole at a line's
      start
    - **ranking:** snippets, then what the value wants, then what fits it, then
      families, then the rest; within each, prefix before humps (`oB` →
      `outBounce`) before inside (from three letters); the code's names first,
      then alphabetical. The list only goes once the word is complete and
      nothing longer matches (`Box` still shows `ChamferBox`, `CornerBox`)
    - **tags** in the library: `/// @hidden` keeps a name out (host plumbing
      like `input`); `/// @fits float colour` on an interface offers its family
      where those types are wanted
  - hovering says what a thing is and what it returns: functions and methods
    with their parameters and result (`rotated` on a Circle returns a Circle),
    a struct's constructor, the code's own functions, `let`s built from a type
  - F2 on a name the code declares renames it everywhere it means the same
    thing (a local only in its function, a field at every `.field`): type the
    new name, Enter keeps it, Esc puts the old one back
  - a slider for every uniform
  - the clips in the code on a timeline under the preview, which drives the
    shader's clock: scrub, play and pause, click a name to jump to it (§11)
  - a bar between the code and the preview that drags to resize them; the
    preview stays 16:9, so effects can be watched at different sizes
  - selection, copy and paste, undo and redo.
- **Examples** (`Tools/Labs/Examples/`): plasma, starfield, Voronoi, tunnel,
  metaballs, a shape badge, a screen ripple, a flashlight and clips, each a slot shader.

## 16. Pitfalls

- **Locals start as noise.** `colour c;` isn't black or transparent: it's
  whatever was left over, and the pixel comes out garbled. Give every local a
  value where it's declared (`colour c = colours.transparent;`). The shader
  editor warns about it (XS003).
- **`let` can't change.** `let c = Circle(40); c.rotate(1);` fails. Declare
  with the type (`Circle c = …`) or `var` to use the in-place calls.
- **A chain inside an expression doesn't change the variable.**
  `float a = c.move(o).filled();` is an error: only a chain that is a statement
  of its own changes `c`. Write `c.move(o);` first, or `c.moved(o).filled()` for
  a copy.
- **Multiplying a colour changes its alpha too.** `c * 0.8` fades the colour
  as well as darkening it. Darken with `c.rgb *= 0.8`.
- **Whole-number division:** `1 / 2` is `0`. Write `0.5` or `1.0 / 2`
  (`Docs/shader-style.md` XS002).
- **The `atan2` seam.** An angle from `atan2` jumps from π to −π on the left,
  and anything smoothed with `fwidth` of it shows a line there. Take the width
  from a copy that wraps elsewhere:
  `min(fwidth(a), fwidth(frac(a + 0.5)))` (see `tunnel.slang`).
- **A variable named `colour`** is legal but hides the type for the rest of
  the function.
- **`anchor` isn't for the mouse.** Anchors are fractions of the quad;
  `mouse` is pixels, so `anchor(mouse)` throws the shape off the stage. Use
  `move(mouse)`. The shader editor warns about it (XS004).
- **A shape's anchor follows the quad.** `anchor(…)` reads the quad's size when
  it's called, so call it inside `shade`, not at file level.

## 17. From shadertoy

Porting a GLSL shader: each habit beside ours. Our space is pixels from the
quad's centre, **y down**; shadertoy's `fragCoord` is pixels from the
bottom-left, y up.

| Shadertoy | Here |
|---|---|
| `void mainImage(out vec4 fragColor, in vec2 fragCoord)` | `float4 shade(Canvas canvas)`, returning the colour |
| `iResolution.xy` | `canvas.size` |
| `iTime` | `time` |
| `iMouse.xy` | `mouse` in the lab (pixels from the centre, like `canvas.centred`); the button (`iMouse.z`) isn't there yet |
| `fragCoord / iResolution.xy` | `i.uv`, but y runs down: `float2(i.uv.x, 1 - i.uv.y)` for his |
| `(fragCoord - 0.5 * iResolution.xy) / iResolution.y` | `canvas.centred / canvas.size.y`, y down (negate `.y` where it matters) |
| `(2.0 * fragCoord - iResolution.xy) / min(iResolution.x, iResolution.y)` | `canvas.centred * (2.0 / min(canvas.size.x, canvas.size.y))`, y down |
| `vec2`, `vec3`, `mat2` | `float2`, `float3`, `float2x2` |
| `mix`, `fract`, `atan(y, x)` | `lerp`, `frac`, `atan2(y, x)` (one-argument `atan` is the same) |
| `mod(x, y)` | `mod(x, y)`, the library's GLSL one; Slang's `fmod` truncates instead |
| `mat2(c, -s, s, c) * p` with `c, s` of `a` | `rotate(p, a)`: the same picture once y is flipped. `p * mat2(…)` (vector first) is `rotate(p, -a)` |
| `fract(uv * n) - 0.5` | `pattern.tile(size, anchor.topLeft).local / size`, `size` the tile in pixels; `floor(uv * n)` is `tile.id` shifted, `floor(uv * n) + 0.5` is `tile.centre / size` |
| `sin(x) * 0.5 + 0.5`, `fract(x / p)` | `sin01(x)`, `saw(x, p)`; `triangle(x, p)` for up and down |
| `a * k + b` to move a range | `remapped(a, float2(from), float2(to))` |
| `length(p) - r` and friends | `Circle(r).distance(p)`, the shapes (§4) |
| `smoothstep(w, 0.0, d)`, `1.0 - smoothstep(-w, w, d)` | `s.feathered(w)`, `s.filled(2 * w)` |
| `col *= k`, `mix(col, vec3(1), t)` | `col.brighten(k)`, `col.tint(colours.white, t)` (§3) |
| `fragColor.a` | shadertoy shows rgb and ignores alpha; our stage blends by it, so return alpha 1 to look the same |
| `#define NAME value` | `static const float NAME = value;` |
| `out` and `inout` parameters, structs | the same in Slang |
