using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Model.Playfields;

/// <summary>The playfield's colours: what a note, a bar or a piece of terrain is coloured, for whoever draws it or matches it.</summary>
public static class PlayfieldColors
{
    public static readonly Color PlayBackground = Palette.DeepBlue;
    public static readonly Color PlayfieldBackground = new(0f, 0f, 0f);
    public static readonly Color BorderColor = Palette.BorderStrong;
    public static readonly Color GridColor = CanvasGrid.LineColor;
    public static readonly Color AxisColor = CanvasGrid.AxisColor;
    public static readonly Color AccentColor = Palette.Accent;
    public static readonly Color DimTextColor = Palette.TextDim;
    public static readonly Color SelectedNoteColor = new(1f, 0.9f, 0.3f);
    public static readonly Color PlayerColor = Palette.Text;
    public static readonly Color NeutralColor = Palette.TextBright;
    public static readonly Color GuideColor = Palette.Text;
    public static readonly Color FrameColor = Palette.SurfaceAlt;

    /// <summary>The dark rim of the editor's handles, which are not part of any block.</summary>
    public static readonly Color HandleEdge = Palette.BorderStrong;


    /// <summary>The curve rework's fixed floor and its top edge.</summary>
    public static readonly Color FloorColor = Palette.SurfaceAlt;
    public static readonly Color FloorEdgeColor = Palette.BorderStrong;

    /// <summary>The rework's hovering body: blue, with a brighter edge.</summary>
    public static readonly Color BodyFill = Palette.PrimarySoft;
    public static readonly Color BodyEdge = Palette.Primary;

    /// <summary>The colour of wall notes.</summary>
    public static readonly Color WallNoteColor = new(0.55f, 0.95f, 0.85f);

    /// <summary>The three action colours, one per action key; they only need to be told apart at a glance.</summary>
    private static readonly Color[] ActionColors =
    {
        Palette.Pink,
        Palette.Accent,
        Palette.Lime,
    };

    /// <summary>A note's colour: its key's, or the neutral colour for a dash, which any direction key hits.</summary>
    public static Color ColorFor(Note note) =>
        note.IsOrb ? OrbColor : note.IsDash || note.IsEntrance ? NeutralColor : ColorFor(note.Target);

    /// <summary>An updraft: pale sky blue, air.</summary>
    public static readonly Color DraftColor = new(0.55f, 0.88f, 1f);

    /// <summary>A pad: hot magenta-pink, like a launch pad.</summary>
    public static readonly Color PadColor = new(1f, 0.35f, 0.75f);

    /// <summary>An orb hold: warm gold, apart from every key's colour.</summary>
    public static readonly Color OrbColor = new(1f, 0.82f, 0.35f);

    /// <summary>The colour of an action key's notes; direction keys get the neutral colour.</summary>
    public static Color ColorFor(KeybindAction action) => action switch
    {
        KeybindAction.Action1 => ActionColors[0],
        KeybindAction.Action2 => ActionColors[1],
        KeybindAction.Action3 => ActionColors[2],
        _ => NeutralColor,
    };
}
