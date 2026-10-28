using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;

namespace Xanadu.Scenes;

/// <summary>Static frame of the song-select screen, authored at 1920x1080.</summary>
public static partial class SongSelectScene
{
    // The one accent: gold, as in the player name and the background notes.
    private static readonly Color Gold = new("#f5c542");
    private static readonly Color GoldLight = new("#fff3c4");
    private static readonly Color ListBackdrop = new(0.031f, 0.031f, 0.031f, 0.7f);

    private const int VisualizerBars = 48;
    private const float VisualizerBarWidth = 7;
    private const float VisualizerHeight = 80;

    private static void BuildChrome(UiBuilder ui, bool loadAssets)
    {
        // One straight edge, leaning the same 6 degrees as the preview frame.
        const float edgeBottom = 662.194f;
        float edgeTop = edgeBottom + Mathf.Tan(PreviewTilt) * 1080;
        Entity wedge = ui.ShapeEntity(
            [
                new(0, 0),
                new(edgeTop, 0),
                new(edgeBottom, 1080),
                new(0, 1080),
            ],
            ListBackdrop);
        Frost(wedge, loadAssets);

        Vector2[] underline =
        [
            new(63.345f, 127.6f), new(440.149f, 127.6f),
            new(481.727f, 84.749f), new(616.372f, 84.749f),
        ];

        Entity line = ui.ShapeEntity(underline, default, Gold, 3, closed: false);
        line.AddComponent(new ShapeGlow(new Color(Gold, 0.9f), radius: 10, intensity: 2f));
        line.AddTag<UiRaised>();

        // The visualizer hangs from the underline, its bars driven by the song preview.
        Entity visualizer = ui.RectEntity(Vector2.Zero, Vector2.One, Transparent);
        visualizer.AddComponent(new UiBarGraph(
            underline,
            VisualizerBars,
            VisualizerBarWidth,
            VisualizerHeight,
            new Color(Gold, 0.8f),
            GoldLight));
        visualizer.AddTag<UiRaised>();

        Entity title = SceneUi.Text(
            ui,
            TextCenter(12.202f, 108.339f, 460, 70),
            new Vector2(460, 84),
            LocalizedText.Get(SongSelectStrings.Title),
            70,
            Colors.White,
            new Color(0, 0, 0, 0));
        title.AddComponent(new TextAlignment(TextAlign.Left));
        title.GetComponent<TextStyle>().Bold = true;
    }

    /// <summary>
    /// Centre of a left-aligned text box whose text starts at <paramref name="left"/>
    /// on <paramref name="baseline"/>, allowing for the canvas's text padding.
    /// </summary>
    private static Vector2 TextCenter(float left, float baseline, float width, int fontSize) =>
        new(left - TextInset + width / 2, baseline - fontSize * 0.33f);

    private const float TextInset = 4;

    /// <summary>Blurs what a translucent panel sits over, when assets are available.</summary>
    private static void Frost(Entity panel, bool loadAssets)
    {
        if (loadAssets)
            panel.AddComponent(new UiMaterial(UiFrostedGlass.Create()));
    }
}
