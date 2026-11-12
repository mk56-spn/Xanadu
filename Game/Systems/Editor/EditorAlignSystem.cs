using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The sidebar's ALIGN row on the editor entity.</summary>
public struct EditorAlignPanel(Entity section) : IComponent
{
    public Entity Section = section;
}

/// <summary>
/// Shows the ALIGN buttons while two or more items are selected, and lines
/// them up on the pressed edge or centre, each press one undo step.
/// </summary>
public sealed class EditorAlignSystem(EditorHistory history) : QuerySystem<NoteSelection, EditorAlignPanel>
{
    private readonly ButtonPress _presses = new();

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        IReadOnlyList<Entity> selection = editor.GetComponent<NoteSelection>().AllNotes;
        bool can = SelectionAlign.CanAlign(selection);
        UiLayout.SetHidden(editor.GetComponent<EditorAlignPanel>().Section, CommandBuffer, !can);

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.Align))
        {
            if (!_presses.Started(button) || !can)
                continue;

            EditorSnapshot before = history.Capture();
            if (SelectionAlign.Apply(selection, (AlignEdge)button.GetComponent<UiCommand>().Arg))
                history.Record(before, history.Capture());
        }
    }
}
