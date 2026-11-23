using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Animator;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Screens.Animator;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Centres the FILE INFO popup over its dimmer while it is open, fills the
/// readout under the path, and closes it on a click outside.
/// </summary>
public sealed class AnimatorInfoPanelSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = AnimatorPointerEdge.Primary(ref _primaryHeld, input);

        foreach (Entity session in Query.ToEntityList())
            Update(session, primary, input.Position);
    }

    private void Update(Entity session, bool primary, Vector2 point)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.InfoPanel.IsNull || ui.InfoBackdrop.IsNull)
            return;

        Entity panel = ui.InfoPanel;
        bool justOpened = panel.IsOpen() && !_wasOpen;
        if (panel.IsOpen() && primary && !justOpened &&
            !AnimatorGeometry.PanelArea(ui.InfoPanel).HasPoint(point))
        {
            panel.SetOpen(false);
        }

        _wasOpen = panel.IsOpen();
        if (!panel.IsOpen())
        {
            AnimatorMenuPlacement.Hide(ui.InfoBackdrop);
            AnimatorMenuPlacement.Hide(ui.InfoPanel);
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
        RigPackage package = document.Package;
        RigClip? clip = AnimatorSession.CurrentClip(document);
        return
        [
            ("BONES", $"{package.Skeleton.Bones.Count}"),
            ("CLIPS", $"{package.Clips.Count}" +
                      (clip is null ? "" : $"   ·   editing {clip.Id}, {clip.Duration:0.00} s")),
            ("PARTS", $"{package.Attachments.Count}"),
            ("CHAINS", $"{package.Chains.Count}"),
            ("CLOTH", $"{package.Cloths.Count}"),
            ("STATE", session.GetComponent<AnimatorFile>().Dirty ? "● unsaved changes" : "saved"),
        ];
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
                UiText.Set(cell, column++ == 0 ? label : value);
        }
    }
}
