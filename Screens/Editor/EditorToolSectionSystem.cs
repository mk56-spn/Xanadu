using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// Shows only the left panel sections the current tool uses, and hides the panel
/// itself when a tool uses none of them.
/// </summary>
public sealed class EditorToolSectionSystem : QuerySystem<EditorToolSection>
{
    private readonly EntityStore _entities;

    public EditorToolSectionSystem(EntityStore entities)
    {
        _entities = entities;

        // A hidden section still has to be found to be shown again.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement, EditorLeftMenu>(_entities);

        if (editor.IsNull)
            return;

        EditorPlacementMode mode = editor.GetComponent<EditorPlacement>().Mode;
        CommandBuffer buffer = CommandBuffer;
        bool any = Query.EachEntity(new ShowSections(Query.Store, CommandBuffer, mode)).Any;

        UiLayout.SetHidden(editor.GetComponent<EditorLeftMenu>().Panel, buffer, !any);
    }

    private struct ShowSections(EntityStore Store, CommandBuffer Buffer, EditorPlacementMode Mode) : IEachEntity<EditorToolSection>
    {
        public bool Any;

        public void Execute(ref EditorToolSection section, int id)
        {
            Entity entity = Store.GetEntityById(id);

            bool shown = Array.IndexOf(section.Modes, Mode) >= 0;
            Any |= shown;
            UiLayout.SetHidden(entity, Buffer, !shown);
        }
    }
}
