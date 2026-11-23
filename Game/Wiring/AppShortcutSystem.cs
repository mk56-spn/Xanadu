using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Docs;
using Xanadu.Storage.Settings;
using Xanadu.Ui.Systems.Input;
using Xanadu.Wiring.Scenes;

namespace Xanadu.Wiring;

/// <summary>
/// Hands out the game's key presses. A key being captured for a binding comes
/// first; then the docs, whose key works everywhere and which take every key while
/// open; then the entity tagged <see cref="Xanadu.Ui.Systems.Input.KeyTarget"/> (the open
/// editor, whose overlay maps them to intents); otherwise the keys every other scene shares.
/// </summary>
public sealed class AppShortcutSystem(
    IKeyPressSource keys,
    KeybindSettings keybinds,
    SceneSystem scenes,
    FeedbackSoundPlayer? sounds = null,
    DocsOverlaySystem? docs = null) : QuerySystem
{
    protected override void OnUpdate()
    {
        while (keys.TryTake(out KeyChord chord))
        {
            if (keybinds.TryCapture(chord.Key))
                continue;

            if (docs is not null && TryDocsKey(docs, chord))
                continue;

            if (KeyTarget() is { IsNull: false } target)
            {
                target.EmitSignal(new KeyPressed(chord));
                continue;
            }

            RunSceneKey(chord);
        }
    }

    /// <summary>The docs key toggles them; while they are up, Escape puts them away and nothing reaches below.</summary>
    private static bool TryDocsKey(DocsOverlaySystem docs, KeyChord chord)
    {
        if (chord == new KeyChord(DocsLogic.Toggle))
        {
            docs.Toggle();
            return true;
        }

        if (!docs.IsOpen)
            return false;

        if (chord.Key == Key.Escape)
            docs.Close();
        return true;
    }

    /// <summary>The entity taking keys (the open editor), if any.</summary>
    private Entity KeyTarget()
    {
        _takesKeys ??= Query.Store.Query().AllTags(Tags.Get<KeyTarget>());
        foreach (Entity entity in _takesKeys.Entities)
            return entity;

        return default;
    }

    private ArchetypeQuery? _takesKeys;

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
