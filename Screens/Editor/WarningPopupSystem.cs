using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// Dismisses the ECS warning popup: on OK, or once a song is loaded. A
/// <see cref="WarningRaised"/> signal shows it (<see cref="EditorWarningPopup"/>).
/// </summary>
public sealed class WarningPopupSystem(EntityStore entities) : QuerySystem<WarningPopup>
{
    private readonly EntityStore _entities = entities;

    protected override void OnUpdate()
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        bool songLoaded = editor.GetComponent<LevelInfo>().Song is not null;
        Query.EachEntity(new Dismiss(CommandBuffer, songLoaded));
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

    private readonly record struct Dismiss(CommandBuffer Buffer, bool SongLoaded) : IEachEntity<WarningPopup>
    {
        public void Execute(ref WarningPopup popup, int id)
        {
            bool dismissed = !popup.Button.IsNull && popup.Button.Tags.Has<Pressed>();
            if (SongLoaded || dismissed)
                UiLayout.SetHidden(Buffer.EntityStore.GetEntityById(id), Buffer, true);
        }
    }
}
