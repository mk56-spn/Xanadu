using System;
using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Ui.Editing;
using static Xanadu.Core.Theme.UiMetrics;

namespace Xanadu.Tools.DevTools.Arch;

/// <summary>
/// Arch: the architecture as it should be, drawn by hand on an open board (the
/// UI's <see cref="UiBoards"/>): boxes for the parts, bigger boxes around groups
/// of them, lines for what talks to what. Atlas shows what the code is; this
/// shows what it is meant to be. It is the one reader and writer of its file,
/// <c>Docs/architecture.xab</c>, saved after every edit.
/// </summary>
internal sealed class ArchStage(string root) : IToolStage
{
    public const string FileName = "Docs/architecture.xab";

    private const string Keys =
        "double-click: new box or retype · drag the right-edge dot to a box: line · drag a corner: size · " +
        "1–9: colour · Del: remove · Ctrl+Z / Ctrl+Y · drag empty or middle/right: pan · wheel: zoom";

    private string Path => System.IO.Path.Combine(root, FileName);

    private Label _status = null!;

    public void Build(UiNode body)
    {
        Board board = Load(out string status);
        _status = new Label(status, UiType.Small, ink: Ink.Dim);
        body.Add(new Row(gap: Space.Md)
        {
            new Label("ARCH", UiType.Heading, ink: Ink.Accent),
            new Label("the architecture as it should be, by hand", UiType.Small),
            new Spacer(),
            _status,
        });
        body.Add(new Label(Keys, UiType.Caption, ink: Ink.Dim, align: TextAlign.Left));

        UiNode node = body.Board(board).Grow().Stretch();
        node.Entity.OnBoardChanged(Save);
    }

    private Board Load(out string status)
    {
        if (!File.Exists(Path))
        {
            status = $"new board: {FileName}";
            return new Board();
        }

        try
        {
            status = FileName;
            return XabBinary.Read(File.ReadAllBytes(Path));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            // Saving would write over it, so keep what was there beside it.
            File.Copy(Path, Path + ".unreadable", overwrite: true);
            status = $"{FileName} could not be read ({exception.Message}); a copy is at .unreadable";
            return new Board();
        }
    }

    private void Save(Board board)
    {
        try
        {
            BinaryFile.Save(Path, XabBinary.Write(board));
            UiText.Set(_status.Node.Entity, $"saved {DateTime.Now:HH:mm:ss}");
        }
        catch (IOException exception)
        {
            UiText.Set(_status.Node.Entity, $"not saved: {exception.Message}");
        }
    }

    public void Update(in ToolFrame frame) { }

    public void Dispose() { }
}
