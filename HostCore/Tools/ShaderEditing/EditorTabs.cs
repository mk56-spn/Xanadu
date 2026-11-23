using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Color = Xanadu.Core.Maths.Color;
using Rect2 = Xanadu.Core.Maths.Rect2;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>One tab as the strip shows it.</summary>
internal readonly record struct TabLook(string Label, bool Dirty, bool ReadOnly, bool Closes);

/// <summary>A question over the code that has to be answered before going on.</summary>
internal sealed record EditorQuestion(string Title, string Message, string Yes, Action OnYes);

/// <summary>
/// The strip of open texts over the code (the shader first, then library
/// modules gone to), each with its close cross, and the box that asks before
/// something lasting happens (editing the library, dropping unsaved changes).
/// The open tab is the chosen one; one that can't be edited says so at the
/// strip's end. Both are ECS UI made of the shared buttons and panel, on the
/// values column's layer; the strip is rebuilt only when its tabs change.
/// </summary>
internal sealed class EditorTabs : IDisposable
{
    public const float Height = 28;
    private const float Cross = 20;

    private readonly UiNode _layer;
    private readonly UiNode _strip;
    private readonly UiNode _scrim;
    private readonly UiNode _dialog;
    private readonly Entity _title, _message, _yes, _no;
    private readonly List<(Entity Tab, Entity Cross)> _tabs = new();
    private string _shown = "";
    private Rect2 _code;
    private EditorQuestion? _question;

    public EditorTabs(EntityStore entities)
    {
        _layer = UiNode.Layer(entities, default, UiLayers.ShaderEditorValues, null, UiStage.Full).Theme(UiThemes.Tools);
        _strip = _layer.Row().Gap(1).Cross(StackCrossAlignment.Stretch).Fill(_layer.Roles.Inset);

        _scrim = _layer.Rect(new Color(0, 0, 0, 0.35f)).Hidden();
        _dialog = _layer.Panel().Gap(UiMetrics.Space.Md).Cross(StackCrossAlignment.Start).Hidden();
        _title = _dialog.Text("", UiType.Label).Entity;
        _message = _dialog.Text("", UiType.Small).DimText().Entity;
        UiNode answers = _dialog.Row().Gap(UiMetrics.Space.Sm);
        _yes = answers.Button("YES", UiType.Small).Primary().Entity;
        _no = answers.Button("CANCEL", UiType.Small).Secondary().Entity;
    }

    /// <summary>The strip's box, over the code.</summary>
    public Rect2 Area
    {
        set => _strip.Box(value);
    }

    /// <summary>The code's box: the question dims it and sits in its middle.</summary>
    public Rect2 Code
    {
        set
        {
            _code = value;
            _scrim.Box(value);
            _dialog.At(value.GetCenter());
        }
    }

    public EditorQuestion? Question
    {
        get => _question;
        set
        {
            _question = value;
            UiLayout.SetHidden(_scrim.Entity, value is null);
            UiLayout.SetHidden(_dialog.Entity, value is null);
            if (value is null)
                return;
            UiText.Set(_title, value.Title);
            UiText.Set(_message, value.Message);
            UiText.Set(_yes, value.Yes);
            _dialog.At(_code.GetCenter());
        }
    }

    public void Draw(IReadOnlyList<TabLook> tabs, int open, Vector2 pointer)
    {
        string shown = open + "|" + string.Join("|", tabs.Select(tab => $"{tab.Label}:{tab.Dirty}:{tab.ReadOnly}:{tab.Closes}"));
        if (shown == _shown)
            return;
        _shown = shown;

        UiLayout.DeleteChildren(_strip.Entity);
        _tabs.Clear();
        for (int i = 0; i < tabs.Count; i++)
        {
            TabLook tab = tabs[i];
            UiNode button = _strip.Button(tab.Dirty ? tab.Label + " •" : tab.Label, UiType.Small).Mono().Height(Height).Chosen(i == open);
            if (i != open)
                button.Quiet();
            Entity cross = tab.Closes
                ? _strip.Button("×", UiType.Small).Quiet().Size(Cross, Height).Tooltip("Close this tab").Entity
                : default;
            _tabs.Add((button.Entity, cross));
        }

        if (open >= 0 && open < tabs.Count && tabs[open].ReadOnly)
            _strip.Text("read-only library · type to unlock", UiType.Small, align: TextAlign.Right).DimText().Grow().Fit();
    }

    /// <summary>The tab under a point, and whether it is on its cross; null off the tabs.</summary>
    public (int Tab, bool Close)? TabAt(Vector2 point)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (!_tabs[i].Cross.IsNull && UiLayout.Box(_tabs[i].Cross).HasPoint(point))
                return (i, true);
            if (UiLayout.Box(_tabs[i].Tab).HasPoint(point))
                return (i, false);
        }

        return null;
    }

    /// <summary>A press while asking: true on yes, false on cancel, null elsewhere.</summary>
    public bool? AnswerAt(Vector2 point) =>
        UiLayout.Box(_yes).HasPoint(point) ? true : UiLayout.Box(_no).HasPoint(point) ? false : null;

    public void Dispose() => UiLayout.DeleteSubtree(_layer.Entity);
}
