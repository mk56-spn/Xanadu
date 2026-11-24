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
13. [Reading textures, the screen and the depth](#13-reading-textures-the-screen-and-the-depth)
14. [Keeping state between frames](#14-keeping-state-between-frames)
15. [Moving the corners: `deform`](#15-moving-the-corners-deform)
16. [Trying things out](#16-trying-things-out)
17. [Pitfalls](#17-pitfalls)
18. [From shadertoy](#18-from-shadertoy)
19. [Globals: data anyone can read](#19-globals-data-anyone-can-read)
20. [Pools: things the GPU keeps](#20-pools-things-the-gpu-keeps)
21. [Build: passes over buffers](#21-build-passes-over-buffers)
22. [Trace: a compute pass over the pixels](#22-trace-a-compute-pass-over-the-pixels)

---

## 1. A shader

A shader is a **stack of bricks**: a folder under `Assets/Shaders` with one
file per brick, and its path is the shader's name (`Assets/Shaders/Items/orb`).
The bricks run in this order, and each one reads what the bricks above it
declare:

| Brick | File | Entry | Runs |
|---|---|---|---|
| UPDATE | `update.slang` | `void update(inout State s, float dt)` | once a frame on the GPU (§14) |
| STEP | `step.slang` | `void step(inout Item t, uint i, float dt)` | once a frame on every item (§20) |
| BUILD | `build.slang` | `void build()`, calling `[Pass]` functions | its passes, in order, once a frame (§21) |
| DEFORM | `deform.slang` | `ItemVertex deform(ItemVertex v)` | on every corner (§15) |
| SHADE | `shade.slang` | `float4 shade(…)` | on every pixel |

Most shaders are a shade brick alone, and that is what the rest of this
section is about. Blend isn't a brick: it's set where the shader is placed. In
the shader tool the strip over the code shows the stack's bricks in this
order; `+ DEFORM` adds one from a template, and the open one can be removed.

A level's shader slot (and every shade brick) holds an
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
| `i.rest` | The quad at rest, before a `deform` pulled its corners (§15), as a `Canvas`: `i.rest.centred` is this pixel's true place on it. The same as `canvas` without a `deform`. |

The host also keeps the pixel being shaded in `shading` (an `ItemInput`). That
is how shapes draw without being given a point.

**`time`** (seconds since the game started) is global in every shader, lab,
level or shipped, and in any function it calls: no need to declare it or pass it down.

## 3. Colours

```slang
colour c = colours.gold;                 // a colour is a float4: r, g, b, a (straight alpha)
colour(1.0, 0.3, 1.0);                   // rgb alone (three numbers or a float3) is opaque
colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
colour half = colours.black.withAlpha(0.5);
colour hue = colours.hsv(0.6, 0.8, 1);   // hue, saturation, value, each 0..1
colour mixed = top.over(bottom);         // straight-alpha "over"
```

- **The type:** `colour` is another name for `float4`. Swizzles (`.rgb`,
  `.a`), arithmetic, `lerp` and anything else that takes a `float4` work on it
  unchanged.
- **A float3 where a colour is wanted** becomes that colour, opaque:
  `colour c = rgb;`, `c = rgb;` for a name declared `colour`, `return rgb;` in a
  function returning `colour` (`colour shade(…)` too), and an argument for a
  `colour` parameter. Slang can't convert implicitly itself, so the compile
  wraps each one in `colour(…)` (`SlangColourConversions`) and the editor shows
  a `(colour)` hint in front of it. A plain `float4` slot still takes only a
  float4.
- **Named colours:** the 141 X11/web colours live under `colours.`
  (`colours.gold`, `colours.hotPink`, `colours.transparent`, …). The editor
  shows each one's swatch in completion.
- **`colours.hsv(h, s, v)`** and **`colours.hue(h)`** (full saturation and
  value) give opaque colours.
- **`colours.palette(t, a, b, c, d)`:** Inigo Quilez's cosine palette,
  `a + b·cos(TAU·(c·t + d))` per channel; **`colours.rainbow(t, middle = 0.5,
  swing = 0.5)`** is the common one, the channels a third of a turn apart.
- **`c.gammaCorrect(g = 2.2)`** / **`c.gammaCorrected(g)`:** rgb to the power
  1 / g, for code that works in linear light and finishes with
  `pow(rgb, 1.0 / 2.2)`.
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
- **Colour spaces.** Colours are stored as the screen shows them (sRGB).
  Light adds up in linear values and the eye judges "halfway" in Oklab, so:

  | Call | Does |
  |---|---|
  | `c.linearised()` / `c.encoded()` (and `linearise()`, `encode()`) | sRGB to linear light and back, the exact curve |
  | `c.mixed(to, t)` / `c.mix(to, t)` | moves towards `to` through Oklab: no muddy grey between complementary colours, as `lerp` gives |
  | `c.oklab()`, `c.oklch()`, `c.hsv()`, `c.hsl()` | the colour's coordinates in each space (hues in turns, 0..1) |
  | `colours.oklab(L, a, b)`, `colours.oklch(L, C, h)`, `colours.hsl(h, s, l)` | colours from them; `colours.oklch(0.7, 0.15, t)` is a rainbow whose every colour looks as bright |
  | `c.posterized(levels)` | each channel cut to `levels` steps |
  | `c.exposed(stops)` | brighter by `stops` (each doubles the light, in linear light) |
  | `c.warmed(amount)` | warmer (towards orange) or, negative, cooler, as white balance does |
  | `c.dithered(pixel, levels = 2)`, `c.debanded(pixel)` | Bayer-dithered steps; the faintest dither to hide gradient banding (`xanadu.filter`) |
| `c.levelled(black, white, gamma = 1)` | Photoshop's Levels: `black`..`white` stretched to the full range |
| `c.graded(lift, gamma, gain)` | a colourist's three wheels, each a float3 (0, 1, 1 leave it alone) |
| `c.duotoned(shadows, highlights)`, `c.sepiaToned(amount = 1)` | brightness mapped onto two colours; an old photo's brown |
| `c.thresholded(level = 0.5, soft = 0)` | black or white |
| `c.enlivened(amount)` | vibrance: dull colours more colourful, vivid ones left nearly alone |

- **Colour maps** for showing a number 0..1: `colours.viridis(t)` (the
  colour-blind-safe default), `colours.inferno(t)`, `colours.magma(t)`,
  `colours.plasma(t)` (Matt Zucker's fits of matplotlib's) and
  `colours.turbo(t)` (Google's rainbow). `colours.blackbody(kelvin)` is the
  colour of a glowing body: 1900 a candle, 2700 a bulb, 6500 daylight.
- **Tonemapping (`tonemap.`):** light brighter than white brought back into
  0..1 the way film does, instead of clipped flat. Each takes linear light and
  gives a screen colour: `tonemap.aces(hdr)` (the usual pick),
  `tonemap.acesFitted`, `tonemap.agx` (the most natural: bright colours go
  white instead of skewing), `tonemap.filmic` (Hable's), `tonemap.reinhard(hdr)`
  and `tonemap.reinhard(hdr, white)`.

  ```slang
  colour hdr = colours.navy.linearised();
  hdr.rgb += colours.orange.linearised().rgb * glow * 6;   // far past white
  return tonemap.aces(hdr);
  ```
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
| `Ellipse` | `Ellipse(radius)` | `radius.x` to the sides, `radius.y` to the top and bottom; exact distance. |
| `Polygon` | `Polygon(sides, radius)` | Regular: 3 a triangle, 6 a hexagon, …; corners `radius` out. Odd counts point up, even ones sit on a side. |
| `Star` | `Star(points, radius, sharpness = 0.6)` | Any number of tips `radius` out; `sharpness` 0 is the plain polygon, 1 needle spikes. |
| `Rhombus` | `Rhombus(size)` | A diamond filling a `size` box. |
| `Trapezoid` | `Trapezoid(bottom, top, height)` | Widths of its bottom and top. |
| `Spike` | `Spike(width, height)` | An isosceles triangle pointing up. |
| `Pie` | `Pie(radius, angle)` | A slice opening upwards `angle` either side of up (PI the whole disc). |
| `Heart` | `Heart(size)` | Point down. |
| `Vesica` | `Vesica(radius, offset)` | A pointed oval (eye, leaf): two circles overlapping. |
| `Saltire` | `Saltire(size, thickness)` | An X with round ends. |
| `TaperedLine` | `TaperedLine(a, b, widthA, widthB)` | A line thickening or thinning from `a` to `b`: a tadpole, a comet. |
| `Arch` | `Arch(a, control, b, width = 0)` | A quadratic Bézier curve from `a` to `b` pulled towards `control`; exact distance. |
| `Outline` | `Outline o; o.add(p); …` | Any closed outline through up to 32 corners, straight between them. |
| `Polyline` | `Polyline(width).added(p).added(q)…` | An open path through up to 32 points. |
| `Arrow` | `Arrow(span, width, head)` | Pointing right, `span` tail to tip; rotate it. |
| `Squircle` | `Squircle(size, power = 4)` | Between a box and an ellipse: the app-icon square. |
| `Egg` | `Egg(radius, tip = 0)` | Round bottom, pointed top; the bottom circle's middle is its centre. |
| `Digits` | `Digits(value, height, thickness = 0, places = 0)` | A number in seven-segment digits: combo counters, BPM, countdowns. `places` pads with zeros. |

Most distances are Inigo Quilez's (iquilezles.org/articles/distfunctions2d).

**Writing your own shape.** Implement `IShape`: a `placement` field and
`local(p)`, the signed distance (negative inside) in the shape's own space.
Everything below then works on it.

```slang
struct Lens : IShape
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
in effect bodies (level slots), not in shipped `.slang` files, and
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
| `a.xor(b)` | where only one of them is |
| `s.grown(amount)` / `s.shrunk(amount)` | the edge moved out or in: growing rounds sharp corners |
| `s.tiled(spacing)` / `s.tiled(spacing, count)` | copies over a grid, forever or `count` either side of the middle |
| `s.mirrored(x = true, y = false)` | folded about its centre: one half copied over the other |
| `s.elongated(amount)` | pulled apart from its middle, the gap filled straight: a circle into a capsule |
| `s.swirled(turns)` | twisted round its centre, more the further out (no longer an exact distance) |

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
| `s.shadowed(reach)`, `s.contoured(spacing, depth = 0.4)`, `s.shown(reach = 80)` | the `sdf.` looks of the distance at the pixel (§8): a shadow hugging the edge, contour lines, the whole field drawn to see it |
| `s.distance()` / `s.distance(position)` | the signed distance itself: negative inside, in pixels |
| `s.antialiased()` | `filled` with an edge exactly one screen pixel wide, whatever units the shape is drawn in (uv, tiles, warped space) |
| `s.glowed(width, falloff = 1)` | a neon glow, `width / \|d\|`: 1 `width` from the edge, brighter nearer, **not** held to 1; paint it with `blend.add` |

### Measured: working a shape out once

Making, copying and joining shapes is free: a shape is a recipe. Every draw
(`filled`, `stroked`, `c.paint(shape, …)`, a join drawn later) works its
distance out again, every shape inside it included. So a shape painted and then
joined into another is worked out twice. `s.measured()` (or
`s.measured(position)`) works it out once and gives a `Measured`: the distance
at that point.

Shapes and `Measured` are both `IMeasurable`: anything with a distance. The
drawing calls without a position (`filled`, `stroked`, `outlined`, `feathered`,
`haloed`, `shadowed`, `contoured`, `shown`, `distance`) and `c.paint`,
`painted`, `erase` and `erased` are written once for both. Joins (`or`, `and`,
`minus`, `melted`, `meltedAnd`, `meltedMinus`) work either way round: two
shapes give a shape, and a `Measured` on either side gives a `Measured`, the
shape measured at the `Measured`'s point (`Star6(60).minus(hole)` and
`hole.minus(Star6(60))` both measure the star once). `m.hollowed(thickness)`
is the shell, as on a shape.
`m.grown(amount)` and `m.shrunk(amount)` move the edge out or in by `amount`
units (one subtraction on the stored distance, nothing worked out again):
growing rounds sharp corners, shrinking keeps them and loses parts thinner than
twice `amount`. On a distance that is only a bound (melted, joined, repeated)
large amounts bulge or pinch a little.

```slang
let ring = Circle(100).hollowed(10).measured();
let dot = Circle(30).moved(float2(100, 0)).measured();
col.paint(ring, colours.red);
col.paint(dot.and(ring), colours.green);     // a max: nothing worked out again
```

A `Measured` is fixed at its point: it can't be moved, turned or drawn
elsewhere, and `m.measured(position)` gives it back unchanged. Place the shape first and measure it last, and keep the shape itself
for anything drawn at another point (a shadow, a trail, `filled(canvas.centred - mouse)`).

**What a shape costs:** each shape's `/// @cost` tag says what working it out
costs a pixel, a `Circle` being 1 (a `Box` 2, a `Star6` 3, an `Arc` 4;
`repeated` adds 2 and the melted joins 1). The editor adds these up from the
text and warns when a shape is worked out twice at the same point (XS005) or the
whole effect goes over 32 a pixel (XS006); see `Docs/shader-style.md`.

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
| `sdf.antialiased(d)`, `sdf.glowed(d, width, falloff = 1)` | as on shapes (§7) |
| `sdf.xor(a, b)` | where only one is |
| `sdf.bevelled(a, b, size)` | union joined by a 45° bevel instead of a round |
| `sdf.stairs(a, b, size, steps)` | union whose join is filled with `steps` stair steps (Mercury's hg_sdf) |
| `sdf.grooved(a, b, width, depth)` | a groove cut into `a` along `b`'s edge |
| `sdf.banded(d, width)` | 0 and 1 by turns every `width`: rings round any shape |
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
| `Hexagons(size, width)` | honeycomb walls, hexagons `size` across flat to flat, pointing up |
| `Bricks(size, width, offset = 0.5)` | a brick wall's mortar, every other row shifted `offset` of a brick |
| `Truchet(size, width, seed = 0)` | quarter circles in every tile, turned at random, linking into endless winding paths |
| `Dots(spacing, radius, staggered = false)` | a grid of dots; staggered rows make a halftone screen |
| `Waves(wavelength, amplitude, width, spacing = 0)` | sine lines across, of even width; `spacing` 0 for one |
| `Zigzag(wavelength, amplitude, width, spacing = 0)` | zigzag lines across |
| `Cells(size, width, jitter = 1, phase = 0)` | the walls between Voronoi cells, of even width; `phase` (pass `time`) makes them wobble |

More ways to cut space, each giving a `Tile` (`.local`, `.id`, `.centre`) like
`pattern.tile`: `pattern.hex(size)` (hexagons; `pattern.hexDistance(local)`
measures as hexagons do), `pattern.bricks(size, offset = 0.5)` and
`pattern.triangles(size)` (`.id.x` even pointing down, odd pointing up).

**Voronoi cells:** `pattern.cells(size, jitter = 1, phase = 0)` splits space
among points scattered one per tile, each owning what's nearest it, and gives a
`Cell`: `.id` (seed colours with it), `.centre`, `.local` (from the cell's
point), `.nearest` and `.second` (Worley's F1 and F2) and `.edge`, the exact
distance to the cell's wall, so `sdf.stroked(cell.edge, 2)` draws even walls.

### Noise

Every kind of noise is a value, centred then placed: it *is* a gradient
(0..1 everywhere), so `amount`, `coloured`, `c.paint(n, from, to)`, `moved`,
`rotated` and `scaled` all work on it.

| Noise | Is |
|---|---|
| `ValueNoise(size, seed = 0)` | a random level per lattice point, blended: soft and blocky, cheapest |
| `Perlin(size, seed = 0)` | gradient noise: smooth hills and hollows about `size` across |
| `Simplex(size, seed = 0)` | the same on triangles: no grid marks, cheaper per octave |
| `Worley(size, jitter = 1)` | how far the nearest scattered point is: cells; `1 - amount` is bubbles |

Building on one:

- `n.evolved(t)` (or `evolve`) moves it along its third axis, so it changes in
  place where `moved` slides it: `Simplex(200).evolved(time * 0.3)` boils.
- `n.fractal(octaves = 5, gain = 0.5, lacunarity = 2)` stacks layers, each
  finer and fainter (fbm: clouds, terrain); `n.ridged(…)` folds them into sharp
  crests (mountains, veins, lightning); `n.billowed(…)` into puffy billows
  (smoke, fire).
- `n.warped(by, amount)` reads it at points pushed up to `amount` pixels by
  another noise: domain warping, the marble and oil look.
- `n.signed()` gives -1..1 instead of 0..1.

```slang
let clouds = Simplex(220).fractal(5).evolved(time * 0.1).moved(float2(time * 20, 0));
c.paint(clouds, colours.navy, colours.white);
float marble = Perlin(120).warped(Simplex(300).fractal(4), 80).amount();
```

Plain functions under `noise.` take a point in features (no size) and give
0..1, for loops, formulas and ports: `noise.value`, `noise.perlin`,
`noise.simplex` (each on a `float2`, or a `float3` such as `float3(p, time)`),
`noise.fbm(p, octaves, gain)`, `noise.ridged(p, …)`, `noise.warped(p, amount,
octaves)` (Quilez's double warp), `noise.worley(p)`, `noise.curl(p, phase)` (a
swirling flow with no sources or sinks, for particles and smoke) and
`noise.wobble(p)` (two noises -1..1, to shiver a position).

`pattern.slice(count)` folds space into `count` slices round the centre, like
a kaleidoscope: draw one thing at `slice.local` and it repeats in every slice.
The first is centred straight up (or on a `facing` direction you give);
`slice.index` counts them clockwise and `rotate(v, slice.turn)` takes a
direction from the first slice back out. `pattern.slice(position, count)` does
it at any point.

`pattern.tile(size)` cuts the quad into tiles from its centre and gives a
`Tile`: `.local` is the pixel's place in its tile (centred there), `.id` is
which tile. Draw one shape at `.local` and it repeats; seed randomness with
`.id`. `pattern.tile(size, anchor.topLeft)` puts that point of a tile on the
centre instead (a corner, as GLSL's `fract(uv * n)` cuts them); `.local` is
still from the tile's own centre; a `float2` size makes oblong tiles
(`pattern.tile(float2(80, 40))`, or with a place, `pattern.tile(position,
float2(80, 40), anchor.topLeft)`); `.centre` is where the tile's middle is, so
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
- `g.mixed(from, to)` is `coloured` through Oklab, as the eye sees halfway:
  no muddy middle between complementary colours.
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
| `.eased(eases.outBack)` | An easing. `eases.` holds one value per `ease.` function, `eases.smooth` is smoothstep's curve and `eases.step` holds until the very end, then jumps. |
| `.delayed(by)` | Starts later. |
| `.staggered(index, step)` | Starts `index × step` later: per tile, per ring, per copy. |
| `.bounced()` | Plays forward then back, taking twice as long. |
| `.reversed()` | Plays 1 to 0. |
| `.then(length)` | A new clip starting where this one ends. |
| `.part(from, to)` | A new clip over that stretch of this one (fractions of it, 0..1), linear: a fade inside a scene. `smoothstep(0.8, 0.2, scene.linear)` is `scene.part(0.2, 0.8).eased(eases.smooth).mixed(1, 0)`. It shows on the timeline too. |

Read one with `.progress` (eased 0..1, held at 0 before and 1 after),
`.linear` (without the easing), `.mixed(from, to)` (floats, vectors or
colours), `.started`, `.finished` and `.playing` (started, not yet finished:
which scene is on). `.at(t)` is the way out: progress at a
reading of the clock you give it.

**Play keys** (`rhythm.play`, so `import rhythm;` as well as `import xanadu;`).
`keys.` names the game's seven keys: `left`, `right`, `up`,
`down` (F J K L by default) and `action1`, `action2`, `action3` (S Space D),
plus `anyDirection` and `anyAction` for whichever of those went down last.
They follow the player's keybinds. `keys.left.since` is the seconds since that
key was last pressed (1e6 when it never has been), and
`keys.left.pressedWithin(0.1)` asks whether it was just now:

```slang
Clip flash = clock.since(keys.action1).clip(0, 0.35).eased(eases.outQuad);
c.paint(Circle(flash.mixed(40, 160)), colours.white.withAlpha(1 - flash.progress));
```

In a level the presses are the ones the play input reads, while a run is
playing. In the shader editor, click the preview so the code lets go of the keyboard,
then F J K L and S Space D press them. Presses run on real time: scrubbing
the timeline doesn't move them.

**The run** (`rhythm.play`: `import rhythm;`). The game's, not the shader
API's: `rhythm` declares its data as globals (§19) the way anyone would, and
only the game and the shader editor (its stand-ins) declare them, so an effect
that imports it runs only there. Plain values:

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
(`Assets/Shaders/hit_bar`):

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
on the preview is the player (its speed is the velocity), S fakes a FLAWLESS
hit, Space a FLAWLESS+, D a miss (breaking the combo), F J K L a dash that
way; accuracy, combo and score follow those. The timeline is the song, with a
note on every beat: a chart of 64 notes zig-zagging across the preview, and S,
Space or D judges the one on the nearest beat. It starts over each time the
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
`?` but not drawn. A press on a clip's name puts the caret on it. In the
shader tool, whose effects keep the host's time, the timeline follows that
time.

**The engine's own clips (`import xanadu.tween;`).** The game and the UI move
things with the same idea on the CPU: Core's `Clip` (Core/Tweens/Clip.cs) is
this `Clip` in C#, with the same builders (`Eased`, `Delayed`, `Staggered`,
`Bounced`, `Reversed`, `Then`, `Part`), the same readings and the same maths,
on the same clocks (`ClipClock.Seconds` is `clock`, `ClipClock.Beats` is
`clock.beats`); its easings are the same curves as `eases.`. A **tween** is
such a clip playing on a target (an offset, turn, scale or colour, mixed from
one weight to another by the clip's progress, maybe looping). The CPU plays
them for the UI and publishes them as the globals `tweens` and
`tweenTargets`; a shader reads the same ones:

| Read | What |
|---|---|
| `tweenClip(tweens[i])` | Any tween as a plain `Clip`: `.progress`, `.mixed(a, b)`, `.playing` as above. |
| `tweenWeight(t)` | Its weight now: the clip's progress, mixed between its two weights. |
| `tweenPose(target)` | Where a target's moves put it: offsets and turns added, scales and opacity multiplied (`.placed(p, pivot)`, `.opacity`). |
| `tweenColour(target, own)` | Its colour over `own`: colours by priority, each fading over what's beneath. |

Keep the two sides one idea: a change to `Clip` here goes into Core's `Clip`
too, and the other way round.

## 12. Maths and random numbers

| Name | What it is |
|---|---|
| `PI`, `TAU` | π and 2π |
| `mod(x, y)` | GLSL's mod: the remainder takes the divisor's sign (HLSL's `fmod` truncates) |
| `rotate(position, angle)` | a point turned round the origin, clockwise on screen like shapes |
| `p.angleTo(target)` | the angle from `p` to `target`, clockwise on screen from pointing right, as `rotate` and `rotated` take it: `eye.rotated(eye.placement.offset.angleTo(mouse))` turns a shape's right side towards the pointer |
| `sin01(x)`, `cos01(x)` | a sine or cosine from 0 to 1: `sin(x) * 0.5 + 0.5` |
| `saw(x, period = 1)` | a ramp 0 to 1 every `period`, dropping back to 0: `frac(x / period)` |
| `triangle(x, period = 1)` | up 0 to 1 and back down every `period`, 0 at 0 |
| `remapped(x, from, to)` | `x` moved from one range to another in proportion, ranges as `float2(low, high)`, not clamped: `remapped(v, float2(0, 1), float2(0.25, 0.75))` |
| `v.contrast(f)` / `v.contrasted(f)` | a number's distance from 0.5 scaled, as a colour's contrast: `lerp(v, 0.5, 0.3)` is `v.contrasted(0.7)` |
| `random(seed)` | a repeatable number in 0..1. `seed` is a `float` or `float2`. |
| `random2(seed)`, `random3(seed)` | two or three independent numbers in 0..1 for a `float2` seed |
| `random(float3)`, `random3(float3)` | the same for a 3D seed (a cell and a time) |
| `randomDirection(seed)` | a point on the unit circle (`float2` seed) or sphere (`float3` seed), every direction as likely |
| `randomIn(seed, range)`, `randomBits(seed)` | a number in `float2(low, high)`; all 32 random bits, for picking with `%` |
| `unlerp(a, b, x)` | lerp's inverse: how far `x` is from `a` to `b` |
| `linearstep(edge0, edge1, x)` | a straight ramp, held at both ends: smoothstep without the curve |
| `aastep(edge, x)` | `step` smoothed over one screen pixel of `x`: no jaggies in any units |
| `snapped(x, step)`, `wrap(x, low, high)` | the nearest multiple; `x` going round inside a range |
| `damped(current, target, rate, dt)` | exponential smoothing that frame rate doesn't change, for kept state |
| `rotationMatrix(angle)` | the matrix for `rotate`: `mul(m, p)` |
| `polar(p)`, `fromPolar(radius, angle)` | a point as (distance, angle) and back |
| `shaping.` | Quilez's and Schlick's curves: `impulse(x, k)` (a thump), `pulse(x, centre, width)`, `parabola(x, k)`, `pcurve(x, a, b)`, `expStep(x, k)`, `bias(x, b)`, `gain(x, g)`, `almostIdentity`, `sinc(x, k)` (a ringing fade), `smoothFloor(x, k)` (stairs that ease), `spring(t, frequency, damping)` (overshoots and settles) |
| `complex.` | complex numbers as `float2`: `mul`, `div`, `square`, `inverse`, `conjugate`, `exp`, `log`, `pow`; Mandelbrot and Julia sets, log-polar tunnels |

The random numbers hash the seed's exact bits (PCG). Big, negative or
fractional seeds are as random as small ones, and `random(id)`, `random2(id)`
and `random3(id)` don't repeat each other. Seed with a tile or cell id.

Slang's own functions are all there too: `sin`, `cos`, `atan2`, `lerp`,
`smoothstep`, `step`, `saturate`, `clamp`, `frac`, `floor`, `length`, `dot`,
`normalize`, `pow`, `exp`, `fwidth`, …

## 13. Reading textures, the screen and the depth

- **`sampleItem(uv)`** reads the item's own texture (as straight colour).
- **`sampleScreen(screenUv, lod = 0)`** reads what has already been drawn on
  the target, for distortion, blur and glass. Start from `i.screenUv`. To
  move by pixels, divide by the screen size: `i.screenUv + offset / frame.screen`.
  A higher `lod` reads a blurrier copy.
- **`sampleDepth(screenUv)`** reads the frame's depth: where the nearest opaque
  thing drawn there sits in the draw order, 0 nearest to 1 (nothing there).
  Later draws are nearer, one step of 2^-22 each, so `(1 - d) * 4194304` counts
  draws. Both `shade` and `deform` can read it (`v.position / frame.screen` in
  `deform`). It holds the opaque rects, drawn first; an effect's own pixels
  never write it. `update` can't read it.

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

### Filters (`xanadu.filter`)

A picture is something to read colours from: `pictures.screen()` (read it at
`i.screenUv`), `pictures.item()` (at `i.uv`) or `pictures.of(texture)`. Filters
read it several times for one colour; points are its uv, sizes its pixels.

| Call | Gives |
|---|---|
| `p.sampled(uv)` | its colour |
| `p.blurred(uv, radius)` | a blur over about `radius` pixels: a blurrier mip smoothed by a ring of reads, cheap at any radius |
| `p.smeared(uv, direction)` | motion blur along `direction` pixels |
| `p.zoomBlurred(uv, centre, strength)`, `p.spinBlurred(uv, centre, angle)` | light rushing out from `centre`; turning round it |
| `p.sharpened(uv, amount = 1)` | an unsharp mask |
| `p.edges(uv, width = 1)` | how strongly brightness changes here (Sobel): an outline of everything |
| `p.embossed(uv, strength)` | pressed into metal |
| `p.pixelated(uv, block)` | blocks `block` pixels across |
| `p.aberrated(uv, amount)`, `p.split(uv, offset)` | red and blue pulled apart (lens fringes), outwards or along a direction |
| `p.painterly(uv, radius)` | flat painted patches with crisp edges (Kuwahara) |
| `p.bloomed(uv, threshold, radius)` | only what's brighter than `threshold`, blurred: add it back for bloom |

Under `filter.`, on a uv or a pixel (combine with any picture): `vignette(uv,
strength, softness)` and `scanlines(pixel)` to multiply by, `grain(pixel,
amount)` to add, `curved(uv, amount)` (an old curved screen) and `lens(uv,
amount)` (fisheye or pinch) to read at, `inside(uv)` (1 on the picture),
`shockwave(pixel, centre, radius, width, strength)` and `heatHaze(pixel)` (a
push in pixels), `ripple(uv, centre)` and `glitch(uv, amount)` (moved uvs:
torn digital bands), and the dither thresholds
`bayer(pixel)` and `ign(pixel)`.
Print looks: `filter.halftone(pixel, ink, size = 8, angle)` (a dot screen's
coverage for an ink of 0..1) and `filter.hatched(pixel, ink, spacing, width)`
(pen strokes, crossing as it darkens): paint ink by them,
`c.paint(filter.halftone(i.pixel, 1 - c.luminance()), colours.black)`.

```slang
let behind = pictures.screen();
float2 push = filter.shockwave(i.pixel, canvas.position, 400 * pop.progress, 50, 24);
colour c = behind.aberrated(i.screenUv + push / frame.screen, 4);
c.brighten(filter.vignette(i.screenUv, 0.5));
```

## 14. Keeping state between frames

`shade` runs once per pixel and forgets everything when the frame ends. To keep
something from frame to frame, such as a light that trails the pointer, give
the stack an UPDATE brick (`update.slang`) with a `State` struct and an `update`;
`shade` (in `shade.slang`) reads it:

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
- State starts zeroed and may take up to 16 KB. Each material keeps its own.
  A recompile starts it again from zero.
- If you also bind textures, give them `[[vk::binding(n, 2)]]` numbers from
  3 up, with no gaps (0, 1 and 2 are the item, the screen and the depth).
- `update` reads the globals (§19) as `shade` does: `player.`, `notes.` … are
  this frame's there too.
- **Lab only for now.** State runs forward only: it pauses when the song
  pauses and carries on from where it is after a seek. A seek backwards does
  not roll it back, and never will. Levels get kept state once their runtime
  passes it a paused `dt` of 0. Until then a level shader with an `update` is
  an error.
- **The lab-only folder:** effects in `Assets/Shaders/Lab/` may keep state too.
  The shader tool lists and runs them (thumbnail and preview), and the game
  never loads them: its start-up compile skips the folder. Kept state anywhere
  else under `Assets/Shaders` is still an error. `raymarch_tiled` and
  `raymarch_single` there compare culling in `update` with a plain single pass;
  `raymarch_pool` draws the same scene as a pool (§20): STEP keeps each sphere's
  nearest melt neighbours, DEFORM draws the exact box its outline takes on screen,
  and SHADE marches only through that sphere, against it and those, with the
  normal and colour from the smooth min's own weights. In the shader tool's
  preview (2026-11-24, GPU, Release, the tool's own 0.1 ms taken off): single
  about 15.5 ms a frame, tiled 7, pooled 0.15.

See `Tools/Labs/Examples/flashlight.slang`.

## 15. Moving the corners: `deform`

`shade` colours pixels where the shape already is. To move the shape itself
(a flag waving, a squash on the beat, a tilt), give the stack a DEFORM brick
(`deform.slang`) with a `deform`: the effect's own vertex stage. It runs once per corner, before the corner lands, and hands
the corner back moved:

```slang
[Default(18)] uniform float height;

ItemVertex deform(ItemVertex v)
{
    v.position.y += sin(time * 3 + v.uv.x * 9) * height;
    return v;
}

float4 shade(Canvas canvas) { return colours.gold * Circle(120).filled(); }
```

| Field | What it is |
|---|---|
| `v.position` | Where the corner lands on the target, in pixels (y down). Move this. |
| `v.uv` | Its uv: 0..1 across a rect. |
| `v.colour` | Its colour, modulates included. |
| `v.local` | Where it is in the item's own space. |

- Whatever is left in `uv`, `colour` and `local` goes on to `shade`, blended
  across the triangle as usual. `canvas` in `shade` follows the moved corners,
  so shapes drawn with it bend with the mesh, as if painted on it: its
  `centred` runs with uv over the whole mesh in the quad's own size (the same
  as `i.rest.size`), so they stay whole across the triangles. Anything that
  wants the quad as it was before `deform` comes through `i.rest`.
- **At rest:** `i.rest` is the quad as it was before `deform` pulled it, like
  an elastic band before the force. Measure a shape at `i.rest.centred` and it
  stays straight on the target however the mesh bends; the bent mesh still
  decides where anything shows, so it frames the shape. Each shape chooses:

  ```slang
  let bent = Circle(100).measured();                       // painted on the mesh
  let straight = Circle(100).measured(i.rest.centred);     // true on the target
  ```

  The wrapper keeps each corner's position from before `deform` and blends
  it like uv; on the quad at rest that position is the corner plus uv along
  the two sides, which blending keeps true on every bent triangle, so how it
  and uv change from pixel to pixel gives the rest quad exactly. Where a
  triangle is squashed too thin to tell, `i.rest` falls back to `canvas`.
- `deform` sees what `shade` does except textures: `time`, `beat`, every
  global (`keys.`, `player.`, `notes.` … with `import rhythm;`), the depth
  (`sampleDepth`) and the same uniforms and sliders. `sampleItem`,
  `sampleScreen` and the effect's own textures are an error there.
- **It moves corners, not pixels.** A plain rect has four, so it can only
  squash, tilt or move. A bend needs corners inside: put `[Grid(columns,
  rows)]` on `deform` and every rect the effect draws, in a level or the
  editors, is cut into that many cells (up to 256 a side; corners shared).
  Without one, the shader tool's preview still draws a grid 96 cells across
  (96 × 54 in its 16:9) so you can try a bend, but a level item stays four
  corners.
- Kept state isn't there: `state` reads as zeros in `deform`.
- Without a `deform` the host's standard vertex stage draws the effect.

**Ready-made moves** (`xanadu.mesh`), on the corner itself: each a verb that
moves it (`v.wave(…)`) or a copy (`v.waved(…)`), measured from the quad at
rest so they stack in any order:

| Call | Does |
|---|---|
| `v.wave(amplitude, wavelength, phase)` | up and down across it: a flag |
| `v.twist(angle)` | the rim turned round the middle, the middle still: a whirlpool |
| `v.bulge(centre, radius, strength)` | swelled out round a point (negative pinches) |
| `v.squash(amount)` | squash and stretch from the bottom, keeping its area |
| `v.sway(amount)` | the top leaned over on a curve: grass in wind |
| `v.shake(amount, rate)`, `v.jitter(amount, rate)` | all corners jumping together; each on its own (a hand-drawn boil) |
| `v.tilt(angle, distance)` | turned in 3D round its upright middle line, on four corners alone |
| `v.curl(progress, radius)` | curled up from the bottom right like a page turning |

```slang
ItemVertex deform(ItemVertex v)
{
    v.wave(12, 200, time);
    v.squash(0.15 * beats.pulse());   // a thump on every beat
    return v;
}
```

### 3D from the grid: `xanadu.mesh`

The grid's uv says where each corner is on the quad; a **surface** says where
that uv sits in 3D, a **camera** where that lands on screen, and `deform` moves
the corner there. One small sum per corner: 3D at the cost of a
2D effect.

```slang
Sphere ball() { return Sphere(160).turned(float3(0.3, time, 0)); }
Camera camera() { return Camera(900).orbited(0, 0.3); }

ItemVertex deform(ItemVertex v) { return ball().deformed(v, camera()); }

float4 shade(ItemInput i)
{
    if (!mesh.facing()) return colours.transparent;      // its far side
    colour c = colours.navy;
    c.paint(Checker(0.08).filled(i.uv), colours.gold);     // painted on in uv
    return ball().lit(i.uv, camera(), c);
}
```

Space is pixels: x right, y down, z into the screen. `Camera(distance, focal)`
looks along +z from `distance` in front of the origin; by default depth 0 shows
1:1, so `Sphere(160)` is 320 px across. `camera.orbited(yaw, pitch)` swings it
round; `projected(p)`, `depth(p)` and `eye` are there for your own maths. Build
the surface in one function and call it from both stages: `deform` places the
corners, and `shade` asks it again at `i.uv` for the normal.

| Surface | Is (u, v) |
|---|---|
| `Sheet(size)` | a flat card facing the camera |
| `Disc(radius, hole = 0)` | flat and facing up, u round it, v rim to middle: caps, records, floors, washers |
| `Sphere(radius)` | u round it, v pole to pole |
| `Torus(radius, thickness)` | a ring doughnut lying flat |
| `Cylinder(radius, height, top = 1, bottom = 1)` | an open tube; `top` 0 is a cone |
| `profile.lathed()` | any 2D curve (`Bezier`, `Spline`; x out from the axis, y down) spun round: vases, bells, tops |
| `path.extruded(depth)` | a 2D curve stood up as a wall `depth` deep |
| `shape.relief(size, height, bevel)` | any 2D shape raised out of a sheet: a badge, a logo, a button made solid |
| `spine.ribbon(width, twist = 0)` | a strip along any curve, `twist` turns round it |
| `spine.tube(radius, taper = 1)` | a round tube along any curve: pipes, worms, cables, springs |
| `Superquadric(radius, roundness, around)` | a ball squared off or pinched: 1 a sphere, towards 0 a rounded cube, 2 an octahedron, above 2 a star |
| `Patch(size).raised(column, row, offset)` | a bicubic Bézier patch: a sheet bent by a 4 × 4 grid of control points |
| `a.morphed(b, amount)` | one surface turning into another, each uv sliding between them: a sheet folding into a ball |
| `Terrain(size, height, feature, travel)` | rolling fbm hills lying flat; `travel` scrolls them towards you |
| `Ocean(size, height, wavelength, choppiness, wind)` | open water: six Gerstner waves, crests sharp and troughs flat, moving with time; `crestAt(uv)` for foam |
| `s.lumpy(amount, feature, phase)` | any surface pushed along its normal by 3D noise: rocks, blobs |
| `s.waving(amount, waves, phase)` | any surface rippling across u: flags, cloth |

**Curves** (`ICurve3`: `at(t)`, `tangent(t)`) give tubes and ribbons: `Bezier3(a, b,
c, d)`, `Spline3` (a Catmull-Rom path through up to 16 points you `add`, passing
through each) and `Helix(radius, height, turns)`. In 2D, `Spline(width)` is the
same path through points as a shape, and `BezierLine(bezier, width)` draws a cubic
`Bezier`. `Spline`, `BezierLine` and `Polyline` take `tracedTo(progress)`: only
the first part drawn, for a path writing itself on (`s.at(progress)` is the pen
tip; `Learn/write_on.slang`).

Every surface: `moved(float3)`, `turned(float3 angles)` (radians round x, y,
z), `scaled(f)` and their verbs; `at(uv)`, `normalAt(uv)`, `pointAt(uv)`;
`deformed(v, camera)` (or `deformed(v, camera, uv)` for a piece's own uv);
`lit(uv, camera, base, shininess)`, a studio light (key, fill, sheen, rim).
Your own: implement `ISurface` (`placement3` and `local(uv)`, u left to right
and v top to bottom as seen from outside).

- **Light:** `lights.diffuse`, `lights.wrapped`, `lights.specular` (Blinn),
  `lights.rim` and `lights.toon` (cel bands), each 0..1; `lights.matcap(normal,
  toEye, shadow, base, light)` for a stylised ceramic or clay look.
  `camera.fog(p, near, far)` fades with distance.
- **Depth:** `deformed()` and `v.place(p, camera)` set the corner's `v.depth`
  (pixels into the screen), so where a surface overlaps itself (a torus edge-on,
  hills in front of hills) the nearest part wins per pixel. The effect keeps its
  place in the frame's draw order: depth sorts only its own triangles. Left at 0,
  triangles draw in order. `[Translucent]` on `deform` tests the depth but never
  writes it: glows and sparks that blend over each other instead of cutting holes
  in the ones behind. Translucent pixels still blend in grid order, so a
  see-through closed surface hides its far side with `mesh.facing()` (the uv's
  winding on screen, no normal needed); it is also a cheap cull for convex ones.
- **Many from one grid:** `mesh.split(uv, count)` cuts the grid into pieces,
  each its own surface (`Piece`: `.index`, `.uv`, `.seam`). Call it with `v.uv` in
  `deform` and `i.uv` in `shade`, and hide `seam` pixels: one row of cells
  stretched between two pieces. Give each piece whole cells of the `[Grid]`.
- **Seeing the triangles:** `mesh.wireframe(uv, cells)` draws each cell's edges
  and diagonal.
- **Grid density is the silhouette:** lighting is worked out per pixel, so
  surfaces shade smoothly, but their outline is as fine as the grid.
- `Learn/planet.slang` goes from the flat grid to a lit planet in nine steps;
  `Learn/tour_mesh.slang` shows every surface.

### 3D by marching: `xanadu.march`

The other 3D path. A mesh bends a grid to a surface it can write as uv → point;
a march finds a surface it can only measure (how far is p from it?), such as
balls melting into each other. Same `Camera`, same `lights.`, same pixels.

```slang
struct Cloud : IField                       // your field: distance, normal and colour at p
{
    Sample sampled(float3 p)
    {
        Melt melt = Melt(12);                // balls melted over 12 px
        melt.add(Ball(float3(-60, 0, 0), 70), p, colours.gold);
        melt.add(Ball(float3(60, 0, 0), 70), p, colours.tomato);
        return melt.sample;
    }
};

float4 shade(Canvas canvas)
{
    Camera camera = Camera(650);
    Ray ray = camera.ray(canvas.centred);                // through this pixel
    Span inside = Ball(float3(0, 0, 0), 160).entered(ray);
    if (inside.missed)
        return colours.transparent;                     // nothing to march
    Hit hit = ray.marched(Cloud(), inside);
    return hit.found ? hit.lit(camera) : colours.transparent;
}
```

- `Ray`: `camera.ray(pixel)` from the eye through a point on screen; `at(t)`;
  `marched(field, span, steps = 48, close = 0.05)` steps by the field's distance
  and gives a `Hit` (`found`, `t`, `position`, `normal`, `tint`, `lit(camera)`
  with the mesh's studio lights).
- `Ball`: `grown(by)`, `distance(p)`, `entered(ray)`, the `Span` where the ray is
  inside it (`missed` when it passes by; `joined` for two). March only inside a span.
- `Melt`: Quilez's polynomial smooth min, built a ball at a time with `add`. Its
  normal and colour come out of the same weights, so a hit needs no extra
  samples for its normal and a melt shades from one ball's colour to the next's.
- Cost is per pixel and per step, so march only where something is: in
  `deform`, `camera.outlined(ball)` is the exact `Footprint` a ball takes on
  screen (`at(uv)` is a corner) and `camera.depth(ball)` its depth. Each
  thing its own quad, each pixel marching only its neighbours:
  `Assets/Shaders/Lab/raymarch_pool` is the worked example (a pool, §20).

### Particles: `xanadu.particles`

The pieces of a particle system, as plain functions over a particle's place
and velocity (px from the middle, y down; px/s), for a pool's `step` or a BUILD
brick's passes. They read `dt` and `time` themselves.

- **Births:** `particles.onShape(shape, size, u, v)` (POINT, LINE, CIRCLE, DISC,
  BOX, RING) and `particles.launched(local, heading, outward, speed, tangent)`.
- **Moves**, each giving the velocity after the frame: `forced`, `dragged`,
  `flowed` / `carried` (curl noise; `flowAt` is the flow), `attracted`,
  `swirled`, `pushed` (AWAY, TOWARDS, ROUND), `limited`, and `steered` for
  Physarum (it gives a heading from three samples of a field you keep).
- **Walls:** `bounce`, `wrap`, and `pastWalls` to kill past them.
- **Looks:** `particles.cover(look, local, halfLength, radius)` (GLOW, DOT,
  RINGED, SPARK, SQUARE), and `reachOf(look, radius)` for how big its quad is.

```slang
velocity = particles.forced(velocity, float2(0, 400));
velocity = particles.dragged(velocity, 0.5);
particles.bounce(position, velocity, -world / 2, world / 2, bool4(true), 0.5, 0.1);
```

`Lab/particles` runs them all: its `emitterOf` and `move` are the parts to edit.

## 16. Trying things out

- **Shader tool** (`./run.sh shaders`): every effect under `Assets/Shaders`
  in a list; NEW writes `new_shader.slang` (then `new_shader_2`, …) from a
  template and opens it. TRIANGLES outlines every triangle of the preview's
  mesh and UV draws lines where uv is 0 (solid) and 1 (dashed), red for u and
  green for v. Both go through the effect's own vertex stage, so they show
  where `deform` really put the corners and the uv (`CanvasMaterial.Overlay`:
  the code drawn with a library function for shade, `overlayTriangles` with a
  line-filled pipeline, `overlayUvLines`). A code editor with
  - live problems under their lines
  - parameter hints
  - **points to drag:** with the caret on a `float2(x, y)` of two plain numbers
    that is a place (given to `moved`, `Line`, `angleTo`, a `Curve`'s points and
    the like, or to a variable whose name isn't a size or a direction), a dot
    shows where it lands on the preview, in pixels from its centre. Dragging
    it writes whole pixels back into the text, the drag one undo step. Every
    `uniform float2` that reads as a place has a dot too, dragged like its
    sliders. A dot off the preview sits hollow on its edge
    (`Slang/SlangPoints.cs`, `PreviewPoints.cs`)
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
  - a slider for every uniform; a `uniform bool` gets an ON/OFF switch
    (give it `[Default(1)]` to start on: `Default` takes numbers)
  - the clip timeline grows a row per clip up to 12, then scrolls
  - the clips in the code on a timeline under the preview, which drives the
    shader's clock: scrub, play and pause, click a name to jump to it (§11)
  - a bar between the code and the preview that drags to resize them; the
    preview stays 16:9, so effects can be watched at different sizes
  - selection, copy and paste, undo and redo.
- **Examples** (`Tools/Labs/Examples/`): plasma, starfield, Voronoi, tunnel,
  metaballs, a shape badge, a screen ripple, a flashlight and clips, each a slot shader.
- **Walkthroughs and tours** (`Assets/Shaders/Learn/`, open in the shader tool
  as `Learn/…`): effects built in numbered steps, each panel showing the picture
  as it was after that step (glow badge, clouds, stained glass, fire, a beat hit,
  a synthwave stage), and one tour per library area drawing everything in it
  (`tour_shapes`, `tour_noise`, `tour_pattern`, `tour_colour`, `tour_filter`,
  `tour_looks`). `Learn/README.md` lists them.

### Showing your steps: `xanadu.steps`

`steps.split(count)` cuts the quad into panels and points everything that draws
at the pixel being shaded (shapes, gradients, noise, `pattern.tile`) at this
pixel's place in its own panel, so the shader runs unchanged in each. Return
early at each step and every panel shows the picture as it was there:

```slang
Step panel = steps.split(4);
if (panel.gap) return panel.backdrop;
let blob = Circle(60).melted(Box(float2(80)).moved(float2(50, 0)), 20);
if (panel.stopsAt(0)) return panel.marked(blob.shown());   // 1: the distance
colour c = colours.midnightBlue;
c.paint(blob, colours.gold);
if (panel.stopsAt(1)) return panel.marked(c);              // 2: filled
c.paint(blob.haloed(40), colours.gold, blend.add);
return panel.marked(c);                                    // 3: glowing
```

`panel.marked(c)` numbers the panel with dots in its corner (a bar for every
fifth); `panel.from(k)` is true from step `k` on; `panel.canvas` and `panel.uv`
are the panel's own. `steps.slideshow(count, seconds)` shows the same steps one after
another, full size: a slideshow. Name the variable `panel`, not `step`: a
local called `step` hides Slang's `step()`.

### Ready-made looks: `xanadu.looks`

Pieces for level backgrounds and hits, each built from the rest of the library
and small enough to read and change:

| Call | Gives |
|---|---|
| `Lightning(a, b, width, seed, jag = 0.12)` | a jagged bolt (a shape): a new `seed` re-strikes it, `floor(time * 12)` twelve times a second; `haloed` for its glow |
| `Sunburst(rays, fill = 0.5)` | rays out from the centre (a shape) |
| `looks.starfield(position, density, layers, drift)` | twinkling stars in depths, drifting |
| `looks.synthwaveFloor(position, horizon, travel)` | the glowing perspective grid, rolling as `travel` grows |
| `looks.retroSun(position, radius, bands)` | the banded synthwave sun |
| `looks.aurora(position, y)` | swaying aurora curtains |
| `looks.caustics(position, size)` | light rippling on a pool floor |
| `looks.rain(position, density, angle, speed, length)`, `looks.snow(position, density, speed)` | weather |
| `looks.burst(position, age, seed, sparks, reach)` | a firework `age` seconds after it went off: pass `clock.since(events.hit).seconds` |
| `looks.plasma(position, scale)` | the classic plasma |
| `looks.hitRing(position, age, radius, length, width)` | a hit's ring bursting out and fading |
| `looks.confetti(position, age, out tint, seed, count, reach)` | paper slips thrown up, tumbling down; `tint` gives the hue of the piece there |
| `looks.speedLines(position, centre, clear, count, rate)` | anime speed lines rushing in towards `centre` |

`Learn/synthwave_stage.slang` layers them into a level background step by step.

**In time with the music: `beats.`** reads the global `beat` (`every` in beats:
1 each beat, 4 each bar of 4/4): `beats.phase(every)` (0..1 through it),
`beats.index(every)`, `beats.pulse(every, decay)` (a thump on each),
`beats.flash(every, length)`, `beats.alternate(every)` (+1, -1 by turns),
`beats.sway(every)` and `beats.bounce(every, frequency)` (a spring on each).
`dot.scale(1 + 0.2 * beats.pulse())` thumps a shape on every beat.

## 17. Pitfalls

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
- **In the library: no global made from a list of numbers.** The library is
  precompiled to `.slang-module` files, and Slang can't read back a global like
  `static PlayFrame p = { … }` or `static float4 v = float4(1, 2, 3, 4)` that
  another module uses: compiling dies with "Generic type/value shouldn't be
  handled here!". Set such a global from a function instead (`playState` in
  `stage.slang` does). `static const` vectors (`colours.gold`) are fine: the
  precompiler turns them into getters on its own (`SlangModules`).

- **A name the library already uses.** `import xanadu;` brings every library
  shape, noise and namespace into scope, so your own `struct Star`, a global
  `float noise(…)` or a `static const float2 Corners[6]` beside a library
  `Corners` is "ambiguous". Pick another name, or the library's version.
- **In the library: no `let N : int` generics.** Slang compiles a struct
  generic over a whole number (`Points<let N : int>`) but reads `N` as 0 inside
  its methods: loops over it never run. Give it a fixed capacity and a count
  instead (`Outline`, `Polyline`).
- **In the library: a colour parameter's name is global to the float3 fix-up.**
  The fix-up that turns a float3 into a colour (§3) decides by name, from every
  `colour <name>` the library declares, unscoped. Don't name a library
  `colour` parameter something users name plain float4s (`c`, `col`): `hdr`,
  `top`, `bottom` are safe. The same goes for calls: a library function with the
  name and argument count of one taking a colour (`steps.over(count)` against
  `c.over(bottom)`) makes the fix-up give up on both.

## 18. From shadertoy

Porting a GLSL shader: each habit beside ours. Our space is pixels from the
quad's centre, **y down**; shadertoy's `fragCoord` is pixels from the
bottom-left, y up.

| Shadertoy | Here |
|---|---|
| `void mainImage(out vec4 fragColor, in vec2 fragCoord)` | `float4 shade(Canvas canvas)`, returning the colour |
| `iResolution.xy` | `canvas.size` |
| `iTime` | `time` |
| `iMouse` | not there yet |
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
| `#define NAME value` | `static const float NAME = value;`, but drop `PI` and `TAU`: the library has both |
| `a != b`, `a == b` on vectors in a condition | `any(a != b)`, `all(a == b)`: in Slang a vector comparison gives one bool per component |
| `pmod(p, n)`: `atan`, `floor` and a rotate to fold space into n slices | `pattern.slice(p, n)`: `.local`, `.index`, and `.turn` to rotate back |
| `mod(p, size) - size * 0.5` with oblong cells | `pattern.tile(p, size, anchor.topLeft).local` with a `float2` size |
| `cos(float3(0, 2, 4) * PI / 3 + x) * s + m`, `a + b * cos(TAU * (c * t + d))` | `colours.rainbow(x / TAU, m, s)`, `colours.palette(t, a, b, c, d)` |
| `pow(col, vec3(1.0 / 2.2))` | `c.gammaCorrect()` |
| `out` and `inout` parameters, structs | the same in Slang |

## 19. Globals: data anyone can read

A global is read-only data every shader can read, declared by whoever owns
it (Docs/shader-pipeline.md §0). The shader API (`xanadu`) has no rhythm in it:
the game declares its own globals (`rhythm.play`: the play state and the
chart) the same way anyone would.

- **The shader side** is one marker in a library module:

  ```slang
  [Global] public StructuredBuffer<PlayFrame> playFrame;
  ```

  The API gives it its binding in every stage (`shade`, `deform`, `update`);
  never write a `vk::binding` for it. Names are unique across the library.
- **The C# side** declares the same name and writes it (one writer):
  `ShaderGlobals.Declare("playFrame", bytesPerElement, everyFrame)`, then
  `global.Write(...)`, or let `everyFrame` write it once a frame. See
  `Core/Rendering/Rhythm/RhythmGlobals.cs`.
- **Where it isn't declared, it doesn't compile.** An effect that reads a
  global the running host doesn't declare fails with "reads the global `x`,
  which nothing here declares". The game declares the rhythm globals at
  start-up; the shader tool, the labs and the dev tools declare them too, and
  the editor's stand-ins fill them.
- Read-only everywhere. A global with nothing written yet reads as zeros.

## 20. Pools: things the GPU keeps

An effect draws pixels and forgets them. A STEP brick (`step.slang`) keeps a
whole crowd of items on the GPU, moves every one of them each frame, and draws
each one once. Start from `Lab/swarm`:

```slang
// step.slang
import xanadu;

[Default(0, 0)] uniform float2 attractor;

struct Dot { float2 position; float2 velocity; float size; float hue; };

Dot start(uint i)
{
    Dot d;
    d.position = (random2(float2(i, 1)) - 0.5) * float2(1600, 900);
    d.velocity = float2(0, 0);
    d.size = lerp(3, 8, random(float2(i, 2)));
    d.hue = random(float2(i, 3));
    return d;
}

[Pool(10000)]
void step(inout Dot d, uint i, float dt)
{
    d.velocity += normalize(attractor - d.position + 0.001) * 600 * dt;
    d.position += d.velocity * dt;
}
```

```slang
// deform.slang: once per dot, it puts the quad round the one being drawn
ItemVertex deform(ItemVertex v)
{
    float2 offset = (v.uv - 0.5) * item.size;
    v.position = v.rest.position + item.position + offset;
    v.local = offset;
    v.colour = colours.hsv(item.hue, 0.6, 1);
    return v;
}
```

- The item is your own struct. `[Pool(count)]` on `step` says how many,
  written as a number (`[Pool(10000)]`), up to 262,144. Item `i` is the same
  item every frame.
- `start(i)` makes item `i` once, after every compile. Optional: without it
  every item starts zeroed.
- `step(t, i, dt)` runs once a frame for every item, all at once on the GPU;
  what it leaves in `t` is what the next frame's `step` gets.
- The item is drawn once: the quad it's placed on is drawn `count` times, and
  the DEFORM and SHADE bricks read the one being drawn as `item`. A pool
  needs a DEFORM brick, or every item would land on the same quad.
- `step` sees the uniforms, the globals (§19) and `time`; an UPDATE brick's
  `state` too, when the stack has one.
- **Born and dying.** Give `[Pool]` a rate of births (`[Pool(count, rate)]`)
  and write `bool step`: return `false` and the item dies, its slot free.
  Each frame the engine bears exactly `rate` a second (the fraction carries over) into free slots, each
  made by `start(n)`. Here `n` counts births, so every birth differs; `step`'s
  `i` is still the slot. Only live items are drawn. A full pool drops the
  births it has no room for, so make `count` at least `rate` times the
  longest life. Start from `Lab/fountain`:

  ```slang
  Particle start(uint n) { ... }       // birth n

  [Pool(8000, 2500)]                   // 8000 slots, 2500 births a second
  bool step(inout Particle p, uint i, float dt)
  {
      p.age += dt;
      if (p.age >= p.life)
          return false;                // it dies; its slot is free
      ...
      return true;
  }
  ```

- Items never see each other: `step` gets one item. Ease with `dt`
  (`exp(-dt * rate)`), so it feels the same at any frame rate.
- **Lab only for now**, as kept state is (§14): pools run in
  `Assets/Shaders/Lab/` and in the shader tool. Elsewhere a pool is an error.

## 21. Build: passes over buffers

Some work can't be done one item at a time: sorting, building a tree over
everything, counting into cells and then filling them. A BUILD brick
(`build.slang`) keeps **buffers** on the GPU and runs **passes** over them, in
an order you write down, every frame. DEFORM and SHADE read the buffers. Start
from `Lab/build_bars` (small) or `Lab/bvh_march` (a BVH over 262,144 blobs,
rebuilt every frame, and marched):

```slang
// build.slang
import xanadu;

static const uint BARS = 256;

// Two halves: the heights grow wrote, then each smoothing's result in the other half.
[Buffer(2 * BARS)] RWStructuredBuffer<float> heights;

[Options("0|1|2|3|4|5|6|7|8")] [Default(4)] uniform int passes;

[Pass(64, over: BARS)]
void grow(uint i) { heights[i] = 0.5 + 0.35 * sin(i * 0.15 + time * 3); }

[Pass(64, over: BARS)]
void smooth(uint i, uint from)
{
    uint a = from * BARS, b = (1 - from) * BARS;
    heights[b + i] = (heights[a + max(i, 1) - 1] + heights[a + i] + heights[a + min(i + 1, BARS - 1)]) / 3;
}

void build()
{
    grow();
    for (uint k = 0; k < passes; k++)
        smooth(k & 1);
}
```

- **Buffers:** `[Buffer(size)] RWStructuredBuffer<T> name;`, `size` elements
  of any struct of yours. The size is worked out on the CPU, so it is made of
  numbers and `static const`s. `globallycoherent` may go before
  `RWStructuredBuffer` (for atomics that hand work up a tree). Never write a
  binding: the engine places every buffer, and each pass, deform and shade
  binds only the ones it really uses. They start as zeros and keep what they
  hold from frame to frame.
- **Passes:** `[Pass(threads, over: n)] void name(uint i, …)` runs `threads` a
  group, a thread for each `i` below `n` (the rest of the last group turned
  away). `[Pass(threads, groups: g)]` runs `g` whole groups and every thread in
  them: what code with `groupshared` memory and barriers needs. `dispatch.thread`
  is a thread's place in its group and `dispatch.group` its group. `n` and `g`
  are worked out on the CPU too, and may read uniforms (`over: 1024 << 2 * spheres`).
- **`build()`** is the order. It runs on the CPU every frame: each call is a
  pass of its own, so a pass sees everything the passes before it wrote. It
  may hold `for` loops and `if`s over numbers, `static const`s and uniforms; a
  call passes uints to the parameters after `i` (`smooth(k & 1)`).
- Passes see the uniforms, the globals (§19), `time` and an UPDATE brick's
  `state`. UPDATE and STEP run before BUILD and can't read its buffers.
- **Limits:** a pass writes at most 8 buffers, and deform and shade read at
  most 8 storage buffers each, counting the library's globals (4 now), kept
  state and a pool's items. Past that the compile says so: share a buffer
  between arrays (`Lab/bvh_march` keeps two arrays in each half of `keys`).
  A frame runs at most 1,024 passes; a buffer takes at most 256 MB.
- **Drawing many from a build:** declare `[Draws] RWStructuredBuffer<uint> drawn;`
  and have a pass write `drawn[0]`: the stack's quad is drawn that many times
  this frame (the count never comes back to the CPU). DEFORM and SHADE read
  which one they draw as `copy` and place it themselves, as a pool's deform
  places its item. `Lab/particles` draws the field as copy 0, then every trail
  segment, then every live particle, all from one count. Without `[Draws]`
  the quad is drawn once.
- Passes and pool steps read the frame's seconds as `dt` (0 while the clock
  stands still).
- **Lab only for now**, as pools are.

## 22. Trace: a compute pass over the pixels

Some looks need the pixels to work together: a raymarcher whose rays share the
list of what a tile of the screen can see, a blur that loads a block once. A
TRACE brick (`trace.slang`) runs `trace` as compute, a thread a pixel over the
quad, in tiles whose threads share `groupshared` memory and wait for each
other. What it returns lands first; the stack's DEFORM and SHADE draw after it,
over it. Start from `Lab/trace_wave` (small) or `Lab/bvh_march` (tile lists
over a BVH, then a march, then sparks):

```slang
// trace.slang
import xanadu;

groupshared float wave[8];

[Tile(8, 8)]
float4 trace(ItemInput i, Canvas canvas)
{
    // One thread a column loads, everyone reads.
    if (group.thread.y == 0)
        wave[group.thread.x] = sin(time * 2 + (group.place.x * 8 + group.thread.x) * 0.05) * 60;
    GroupMemoryBarrierWithGroupSync();
    float d = canvas.centred.y - wave[group.thread.x];
    traced.depth = d;
    return lerp(colours.midnightBlue, colours.skyBlue, saturate(0.5 - d / 200));
}
```

- **`trace`** takes `Canvas` and `ItemInput` as `shade` does. `i.colour` is
  white (compute reads no item texture) and `local` is uv across the quad.
- **Tiles:** `[Tile(w, h)]` (8 × 8 without one; at most 1,024 threads). Every
  thread of a tile runs `trace`, even past the quad's edge, so a barrier is met
  by all; only those on the quad write. `group` says where a thread is:
  `group.thread` in its tile, `group.place` the tile's place, `group.index` the
  thread as one number, `group.threads` how many, and `group.low`/`group.high`
  the tile's corners on the target in pixels (for working out what the tile sees).
- **Depth:** set `traced.depth`, pixels into the screen as deform's `v.depth`
  (`camera.depth(p) - camera.distance` for a point of a 3D scene). The
  stack's own triangles drawn after it are tested against it, so they hide
  behind it or show in front. A pixel left clear (alpha 0) sets no depth.
  Sparks and glows over it want `[Translucent]` on their deform (§15).
- **Reads:** the uniforms, the globals, `time` and `dt`, kept state, the
  depth (`sampleDepth`), and the BUILD brick's buffers (it may write them too).
  Not the screen or the item's texture.
- Without a SHADE brick the quad is drawn by the trace alone. With one, the
  quad's copies (every one, or as many as a `[Draws]` buffer says) draw over
  it: `Lab/bvh_march` places 6,144 sparks that way.
- The quad must stay uncut: a deform with `[Grid]` can't sit under a trace.
- **Lab only for now**, as pools and builds are.

