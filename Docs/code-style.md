# Code style

How code in this repo is written. Loaded every session through CLAUDE.md.

## C#

- **Collection expressions.** Write `[]`, `[a, b]` and `[..xs, y]`, not
  `new()`, `new List<T>()`, `new[] { … }` or `Array.Empty<T>()`, wherever the
  target type allows it. (IDE0028, IDE0300–IDE0305)
- **Primary constructors** wherever a constructor only stores its arguments
  or passes them on: `sealed class Foo(Bar bar)`, not a constructor that
  copies `bar` into a `_bar` field. (IDE0290)
- **Unused usings.** When you edit a file, delete its unused `using`
  directives in the same edit. Don't sweep files you aren't otherwise
  touching. (IDE0005)
