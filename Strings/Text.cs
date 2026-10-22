using System;
using Godot;

namespace Writer.Strings;

/// <summary>A translatable key with an English fallback.</summary>
public readonly record struct LString(string Key, string Fallback);

/// <summary>Central lookup for user-facing text.</summary>
public static class LocalizedText
{
    private static bool _useGodot;

    /// <summary>Enables Godot's translation server for the running game.</summary>
    public static void EnableGodot() => _useGodot = true;

    public static string Get(LString value)
    {
        if (!_useGodot)
            return value.Fallback;

        string translated = TranslationServer.Translate(value.Key);
        return translated == value.Key ? value.Fallback : translated;
    }

    public static string Format(LString value, params object[] args) =>
        string.Format(Get(value), args);
}
