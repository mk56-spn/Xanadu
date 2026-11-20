using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Editor;
using Xanadu.Wiring.Scenes;

namespace Xanadu.Wiring;

/// <summary>
/// Hands out the game's key presses. A key being captured for a binding comes
/// first; then the open editor's table; otherwise the keys every other scene shares.
/// </summary>
public sealed class AppShortcutSystem(
    IKeyPressSource keys,
    KeybindSettings keybinds,
    SceneSystem scenes,
    FeedbackSoundPlayer? sounds = null) : QuerySystem<EditorShortcutHost>
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
            case Key.Escape:
                sounds?.Play(FeedbackSound.Back);
                scenes.TryBack();
                break;
            case Key.Enter:
            case Key.KpEnter:
                scenes.TryForward();
                break;
        }
    }
}
