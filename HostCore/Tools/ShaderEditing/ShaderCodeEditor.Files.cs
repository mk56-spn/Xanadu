using System.Collections.Generic;
using System.Linq;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The shader's texts: one, or a stack's files. They are read as a whole (the clips, the warnings, the compile) joined
/// as <see cref="ShaderStack.Join"/> orders them, and every place in the whole maps back to its file (<see cref="Unjoin"/>).
/// </summary>
internal sealed partial class ShaderCodeEditor
{
    /// <summary>Where the preview draws instead of its slot (the whole stage, for a full-size shot); null for its slot.</summary>
    public static Rect2? FillPreview { get; set; }

    /// <summary>The preview's expand button (<see cref="PreviewExpand"/>); none for a full-size shot (<see cref="FillPreview"/>).</summary>
    private PreviewExpand? _expand;

    /// <summary>
    /// The preview's item, given its expand button; for a full-size shot, drawn over the whole UI with no button, so
    /// the shot shows the shader alone.
    /// </summary>
    private Handle Expandable(Handle item, Friflo.Engine.ECS.EntityStore entities)
    {
        if (FillPreview is not null)
            Xanadu.Ui.Systems.Canvas.CanvasServer.Current.SetZIndex(item, 4096);
        else
            _expand = new PreviewExpand(entities, item);
        return item;
    }

    /// <summary>Where the preview draws this frame: its slot, or the whole stage while expanded (or for a full-size shot).</summary>
    private Rect2 PreviewArea()
    {
        Rect2 slot = UiLayout.Box(_slots.Preview.Entity);
        return FillPreview ?? _expand?.Follow(slot) ?? slot;
    }

    /// <summary>The stack's name, as its files' labels carry it; null while editing one text.</summary>
    private readonly string? _stack;

    /// <summary>The shader's texts by name: the one text, or each of the stack's files.</summary>
    private readonly List<Tab> _files = [];

    /// <summary>A stack's files over the code; none for one text.</summary>
    private readonly FileStrip? _fileStrip;

    /// <summary>The kinds of file "+ FILE" offers, made the first time it is pressed.</summary>
    private FilePresets? _presets;

    /// <summary>Goes up as files are added, renamed and removed.</summary>
    private int _structure;

    /// <summary>Each file's look on the strip, worked out again only when the texts change.</summary>
    private List<FileLook> _looks = [];
    private int _looksVersion = -1;

    /// <summary>Where each text was in the whole the clips were read from.</summary>
    private List<Placed> _clipPlaces = [];

    /// <summary>A text in the joined whole: its line 1 is the whole's line <c>Start + 1</c>, and it has <c>Count</c> lines.</summary>
    private readonly record struct Placed(Tab Tab, int Start, int Count);

    /// <summary>The file a stack opens on: the one with its graph, else its shade, else the first.</summary>
    private static StackFile FirstOpen(IReadOnlyList<StackFile> files) =>
        files.FirstOrDefault(file => ShaderStack.RolesOf(file.Text).Contains(StackRole.Graph))
        ?? files.FirstOrDefault(file => ShaderStack.RolesOf(file.Text).Contains(StackRole.Shade))
        ?? files[0];

    /// <summary>A text for each file; the open one uses the editor's own buffer.</summary>
    private void MakeFiles(IReadOnlyList<StackFile> files, StackFile open, TextBuffer buffer, TextHistory history)
    {
        foreach (StackFile file in files.OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            Tab tab = ReferenceEquals(file, open) ? NewFile(file, buffer, history) : NewFile(file);
            _files.Add(tab);
            if (ReferenceEquals(file, open))
                _tabs.Add(tab);
        }
    }

    private Tab NewFile(StackFile file)
    {
        var buffer = new TextBuffer();
        buffer.Load(file.Text);
        return NewFile(file, buffer, new TextHistory(buffer));
    }

    private Tab NewFile(StackFile file, TextBuffer buffer, TextHistory history) =>
        _stack is null
            ? new Tab(_view.Label, null, buffer, history, "")
            : new Tab(file.Name, null, buffer, history, "") { File = file.Name };

    /// <summary>The label Slang's errors carry for a text's own lines.</summary>
    private string LabelOf(Tab tab) => _stack is null ? _view.Label : ShaderStack.Label(_stack, tab.File!);

    /// <summary>The texts as one, as the compile reads them, and where each one is in it.</summary>
    private (string Text, List<Placed> Places) Joined()
    {
        if (_stack is null)
        {
            TextBuffer only = _files[0].Buffer;
            return (only.Text, [new Placed(_files[0], 0, only.Lines.Count)]);
        }

        string joined = ShaderStack.Join(_stack, Files);
        var places = new List<Placed>();
        foreach ((string label, int line) in SlangShaders.LineMarks(joined))
        {
            if (_files.Find(file => LabelOf(file) == label) is { } tab)
                places.Add(new Placed(tab, line - 1, tab.Buffer.Lines.Count));
        }

        return (joined, places);
    }

    /// <summary>The text a 1-based line of the whole is on, and its own 1-based line there; null on none (a mark between them).</summary>
    private static (Tab Tab, int Line)? Unjoin(List<Placed> places, int line)
    {
        foreach (Placed place in places)
        {
            if (line > place.Start && line <= place.Start + place.Count)
                return (place.Tab, line - place.Start);
        }

        return null;
    }

    /// <summary>Puts a text in the first tab and shows it.</summary>
    private void OpenFile(Tab tab)
    {
        if (_open == 0)
        {
            if (Main == tab)
                return;
            Leave();
            _tabs[0] = tab;
            Load(tab);
        }
        else
        {
            _tabs[0] = tab;
            Show(0);
        }

        ListProblems();
    }

    /// <summary>The roles the stack's files take now.</summary>
    /// <summary>The stack's files other than the one in the editor, as they are now.</summary>
    private IEnumerable<string> OtherFiles() => _files.Where(file => file.Buffer != _editor.GetComponent<TextEditor>().Buffer).Select(file => file.Buffer.Text);

    private HashSet<StackRole> Roles() => [.. _files.SelectMany(file => ShaderStack.RolesOf(file.Buffer.Text))];

    /// <summary>Puts up the kinds of file to add, over the code; the code lets go of the keys meanwhile.</summary>
    private void ShowPresets()
    {
        _presets ??= new FilePresets(_editor.Store);
        _presets.Show(UiLayout.Box(_code.Entity), Roles());
        _editor.RemoveTag<TextEditorFocused>();
    }

    /// <summary>The preset list's answer, every frame it is up: a kind picked adds its file; Escape or CANCEL lets it go.</summary>
    private void FollowPresets(in ToolFrame frame)
    {
        if (FilePresets.StartOpen && UiLayout.Box(_code.Entity).Size.X > 0)
        {
            FilePresets.StartOpen = false;
            ShowPresets();
        }

        if (_presets is not { Open: true } presets)
            return;
        if (frame.Inputs?.Keyboard.IsPressed(MoonWorks.Input.KeyCode.Escape) == true)
            presets.Cancel();
        if (presets.Take(out bool cancelled) is { } preset)
            AddFile(preset);
        else if (cancelled)
            Focus();
    }

    /// <summary>Adds a file of <paramref name="preset"/>'s kind, opens it and puts its name up for typing.</summary>
    private void AddFile(StackPreset preset)
    {
        string name = ShaderStack.FreeName(_files.Select(file => file.File!), preset.Stem);
        Tab tab = NewFile(new StackFile(name, preset.Text(name, Roles().Contains(StackRole.Graph))));
        _files.Add(tab);
        _files.Sort((a, b) => string.CompareOrdinal(a.File, b.File));
        Changed();
        OpenFile(tab);
        _fileStrip?.Rename(name);
        _editor.RemoveTag<TextEditorFocused>();
    }

    /// <summary>Asks, then takes a file out of the stack, opening another.</summary>
    private void RemoveFile(string name)
    {
        if (_files.Find(file => file.File == name) is not { } gone || _files.Count <= 1)
            return;
        _strip.Question = new EditorQuestion($"Remove {name}?", "Its code goes from the stack; saving deletes the file.", "REMOVE", () =>
        {
            if (Main == gone)
                OpenFile(_files.First(file => file != gone));
            _files.Remove(gone);
            Changed();
            ListProblems();
            Focus();
        });
    }

    /// <summary>Gives a file the name typed for it (<c>.slang</c> added), when it is a free, plain one.</summary>
    private void RenameFile(string old, string typed)
    {
        string name = typed.EndsWith(ShaderStack.Extension, StringComparison.OrdinalIgnoreCase) ? typed : typed + ShaderStack.Extension;
        if (name == old || _files.Find(file => file.File == old) is not { } tab)
            return;
        if (!ShaderStack.IsFileName(name) || _files.Exists(file => string.Equals(file.File, name, StringComparison.OrdinalIgnoreCase)))
        {
            Toasts?.ShowStatus($"{name} can't be a file of this stack: a plain name (letters, digits, _ and -) the stack hasn't got");
            return;
        }

        tab.File = tab.Label = name;
        _files.Sort((a, b) => string.CompareOrdinal(a.File, b.File));
        Changed();
    }

    /// <summary>The files changed: everything that reads the whole reads it again, and it compiles now.</summary>
    private void Changed()
    {
        _structure++;
        _sinceEdit = Settle;
    }

    /// <summary>What stops the preview running what it shows (<see cref="IEditorPreview.Problem"/>), as last listed.</summary>
    private string? _running;

    /// <summary>The preview's frame, then its running problem into the list when it changed.</summary>
    private void UpdateView(in ToolFrame frame)
    {
        _view.Update(frame, _pointer, _tuning);
        if (_view.Problem == _running)
            return;
        _running = _view.Problem;
        ListProblems();
    }

    /// <summary>
    /// The list under the preview: the global problems, then the open text's, then the other files', each of those
    /// named by its file.
    /// </summary>
    private void ListProblems()
    {
        var shown = new List<SlangProblem>(_global);
        if (_running is { } running)
            shown.Add(new SlangProblem(0, 0, running, Warning: false));
        var listed = new List<Tab?>(shown.Select(_ => (Tab?)null));
        foreach (Tab tab in _files.OrderBy(file => file != Main))
        {
            foreach (SlangProblem problem in tab.Problems)
            {
                shown.Add(tab == Main || tab.File is null ? problem : problem with { Message = $"{tab.File} · {problem.Message}" });
                listed.Add(tab);
            }
        }

        (_shownProblems, _listed) = (shown, listed);
    }

    /// <summary>The file strip, following the files and their problems; a name typed there is taken once its field lets go.</summary>
    private void DrawFiles(in ToolFrame frame)
    {
        if (_fileStrip is null)
            return;
        FollowPresets(frame);
        if (_fileStrip.Renaming is { } renaming && _fileStrip.Renamed() is { } typed)
        {
            RenameFile(renaming, typed);
            Focus();
        }

        if (_looksVersion != Version)
        {
            _looksVersion = Version;
            _looks = [.. _files.Select(file => new FileLook(file.File!, ShaderStack.RolesOf(file.Buffer.Text).Select(role => (StackRole?)role).FirstOrDefault(),
                file.Problems.Count(problem => !problem.Warning), file.Problems.Count(problem => problem.Warning)))];
        }

        _fileStrip.Draw(_looks, Main.File!);
    }

    private void DisposeFiles()
    {
        _fileStrip?.Dispose();
        _presets?.Dispose();
    }

    /// <summary>A press on the file strip's buttons: open, add, rename or remove a file. True when there was one.</summary>
    private bool PressFiles()
    {
        if (_presets is { Open: true })
            return true; // the list has the pointer
        if (_fileStrip?.TakePress() is not (FilePress press, string name))
            return false;
        switch (press)
        {
            case FilePress.Open:
                OpenFile(_files.Find(file => file.File == name)!);
                break;
            case FilePress.Add:
                ShowPresets();
                return true;
            case FilePress.Rename:
                _fileStrip.Rename(name);
                _editor.RemoveTag<TextEditorFocused>();
                return true;
            case FilePress.Remove:
                RemoveFile(name);
                break;
        }

        Focus();
        return true;
    }
}
