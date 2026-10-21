using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Shows and dismisses the ECS warning popup.</summary>
public sealed class WarningPopupSystem : QuerySystem<WarningPopup>
{
    private readonly EntityStore _entities;

    public WarningPopupSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        bool requested = editor.HasComponent<WarningRequest>();
        string message = requested
            ? editor.GetComponent<WarningRequest>().Message
            : "";

        if (requested)
            editor.RemoveComponent<WarningRequest>();

        LevelInfo level = editor.GetComponent<LevelInfo>();
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref popup, entity) =>
        {
            if (popup.Visible)
            {
                bool songLoaded = !string.IsNullOrEmpty(level.AudioPath);
                bool dismissed = !popup.Button.IsNull &&
                                 popup.Button.Tags.Has<Pressed>();

                if (songLoaded || dismissed)
                    Hide(entity, ref popup, buffer);

                return;
            }

            if (requested)
                Show(entity, ref popup, message, Center(editor), buffer);
        });
    }

    private static Vector2 Center(Entity editor)
    {
        EditorView view = editor.GetComponent<EditorView>();
        Playfield playfield = editor.GetComponent<Playfield>();

        return view.PlayfieldToScreen(
            new Vector2(playfield.Width / 2f, playfield.Height / 2f));
    }

    private static void Show(
        Entity popupEntity,
        ref WarningPopup popup,
        string message,
        Vector2 center,
        CommandBuffer buffer)
    {
        if (!popup.Message.IsNull && popup.Message.HasComponent<Text>())
        {
            ref Text text = ref popup.Message.GetComponent<Text>();
            text.Value = message;
            buffer.AddTag<Dirty>(popup.Message.Id);
        }

        ref Position position = ref popupEntity.GetComponent<Position>();
        position.Value = center;
        buffer.AddTag<Moved>(popupEntity.Id);
        popup.Visible = true;
    }

    private static void Hide(
        Entity popupEntity,
        ref WarningPopup popup,
        CommandBuffer buffer)
    {
        ref Position position = ref popupEntity.GetComponent<Position>();
        position.Value = EditorWarningPopup.HiddenPosition;
        buffer.AddTag<Moved>(popupEntity.Id);
        popup.Visible = false;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities
                     .Query<EditorView, LevelInfo, Playfield>()
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }
}
