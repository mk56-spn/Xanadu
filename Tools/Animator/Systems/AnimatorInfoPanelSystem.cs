using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Centres the FILE INFO popup over its dimmer while it is open, fills the
/// readout under the path, and closes it on a click outside.
/// </summary>
public sealed class AnimatorInfoPanelSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorInfoPanel, AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = input.Pressed && !_primaryHeld;
        _primaryHeld = input.Pressed;

        foreach (Entity session in Query.ToEntityList())
            Update(session, primary, input.Position);
    }

    private void Update(Entity session, bool primary, Vector2 point)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.InfoPanel.IsNull || ui.InfoBackdrop.IsNull)
            return;

        ref AnimatorInfoPanel panel = ref session.GetComponent<AnimatorInfoPanel>();
        bool justOpened = panel.Open && !_wasOpen;
        if (panel.Open && primary && !justOpened &&
            !AnimatorGeometry.PanelArea(ui.InfoPanel).HasPoint(point))
        {
            panel.Open = false;
        }

        _wasOpen = panel.Open;
        if (!panel.Open)
        {
            AnimatorMenuPlacement.Place(ui.InfoBackdrop, UiLayout.Hidden);
            AnimatorMenuPlacement.Place(ui.InfoPanel, UiLayout.Hidden);
            if (!ui.PathField.IsNull && ui.PathField.Tags.Has<TextFieldFocused>())
                ui.PathField.RemoveTag<TextFieldFocused>();
            return;
        }

        Vector2 size = AnimatorLayout.InfoPanelSize;
        AnimatorMenuPlacement.Place(ui.InfoBackdrop, ui.ScreenSize / 2);
        AnimatorMenuPlacement.Place(ui.InfoPanel, (ui.ScreenSize - size) / 2, size);
        Fill(ui.InfoText, Lines(session));
    }

    /// <summary>One fact per line: what is in the file, and whether it is saved.</summary>
    private static List<(string Label, string Value)> Lines(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafPackage package = document.Package;
        XafClip? clip = AnimatorSession.CurrentClip(document);
        return new List<(string, string)>
        {
            ("BONES", $"{package.Skeleton.Bones.Count}"),
            ("CLIPS", $"{package.Clips.Count}" +
                      (clip is null ? "" : $"   ·   editing {clip.Id}, {clip.Duration:0.00} s")),
            ("ATTACHMENTS", $"{package.Attachments.Count}"),
            ("CHAINS", $"{package.Chains.Count}"),
            ("EFFECTS", $"{package.Effects.Count}"),
            ("STATE", session.GetComponent<AnimatorFile>().Dirty ? "● unsaved changes" : "saved"),
        };
    }

    /// <summary>Writes each fact into its row: the label, then the value.</summary>
    private static void Fill(Entity list, List<(string Label, string Value)> lines)
    {
        if (list.IsNull)
            return;

        int i = 0;
        foreach (Entity row in list.ChildEntities)
        {
            (string label, string value) = i < lines.Count ? lines[i] : ("", "");
            i++;
            int column = 0;
            foreach (Entity cell in row.ChildEntities)
                SetText(cell, column++ == 0 ? label : value);
        }
    }

    private static void SetText(Entity cell, string value)
    {
        if (!cell.HasComponent<Text>())
            return;

        ref Text text = ref cell.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        cell.AddTag<Dirty>();
    }
}
