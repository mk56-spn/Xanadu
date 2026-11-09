# Godot's math structs

Copied from Godot 4.4-stable (`modules/mono/glue/GodotSharp/GodotSharp/Core`),
MIT licence (`LICENSE.txt`), so the game keeps `Vector2`, `Rect2`, `Color`,
`Transform2D`, `Mathf`, `Rid` and the rest under the same names without the
engine (docs/moonworks-port.md P8).

Changes from upstream:
- `Rid`: the constructor from an id is public; the one from a `GodotObject` is gone.
- `Color`: the OKHSL members (native calls) are gone.
- `RealT.cs` aliases `real_t` to `float`.
- `Key` and `KeyModifierMask` were generated from GodotSharp 4.8.0-dev.7.
- `Color.ToHex32` formats with `ToString("x2")` in place of Godot's `HexEncode` extension.
