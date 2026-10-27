using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;

namespace Xanadu.Scenes;

/// <summary>Static frame of the song-select screen, authored at 1920x1080.</summary>
public static partial class SongSelectScene
{
    private static readonly Color Amber = new("#ffc035");
    private static readonly Color ListBackdrop = new(0.031f, 0.031f, 0.031f, 0.7f);
    private const float StripTop = 1011.282f;
    private static readonly Vector2 WedgeApex = new(778.485f, 551.956f);

    private static void BuildChrome(UiBuilder ui)
    {
        // Both edges lean away from the apex by the preview portrait's tilt.
        float lean = Mathf.Tan(PreviewTilt);
        ui.ShapeEntity(
            [
                new(0, 0),
                new(0, StripTop),
                new(WedgeApex.X - (StripTop - WedgeApex.Y) * lean, StripTop),
                WedgeApex,
                new(WedgeApex.X - WedgeApex.Y * lean, 0),
            ],
            ListBackdrop,
            new Color(0.251f, 0.251f, 0.251f, 0.7f),
            3);

        ui.ShapeEntity(
            [new(0, StripTop), new(1920, StripTop), new(1920, 1080), new(0, 1080)],
            Colors.Black);
        ui.ShapeEntity(
            [new(0, StripTop), new(1920, StripTop)],
            default,
            Amber,
            3,
            closed: false);

        ui.ShapeEntity(
            [
                new(63.345f, 127.6f), new(440.149f, 127.6f),
                new(481.727f, 84.749f), new(616.372f, 84.749f),
            ],
            default,
            Amber,
            3,
            closed: false);

        Entity title = SceneUi.Text(
            ui,
            new Vector2(17.836f + 230, 60),
            new Vector2(460, 84),
            LocalizedText.Get(SongSelectStrings.Title),
            70,
            Colors.White,
            new Color(0, 0, 0, 0));
        title.AddComponent(new TextAlignment(TextAlign.Left));
        title.GetComponent<TextStyle>().Bold = true;
    }
}
