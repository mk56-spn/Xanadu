using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Hosts the shared ease picker in the level editor's group panel. It only
/// reads and writes <see cref="EditorGroupState"/>: the EASE buttons open it,
/// and <see cref="EditorGroupSystem"/> refreshes the curve it shows and applies
/// a pick to the selected effect or key as one undo step.
/// </summary>
public sealed class EditorGroupEaseHost(EntityStore store) : IEasePickerHost
{
    private Entity Editor
    {
        get
        {
            foreach (Entity editor in store.Query<EditorGroupState, PlayfieldView>().ToEntityList())
                return editor;
            return default;
        }
    }

    public bool Open => Editor is { IsNull: false } editor && IsOpen(editor.GetComponent<EditorGroupState>());

    public Rect2 Anchor => Editor is { IsNull: false } editor ? editor.GetComponent<EditorGroupState>().EaseAnchor : new Rect2();

    public Rect2 Bounds => Editor is { IsNull: false } editor
        ? new Rect2(Vector2.Zero, editor.GetComponent<PlayfieldView>().ScreenSize)
        : new Rect2();

    public EasePreset? Current => Editor is { IsNull: false } editor ? editor.GetComponent<EditorGroupState>().EaseCurrent : null;

    public string EmptyHint => "Select an effect or a key to set its curve";

    public void Pick(EasePreset preset)
    {
        Entity editor = Editor;
        if (!editor.IsNull)
            editor.GetComponent<EditorGroupState>().EasePick = preset;
    }

    public void Close()
    {
        Entity editor = Editor;
        if (!editor.IsNull)
            editor.GetComponent<EditorGroupState>().EaseFor = EditorEaseTarget.None;
    }

    /// <summary>Whether the panel is open with the picker up on it.</summary>
    public static bool IsOpen(EditorGroupState state) => state.Open >= 0 && state.EaseFor != EditorEaseTarget.None;
}
