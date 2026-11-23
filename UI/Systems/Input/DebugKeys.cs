
namespace Xanadu.Ui.Systems.Input;

/// <summary>
/// The function keys, banded by what they affect so the debug views never
/// interlace: engine views (F1–F4) work anywhere, F5 previews whatever tool is
/// open, F6–F11 belong to the app that hosts the UI (the game's bands are
/// <c>GameDebugKeys</c> in Model), and F12 is the app's screenshot. A new view
/// takes its band's spare slot; it never fills a hole in another band.
/// </summary>
public static class DebugKeys
{
    /// <summary>F1: frame rate, frame time and allocation readout.</summary>
    public const Key FrameRate = Key.F1;

    /// <summary>F2: the shader uniform tuner.</summary>
    public const Key ShaderTuner = Key.F2;

    /// <summary>F3: ECS counts, system timings and the archetype table.</summary>
    public const Key Ecs = Key.F3;

    /// <summary>F4: UI bounds and the UI tree.</summary>
    public const Key UiTree = Key.F4;

    /// <summary>F5: preview what the open tool is editing; Shift is its variant.</summary>
    public const Key Preview = Key.F5;

    /// <summary>Ctrl+`: the text terminal, dropped down over any screen (plain ` is the game's docs).</summary>
    public const Key Terminal = Key.Quoteleft;

    /// <summary>F12: save a screenshot.</summary>
    public const Key Screenshot = Key.F12;

    /// <summary>Whether a key belongs to the debug bands, so a note key may not take it.</summary>
    public static bool IsReserved(Key key) => key is
        Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5 or Key.F6 or
        Key.F7 or Key.F8 or Key.F9 or Key.F10 or Key.F11 or Key.F12;
}
