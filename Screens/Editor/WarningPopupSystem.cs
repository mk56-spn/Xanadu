using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>Shows and dismisses the ECS warning popup.</summary>
public sealed class WarningPopupSystem : QuerySystem<WarningPopup>
{
    private readonly EntityStore _entities;

    public WarningPopupSystem(EntityStore entities)
    {
        _entities = entities;

        // A hidden popup still has to hear about the next warning.
        Filter.WithDisabled();
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

        Query.EachEntity(new ShowWarning(Query.Store, CommandBuffer, editor, level, requested, message));
    }

    private static Vector2 Center(Entity editor)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
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
        UiLayout.SetHidden(popupEntity, buffer, false);
    }

    private static void Hide(
        Entity popupEntity,
        CommandBuffer buffer)
    {
        UiLayout.SetHidden(popupEntity, buffer, true);
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities
                     .Query<PlayfieldView, LevelInfo, Playfield, NoteSelection>()
                     .Entities)
        {
            return entity;
        }

        return default;
    }

    private readonly record struct ShowWarning(EntityStore Store, CommandBuffer Buffer, Entity Editor, LevelInfo Level, bool Requested, string Message) : IEachEntity<WarningPopup>
    {
        public void Execute(ref WarningPopup popup, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (entity.Enabled)
            {
                bool songLoaded = Level.Song is not null;
                bool dismissed = !popup.Button.IsNull &&
                                 popup.Button.Tags.Has<Pressed>();

                if (songLoaded || dismissed)
                    Hide(entity, Buffer);

                return;
            }

            if (Requested)
                Show(entity, ref popup, Message, Center(Editor), Buffer);
        }
    }
}
