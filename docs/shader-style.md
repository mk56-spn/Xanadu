# Shader style rules

The house style for Xanadu shaders (level shader slots, the playground, the
shader editor, shipped effects). Each rule has an id so the shader editor can
show it as a warning later and a level can say which ones it ignores.

**Status:** none of these are checked yet. They are collected here as they come
up; the shader editor (`Tools/Labs/Editor/`, `docs/host.md` §5.9) will turn them
into warnings when that is wanted.

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

## XS002 · Whole-number division (warning)

**Flags** a division whose two sides are both whole numbers or int values, in a
place that wants a float: `1 / 2`, `count / 3` with `int count`.

**Why** The division happens before the conversion to float, so it drops the
fraction: `1 / 2` is `0`, and `x * (1 / 2)` is quietly `x * 0`. This is the one
place where XS001's whole numbers bite.

**Fix** Make one side a float: `1.0 / 2`, `float(count) / 3`, or write the
result (`0.5`).

```slang
// flagged
float half = 1 / 2;          // 0
float share = count / 3;     // whole thirds only

// fine
float half = 0.5;
float share = float(count) / 3;
float scaled = x / 2;        // x is a float, so this is a float division
```
