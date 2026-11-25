# Shader style rules

The house style for Xanadu shaders (level shader slots, the shader tool, the
shader editor, shipped effects). Each rule has an id so the shader editor can
show it as a warning later and a level can say which ones it ignores.

**Status:** XS002 to XS009 are checked: the shader editor shows them as warnings
(`Slang/SlangLint.cs`, the shape-cost rules in `Slang/SlangLint.Cost.cs`). The rest are collected here as they come
up, for the editor to check when that is wanted.

Each rule says what it flags, why, how to fix it, and how hard it complains:

- **style**: a matter of taste. Off unless asked for, never blocks anything.
- **warning**: probably a mistake. Shown by default.

---

## XS001 · Whole numbers without `.0` (style)

**Flags** a number written with an empty fraction: `80.0`, `1.0`, `2.`.

**Why** Slang turns a whole number into a float wherever a float is expected
(`Box(float2(80), 16)`, `sin(time * 2)`, `[Default(40)]`), so the `.0` is noise.
Numbers with a real fraction keep it (`0.5`, `1.25`).

**Fix** Drop the `.0`: `Circle(120.0 + 20.0 * t)` → `Circle(120 + 20 * t)`.

**Not flagged**
- A division where the `.0` is what makes it a float division (XS002).
- A call that would become ambiguous with only whole numbers, where a function
  has both an int and a float version (`mod(5, 3)`): leave one `.0` in.

```slang
// flagged
let box = Box(float2(80.0), 16.0).moved(float2(140.0, 0.0));
return float4(colour, 1.0);

// fine
let box = Box(float2(80), 16).moved(float2(140, 0));
return float4(colour, 1);
```

---

## XS002 · Whole-number division (warning) · checked

**Flags** a division whose two sides are both whole numbers or int values, in a
place that wants a float: `1 / 2`, `count / 3` with `int count`.

**Why** The division happens before the conversion to float, so it drops the
fraction: `1 / 2` is `0`, and `x * (1 / 2)` is quietly `x * 0`. This is the one
place where XS001's whole numbers bite.

**Fix** Make one side a float: `1.0 / 2`, `float(count) / 3`, or write the
result (`0.5`).

**Not flagged** where an int is wanted: an index (`points[n / 2]`), a for's
header, an int being given a value (`int mid = n / 2`), or what an int function
returns. Only plain names and numbers count as sides: `s.count / 2` and
`(a + b) / 2` aren't checked.

```slang
// flagged
float half = 1 / 2;          // 0
float share = count / 3;     // whole thirds only

// fine
float half = 0.5;
float share = float(count) / 3;
float scaled = x / 2;        // x is a float, so this is a float division
```

---

## XS003 · A local read before it has a value (warning) · checked

**Flags** a local declared without a value whose first use reads it:
`colour c;` followed by `c.paint(…)`, `return c;` or `c + 1`.

**Why** Slang doesn't zero locals. Until something is assigned, a local holds
whatever was left in that register, so the pixel comes out as noise that
changes from frame to frame. Slang only warns about this in a late pass whose
messages the host doesn't see when the compile succeeds, so the editor checks
it itself.

**Fix** Give it a value where it's declared:
`colour c = colours.transparent;`, `float d = 1e5;`.

**Not flagged**
- A first use that assigns it or one of its fields: `float v; v = 1;`,
  `Tile t; t.id = …;`.
- Uniforms and other file-level values, which the host sets.

**Not caught** The check reads text a statement at a time, so a value set in
only one branch of an `if` before it is read slips through, and so does a
vector whose first use sets only some channels (`w.rgb = …` leaves `w.a`).

```slang
// flagged
colour c;
c.paint(Circle(40), colours.gold);

// fine
colour c = colours.transparent;
c.paint(Circle(40), colours.gold);
```

## XS004 · `mouse` passed to `anchor` (warning) · checked

**Flags** `anchor(…)` or `anchored(…)` whose argument uses `mouse`:
`s.anchor(mouse)`, `s.anchor(mouse + anchor.topLeft)`.

**Why** An anchor is a fraction of the quad's size from its centre
(`anchor.topLeft` is `(-0.5, -0.5)`). `mouse` is in pixels from the centre, so
`anchor(mouse)` asks for hundreds of quad widths and the shape lands far off
the stage.

**Fix** Move by pixels: `s.move(mouse)`, or `s.moved(mouse)` for a copy.

**Not flagged** `mouseUv`, and anchors that don't mention `mouse`.

```slang
// flagged
triangle.anchor(mouse);

// fine
triangle.move(mouse);
```

## XS005 · A shape worked out twice (warning) · checked

**Flags** a named shape drawn a second time at the same point with nothing
changed in between: painted, then joined into another shape that's drawn;
`filled` and then `stroked`; `distance(p)` and then `filled(p, …)`.

**Why** A shape is a recipe. Every draw works its distance out again, so the
second draw repeats all the first one's maths.

**Fix** Measure it once and draw the `Measured`:
`let ring = Circle(100).hollowed(10).measured();`. For a fill plus an
outline, `c.paint(shape, Look(…))` is the same two draws, so measure first if
the shape is dear.

**Not flagged** A shape moved, turned, scaled, anchored or given a new value
between draws (that's another shape), one drawn at a different point
(`distance(p)` and `distance(mouse)`), one drawn in different cases of a
`switch` or in a block that returns and after it (only one runs), a name
declared as a number or other non-shape, a shape read for a point or a field
(`s.at(t)`, `s.points[k]`), and a `Measured` (it's free to draw).

```slang
// flagged: ring is worked out by both paints
col.paint(ring, colours.red);
col.paint(dot.and(ring), colours.green);

// fine
let ring = Circle(100).hollowed(10).measured();
col.paint(ring, colours.red);
col.paint(dot.and(ring), colours.green);
```

## XS006 · Too much shape work a pixel (warning) · checked

**Flags** an effect whose draws add up to more than 32 shape costs a pixel,
shown on `shade`. Each shape costs its library `/// @cost` tag (a `Circle` is 1,
a `Box` 2, a `Star6` 3; `repeated` adds 2, the melted joins 1). A draw costs
every shape inside it, a `Look` paints twice, and a draw in a `for` counts once
per pass. The bound is read from a number or a `const int`, and taken as 8 passes
when the text doesn't give one. A `switch` runs one case a pixel, so it costs its
dearest case, not all of them added up; a block that ends in `return` costs the
dearer of itself and everything after it.

**Why** Every pixel of the quad pays it, every frame.

**Fix** Measure shapes drawn more than once (XS005), draw fewer, or join
distances by hand in a loop (§8 of `Docs/shaders.md`) so each shape is worked
out once.

**Not flagged** Work the text can't see. A helper function counts once,
wherever it's called from, and an `if` counts as though it always runs.

## XS007 · A name that hides a function (warning) · checked

**Flags** a parameter or local with the same name as a function that's called
while the name is in scope: `float2 reach(float2 from, float2 to, float length)`
whose body calls `length(d)`. The warning goes on the name.

**Why** Inside its scope the name means the value, and a value can't be
called. Slang reports it at the call as `no call operation found for type`,
which doesn't say what's wrong.

**Fix** Give the value another name: `span`, `size`, `reach`.

**Not flagged** A name that's never called in its scope, or a call outside it
(before a local is declared, or after its block closes).

```slang
// flagged
float2 reach(float2 from, float2 to, float length) { return from + normalize(to - from) * length(to - from); }

// fine
float2 reach(float2 from, float2 to, float span) { return from + normalize(to - from) * span; }
```

## XS008 · A bend without a `[Grid]` (warning) · checked

A `deform` that bends the quad (`place`, `wave`, `twist`, `bulge`, `sway`,
`curl`, `jitter`) needs corners inside
it. The shader tool's preview draws its own grid, so it looks right there, but
a level item is four corners and the bend comes out flat.

**Fix** Put `[Grid(columns, rows)]` on `deform`: `[Grid(96, 54)]` suits a 16:9
quad. Moves that work on four corners (`squash`, `tilt`, `shake`) don't need one.
Not on a stack with a `[Draws]` buffer: its deform places copies of the quad,
it doesn't bend it.

## XS009 · A `[Grid]` on a stack that draws copies (warning) · checked

A stack with a `[Draws]` buffer draws its quad once per copy (a particle, a
trail segment). With a `[Grid(columns, rows)]` every copy is drawn with every
cell: copies × columns × rows × 6 vertices, nearly all of them on cells smaller
than a pixel. 48,000 particle copies on a 72 × 72 grid is 1.5 billion vertices a
frame, a few frames a second on any card.

**Fix** Take the `[Grid]` off and place each copy as one quad (`v.uv` gives the
corner). The renderer says so on the console too when a copy has more than
`CanvasRenderer.MostCopyVertices` vertices, whatever cut it.
