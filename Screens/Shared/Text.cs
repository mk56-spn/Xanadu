using System;

namespace Xanadu.Screens.Shared;

/// <summary>A translatable key with an English fallback.</summary>
/// <remarks>
/// It reads as its looked-up text wherever a string is wanted, so a screen hands
/// one straight to a label or button: <c>new Label(SettingsStrings.Title)</c>.
/// </remarks>
public readonly record struct LString(string Key, string Fallback)
{
    public static implicit operator string(LString value) => LocalizedText.Get(value);
}

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
