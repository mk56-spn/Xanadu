using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Color = Xanadu.Core.Maths.Color;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>What a press on the preset list asks for: a preset (its place in <see cref="StackPresets.All"/>), or none.</summary>
internal enum PresetPress { Pick, Cancel }

/// <summary>
/// What "+ FILE" opens: every <see cref="StackPresets"/> kind of file, each a button with the paragraph that says what
/// it is for, over the dimmed code. One the stack can't take (a second shade) is greyed, with the reason. ECS UI on the
/// values column's layer, as the editor's questions are; its buttons carry <see cref="PresetPress"/> commands.
/// </summary>
internal sealed class FilePresets : IDisposable
{
    private const float Width = 760, NameWidth = 130;

    private readonly UiNode _layer, _scrim, _panel, _list;
    private readonly IDisposable _presses;
    private int? _picked;
    private bool _cancelled;

    /// <summary>Whether a stack's editor starts with the list up (the shader tool's <c>--add-file</c>).</summary>
    public static bool StartOpen { get; set; }

    /// <summary>Whether the list is up: it has the pointer and the keys.</summary>
    public bool Open { get; private set; }

    public FilePresets(EntityStore entities)
    {
        _layer = UiNode.Layer(entities, default, UiLayers.ShaderEditorValues, null, UiStage.Full).Theme(UiThemes.Tools);
        _scrim = _layer.Rect(new Color(0, 0, 0, 0.35f)).Hidden();
        _panel = _layer.Panel().Width(Width).Gap(UiMetrics.Space.Md).Cross(StackCrossAlignment.Stretch).Hidden();
        _panel.Text("ADD A FILE", UiType.Label);
        _panel.Text("Every file is read with the stack's others as one. Pick what this one starts as.", UiType.Small).DimText().WrapText();
        _list = _panel.Column().Gap(UiMetrics.Space.Lg).Cross(StackCrossAlignment.Stretch);
        UiNode foot = _panel.Row().Gap(UiMetrics.Space.Sm);
        foot.Column().Grow();
        foot.Button("CANCEL", UiType.Small).Secondary().With(UiCommand.Of(PresetPress.Cancel, owner: _layer.Entity));
        _presses = UiCommandEvents.OnPressed<PresetPress>(entities, Heard);
    }

    /// <summary>Puts the list up over <paramref name="code"/>, for a stack whose files take <paramref name="roles"/>.</summary>
    public void Show(Rect2 code, IReadOnlyCollection<StackRole> roles)
    {
        UiLayout.DeleteChildren(_list.Entity);
        for (int k = 0; k < StackPresets.All.Count; k++)
        {
            StackPreset preset = StackPresets.All[k];
            string? refused = preset.Refused(roles);
            // The name to press, then what it is for under it, wrapped to the panel's width.
            UiNode item = _list.Column().Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Start);
            UiNode button = item.Button(preset.Name.ToUpperInvariant(), UiType.Small).Width(NameWidth)
                .With(UiCommand.Of(PresetPress.Pick, arg: k, owner: _layer.Entity));
            if (refused is not null)
                UiLayout.SetDisabled(button.Entity, true);
            item.Text(refused is null ? preset.About : $"{preset.About} {refused}", UiType.Small).DimText().WrapText().Stretch();
        }

        _scrim.Box(code);
        _panel.At(code.GetCenter());
        UiLayout.SetHidden(_scrim.Entity, false);
        UiLayout.SetHidden(_panel.Entity, false);
        (_picked, _cancelled, Open) = (null, false, true);
    }

    public void Hide()
    {
        UiLayout.SetHidden(_scrim.Entity, true);
        UiLayout.SetHidden(_panel.Entity, true);
        Open = false;
    }

    /// <summary>The preset picked since the last call (the list goes down), or null; <paramref name="cancelled"/> when it was let go.</summary>
    public StackPreset? Take(out bool cancelled)
    {
        cancelled = _cancelled;
        int? picked = _picked;
        (_picked, _cancelled) = (null, false);
        if (picked is null && !cancelled)
            return null;
        Hide();
        return picked is { } k ? StackPresets.All[k] : null;
    }

    /// <summary>Lets the list go, as CANCEL does: Escape.</summary>
    public void Cancel() => _cancelled = true;

    private void Heard(PresetPress press, UiCommand command, Entity _)
    {
        if (!Open || command.Owner != _layer.Entity)
            return;
        if (press == PresetPress.Pick)
            _picked = command.Arg;
        else
            _cancelled = true;
    }

    public void Dispose()
    {
        _presses.Dispose();
        UiLayout.DeleteSubtree(_layer.Entity);
    }
}
