using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Model.Playfields;

/// <summary>The playfield's colours: what a note, a bar or a piece of terrain is coloured, for whoever draws it or matches it.</summary>
public static class PlayfieldColors
{
    public static readonly Color PlayBackground = Palette.Stage;
    public static readonly Color PlayfieldBackground = Palette.Stage;
    public static readonly Color BorderColor = Palette.BorderStrong;
    public static readonly Color GridColor = CanvasGrid.LineColor;
    public static readonly Color AxisColor = CanvasGrid.AxisColor;
    public static readonly Color AccentColor = Palette.Accent;
    public static readonly Color DimTextColor = Palette.TextDim;
    public static readonly Color SelectedNoteColor = new(1f, 0.9f, 0.3f);
    public static readonly Color PlayerColor = Palette.Text;
    public static readonly Color QueueColor = Palette.Info;
    public static readonly Color ContactBarrierColor = Palette.TextBright;
    public static readonly Color NeutralColor = Palette.TextBright;
    public static readonly Color DashColor = Palette.Cyan;
    public static readonly Color MomentumColor = Palette.Violet;
    public static readonly Color GuideColor = Palette.Text;
    public static readonly Color StartPointColor = Palette.SuccessBright;
    public static readonly Color FrameColor = Palette.SurfaceAlt;
    public static readonly Color BlockFill = Colors.White;

    /// <summary>The dark rim of the editor's handles, which are not part of any block.</summary>
    public static readonly Color HandleEdge = Palette.BorderStrong;

    public static readonly Color BlockSelectedEdge = Palette.Accent;

    /// <summary>Spikes have no colour of their own: white shows exactly what their self modulate says.</summary>
    public static readonly Color SpikeColor = Colors.White;

    /// <summary>Text has no colour of its own: white shows exactly what its self modulate says.</summary>
    public static readonly Color TextColor = Colors.White;

    /// <summary>A new polygon's fill.</summary>
    public static readonly Color PolygonFill = new("#7aa2f7");

    /// <summary>A friction bar is orange, for now: a placeholder look until it has its own art.</summary>
    public static readonly Color FrictionBarColor = Palette.Warning;

    /// <summary>The colour of wall notes.</summary>
    public static readonly Color WallNoteColor = new(0.55f, 0.95f, 0.85f);

    /// <summary>The three action colours, one per action key; they only need to be told apart at a glance.</summary>
    private static readonly Color[] ActionColors =
    {
        Palette.Pink,
        Palette.Accent,
        Palette.Lime,
    };

    public static Color ColorFor(Note note) =>
        note.Kind switch
        {
            NoteKind.Contact => ContactBarrierColor,
            NoteKind.Wall => WallNoteColor,
            NoteKind.Neutral => NeutralColor,
            NoteKind.Dash => DashColor,
            NoteKind.Momentum => MomentumColor,
            _ => ColorFor(note.Target),
        };

    /// <summary>The colour of an action key's notes and Contact bar; direction keys get the neutral colour.</summary>
    public static Color ColorFor(KeybindAction action)
    {
        int bar = ContactCounters.BarOf(action);
        return bar >= 0 ? ActionColors[bar] : NeutralColor;
    }

    /// <summary>The colour of Contact bar 0, 1 or 2: its action key's.</summary>
    public static Color BarColor(int bar) => ActionColors[bar];
}
