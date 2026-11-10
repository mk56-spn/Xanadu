# Maths types

Vectors are `System.Numerics.Vector2/3/4`, the same types MoonWorks and Friflo
use. `Maths.props` (imported by every project) aliases them and imports this
namespace, so no file needs a `using` for them.

Godot 4.4-stable's vector methods (`Normalized`, `Rotated`, `Angle`,
`DistanceTo`, `Vector2.Up`, `%`, `<`…) are kept as C# 14 extension members in
`VectorExtensions.cs`, so code written against Godot's API behaves as before.
Note `Reflect` is Godot's (instance call), not System.Numerics' static
`Vector2.Reflect`, which has the opposite sign convention.

The rest have no equivalent in .NET and are copied from Godot 4.4-stable
(`modules/mono/glue/GodotSharp/GodotSharp/Core`), MIT licence (`LICENSE.txt`),
in namespace `Xanadu.Maths`: `Color`, `Colors`, `Rect2`, `Rect2I`,
`Transform2D`, `Vector2I/3I/4I`, `Mathf`, `MathfEx`, `Key`, `KeyModifierMask`.

Changes from upstream:
- `Rid` is `Handle` (a canvas/texture id); its constructor from an id is public.
- `Color`: the OKHSL members (native calls) are gone.
- `RealT.cs` aliases `real_t` to `float`.
- `Vector3.Rotated(axis, angle)` uses a System.Numerics quaternion (Godot's `Basis` is gone).
- `Key` and `KeyModifierMask` were generated from GodotSharp 4.8.0-dev.7.
- `Color.ToHex32` formats with `ToString("x2")` in place of Godot's `HexEncode` extension.
