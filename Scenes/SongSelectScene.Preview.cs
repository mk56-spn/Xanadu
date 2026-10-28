using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Strings;
using Xanadu.Systems.Shaders;

namespace Xanadu.Scenes;

/// <summary>The tilted preview frame and the difficulty chips beneath it.</summary>
public static partial class SongSelectScene
{
    private const int ChipCount = 3;
    private static readonly float PreviewTilt = Mathf.DegToRad(6);
    private static readonly Color PanelBlack = new(0, 0, 0, 0.7f);

    // Pushed left, as far as the list panel's slanted edge allows, to leave a
    // gap between the level art and the player banner.
    private static readonly Vector2 FrameCenter = new(1029.11f, 301.195f);

    // The player art frame sits in that gap, tilted like the level art.
    private static readonly Vector2 PlayerFrameCenter = new(1417, 135);
    private const float PlayerFrameBacking = 144;
    private const float PlayerFrameArt = 120;
    private static readonly Color Dim = new(0, 0, 0, 0.3f);

    // The chips and their bar are sheared by the same tilt as the frame.
    private static readonly Vector2 ChipSize = new(64, 64);
    private const float ChipSpacing = 78;

    private static readonly Color[] ChipFill =
    [
        new("#22b573"),
        new("#e6c73a"),
        new("#e63a3a"),
    ];

    private static readonly Color[] ChipStroke =
    [
        new("#136842"),
        new("#8a7510"),
        new("#8a1f1f"),
    ];

    private static Vector2[] Box(Vector2 size) =>
    [
        -size / 2,
        new(size.X / 2, -size.Y / 2),
        size / 2,
        new(-size.X / 2, size.Y / 2),
    ];

    private readonly record struct PreviewViews(List<Entity> Chips, Entity Art);

    private static PreviewViews BuildPreview(UiBuilder ui, bool loadAssets)
    {
        Entity backing = ui.ShapeEntity(
            Box(new Vector2(530, 530)),
            PanelBlack,
            position: FrameCenter,
            rotation: PreviewTilt);
        Frost(backing, loadAssets);
        // Clicking the level's art plays the level.
        Entity levelArt = TiltedArt(
            ui,
            FrameCenter,
            500,
            NoteMaterial(
                loadAssets,
                aspect: 1,
                zoom: 2.2f,
                notes: new Color(0.45f, 0.95f, 0.6f),
                top: new Color(0.04f, 0.17f, 0.1f),
                bottom: new Color(0.07f, 0.36f, 0.19f)),
            takesInput: true);
        levelArt.AddComponent(new SceneCommand(SceneActionId.Play));
        levelArt.AddComponent(new UiTooltip(LocalizedText.Get(SongSelectStrings.Play)));

        // Holds the shader for now; the player's own art goes here later.
        Entity playerBacking = ui.ShapeEntity(
            Box(new Vector2(PlayerFrameBacking, PlayerFrameBacking)),
            PanelBlack,
            position: PlayerFrameCenter,
            rotation: PreviewTilt);
        Frost(playerBacking, loadAssets);
        TiltedArt(
            ui,
            PlayerFrameCenter,
            PlayerFrameArt,
            NoteMaterial(
                loadAssets,
                aspect: 1,
                zoom: 4.5f,
                notes: new Color(0.5f, 0.75f, 1f),
                top: new Color(0.04f, 0.08f, 0.2f),
                bottom: new Color(0.08f, 0.2f, 0.45f)));

        // The chip bar is a skewed container; its right end runs off screen.
        const float barLeft = 1459.085f;
        ui.Container(
            new Vector2(barLeft + 210, 528),
            new Vector2(420, 80),
            StackDirection.Horizontal,
            spacing: ChipSpacing - ChipSize.X,
            color: Dim,
            padding: 8,
            main: StackMainAlignment.Start,
            skew: PreviewTilt);

        var chips = new List<Entity>();
        for (int i = 0; i < ChipCount; i++)
        {
            ui.Container(
                Vector2.Zero,
                ChipSize,
                StackDirection.Vertical,
                color: Transparent,
                text: "",
                skew: PreviewTilt);
            Entity chip = ui.Current;
            chip.AddComponent(new TextStyle(28, Colors.Black, bold: true));
            chip.AddComponent(new UiBorder(Transparent, 4));
            chips.Add(chip);
            ui.End();
        }

        ui.End();
        return new PreviewViews(chips, levelArt);
    }

    /// <summary>
    /// A black square rotated by the preview tilt, carrying a notes shader when
    /// there is one. It is a rect, not a polygon, so the shader gets real UVs.
    /// </summary>
    private static Entity TiltedArt(
        UiBuilder ui,
        Vector2 center,
        float size,
        ShaderMaterial? material,
        bool takesInput = false)
    {
        Entity art = ui.RectEntity(
            center,
            new Vector2(size, size),
            Colors.Black,
            takesInput: takesInput);
        art.AddComponent(new UiRotation(PreviewTilt));

        // A frosted backing draws over anything made after it unless that is raised.
        art.AddTag<UiRaised>();
        if (material is null)
            return art;

        art.AddComponent(new UiMaterial(material));
        art.AddComponent(new SpectrumEnergy());
        return art;
    }

    private static void RefreshChips(
        ref SongSelectState state,
        IReadOnlyList<LevelEntry> catalog)
    {
        LevelEntry? selected = catalog.Count == 0 ? null : catalog[state.Selected];
        for (int i = 0; i < ChipCount; i++)
        {
            string? label = ChipLabel(selected, i);
            SceneUi.SetText(state.Chips[i], label ?? "");

            ref FillColor fill = ref state.Chips[i].GetComponent<FillColor>();
            ref UiBorder border = ref state.Chips[i].GetComponent<UiBorder>();
            Color body = label is null ? Transparent : ChipFill[i];
            Color edge = label is null ? Transparent : ChipStroke[i];
            if (fill.Value != body || border.Color != edge)
            {
                fill.Value = body;
                border.Color = edge;
                state.Chips[i].AddTag<Dirty>();
            }
        }
    }

    /// <summary>
    /// Levels have no difficulty rating yet, so the first chip shows the
    /// note count and the others stay hidden.
    /// </summary>
    private static string? ChipLabel(LevelEntry? entry, int chip) =>
        entry is null || chip > 0 ? null : entry.Document.Notes.Count.ToString();
}
