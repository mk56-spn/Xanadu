using Godot;

namespace Xanadu.Theme;

/// <summary>
/// The single source of truth for colour in Xanadu. Every surface, line, text
/// tone, and accent used by more than one feature lives here, so a retheme is a
/// change to this file rather than a sweep across the game.
/// <para>
/// The values follow the Tailwind slate/blue scale the UI was originally drawn
/// with. Names describe the role a colour plays, not its hue; where a tone is
/// reused for two roles the doc comment says so.
/// </para>
/// </summary>
public static class Palette
{
    // ── Surfaces ──────────────────────────────────────────────────────────
    // Darkest to lightest. Panels are drawn on top of the window background,
    // and inset controls sit below panels.

    /// <summary>The window/game backdrop behind everything.</summary>
    public static readonly Color Background = new("#0b1020");

    /// <summary>Letterbox behind the playfield, a shade deeper than the window.</summary>
    public static readonly Color BackgroundPlay = new("#090d16");

    /// <summary>Inputs, rulers, and preview wells recessed into a panel.</summary>
    public static readonly Color SurfaceInset = new("#0b1220");

    /// <summary>The playfield well and alternating timeline rows.</summary>
    public static readonly Color SurfaceDeep = new("#0f172a");

    /// <summary>Panels, cards, and popup bodies.</summary>
    public static readonly Color Surface = new("#111827");

    /// <summary>
    /// Rows, fields, and hover states that sit above a panel: the idle row of a
    /// list, an editable text field, or the row under the pointer.
    /// </summary>
    public static readonly Color SurfaceRaised = new("#16223a");

    /// <summary>
    /// Secondary panels, grid lines, and dropdown options. Also the dark ink
    /// drawn on top of an accent fill, where a light tone would not read.
    /// </summary>
    public static readonly Color SurfaceAlt = new("#1e293b");

    /// <summary>Disabled controls and unselected block fills.</summary>
    public static readonly Color SurfaceDisabled = new("#1f2937");

    // ── Lines ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Muted control fill and subtle border. Serves both roles deliberately:
    /// a muted button and its outline share one tone.
    /// </summary>
    public static readonly Color Muted = new("#334155");

    /// <summary>Emphasised borders and block edges.</summary>
    public static readonly Color BorderStrong = new("#475569");

    /// <summary>Axes and tick marks on grids and timelines.</summary>
    public static readonly Color BorderSubtle = new("#64748b");

    /// <summary>Grid lines inside canvases and timelines.</summary>
    public static readonly Color GridLine = new("#273449");

    /// <summary>Tooltip bodies.</summary>
    public static readonly Color Tooltip = new("#243044");

    // ── Text ──────────────────────────────────────────────────────────────

    /// <summary>Primary body and label text.</summary>
    public static readonly Color Text = new("#e2e8f0");

    /// <summary>Secondary text: row labels, table cells, instructions.</summary>
    public static readonly Color TextMuted = new("#cbd5e1");

    /// <summary>Tertiary text: hints, disabled labels, misses.</summary>
    public static readonly Color TextDim = new("#94a3b8");

    /// <summary>Emphasised text, focus rings, and toast copy.</summary>
    public static readonly Color TextBright = new("#f8fafc");

    // ── Accents ───────────────────────────────────────────────────────────

    /// <summary>Amber. Selection, playheads, animator bone strokes, and the top judgement.</summary>
    public static readonly Color Accent = new("#facc15");

    /// <summary>Sky blue. The input queue, group badges, and animator bone fills.</summary>
    public static readonly Color Info = new("#38bdf8");

    /// <summary>A softer blue for informational text and the "fair" judgement.</summary>
    public static readonly Color InfoSoft = new("#60a5fa");

    /// <summary>Cyan. Open cells and the "flawless" judgement.</summary>
    public static readonly Color Cyan = new("#22d3ee");

    /// <summary>Primary action buttons.</summary>
    public static readonly Color Primary = new("#2563eb");

    /// <summary>A lighter primary for secondary lanes and calm analysis states.</summary>
    public static readonly Color PrimarySoft = new("#3b82f6");

    /// <summary>A deeper primary for occupied cells and pressed states.</summary>
    public static readonly Color PrimaryDeep = new("#1d4ed8");

    /// <summary>Teal. Toggled-on state and the current selection in a list.</summary>
    public static readonly Color Selected = new("#0f766e");

    /// <summary>A deeper teal for nested selected surfaces.</summary>
    public static readonly Color SelectedDeep = new("#134e4a");

    /// <summary>Lime. Character switches and attachment markers.</summary>
    public static readonly Color Lime = new("#a3e635");

    /// <summary>Violet. Lane and group swatches.</summary>
    public static readonly Color Violet = new("#8b5cf6");

    /// <summary>Pink. Lane and group swatches.</summary>
    public static readonly Color Pink = new("#ec4899");

    // ── Status ────────────────────────────────────────────────────────────

    /// <summary>Green. Success states and the player hitbox.</summary>
    public static readonly Color Success = new("#22c55e");

    /// <summary>A brighter green for the "clean" judgement and start markers.</summary>
    public static readonly Color SuccessBright = new("#4ade80");

    /// <summary>A pale green for hover text on success-coloured controls.</summary>
    public static readonly Color SuccessLight = new("#86efac");

    /// <summary>Orange. Warnings, the "deficient" judgement, and unkeyed tracks.</summary>
    public static readonly Color Warning = new("#fb923c");

    /// <summary>A deep amber used as a warning button fill.</summary>
    public static readonly Color WarningDeep = new("#b45309");

    /// <summary>Red. Errors, busy states, and the "terrible" judgement.</summary>
    public static readonly Color Danger = new("#ef4444");

    /// <summary>A brighter red for mines and dropped inputs.</summary>
    public static readonly Color DangerBright = new("#ff3b30");

    /// <summary>A deep red for muted destructive states.</summary>
    public static readonly Color DangerDeep = new("#b91c1c");

    /// <summary>A dark red for destructive dialog buttons.</summary>
    public static readonly Color DangerDark = new("#9f1d1d");

    /// <summary>The surface of a destructive confirmation dialog.</summary>
    public static readonly Color DangerSurface = new("#2a1215");

    /// <summary>The surface of a destructive warning popup.</summary>
    public static readonly Color DangerSurfaceAlt = new("#3b1515");

    /// <summary>Rose. Block hitboxes and animator attachment previews.</summary>
    public static readonly Color Rose = new("#fb7185");

    /// <summary><paramref name="color"/> with its alpha replaced.</summary>
    public static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);
}
