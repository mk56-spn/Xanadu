using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Editor;

/// <summary>Builds and requests the editor's ECS warning popup.</summary>
public static class EditorWarningPopup
{
    public const int LayerZIndex = 850;
    public const string NoSongMessage = "Load a song before placing notes.";

    public static readonly Vector2 HiddenPosition = new(-10_000, -10_000);

    private static readonly Vector2 PopupSize = new(440, 170);

    /// <summary>Creates the popup hierarchy hidden below the editor overlay.</summary>
    public static Entity Build(EntityStore entities, IVisualRidFactory? visuals = null)
    {
        UiBuilder builder = UiBuilder.Begin(entities, visuals).Layer(LayerZIndex);
        builder.Container(
            HiddenPosition,
            PopupSize,
            StackDirection.Vertical,
            spacing: 10,
            color: new Color("#3b1515"));

        Entity panel = builder.Current;

        builder.Rect(
            Vector2.Zero,
            new Vector2(400, 26),
            new Color("#9f1d1d"),
            "WARNING");

        builder.Rect(
            Vector2.Zero,
            new Vector2(400, 48),
            new Color("#3b1515"),
            NoSongMessage);
        Entity message = LastChild(panel);

        builder.Rect(
            Vector2.Zero,
            new Vector2(120, 34),
            new Color("#b45309"),
            "OK",
            takesInput: true);
        Entity button = LastChild(panel);
        button.AddComponent(ButtonVisual.Create(new Color("#b45309")));

        builder.End();

        panel.AddComponent(new WarningPopup(message, button));
        return panel;
    }

    /// <summary>Queues a warning for the popup system to display.</summary>
    public static void Request(Entity editor, string message)
    {
        if (editor.IsNull)
            return;

        if (editor.HasComponent<WarningRequest>())
        {
            ref WarningRequest request = ref editor.GetComponent<WarningRequest>();
            request.Message = message;
            return;
        }

        editor.AddComponent(new WarningRequest(message));
    }

    private static Entity LastChild(Entity parent)
    {
        Entity last = default;
        foreach (Entity child in parent.ChildEntities)
            last = child;

        return last;
    }
}
