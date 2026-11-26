using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The code's right-click menu: RENAME (F2) and GO TO DECLARATION (F12) on the name the right press put the caret on.
/// </summary>
internal static class CodeMenu
{
    private static readonly MenuItem[] Items = [new("RENAME   F2"), new("GO TO DECLARATION   F12")];

    /// <summary>Has <paramref name="host"/> hear the editor on <paramref name="code"/> and gives it its menu; <paramref name="focus"/> gives it the keys back after a pick.</summary>
    public static void Attach(Entity code, SlangEditorHost host, Action focus)
    {
        SlangEditorSignals.Listen(code, host);
        // The menu opens on a right press over a node that takes input.
        code.AddTag<TakesInput>();
        UiNode.Wrap(code).Menu(Items).Entity.OnMenuPicked(item =>
        {
            TextBuffer buffer = code.GetComponent<TextEditor>().Buffer;
            _ = item == 0 ? host.StartRename(buffer) : host.GoToDeclaration(buffer);
            focus();
        });
    }
}
