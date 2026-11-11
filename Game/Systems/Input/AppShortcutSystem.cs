using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Settings;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Scenes;

namespace Xanadu.Systems.Input;

/// <summary>
/// Hands out the game's key presses. A key being captured for a binding comes
/// first; then the open editor's table; otherwise the keys every other scene shares.
/// </summary>
public sealed class AppShortcutSystem(
    IKeyPressSource keys,
    KeybindSettings keybinds,
    SceneSystem scenes) : QuerySystem<EditorShortcutHost>
{
    protected override void OnUpdate()
    {
        while (keys.TryTake(out KeyChord chord))
        {
            if (keybinds.TryCapture(chord.Key))
                continue;

            if (FindEditor() is { } editor)
            {
                if (EditorShortcuts.Find(chord, editor.ActiveScopes) is { } binding)
                    editor.Run(binding);
                continue;
            }

            RunSceneKey(chord);
        }
    }

    private IEditorShortcutTarget? FindEditor()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            IEditorShortcutTarget target = entity.GetComponent<EditorShortcutHost>().Target;
            if (target.IsOpen)
                return target;
        }

        return null;
    }

    private void RunSceneKey(KeyChord chord)
    {
        switch (chord.Key)
        {
            case Key.Bracketleft:
                scenes.CyclePreviewStart(-1);
                break;
            case Key.Bracketright:
                scenes.CyclePreviewStart(1);
                break;
            case Key.Escape:
                scenes.TryBack();
                break;
            case Key.Enter:
            case Key.KpEnter:
                scenes.TryForward();
                break;
        }
    }
}
