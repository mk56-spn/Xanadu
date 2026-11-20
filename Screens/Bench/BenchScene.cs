using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Logic.Bench;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Screens.Shared;

namespace Xanadu.Screens.Bench;

/// <summary>
/// The gameplay bench, for getting a feel for things against real songs: a flat
/// surface, the player as a plain box (2.5 tall to 1 wide, as in the GAMEPLAY
/// lab) and a background the bench system pulses on the song's beats.
/// </summary>
public static class BenchScene
{
    /// <summary>How many vocal-note disks the bench keeps to place on the beats.</summary>
    public const int NotePool = 16;

    /// <summary>How many curve-note lines the bench keeps to draw a map with.</summary>
    public const int CurvePool = 24;

    public const float CurveWidth = 6;

    /// <summary>The quarter disks behind the player: their radius and the gap between them.</summary>
    public const float QuarterRadius = 95;
    private const float QuarterGap = 6;
    private const int QuarterSteps = 16;

    /// <summary>
    /// Where each quarter sits, in the order the bench's action keys are listed:
    /// the left hand's on the left, the right hand's on the right, outer fingers
    /// on top (S top-left, D bottom-left, K bottom-right, L top-right). Each is
    /// the angle its arc starts at, y pointing down.
    /// </summary>
    private static readonly float[] QuarterStarts = [180f, 90f, 0f, 270f];

    private const float LineWidth = 1400;
    private const float ActionWidth = 180;
    private const float ActionHeight = 46;

    public static void Build(EntityStore entities, Entity root, IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage);
        float centerX = stage.Center.X;

        Entity background = ui.Rect(Palette.Stage).Cover().Below().Entity;

        float top = stage.Height * BenchLayout.SurfaceAt;
        float depth = stage.Height - top;
        Entity surface = ui.Rect(Palette.SurfaceAlt, size: new Vector2(stage.Width, depth))
            .At(new Vector2(centerX, top + depth / 2))
            .Entity;
        Vector2 hidden = new(-1000, -1000);
        var curves = new Entity[CurvePool];
        for (int i = 0; i < curves.Length; i++)
        {
            curves[i] = ui.Shape([Vector2.Zero, Vector2.One], Colors.Transparent, PlayfieldColors.CurveColor, CurveWidth, closed: false)
                .At(hidden)
                .Entity;
        }

        var notes = new Entity[NotePool];
        for (int i = 0; i < notes.Length; i++)
            notes[i] = ui.Shape(Disk(BenchLayout.NoteRadius), Palette.TextMuted).At(hidden).Entity;

        float playerX = stage.Width * BenchLayout.PlayerAt;
        var quarters = new Entity[QuarterStarts.Length];
        for (int i = 0; i < quarters.Length; i++)
        {
            quarters[i] = ui.Shape(Quarter(QuarterStarts[i]), Palette.PrimaryMuted)
                .At(new Vector2(playerX, top - BenchLayout.HoverHeight))
                .Entity;
        }

        // A shape rather than a rect, so it turns about its centre when it leans.
        Vector2 half = BenchLayout.PlayerSize / 2;
        Entity player = ui.Shape(
                [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)],
                Palette.PrimarySoft)
            .At(new Vector2(playerX, top - BenchLayout.HoverHeight))
            .Entity;

        // The chrome flows over the playfield; the song lines are filled in later,
        // so they are as wide as the stage allows and centre what they get.
        var title = new Label("", UiType.Heading, align: Align.Center, width: LineWidth);
        var info = new Label("", UiType.Body, ink: Ink.Dim, align: Align.Center, width: LineWidth);
        ui.Page().Cross(Cross.Center).Pad(UiMetrics.Space.Xxl).Gap(UiMetrics.Space.Sm).Add(
            title,
            info,
            new Spacer(),
            new Row(gap: UiMetrics.Space.Xl)
            {
                Action("< SONG", n => n.With(UiCommand.Of(BenchAction.Previous))),
                Action("SONG >", n => n.With(UiCommand.Of(BenchAction.Next))),
                Action("BACK", n => n.On(SceneActionId.Back)),
            });

        ui.With(new BenchView(background, title.Node.Entity, info.Node.Entity, player, quarters, notes, surface, curves));
    }

    private static Button Action(string label, Action<UiNode> does) =>
        new(label, tone: Tone.Secondary, width: ActionWidth, height: ActionHeight) { Style = does };

    private static Vector2[] Disk(float radius)
    {
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = MathF.Tau * i / points.Length;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        return points;
    }

    /// <summary>A quarter disk from <paramref name="startDegrees"/> round 90°, pushed out along its middle to leave a gap.</summary>
    private static Vector2[] Quarter(float startDegrees)
    {
        float start = startDegrees * MathF.PI / 180f;
        float middle = start + MathF.PI / 4;
        Vector2 offset = new Vector2(MathF.Cos(middle), MathF.Sin(middle)) * (QuarterGap / 2 / MathF.Sin(MathF.PI / 4));
        var points = new Vector2[QuarterSteps + 2];
        points[0] = offset;
        for (int i = 0; i <= QuarterSteps; i++)
        {
            float angle = start + MathF.PI / 2 * i / QuarterSteps;
            points[i + 1] = offset + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * QuarterRadius;
        }

        return points;
    }
}
