using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Shaders;

namespace Xanadu.Scenes;

/// <summary>The tilted preview frame and the difficulty chips beneath it.</summary>
public static partial class SongSelectScene
{
    private const int ChipCount = 3;
    private static readonly float PreviewTilt = Mathf.DegToRad(6);
    private static readonly Color PanelBlack = new(0, 0, 0, 0.7f);

    private static readonly Vector2 FrameCenter = new(1404.862f, 332.754f);
    private static readonly Vector2 ChipBarCenter = new(1364.066f, 652.494f);
    private static readonly Vector2 ChipSize = new(63, 49);

    private static readonly Vector2[] ChipCenter =
    [
        new(1189.638f, 633.83f),
        new(1269.383f, 642.212f),
        new(1349.129f, 650.593f),
    ];

    private static readonly Color[] ChipColor =
    [
        new("#3ae652"),
        new("#e6c73a"),
        new("#e63a3a"),
    ];

    private static Vector2[] Box(Vector2 size) =>
    [
        -size / 2,
        new(size.X / 2, -size.Y / 2),
        size / 2,
        new(-size.X / 2, size.Y / 2),
    ];

    private const string PlasmaShader = "res://Assets/Shaders/preview_plasma.gdshader";

    private static List<Entity> BuildPreview(UiBuilder ui, bool loadAssets)
    {
        ui.ShapeEntity(
            Box(new Vector2(530, 530)),
            PanelBlack,
            position: FrameCenter,
            rotation: PreviewTilt);
        Entity art = ui.ShapeEntity(
            Box(new Vector2(500, 500)),
            Colors.Black,
            position: FrameCenter,
            rotation: PreviewTilt);
        if (loadAssets && ShaderMaterials.Load(PlasmaShader) is { } material)
            art.AddComponent(new UiMaterial(material));

        // The outline is its own entity: the shader would otherwise recolour it.
        Entity frame = ui.ShapeEntity(
            Box(new Vector2(500, 500)),
            Transparent,
            Amber,
            3,
            position: FrameCenter,
            rotation: PreviewTilt);
        frame.AddTag<UiRaised>();
        ui.ShapeEntity(
            Box(new Vector2(462, 81.4f)),
            PanelBlack,
            Amber,
            3,
            position: ChipBarCenter,
            rotation: PreviewTilt);

        var chips = new List<Entity>();
        for (int i = 0; i < ChipCount; i++)
        {
            Entity chip = ui.RectEntity(
                ChipCenter[i],
                ChipSize,
                Transparent,
                "");
            chip.AddComponent(new TextStyle(50, ChipColor[i], bold: true));
            chip.AddComponent(new UiRotation(PreviewTilt));
            chips.Add(chip);
        }

        return chips;
    }

    private static void RefreshChips(
        ref SongSelectState state,
        IReadOnlyList<LevelEntry> catalog)
    {
        LevelEntry? selected = catalog.Count == 0 ? null : catalog[state.Selected];
        for (int i = 0; i < ChipCount; i++)
        {
            string? label = ChipLabel(selected, i);
            Entity chip = state.Chips[i];
            SceneUi.SetText(chip, label ?? "");
            ref FillColor fill = ref chip.GetComponent<FillColor>();
            Color color = label is null
                ? Transparent
                : new Color(ChipColor[i], 0.2f);
            if (fill.Value != color)
            {
                fill.Value = color;
                chip.AddTag<Dirty>();
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
