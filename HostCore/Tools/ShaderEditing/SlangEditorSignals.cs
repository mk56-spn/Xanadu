using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>Has a <see cref="SlangEditorHost"/> answer a text editor's signals, so the plain editor becomes the Slang one.</summary>
internal static class SlangEditorSignals
{
    public static void Listen(Entity code, SlangEditorHost host)
    {
        code.GetComponent<TextEditor>().LineComment = "//";
        code.AddSignalHandler<TextVersionDue>(signal => signal.Entity.GetComponent<TextEditor>().OwnerVersion = host.Version);
        code.AddSignalHandler<TextPrepare>(signal => host.Prepare(signal.Event.Buffer, signal.Event.Boxed));
        code.AddSignalHandler<TextDecorate>(signal => host.Decorate(signal.Event.Buffer, signal.Event.Line, signal.Event.Decor));
        code.AddSignalHandler<TextIndentAsked>(signal => signal.Entity.AnswerIndent(host.IndentFor(signal.Event.Buffer, signal.Event.Line)));
        code.AddSignalHandler<TextKey>(signal =>
        {
            TextKey key = signal.Event;
            if (host.TakeKey(key.Buffer, key.Key, key.Shift, key.Ctrl))
                signal.Entity.Take();
        });
        code.AddSignalHandler<TextTyped>(signal =>
        {
            if (host.TakeTyped(signal.Event.Buffer, signal.Event.Text))
                signal.Entity.Take();
        });
        code.AddSignalHandler<TextPressed>(signal =>
        {
            if (host.TakePress(signal.Event.Buffer, signal.Event.Point))
                signal.Entity.Take();
        });
        code.AddSignalHandler<TextCtrlClicked>(signal =>
        {
            if (host.TakeCtrlClick(signal.Event.Buffer))
                signal.Entity.Take();
        });
    }
}
