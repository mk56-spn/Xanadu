using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor;

/// <summary>Asks the editor's warning popup to show a message; the popup itself is screen work.</summary>
public static class EditorWarnings
{
    public const string NoSongMessage = "Load a song before placing notes.";

    /// <summary>Queues a warning for the popup system to display.</summary>
    public static void Request(Entity editor, string message)
    {
        if (editor.IsNull)
            return;

        if (editor.HasComponent<WarningRequest>())
        {
            ref WarningRequest request = ref editor.GetComponent<WarningRequest>();
            request.Message = message;
            return;
        }

        editor.AddComponent(new WarningRequest(message));
    }
}
