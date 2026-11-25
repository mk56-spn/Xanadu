using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Color = Xanadu.Core.Maths.Color;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>One file as the strip shows it: its name, the first role its code takes (null for none), and its problems.</summary>
internal readonly record struct FileLook(string Name, StackRole? Role, int Errors, int Warnings);

/// <summary>What a press on the strip asks for.</summary>
internal enum FilePress { Open, Add, Rename, Remove }

/// <summary>
/// A shader stack's files, as a row over its code: each file by name, written in the colour of the first role its code
/// takes (<see cref="Colour"/>, which the code's entry names share), the open one on a surface tinted with it, each with
/// its problems; then "+ FILE", the blend (set where the stack is placed), and renaming or removing the open file.
/// Renaming puts a field in place of the open file's button; the name it holds is taken when it lets go of the keys.
/// What the stack does is on the GRAPH board, not here. ECS UI, rebuilt only when what it shows changes.
/// </summary>
internal sealed class FileStrip : IDisposable
{
    private const float Height = 30, NameWidth = 180;

    /// <summary>The theme category each role is told apart by (<see cref="UiThemeRoles.Categories"/>); a file with none is the graph's.</summary>
    internal static int Category(StackRole? role) => role switch
    {
        StackRole.Update => 4,
        StackRole.Step => 5,
        StackRole.Trace => 2,
        StackRole.Deform => 3,
        _ => 6,
    };

    /// <summary>A role's colour, for what shows it outside the strip (its entry names in the code).</summary>
    public static Color Colour(StackRole? role) => UiThemes.Tools.Category(Category(role));

    private readonly UiNode _row;
    private readonly List<(string Name, Entity Button)> _files = [];
    private Entity _add, _rename, _remove, _field;
    private string _shown = "";

    /// <summary>The file being renamed, while its field is up; null otherwise.</summary>
    public string? Renaming { get; private set; }

    /// <param name="column">The code's column: the strip is its first row.</param>
    public FileStrip(UiNode column) =>
        _row = column.Row().Height(Height).Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Center).Pad(UiMetrics.Space.Xs);

    public void Draw(IReadOnlyList<FileLook> files, string open)
    {
        string shown = open + "|" + Renaming + "|" + string.Join("|", files);
        if (shown == _shown)
            return;
        _shown = shown;

        UiLayout.DeleteChildren(_row.Entity);
        _files.Clear();
        _field = default;
        float button = Height - UiMetrics.Space.Xs * 2;
        foreach (FileLook file in files)
        {
            if (file.Name == Renaming)
            {
                _field = _row.Add(new Field(System.IO.Path.GetFileNameWithoutExtension(file.Name), UiType.Small, width: NameWidth, height: button)).Node.Entity;
                _field.AddTag<TextFieldFocused>();
                continue;
            }

            string problems = file.Errors > 0 ? $" · {file.Errors}✕" : file.Warnings > 0 ? $" · {file.Warnings}!" : "";
            UiNode node = _row.Button(file.Name + problems, UiType.Small).Mono().Height(button);
            if (file.Name == open)
                node.CategoryTone(Category(file.Role));
            else
                node.Quiet();
            node.CategoryText(Category(file.Role)).Tooltip(file.Role is { } role
                ? $"Holds {string.Join(", ", ShaderStack.Entries(role).Take(1))}(): {ShaderStack.Title(role)}."
                : "Declarations the stack's other files read.");
            _files.Add((file.Name, node.Entity));
        }

        _add = _row.Button("+ FILE", UiType.Small).Mono().Quiet().DimText().Height(button)
            .Tooltip("Add a file to the stack: every file is read with the others as one.").Entity;
        _row.Text("blend (where placed)", UiType.Small).DimText().Tooltip(ShaderStack.BlendDescription);
        _row.Column().Grow();
        _rename = _row.Button("RENAME", UiType.Small).Quiet().Height(button).Tooltip($"Rename {open} (saving renames its file)").Entity;
        UiNode remove = _row.Button("REMOVE " + open, UiType.Small).Quiet().Height(button)
            .Tooltip("Take the open file out of the stack (saving deletes it)");
        UiLayout.SetDisabled(remove.Entity, files.Count <= 1);
        _remove = remove.Entity;
    }

    /// <summary>What a press at a point asks for, and on which file; null off the strip's buttons.</summary>
    public (FilePress Press, string File)? At(Vector2 point, string open)
    {
        foreach ((string name, Entity button) in _files)
        {
            if (Hit(button, point))
                return (FilePress.Open, name);
        }

        if (Hit(_add, point))
            return (FilePress.Add, open);
        if (Hit(_rename, point))
            return (FilePress.Rename, open);
        return !_remove.Tags.Has<UiDisabled>() && Hit(_remove, point) ? (FilePress.Remove, open) : null;
    }

    /// <summary>Puts a field in place of <paramref name="file"/>'s button, holding its name, with the keys.</summary>
    public void Rename(string file) => Renaming = file;

    /// <summary>The name typed for the file being renamed, once its field has let go of the keys; null until then.</summary>
    public string? Renamed()
    {
        if (Renaming is null || _field.IsNull || _field.Tags.Has<TextFieldFocused>())
            return null;
        string typed = _field.GetComponent<TextField>().Value.Trim();
        Renaming = null;
        return typed;
    }

    private static bool Hit(Entity button, Vector2 point) => !button.IsNull && UiLayout.Box(button).HasPoint(point);

    public void Dispose() => UiLayout.DeleteSubtree(_row.Entity);
}
