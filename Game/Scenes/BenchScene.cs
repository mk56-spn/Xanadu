using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The gameplay bench, for getting a feel for things against real songs: a flat
/// surface, the player as a plain box (2.5 tall to 1 wide, as in the GAMEPLAY
/// lab) and a background the bench system pulses on the song's beats.
/// </summary>
public static class BenchScene
{
    /// <summary>The player box, in stage pixels: 2.5 tall to 1 wide.</summary>
    public static readonly Vector2 PlayerSize = new(40, 100);

    /// <summary>How far down the stage the surface's top sits (0 = top, 1 = bottom).</summary>
    public const float SurfaceAt = 0.75f;

    /// <summary>How high above the surface the player box hovers, centre to surface.</summary>
    public const float HoverHeight = 220;

    /// <summary>Where the player box stands across the stage (0 = left, 1 = right); the world scrolls past it.</summary>
    public const float PlayerAt = 0.3f;

    /// <summary>How many vocal-note disks the bench keeps to place on the beats.</summary>
    public const int NotePool = 16;

    public const float NoteRadius = 22;

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

    private const float TitleY = 80;
    private const float InfoY = 130;

    /// <summary>The song lines are filled in later, so they get a fixed box to centre in.</summary>
    private static readonly Vector2 LineSize = new(1400, 50);
    private const float ActionWidth = 180;
    private const float ActionHeight = 46;
    private const float ActionsInset = 80;

    public static void Build(EntityStore entities, Entity root, IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage);
        float centerX = stage.Center.X;

        Entity background = ui.Rect(Palette.Stage).Cover().Below().Entity;

        float top = stage.Height * SurfaceAt;
        float depth = stage.Height - top;
        ui.Rect(Palette.SurfaceAlt, size: new Vector2(stage.Width, depth))
            .At(new Vector2(centerX, top + depth / 2));
        Vector2 hidden = new(-1000, -1000);
        var notes = new Entity[NotePool];
        for (int i = 0; i < notes.Length; i++)
            notes[i] = ui.Shape(Disk(NoteRadius), Palette.TextMuted).At(hidden).Entity;

        float playerX = stage.Width * PlayerAt;
        var quarters = new Entity[QuarterStarts.Length];
        for (int i = 0; i < quarters.Length; i++)
        {
            quarters[i] = ui.Shape(Quarter(QuarterStarts[i]), Palette.PrimaryMuted)
                .At(new Vector2(playerX, top - HoverHeight))
                .Entity;
        }

        // A shape rather than a rect, so it turns about its centre when it leans.
        Vector2 half = PlayerSize / 2;
        Entity player = ui.Shape(
                [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)],
                Palette.PrimarySoft)
            .At(new Vector2(playerX, top - HoverHeight))
            .Entity;

        Entity title = ui.Text("", UiType.Heading, SceneUi.TextColor, TextAlign.Center)
            .Size(LineSize)
            .At(new Vector2(centerX, TitleY))
            .Entity;
        Entity info = ui.Text("", UiType.Body, SceneUi.DimTextColor, TextAlign.Center)
            .Size(LineSize)
            .At(new Vector2(centerX, InfoY))
            .Entity;

        UiNode actions = ui.Row()
            .Gap(UiMetrics.Space.Xl)
            .At(new Vector2(centerX, stage.Height - ActionsInset));
        actions.Button("< SONG", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(BenchAction.Previous));
        actions.Button("SONG >", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .With(UiCommand.Of(BenchAction.Next));
        actions.Button("BACK", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Back);

        ui.With(new BenchView(background, title, info, player, quarters, notes));
    }

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
