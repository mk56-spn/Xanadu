using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor;

/// <summary>Asks the editor's warning popup to show a message; the popup itself is screen work.</summary>
public static class EditorWarnings
{
    public const string NoSongMessage = "Load a song before placing notes.";

    /// <summary>Shows a warning in the editor's popup (emits <see cref="WarningRaised"/> on the editor).</summary>
    public static void Request(Entity editor, string message)
    {
        if (!editor.IsNull)
            editor.EmitSignal(new WarningRaised(message));
    }
}
