# Writing shaders

How to write a Xanadu effect with the shipped library
(`Assets/Shaders/Library`): what a shader is, the coordinates it gets, and
every type and call the library offers. How the host compiles and runs effects
is in `docs/host.md` §5.5; the house style is `docs/shader-style.md`; worked
examples are in `Tools/Labs/Examples/`.

## Contents

1. [A shader](#1-a-shader)
2. [Where a pixel is](#2-where-a-pixel-is)
3. [Colours](#3-colours)
4. [Shapes](#4-shapes)
5. [Placing shapes](#5-placing-shapes)
6. [Combining shapes](#6-combining-shapes)
7. [Drawing: fill, stroke, glow](#7-drawing-fill-stroke-glow)
8. [Distances by hand: `sdf.`](#8-distances-by-hand-sdf)
9. [Painting a picture in layers](#9-painting-a-picture-in-layers)
10. [Patterns, gradients, easing, curves](#10-patterns-gradients-easing-curves)
11. [Maths and random numbers](#11-maths-and-random-numbers)
12. [Reading textures and the screen](#12-reading-textures-and-the-screen)
13. [Keeping state between frames](#13-keeping-state-between-frames)
14. [Trying things out](#14-trying-things-out)
15. [Pitfalls](#15-pitfalls)

---

## 1. A shader

A level's shader slot (and the lab's playground and editor) holds an
*effect body*: some uniforms and one function, `shade`, which returns the
pixel's colour.

```slang
import xanadu;

[Default(colours.gold)] uniform colour tint;
[Instance] [Default(0.5)] uniform float strength;

float4 shade(ItemInput i)
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
    item.
  - A four-channel value named like a colour (`tint`, `…colour`, `…color`)
    gets a colour picker in the tuners.
- **Which pixel:** `shade` runs once per pixel of the quad being drawn. It
  gets that pixel in `i` (§2) and returns a straight-alpha colour.
- **Numbers:** write whole numbers without `.0` (`Circle(120)`, `sin(time * 2)`);
  Slang makes them floats where floats are wanted. See `docs/shader-style.md`.
- **Named arguments:** Slang has none, so the shader editor shows parameter
  names as hints (`Box(size: …, radius: …)`).

## 2. Where a pixel is

`ItemInput i` describes the pixel being shaded:

| Field | What it is |
|---|---|
| `i.canvas.centred` | **The usual one.** Pixels from the quad's centre, along the quad's own sides (y down). Turning the quad turns it too. Shapes use it by default. |
| `i.canvas.size` | The quad's width and height in pixels. `Box(i.canvas.size)` fills the quad. |
| `i.canvas.aspect` | Width over height. |
| `i.canvas.position`, `.corner`, `.rotation`, `.shear`, `.xAxis`, `.yAxis` | Where the quad lands on the target: its centre, its uv-(0, 0) corner, its turn (radians, clockwise), its lean, and its two sides as vectors. |
| `i.canvas.uvAt(pixel)` | The quad's uv at any target pixel. |
| `i.uv` | 0..1 across the quad. |
| `i.centred`, `i.halfSize` | The older item-unit version of `canvas.centred`, and half the quad's size in item units. |
| `i.colour` | The item's own colour: vertex colour × modulates × its texture. |
| `i.vertexColour` | The vertex colour alone. |
| `i.local` | The pixel in the item's own space. |
| `i.pixel` | The pixel on the target, in pixels. |
| `i.screenUv` | The pixel on the target, 0..1 (for `sampleScreen`). |
| `i.time` | Seconds since the game started. |

The host also keeps the pixel being shaded in `shading` (an `ItemInput`). That
is how shapes draw without being given a point.

**Values the lab provides.** In the playground and editor, using one of these
names declares it for you and the lab fills it every frame:

- `time`: seconds since the lab started
- `mouse`: the pointer, in pixels from the centre like `i.canvas.centred`,
  so `s.move(mouse)` puts a shape under it
- `mouseUv`: the pointer, 0..1 on the stage
- `resolution`: the stage's size in pixels

In a level, use `i.time`.

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
- **`c.withAlpha(a)`:** the same colour with another alpha.
- **`top.over(bottom)`:** lays one colour on another by `top`'s alpha.
- **Building a picture:** for that, `paint` (§9) is usually what you want.

## 4. Shapes

A shape is a value: make one, place it, draw it. Sizes are **full** widths and
heights in pixels (by default); round shapes take a radius.

| Shape | Made with | Notes |
|---|---|---|
| `Circle` | `Circle(radius)` | |
| `Line` | `Line(a, b, width = 0)` | From `a` to `b`, round ends. With width 0 it's a bare line: stroke or glow it. |
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

let petals = Box(float2(40), 6).rotate(0.3).repeat(8, 140).rotate(time);
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
| `a.melt(b, blend)` | both, melted together over `blend` pixels (smooth union) |
| `a.meltAnd(b, blend)` | the overlap, its edge rounded over `blend` (smooth intersection) |
| `a.meltMinus(b, blend)` | `a` with `b` cut out, the cut rounded (smooth cut) |
| `s.repeat(count, radius)` | `count` copies in a ring, `radius` out, each facing outwards |
| `s.onion(thickness)` | just a shell `thickness` wide round the edge |

```slang
let badge = Circle(70).meltMinus(Star6(34).rotated(-time), 4);
let ring = Arc(110, 14, 0.5).or(Box(float2(14, 4), 2).repeat(24, 135));
```

**In a loop:** each combination has its own type, which grows with every
join, so a loop can't keep combining shapes into one variable. Combine their
distances instead (§8).

## 7. Drawing: fill, stroke, glow

Each call turns a shape into a 0..1 coverage at the pixel. Leave the point
out and it's the pixel being shaded (`i.canvas.centred`). Pass one to draw
somewhere else, e.g. `s.fill(i.canvas.centred - mouse)` to follow the
pointer.

| Call | Gives |
|---|---|
| `s.fill(soft = 1)` / `s.fill(p, soft)` | 1 inside, 0 outside, the edge smoothed over `soft` pixels. A big `soft` makes a blurred shadow. |
| `s.stroke(width, soft = 1)` / `s.stroke(p, width, soft)` | 1 on a line `width` wide along the edge |
| `s.glow(reach, falloff = 1)` / `s.glow(p, reach, falloff)` | 1 at the edge, fading to 0 `reach` pixels away on both sides; higher `falloff` is tighter |
| `s.distance(p)` | the signed distance itself: negative inside, in pixels |

## 8. Distances by hand: `sdf.`

When you build a distance yourself (in a loop, from a formula, from
`s.distance(p)`), the same drawing and joining work on the plain number:

| Call | Gives |
|---|---|
| `sdf.fill(d, soft = 1)` | as `fill` |
| `sdf.stroke(d, width, soft = 1)` | as `stroke` |
| `sdf.glow(d, reach, falloff = 1)` | as `glow` |
| `sdf.melt(a, b, blend)` | smooth union (a smooth min) |
| `sdf.meltAnd(a, b, blend)` | smooth intersection (a smooth max) |
| `sdf.meltMinus(a, b, blend)` | `a` with `b` cut out, smoothly |
| `min(a, b)`, `max(a, b)`, `max(a, -b)` | the hard union, intersection and cut |

```slang
float d = 1e5;
for (int b = 0; b < 7; b++)
{
    Circle ball = Circle(50);
    ball.move(float2(sin(time + b), cos(time * 0.7 + b * 2)) * 250);
    d = sdf.melt(d, ball.distance(i.canvas.centred), 60);
}
float blob = sdf.fill(d);
```

## 9. Painting a picture in layers

Start from a colour and paint on top of it, layer by layer. A layer goes
where a shape is (or any 0..1 coverage), with the paint colour's own alpha,
and with a blend mode if you like:

```slang
colour c = colours.midnightBlue;
c.paint(plate.moved(float2(10, 14)).fill(30), colours.black.withAlpha(0.5));   // soft shadow
c.paint(plate, colours.darkSlateBlue);                                          // a shape: its fill
c.paint(plate.stroke(3), colours.slateBlue);                                    // any coverage
c.paint(halo.glow(40), colours.hotPink, blend.Mode.Screen);                     // with a blend mode
return c;
```

- **In place or a copy:** `c.paint(…)` changes `c` and chains
  (`c.paint(a, x).paint(b, y);`); `c.painted(…)` returns a painted copy.
- **What it takes:** the first argument is a shape (painted where it's
  filled) or a coverage number (`s.stroke(3)`, `sdf.fill(d)`, a pattern, a
  gradient).
- **Blend modes** come from `blend.Mode`: `Normal` (the default), `Multiply`,
  `Screen`, `Darken`, `Lighten`, `Overlay`, `HardLight`, `SoftLight`,
  `Difference`, `Exclusion`, `Dodge` and `Burn`.

## 10. Patterns, gradients, easing, curves

**`pattern.`** works in pixels; feed it `i.canvas.centred`.

| Call | Gives |
|---|---|
| `pattern.grid(p, width, spacing)` | grid lines `width` pixels thick, 0..1, with lines through the centre. `spacing` can be a `float2` for oblong cells. |
| `pattern.columns(p, width, spacing)` / `pattern.rows(…)` | lines, 0..1. They sit between multiples of `spacing`, and `width` is half their thickness. |
| `pattern.checker(p, size)` | 0 or 1 squares |
| `pattern.checkerSplit(p, size, levels)` | a checker whose squares split again, 0 to 3 times |
| `pattern.tile(p, size)` | a `Tile`: `.local` is the point within its tile (centred there), `.id` is which tile. Draw one shape at `.local` and it repeats; seed randomness with `.id`. |

**`gradient.`** gives how far along a gradient a point is, 0..1. Feed it to
`lerp` or `paint`.

| Call | Gives |
|---|---|
| `gradient.linear(p, from, to)` | along the line, 0 at `from`, 1 at `to` |
| `gradient.radial(p, centre, edge)` | outwards, 1 on the circle through `edge` |
| `gradient.conical(p, centre, start)` | round the centre, starting towards `start` |
| `gradient.square(p, centre, edge)` | outwards in squares |

**`ease.`** holds Robert Penner's easings: 0..1 in, eased 0..1 out (`back`
and `elastic` overshoot).

- `ease.linear` plus `in`, `out` and `inOut` versions of `Quad`, `Cubic`,
  `Quart`, `Quint`, `Sine`, `Expo`, `Circ`, `Elastic`, `Back` and `Bounce`,
  e.g. `ease.inOutSine(t)`.
- `ease.by(n, t)` picks one by number, for an easing chosen from a uniform.

**`Bezier(start, control1, control2, end)`** is a cubic curve. `.at(t)` gives
its point and `.tangent(t)` its direction, for `t` in 0..1.

## 11. Maths and random numbers

| Name | What it is |
|---|---|
| `PI`, `TAU` | π and 2π |
| `mod(x, y)` | GLSL's mod: the remainder takes the divisor's sign (HLSL's `fmod` truncates) |
| `rotate(p, angle)` | a point turned round the origin, clockwise on screen like shapes |
| `random(seed)` | a repeatable number in 0..1. `seed` is a `float` or `float2`. |
| `random2(seed)`, `random3(seed)` | two or three independent numbers in 0..1 for a `float2` seed |

The random numbers hash the seed's exact bits (PCG). Big, negative or
fractional seeds are as random as small ones, and `random(id)`, `random2(id)`
and `random3(id)` don't repeat each other. Seed with a tile or cell id.

Slang's own functions are all there too: `sin`, `cos`, `atan2`, `lerp`,
`smoothstep`, `step`, `saturate`, `clamp`, `frac`, `floor`, `length`, `dot`,
`normalize`, `pow`, `exp`, `fwidth`, …

## 12. Reading textures and the screen

- **`sampleItem(uv)`** reads the item's own texture (as straight colour).
- **`sampleScreen(screenUv, lod = 0)`** reads what has already been drawn on
  the target, for distortion, blur and glass. Start from `i.screenUv`. To
  move by pixels, divide by the screen size: `i.screenUv + offset / frame.screen`.
  A higher `lod` reads a blurrier copy.

```slang
float2 push = normalize(i.canvas.centred) * sin(length(i.canvas.centred) / 20 - time * 6) * 8;
return sampleScreen(i.screenUv + push / frame.screen);
```

## 13. Keeping state between frames

`shade` runs once per pixel and forgets everything when the frame ends. To keep
something from frame to frame, such as a light that trails the pointer,
declare a `State` struct and an `update`:

```slang
struct State { float2 light; };

void update(inout State s, float dt)
{
    s.light = lerp(s.light, mouse, 1 - exp(-dt * 8));
}

float4 shade(ItemInput i)
{
    float d = distance(i.canvas.centred, state.light);
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
- **Lab only for now.** State built up frame by frame can't be recreated when a
  song seeks, so a level shader with an `update` is an error until levels have
  an answer for that.

See `Tools/Labs/Examples/flashlight.slang`.

## 14. Trying things out

- **Shader playground** (`./run-labs.sh`, first tab): edit
  `Tools/Labs/Playground/playground.slang` in any editor. Every save
  recompiles; errors show in the panel and the last good version stays.
  `--shader=<file>` watches another file, e.g.
  `./run-labs.sh --shader=Tools/Labs/Examples/voronoi.slang`.
- **Shader editor** (second tab): a code editor with
  - live problems under their lines
  - parameter hints
  - completion: after a dot, and while typing a word
  - a slider for every uniform
  - selection, copy and paste, undo and redo.
- **Examples** (`Tools/Labs/Examples/`): plasma, starfield, Voronoi, tunnel,
  metaballs, a shape badge, a screen ripple and a flashlight, each a slot shader.

## 15. Pitfalls

- **Locals start as noise.** `colour c;` isn't black or transparent: it's
  whatever was left over, and the pixel comes out garbled. Give every local a
  value where it's declared (`colour c = colours.transparent;`). The shader
  editor warns about it (XS003).
- **`let` can't change.** `let c = Circle(40); c.rotate(1);` fails. Declare
  with the type (`Circle c = …`) or `var` to use the in-place calls.
- **A chain inside an expression doesn't change the variable.**
  `float a = c.move(o).fill();` is an error: only a chain that is a statement
  of its own changes `c`. Write `c.move(o);` first, or `c.moved(o).fill()` for
  a copy.
- **Multiplying a colour changes its alpha too.** `c * 0.8` fades the colour
  as well as darkening it. Darken with `c.rgb *= 0.8`.
- **Whole-number division:** `1 / 2` is `0`. Write `0.5` or `1.0 / 2`
  (`docs/shader-style.md` XS002).
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
