using System.Collections.Generic;
using Xanadu.Slang;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>One open text: the shader's own or one of its stack's files (no path), or a library module's.</summary>
internal sealed class Tab(string label, string? path, TextBuffer buffer, TextHistory history, string disk)
{
    public string Label = label;

    /// <summary>The stack file it is, by name; null for a single text or a library module.</summary>
    public string? File;

    /// <summary>The last compile's problems and colour conversions on this text's own lines.</summary>
    public List<SlangProblem> Problems = [];
    public List<(ColourConversion Place, string Text)> Conversions = [];
    public readonly string? Path = path;
    public readonly TextBuffer Buffer = buffer;
    public readonly TextHistory History = history;
    public string Disk = disk;
    public int Scroll;
    public bool ReadOnly = path is not null;

    /// <summary>Whether a module's text differs from its file, worked out again only when it changes.</summary>
    public bool Dirty
    {
        get
        {
            if (Path is null)
                return false;
            if (_dirtyVersion != Buffer.Version)
            {
                _dirtyVersion = Buffer.Version;
                _dirty = Buffer.Text != Disk;
            }

            return _dirty;
        }
    }

    private int _dirtyVersion = -1;
    private bool _dirty;

    public void Saved()
    {
        Disk = Buffer.Text;
        _dirtyVersion = -1;
    }
}
