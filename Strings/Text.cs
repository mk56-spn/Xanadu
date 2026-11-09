using System;

namespace Xanadu.Strings;

/// <summary>A translatable key with an English fallback.</summary>
public readonly record struct LString(string Key, string Fallback);

/// <summary>
/// Central lookup for user-facing text. There are no translations yet, so
/// every string is its English fallback; a translation table plugs in here.
/// </summary>
public static class LocalizedText
{
    public static string Get(LString value) => value.Fallback;

    public static string Format(LString value, params object[] args) =>
        string.Format(Get(value), args);
}
