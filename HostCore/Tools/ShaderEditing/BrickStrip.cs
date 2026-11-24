using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Color = Xanadu.Core.Maths.Color;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>One brick as the strip shows it: whether the stack has it, and its problems.</summary>
internal readonly record struct BrickLook(BrickKind Kind, bool Present, int Errors, int Warnings);

/// <summary>What a press on the strip asks for.</summary>
internal enum BrickPress { Open, Add, Remove }

/// <summary>
/// The pipeline a shader stack composes, as a row over its code: every brick
/// kind in the order it runs, UPDATE → STEP → BUILD → TRACE → DEFORM → SHADE, then the blend
/// (set where the stack is placed, not a brick). Each kind has its own colour
/// (<see cref="Colour"/>), which the code's entry names share: a brick the stack
/// has is written in it, the open one on a surface tinted with it; one it hasn't
/// is a dim "+ KIND" that adds it from <see cref="ShaderStack.Template"/>. Each brick says its
/// problems; the row ends with removing the open brick. ECS UI, rebuilt only
/// when what it shows changes.
/// </summary>
internal sealed class BrickStrip : IDisposable
{
    private const float Height = 30;

    /// <summary>The theme category each kind is told apart by (<see cref="UiThemeRoles.Categories"/>).</summary>
    internal static int Category(BrickKind kind) => kind switch
    {
        BrickKind.Update => 4,
        BrickKind.Step => 5,
        BrickKind.Trace => 2,
        BrickKind.Deform => 3,
        _ => 6,
    };

    /// <summary>A kind's colour, for what shows it outside the strip (its entry names in the code).</summary>
    public static Color Colour(BrickKind kind) => UiThemes.Tools.Category(Category(kind));

    private readonly UiNode _row;
    private readonly List<(BrickKind Kind, bool Present, Entity Button)> _bricks = [];
    private Entity _remove;
    private string _shown = "";

    /// <param name="column">The code's column: the strip is its first row.</param>
    public BrickStrip(UiNode column) =>
        _row = column.Row().Height(Height).Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Center).Pad(UiMetrics.Space.Xs);

    public void Draw(IReadOnlyList<BrickLook> bricks, BrickKind open)
    {
        string shown = open + "|" + string.Join("|", bricks);
        if (shown == _shown)
            return;
        _shown = shown;

        UiLayout.DeleteChildren(_row.Entity);
        _bricks.Clear();
        foreach (BrickLook brick in bricks)
        {
            if (_bricks.Count > 0)
                _row.Text("→", UiType.Small).DimText();
            string title = ShaderStack.Title(brick.Kind);
            UiNode button;
            if (brick.Present)
            {
                string problems = brick.Errors > 0 ? $" · {brick.Errors}✕" : brick.Warnings > 0 ? $" · {brick.Warnings}!" : "";
                button = _row.Button(title + problems, UiType.Small).Mono().Height(Height - UiMetrics.Space.Xs * 2);
                if (brick.Kind == open)
                    button.CategoryTone(Category(brick.Kind));
                else
                    button.Quiet();
                button.CategoryText(Category(brick.Kind))
                    .Tooltip(ShaderStack.Description(brick.Kind));
            }
            else
            {
                button = _row.Button("+ " + title, UiType.Small).Mono().Quiet().DimText().Height(Height - UiMetrics.Space.Xs * 2)
                    .Tooltip($"Click to add it. {ShaderStack.Description(brick.Kind)}");
            }

            _bricks.Add((brick.Kind, brick.Present, button.Entity));
        }

        _row.Text("→", UiType.Small).DimText();
        _row.Text("blend (where placed)", UiType.Small).DimText().Tooltip(ShaderStack.BlendDescription);
        _row.Column().Grow();
        UiNode remove = _row.Button("REMOVE " + ShaderStack.Title(open), UiType.Small).Quiet().Height(Height - UiMetrics.Space.Xs * 2)
            .Tooltip("Take the open brick out of the stack (saving deletes its file)");
        UiLayout.SetDisabled(remove.Entity, bricks.Count(brick => brick.Present) <= 1);
        _remove = remove.Entity;
    }

    /// <summary>What a press at a point asks for, and of which brick; null off the strip's buttons.</summary>
    public (BrickPress Press, BrickKind Kind)? At(Vector2 point, BrickKind open)
    {
        foreach ((BrickKind kind, bool present, Entity button) in _bricks)
        {
            if (Hit(button, point))
                return (present ? BrickPress.Open : BrickPress.Add, kind);
        }

        return !_remove.IsNull && !_remove.Tags.Has<UiDisabled>() && Hit(_remove, point) ? (BrickPress.Remove, open) : null;
    }

    private static bool Hit(Entity button, Vector2 point) => UiLayout.Box(button).HasPoint(point);

    public void Dispose() => UiLayout.DeleteSubtree(_row.Entity);
}
